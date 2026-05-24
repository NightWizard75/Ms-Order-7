namespace Application.Shared.Events;

/// <summary>
/// Событие: результат обработки платежа.
/// Публикуется PaymentService после имитации списания.
/// </summary>
public record PaymentProcessedEvent(
    Guid OrderId,
    bool Success,
    string? ErrorMessage, // Заполняется только если Success = false
    string CorrelationId);
