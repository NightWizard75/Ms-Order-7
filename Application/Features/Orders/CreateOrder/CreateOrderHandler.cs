using System.Diagnostics.Metrics;
using System.Text.Json;
using Application.Exceptions;
using Application.Shared.DTOs.Order;
using Application.Shared.Events;
using Application.Shared.Interfaces;
using Domain.Entities;
using Domain.Entities.Saga;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Orders.CreateOrder;

public class CreateOrderHandler(
    IProductServiceClient productServiceClient,  // Query: синхронная валидация и получение цены
    IOrderRepository repository,
    IOutboxMessageRepository outboxRepository,
    ISagaRepository sagaRepository,
    ILogger<CreateOrderHandler> logger)
    : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private static readonly Meter Meter = new("OrderService", "1.0");
    private static readonly Counter<int> OrdersCreated = Meter.CreateCounter<int>("orders.created.total", description: "Total orders accepted for processing");
    private static readonly Counter<int> OrdersFailed = Meter.CreateCounter<int>("orders.failed.total", description: "Total orders rejected");

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        try
        {
            // ==========================================
            // 🔹 ШАГ 1: Валидация продукта (FAIL-FAST, Query)
            // ==========================================
            var product = await productServiceClient.GetProductAsync(request.ProductId, ct);
            
            if (product is null)
                throw new ProductNotFoundException(request.ProductId);
            
            logger.LogInformation("Продукт подтверждён: ProductId={ProductId}, Price={Price}", 
                request.ProductId, product.PriceInKopecks);

            // ==========================================
            // 🔹 ШАГ 2: Создаём заказ с известной ценой
            // ==========================================
            var order = new Order(
                id: Guid.NewGuid(),
                productId: request.ProductId,
                quantity: request.Quantity,
                priceInKopecks: product.PriceInKopecks, // Цена зафиксирована до создания
                customerEmail: request.CustomerEmail,
                correlationId: request.CorrelationId
            );

            await repository.AddAsync(order, ct);
            logger.LogInformation("Заказ создан (Pending): OrderId={OrderId}", order.Id);

            // ==========================================
            // 🔹 ШАГ 3: Инициализируем Saga (шаг валидации уже выполнен)
            // ==========================================
            var saga = OrderSaga.Create(order.Id, request.CorrelationId, timeoutSeconds: 30);
            saga.StartProcessing();
            
            var getProductStep = SagaStep.Create("GetProduct");
            getProductStep.Complete(); // Сразу помечаем как выполненный
            saga.AddStep(getProductStep);

            // ==========================================
            // 🔹 ШАГ 4: Формируем событие для оркестратора
            // ==========================================
            var orderCreatedEvent = new OrderCreatedEvent(
                OrderId: order.Id,
                ProductId: request.ProductId,
                Quantity: request.Quantity,
                CorrelationId: request.CorrelationId
            );

            var outboxMessage = new OutboxMessage
            {
                OrderId = order.Id,
                EventType = nameof(OrderCreatedEvent),
                Payload = JsonSerializer.Serialize(orderCreatedEvent)
            };

            await outboxRepository.AddAsync(outboxMessage, ct);

            // ==========================================
            // 🔹 ШАГ 5: ЕДИНЫЙ КОММИТ (Order + Saga + Outbox)
            // ==========================================
            await sagaRepository.CreateWithStepsAsync(saga, ct);
            await repository.SaveChangesAsync(ct);

            logger.LogInformation("Заказ {OrderId} принят. Оркестрация запущена.", order.Id);
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
        catch (Exception ex)
        {
            OrdersFailed.Add(1, new KeyValuePair<string, object?>("reason", ex.GetType().Name));
            logger.LogError(ex, "Неожиданная ошибка при создании заказа: {Message}", ex.Message);
            throw;
        }
    }
}
