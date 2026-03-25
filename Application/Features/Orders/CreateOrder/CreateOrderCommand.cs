using Application.Shared.DTOs.Order;using MediatR;

namespace Application.Features.Orders.CreateOrder;

/// <summary>
/// Команда на создание нового заказа.
/// </summary>
/// <param name="ProductId">ID продукта</param>
/// <param name="Quantity">Количество</param>
/// <param name="CustomerEmail">Email клиента</param>
/// <param name="CorrelationId">ID корреляции для трассировки</param>
public record CreateOrderCommand(
    Guid ProductId,
    int Quantity,
    string CustomerEmail,
    string CorrelationId
) : IRequest<OrderDto>;  // 👈 Возвращает DTO созданного заказа
