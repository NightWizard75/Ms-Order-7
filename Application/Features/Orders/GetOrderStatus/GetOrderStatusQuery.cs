using Application.Shared.DTOs.Order;
using MediatR;

namespace Application.Features.Orders.GetOrderStatus;

/// <summary>
/// Запрос на получение статуса заказа по ID.
/// </summary>
/// <param name="Id">Идентификатор заказа</param>
public record GetOrderStatusQuery(Guid Id) : IRequest<OrderStatusDto>;
