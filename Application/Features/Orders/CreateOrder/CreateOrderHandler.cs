using System.Diagnostics.Metrics;
using Application.Exceptions;
using Application.Shared.DTOs.Order;
using Application.Shared.Interfaces;
using Domain.Entities;
using Domain.Entities.Saga;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Orders.CreateOrder;

public class CreateOrderHandler(
    IProductServiceClient productServiceClient,
    IOrderRepository repository,
    ISagaRepository sagaRepository,
    ILogger<CreateOrderHandler> logger)
    : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private static readonly Meter Meter = new("OrderService", "1.0");
    
    private static readonly Counter<int> OrdersCreated = 
        Meter.CreateCounter<int>("orders.created.total", description: "Total orders created successfully");
    
    private static readonly Counter<int> OrdersFailed = 
        Meter.CreateCounter<int>("orders.failed.total", description: "Total orders failed");
    
    // Обработчик
    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        try
        {
            // ==========================================
            // 🔹 ШАГ 1: Создаём заказ в статусе Pending
            // ==========================================
        
            // 👇 Rich Domain: создаём сущность с валидацией инвариантов внутри конструктора
            // Все инварианты проверяются автоматически:
            // - quantity > 0
            // - priceInKopecks >= 0
            // - customerEmail не пустой
            var order = new Order(
                id: Guid.NewGuid(),
                productId: request.ProductId,
                quantity: request.Quantity,
                priceInKopecks: 0,
                customerEmail: request.CustomerEmail,
                correlationId: request.CorrelationId
            );

            // 👇 Сохраняем через интерфейс репозитория (не зависит от EF Core)
            await repository.AddAsync(order, ct);
            logger.LogInformation("Заказ создан (Pending): OrderId={OrderId}", order.Id);

            // ==========================================
            // 🔹 ШАГ 2: Создаём Saga (пока не сохраняем!)
            // ==========================================
            var saga = OrderSaga.Create(order.Id, request.CorrelationId, timeoutSeconds: 30);
            saga.StartProcessing();

            // ==========================================
            // 🔹 ШАГ 3: Получаем продукт (цена)
            // ==========================================
            var getProductStep = SagaStep.Create("GetProduct");
            getProductStep.Start();
            saga.AddStep(getProductStep);

            var product = await productServiceClient.GetProductAsync(request.ProductId, ct);
            
            if (product is null)
                throw new ProductNotFoundException(request.ProductId);
            
            order.UpdatePrice(product.PriceInKopecks);
            await repository.UpdateAsync(order, ct);
            
            getProductStep.Complete();

            // ==========================================
            // 🔹 ШАГ 4: Резервируем сток (с таймаутом)
            // ==========================================
            var reserveStep = SagaStep.Create("ReserveStock", timeoutSeconds: 30);
            reserveStep.Start();
            saga.AddStep(reserveStep);

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(reserveStep.TimeoutSeconds));
                
                var reserved = await productServiceClient.ReserveStockAsync(
                    request.ProductId, 
                    request.Quantity, 
                    request.CorrelationId, 
                    cts.Token);
                
                if (!reserved)
                    throw new StockReservationException(request.ProductId, request.Quantity, "Недостаточно стока");
                
                reserveStep.Complete();
            }
            catch (OperationCanceledException)
            {
                reserveStep.Fail($"Таймаут резерва: {reserveStep.TimeoutSeconds} сек");
                await CompensateAsync(order, saga, "Таймаут при резервировании стока", ct);
                throw new TimeoutException("Product Service не ответил в течение 30 секунд");
            }
            catch (StockReservationException ex)
            {
                reserveStep.Fail(ex.Message);
                await CompensateAsync(order, saga, ex.Message, ct);
                throw;
            }

            // ==========================================
            // 🔹 ШАГ 5: Подтверждаем заказ
            // ==========================================
            var confirmStep = SagaStep.Create("ConfirmOrder");
            confirmStep.Start();
            
            order.Confirm();
            await repository.UpdateAsync(order, ct);
            
            confirmStep.Complete();
            saga.AddStep(confirmStep);
            saga.Complete();

            // ==========================================
            // 🔹 ШАГ 6: СОХРАНЯЕМ SAGA ОДИН РАЗ В КОНЦЕ
            // ==========================================
            await sagaRepository.CreateWithStepsAsync(saga, ct);
            
            logger.LogInformation("Заказ подтверждён: OrderId={OrderId}", order.Id);
            OrdersCreated.Add(1, new[] { 
                new KeyValuePair<string, object?>("productId", request.ProductId.ToString()) 
            });

            return order.ToDto();
        }
        catch (ProductNotFoundException)
        {
            OrdersFailed.Add(1, new KeyValuePair<string, object?>("reason", "ProductNotFound"));
            throw;
        }
        catch (StockReservationException)
        {
            OrdersFailed.Add(1, new KeyValuePair<string, object?>("reason", "StockReservation"));
            throw;
        }
        catch (TimeoutException)
        {
            OrdersFailed.Add(1, new KeyValuePair<string, object?>("reason", "Timeout"));
            throw;
        }
        catch (Exception ex)
        {
            // 👇 Запись метрики неудачного заказа
            OrdersFailed.Add(1, new KeyValuePair<string, object?>("reason", ex.GetType().Name));
            logger.LogError(ex, "Неожиданная ошибка: {Message}", ex.Message);
            throw;
        }
    }

    private async Task CompensateAsync(Order order, OrderSaga saga, string reason, CancellationToken ct)
    {
        logger.LogWarning("Компенсация Saga: OrderId={OrderId}, Reason={Reason}", order.Id, reason);
        
        saga.StartCompensation();
        
        var compensateStep = SagaStep.Create("Compensate");
        compensateStep.Start();
        
        try
        {
            order.Cancel(reason);
            await repository.UpdateAsync(order, ct);
            
            compensateStep.Compensate($"Заказ отменён: {reason}");
            saga.AddStep(compensateStep);
            saga.Fail();
            
            // 👇 Для компенсации тоже сохраняем один раз
            await sagaRepository.CreateWithStepsAsync(saga, ct);
            
            logger.LogInformation("Заказ отменён: OrderId={OrderId}", order.Id);
        }
        catch (Exception ex)
        {
            compensateStep.Fail($"Компенсация не удалась: {ex.Message}");
            saga.Fail();
            await sagaRepository.CreateWithStepsAsync(saga, ct);
            logger.LogError(ex, "Компенсация не удалась: OrderId={OrderId}", order.Id);
            throw;
        }
    }
}
