using Application.Shared.DTOs.Order;
using Application.Shared.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Orders.GetOrderStatus;

/// <summary>
/// Обработчик запроса на получение статуса заказа.
/// Оптимизирован: выбирает из БД только поля Id и Status.
/// </summary>
public class GetOrderStatusHandler(IOrderRepository repository)
    : IRequestHandler<GetOrderStatusQuery, OrderStatusDto>
{
    /// <summary>
    /// Обрабатывает запрос на получение статуса заказа.
    /// </summary>
    public async Task<OrderStatusDto> Handle(GetOrderStatusQuery request, CancellationToken ct)
    {
        var statusDto = await repository.GetStatusByIdAsync(request.Id, ct);
        
        // 👇 Если не найден — выбрасываем исключение (обработается в ExceptionHandler)
        if (statusDto is null)
            throw new EntityNotFoundException(
                "Order", 
                new Dictionary<string, object?> { ["id"] = request.Id });
        
        return statusDto;
    }
}
