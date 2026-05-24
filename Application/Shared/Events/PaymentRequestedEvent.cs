namespace Application.Shared.Events;

/// <summary>
/// Команда: запрос на оплату заказа.
/// Публикуется оркестратором после успешного резерва стока.
/// </summary>
public record PaymentRequestedEvent(
    Guid OrderId,
    long AmountInKopecks, // Сумма в копейках
    string CorrelationId);
