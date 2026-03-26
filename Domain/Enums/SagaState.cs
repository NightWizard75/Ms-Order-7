namespace Domain.Enums;

/// <summary>
/// Статусы Saga-оркестрации.
/// </summary>
public enum SagaState
{
    /// <summary>
    /// Saga создана, первый шаг не начат.
    /// </summary>
    Started,
    
    /// <summary>
    /// Выполняется текущий шаг (например, ждём ответ от Product Service).
    /// </summary>
    Processing,
    
    /// <summary>
    /// Все шаги успешны, Saga завершена.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Произошла ошибка, выполняем компенсирующие транзакции.
    /// </summary>
    Compensating,
    
    /// <summary>
    /// Откат не удался, требуется ручное вмешательство.
    /// </summary>
    Failed
}
