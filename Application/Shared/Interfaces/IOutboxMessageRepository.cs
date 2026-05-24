using Domain.Entities;

namespace Application.Shared.Interfaces;

/// <summary>
/// Репозиторий для сохранения сообщений в Transactional Outbox.
/// Реализация использует тот же DbContext, что и OrderRepository, 
/// поэтому SaveChangesAsync коммитит всё одной транзакцией.
/// </summary>
public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
}
