using Application.Shared.Events;
using Application.Shared.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Domain.Entities.Saga;

namespace Application.Services;

public class OrderSagaOrchestrator(
    ISagaRepository sagaRepository,
    IOrderRepository orderRepository,
    IOutboxMessageRepository outboxRepository,
    ILogger<OrderSagaOrchestrator> logger)
{
    /// <summary>
    /// Обрабатывает событие StockReserved от ProductService.
    /// Запускает процесс оплаты.
    /// </summary>
    public async Task HandleStockReservedAsync(Guid orderId, StockReservedEvent @event, CancellationToken ct)
    {
        var saga = await sagaRepository.GetByOrderIdAsync(orderId, ct);
        
        if (saga is not { State: SagaState.Processing })
        {
            logger.LogWarning("Saga не найдена или не в статусе Processing: OrderId={OrderId}, State={State}", orderId, saga?.State);
            return;
        }
        
        var step = saga.Steps.FirstOrDefault(s => s.StepName == "ReserveStock");
        step?.Complete();
        
        // Получаем цену из БД через репозиторий.
        var order = await orderRepository.GetByIdAsync(orderId, ct);
        if (order == null)
        {
            logger.LogError("Заказ не найден при обработке резерва: OrderId={OrderId}", orderId);
            return;
        }
        
        // 👇 Обновляем статус заказа (например, на "Processing" = 1)
        // Если у тебя в домене нет метода для этого — добавь или используй прямой доступ к свойству
        order.MarkAsProcessing();
        await orderRepository.UpdateAsync(order, ct);

        // 👇 Публикуем команду на оплату через Outbox
        var paymentRequested = new PaymentRequestedEvent(
            OrderId: orderId,
            AmountInKopecks: order.PriceInKopecks, // ← Берём цену из сущности заказа
            CorrelationId: saga.CorrelationId
        );

        await outboxRepository.AddAsync(new OutboxMessage
        {
            OrderId = orderId,
            EventType = nameof(PaymentRequestedEvent),
            Payload = JsonSerializer.Serialize(paymentRequested)
        }, ct);

        await sagaRepository.UpdateAsync(saga, ct);
        
        logger.LogInformation("Сток зарезервирован. Запрошена оплата: OrderId={OrderId}", orderId);
    }

    /// <summary>
    /// Обрабатывает событие об ошибке резерва.
    /// </summary>
    public async Task HandleStockReservationFailedAsync(Guid orderId, StockReservationFailedEvent @event, CancellationToken ct)
    {
        await HandleStepFailedAsync(orderId, "ReserveStock", @event.Reason, ct);
    }

    /// <summary>
    /// Обрабатывает событие PaymentProcessed от PaymentService.
    /// Финализирует заказ или запускает компенсацию.
    /// </summary>
    public async Task HandlePaymentProcessedAsync(Guid orderId, PaymentProcessedEvent @event, CancellationToken ct)
    {
        var saga = await sagaRepository.GetByOrderIdAsync(orderId, ct);
        if (saga == null || saga.State != SagaState.Processing) return;

        var step = saga.Steps.FirstOrDefault(s => s.StepName == "ProcessPayment");
        
        if (@event.Success)
        {
            step?.Complete();
            saga.Complete();
            
            var order = await orderRepository.GetByIdAsync(orderId, ct);
            if (order != null)
            {
                order.Confirm();
                await orderRepository.UpdateAsync(order, ct);
            }
            
            await sagaRepository.UpdateAsync(saga, ct);
            await orderRepository.SaveChangesAsync(ct);
            
            logger.LogInformation("Оплата прошла. Заказ подтверждён: OrderId={OrderId}", orderId);
        }
        else
        {
            // 🔹 ErrorMessage — ок, так и определено в record (nullable string)
            step?.Fail(@event.ErrorMessage ?? "Неизвестная ошибка оплаты");
            await CompensateAsync(saga, @event.ErrorMessage ?? "Неизвестная ошибка оплаты", ct);
        }
    }

    /// <summary>
    /// Обработка ошибок на любом шаге.
    /// </summary>
    public async Task HandleStepFailedAsync(Guid orderId, string stepName, string reason, CancellationToken ct)
    {
        var saga = await sagaRepository.GetByOrderIdAsync(orderId, ct);
        if (saga == null) return;
        
        var step = saga.Steps.FirstOrDefault(s => s.StepName == stepName);
        step?.Fail(reason);

        await CompensateAsync(saga, reason, ct);
        logger.LogWarning("Шаг саги провалился, запущена компенсация: OrderId={OrderId}, Step={Step}", orderId, stepName);
    }

    private async Task CompensateAsync(OrderSaga saga, string reason, CancellationToken ct)
    {
        if (saga.State != SagaState.Compensating)
        {
            saga.StartCompensation();
        }
        
        var order = await orderRepository.GetByIdAsync(saga.OrderId, ct);
        if (order != null)
        {
            order.Cancel(reason);
            await orderRepository.UpdateAsync(order, ct);
        }

        saga.Fail(); // Завершаем сагу как неудачную после компенсации
        
        await sagaRepository.UpdateAsync(saga, ct);
        await orderRepository.SaveChangesAsync(ct);
        
        logger.LogInformation("Заказ отменён (компенсация): OrderId={OrderId}", saga.OrderId);
    }
}
