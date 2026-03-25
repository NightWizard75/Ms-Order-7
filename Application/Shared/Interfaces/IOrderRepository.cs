using Application.Shared.DTOs.Order;
using Domain.Entities;

namespace Application.Shared.Interfaces;

/// <summary>
/// Репозиторий для работы с заказами.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    Task<OrderStatusDto?> GetStatusByIdAsync(Guid id, CancellationToken ct = default);
}
