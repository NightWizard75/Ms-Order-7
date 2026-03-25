using System.Diagnostics.Metrics;
using Application.Exceptions;
using Application.Shared.DTOs.Order;
using Application.Shared.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Orders.CreateOrder;

/// <summary>
/// Обработчик команды создания заказа.
/// Использует Rich Domain Model: бизнес-правила инкапсулированы в сущности.
/// Этот хендлер создаёт заказ в статусе Pending (OrderStatus).
/// </summary>
public class CreateOrderHandler(
    IProductServiceClient productServiceClient,
    IOrderRepository repository,
    ILogger<CreateOrderHandler> logger)
    : IRequestHandler<CreateOrderCommand, OrderDto>
{
    // Объявление метрик (статические, один раз на класс)
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
            // 👇 ШАГ 1: Проверка существования продукта
            // ==========================================
            var product = await productServiceClient.GetProductAsync(request.ProductId, ct);
            
            if (product is null)
                throw new ProductNotFoundException(request.ProductId);
            
            // ==========================================
            // 👇 ШАГ 2: Создание заказа (если продукт найден)
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
                priceInKopecks: product.PriceInKopecks,
                customerEmail: request.CustomerEmail,
                correlationId: request.CorrelationId
            );

            // 👇 Сохраняем через интерфейс репозитория (не зависит от EF Core)
            await repository.AddAsync(order, ct);

            logger.LogInformation(
                "Заказ создан: OrderId={OrderId}, ProductId={ProductId}, Quantity={Quantity}, TotalAmount={TotalAmount}, CorrelationId={CorrelationId}",
                order.Id,
                order.ProductId,
                order.Quantity,
                order.GetTotalAmountAsMoney(),
                order.CorrelationId
            );
            
            // 👇 инкрементируем метрику ТОЛЬКО при успехе
            OrdersCreated.Add(1, new[] { 
                new KeyValuePair<string, object?>("productId", request.ProductId.ToString()) 
            });

            // ==========================================
            // 👇 ШАГ 3: Возврат DTO
            // ==========================================
            return order.ToDto();
        }
        catch (Exception ex)
        {
            // 👇 Запись метрики неудачного заказа
            OrdersFailed.Add(1, new KeyValuePair<string, object?>("reason", ex.GetType().Name));
            throw;
        }
    }
}
