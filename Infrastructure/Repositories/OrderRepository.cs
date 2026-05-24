using Application.Shared.DTOs.Order;
using Application.Shared.Interfaces;
using Domain.Entities;
using Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// Реализация репозитория заказов через EF Core.
/// </summary>
public class OrderRepository(OrderDbContext context) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Orders.FindAsync([id], ct).AsTask();
    
    public async Task<OrderStatusDto?> GetStatusByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Orders
            .Where(o => o.Id == id)
            .Select(o => new OrderStatusDto(o.Id, o.Status.ToString()))
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        await context.Orders.AddAsync(order, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        context.Orders.Update(order);
        await context.SaveChangesAsync(ct);
    }
    
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await context.SaveChangesAsync(ct);
    }
}
