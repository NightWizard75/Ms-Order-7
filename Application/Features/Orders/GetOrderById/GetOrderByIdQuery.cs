using Application.Shared.DTOs.Order;
using MediatR;

namespace Application.Features.Orders.GetOrderById;

/// <summary>
/// Запрос на получение заказа по идентификатору.
/// </summary>
/// <param name="Id">Идентификатор заказа</param>
public record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto>;
