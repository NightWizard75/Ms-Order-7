using Application.Shared.Interfaces;
using Domain.Entities;
using Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class OutboxMessageRepository(OrderDbContext context) : IOutboxMessageRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await context.OutboxMessages.AddAsync(message, ct);
        // 👇 Не вызываем SaveChangesAsync здесь — это сделает вызывающая сторона (единый коммит)
    }
}
