using Domain.Entities.Saga;

namespace Application.Shared.Interfaces;

/// <summary>
/// Репозиторий для работы с Saga-оркестрацией.
/// </summary>
public interface ISagaRepository
{
    /// <summary>
    /// Создаёт новую Saga в БД.
    /// </summary>
    Task<OrderSaga> CreateAsync(OrderSaga saga, CancellationToken ct = default);
    
    /// <summary>
    /// Создаёт Saga со всеми шагами в одной транзакции
    /// </summary>
    Task<OrderSaga> CreateWithStepsAsync(OrderSaga saga, CancellationToken ct = default);
    
    /// <summary>
    /// Находит Saga по ID заказа.
    /// Возвращает null, если Saga не найдена.
    /// </summary>
    Task<OrderSaga?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    
    /// <summary>
    /// Обновляет существующую Saga (статус, шаги).
    /// </summary>
    Task UpdateAsync(OrderSaga saga, CancellationToken ct = default);
    
    /// <summary>
    /// Добавляет шаг к существующей Saga.
    /// </summary>
    Task AddStepAsync(Guid sagaId, SagaStep step, CancellationToken ct = default);
}
