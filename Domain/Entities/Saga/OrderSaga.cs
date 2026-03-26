using Domain.Enums;

namespace Domain.Entities.Saga;

/// <summary>
/// Saga-оркестратор для создания заказа.
/// Управляет последовательностью шагов и компенсирующими транзакциями.
/// </summary>
public class OrderSaga
{
    public Guid Id { get; private set; }
    
    /// <summary>
    /// Связь с заказом (1:1).
    /// </summary>
    public Guid OrderId { get; private set; }
    
    /// <summary>
    /// Текущий статус Saga.
    /// </summary>
    public SagaState State { get; internal set; }
    
    /// <summary>
    /// Список шагов Saga.
    /// </summary>
    public List<SagaStep> Steps { get; private set; } = new();
    
    /// <summary>
    /// Когда Saga создана.
    /// </summary>
    public DateTime CreatedAt { get; private set; }
    
    /// <summary>
    /// Когда Saga завершена (Completed или Failed).
    /// </summary>
    public DateTime? CompletedAt { get; private set; }
    
    /// <summary>
    /// CorrelationId для сквозной трассировки.
    /// </summary>
    public string CorrelationId { get; private set; } = string.Empty;
    
    /// <summary>
    /// Таймаут на каждый шаг (в секундах).
    /// </summary>
    public int TimeoutSeconds { get; private set; } = 30;
    
    // 👇 Конструктор для EF Core
    private OrderSaga() { }
    
    /// <summary>
    /// Создаёт новую Saga для заказа.
    /// </summary>
    public static OrderSaga Create(Guid orderId, string correlationId, int timeoutSeconds = 30)
    {
        return new OrderSaga
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            State = SagaState.Started,
            CorrelationId = correlationId,
            TimeoutSeconds = timeoutSeconds,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Начинает выполнение Saga (переход в Processing).
    /// </summary>
    public void StartProcessing()
    {
        if (State != SagaState.Started)
            throw new InvalidOperationException($"Нельзя начать Saga в статусе {State}");
        
        State = SagaState.Processing;
    }
    
    /// <summary>
    /// Добавляет новый шаг к Saga.
    /// </summary>
    public void AddStep(SagaStep step)
    {
        Steps.Add(step);
    }
    
    /// <summary>
    /// Получает текущий шаг (последний добавленный).
    /// </summary>
    public SagaStep? GetCurrentStep() => Steps.LastOrDefault();
    
    /// <summary>
    /// Завершает Saga успешно.
    /// </summary>
    public void Complete()
    {
        if (State != SagaState.Processing)
            throw new InvalidOperationException($"Нельзя завершить Saga в статусе {State}");
        
        State = SagaState.Completed;
        CompletedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Начинает компенсацию (откат).
    /// </summary>
    public void StartCompensation()
    {
        State = SagaState.Compensating;
    }
    
    /// <summary>
    /// Завершает Saga с ошибкой (требует ручного вмешательства).
    /// </summary>
    public void Fail()
    {
        State = SagaState.Failed;
        CompletedAt = DateTime.UtcNow;
    }
}
