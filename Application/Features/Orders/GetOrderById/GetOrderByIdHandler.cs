using Application.Shared.DTOs.Order;
using Application.Shared.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Orders.GetOrderById;

/// <summary>
/// Обработчик запроса на получение заказа по ID.
/// </summary>
public class GetOrderByIdHandler(IOrderRepository repository)
    : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        // 👇 Получаем сущность из репозитория
        var order = await repository.GetByIdAsync(request.Id, ct);

        // 👇 Если не найден — выбрасываем исключение (обработается глобально)
        if (order is null)
            throw new EntityNotFoundException(
                "Order", 
                new Dictionary<string, object?> { ["id"] = request.Id });

        // 👇 Конвертируем в DTO через extension-метод
        return order.ToDto();
    }
}
