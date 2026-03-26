using Application.Shared.Interfaces;
using Domain.Entities.Saga;
using Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class SagaRepository(OrderDbContext context) : ISagaRepository
{
    public async Task<OrderSaga> CreateAsync(OrderSaga saga, CancellationToken ct = default)
    {
        await context.OrderSagas.AddAsync(saga, ct);
        await context.SaveChangesAsync(ct);
        return saga;
    }
    
    // 👇 НОВЫЙ МЕТОД: Создаёт Saga + все шаги в одной транзакции
    public async Task<OrderSaga> CreateWithStepsAsync(OrderSaga saga, CancellationToken ct = default)
    {
        await context.OrderSagas.AddAsync(saga, ct);
        
        foreach (var step in saga.Steps)
        {
            step.SetSagaId(saga.Id);  // 👈 Устанавливаем связь
            await context.SagaSteps.AddAsync(step, ct);
        }
        
        await context.SaveChangesAsync(ct);
        return saga;
    }
    
    public async Task<OrderSaga?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        return await context.OrderSagas
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);
    }
    
    public async Task UpdateAsync(OrderSaga saga, CancellationToken ct = default)
    {
        // 👇 Теперь используется только для компенсации (редко)
        var entry = context.Entry(saga);
        
        if (entry.State == EntityState.Detached)
        {
            context.OrderSagas.Attach(saga);
        }
        
        foreach (var step in from step in saga.Steps.ToList() let stepEntry = context.Entry(step) where stepEntry.State == EntityState.Detached select step)
        {
            step.SetSagaId(saga.Id);
            context.SagaSteps.Add(step);
        }
        
        await context.SaveChangesAsync(ct);
    }
    
    public async Task AddStepAsync(Guid sagaId, SagaStep step, CancellationToken ct = default)
    {
        var saga = await context.OrderSagas.FindAsync([sagaId], ct);
        if (saga is null)
            throw new InvalidOperationException($"Saga {sagaId} not found");
        
        saga.AddStep(step);
        await context.SaveChangesAsync(ct);
    }
}
