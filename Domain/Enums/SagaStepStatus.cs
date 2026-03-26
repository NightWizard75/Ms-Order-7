namespace Domain.Enums;

/// <summary>
/// Статусы отдельного шага Saga.
/// </summary>
public enum SagaStepStatus
{
    /// <summary>
    /// Шаг ещё не начат.
    /// </summary>
    Pending,
    
    /// <summary>
    /// Шаг успешно выполнен.
    /// </summary>
    Success,
    
    /// <summary>
    /// Шаг не удался (ошибка или таймаут).
    /// </summary>
    Failed,
    
    /// <summary>
    /// Шаг был успешно откомпенсирован (откат).
    /// </summary>
    Compensated
}
