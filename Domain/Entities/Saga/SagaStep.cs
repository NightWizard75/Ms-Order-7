using Domain.Enums;

namespace Domain.Entities.Saga;

/// <summary>
/// Отдельный шаг Saga-оркестрации.
/// Хранится в БД для отладки, повтора и мониторинга.
/// </summary>
public class SagaStep
{
    public Guid Id { get; private set; }
    
    public Guid SagaId { get; private set; }  
    
    /// <summary>
    /// Имя шага: "CreateOrder", "ReserveStock", "ConfirmOrder", "Compensate"
    /// </summary>
    public string StepName { get; private set; } = string.Empty;
    
    /// <summary>
    /// Текущий статус шага.
    /// </summary>
    public SagaStepStatus Status { get; private set; }
    
    /// <summary>
    /// Когда шаг начал выполнение.
    /// </summary>
    public DateTime? StartedAt { get; private set; }
    
    /// <summary>
    /// Когда шаг завершился (успех или ошибка).
    /// </summary>
    public DateTime? CompletedAt { get; private set; }
    
    /// <summary>
    /// Сообщение об ошибке (если статус = Failed).
    /// </summary>
    public string? ErrorMessage { get; private set; }
    
    /// <summary>
    /// Результат компенсации (если статус = Compensated).
    /// </summary>
    public string? CompensationResult { get; private set; }
    
    /// <summary>
    /// Сколько раз пытались выполнить шаг (для retry).
    /// </summary>
    public int RetryCount { get; private set; }
    
    /// <summary>
    /// Таймаут для этого шага (в секундах).
    /// </summary>
    public int TimeoutSeconds { get; private set; }
    
    // 👇 Конструктор для EF Core
    private SagaStep() { }
    
    public void SetSagaId(Guid sagaId) => SagaId = sagaId;
    
    /// <summary>
    /// Создаёт новый шаг в статусе Pending.
    /// </summary>
    public static SagaStep Create(string stepName, int timeoutSeconds = 30)
    {
        return new SagaStep
        {
            Id = Guid.NewGuid(),
            StepName = stepName,
            Status = SagaStepStatus.Pending,
            TimeoutSeconds = timeoutSeconds,
            RetryCount = 0
        };
    }
    
    /// <summary>
    /// Начинает выполнение шага.
    /// </summary>
    public void Start()
    {
        Status = SagaStepStatus.Pending;
        StartedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Помечает шаг как успешный.
    /// </summary>
    public void Complete()
    {
        Status = SagaStepStatus.Success;
        CompletedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Помечает шаг как неудачный.
    /// </summary>
    public void Fail(string errorMessage)
    {
        Status = SagaStepStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
        RetryCount++;
    }
    
    /// <summary>
    /// Помечает шаг как откомпенсированный.
    /// </summary>
    public void Compensate(string result)
    {
        Status = SagaStepStatus.Compensated;
        CompensationResult = result;
        CompletedAt = DateTime.UtcNow;
    }
}
