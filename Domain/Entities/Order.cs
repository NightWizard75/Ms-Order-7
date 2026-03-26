using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Сущность заказа. Использует Rich Domain Model с инкапсуляцией бизнес-правил.
/// </summary>
public class Order
{
    // 👇 Приватный параметрless-конструктор для EF Core
    private Order() { }

    // 👇 Публичный первичный конструктор для создания новых заказов
    public Order(
        Guid id,
        Guid productId,
        int quantity,
        int priceInKopecks,  // 👈 Цена в копейках
        string customerEmail,
        string correlationId)
    {
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));
        Guard.AgainstNegative(priceInKopecks, nameof(priceInKopecks));
        Guard.AgainstNullOrEmpty(customerEmail, nameof(customerEmail));
        // 👇 Defensive check: если что-то сломается, поймаем баг на раннем этапе
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId обязателен", nameof(correlationId));

        Id = id;
        ProductId = productId;
        Quantity = quantity;
        PriceInKopecks = priceInKopecks;
        CustomerEmail = customerEmail.Trim().ToLowerInvariant();
        CorrelationId = correlationId;
        
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        TotalAmountInKopecks = CalculateTotalAmount();
    }

    // ========================================================================
    // Свойства (приватные сеттеры — изменение только через методы)
    // ========================================================================

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    
    /// <summary>Цена за единицу товара в копейках</summary>
    public int PriceInKopecks { get; private set; }
    
    /// <summary>Общая сумма заказа в копейках (вычисляемое поле)</summary>
    public int TotalAmountInKopecks { get; private set; }
    
    public OrderStatus Status { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Обновляет цену заказа после получения данных из продукта.
    /// Может вызываться только в статусе Pending.
    /// </summary>
    public void UpdatePrice(int priceInKopecks)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException(
                $"Нельзя обновить цену заказа в статусе {Status}");

        if (priceInKopecks < 0)
            throw new InvalidOperationException("Цена не может быть отрицательной");

        // 👇 Используем приватный сеттер через метод внутри класса
        PriceInKopecks = priceInKopecks;

        // 👇 Пересчитываем итоговую сумму после изменения цены
        TotalAmountInKopecks = CalculateTotalAmount();
    }

    // ========================================================================
    // Публичные методы-конвертеры
    // ========================================================================

    /// <summary>
    /// Возвращает цену за единицу в формате "X.XX RUB".
    /// Пример: 12500 → "125.00 RUB"
    /// </summary>
    public string GetPriceAsMoney() => FormatKopecks(PriceInKopecks);

    /// <summary>
    /// Возвращает общую сумму заказа в формате "X.XX RUB".
    /// </summary>
    public string GetTotalAmountAsMoney() => FormatKopecks(TotalAmountInKopecks);

    /// <summary>
    /// Внутренний метод форматирования: копейки → строка "X.XX RUB".
    /// </summary>
    private static string FormatKopecks(int kopecks)
    {
        var rubles = kopecks / 100m;
        return $"{rubles:F2} RUB";
    }

    // ========================================================================
    // Бизнес-методы (инкапсулируют инварианты)
    // ========================================================================

    /// <summary>
    /// Подтверждает заказ после успешного резервирования стока.
    /// Может быть вызван только для заказа в статусе Pending.
    /// </summary>
    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException(
                $"Невозможно подтвердить заказ в статусе {Status}. Ожидается статус Pending.");
        
        Status = OrderStatus.Confirmed;
    }

    /// <summary>
    /// Отменяет заказ (компенсирующее действие Saga).
    /// Может быть вызван для Pending или Processing.
    /// </summary>
    /// <param name="reason">Причина отмены для логирования</param>
    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Confirmed)
            throw new InvalidOperationException(
                $"Невозможно отменить заказ в статусе {Status}.");
        
        Status = OrderStatus.Cancelled;
        // 👇 Здесь можно добавить логирование причины, если нужно
    }

    /// <summary>
    /// Завершает заказ с ошибкой (финальный статус при сбое Saga).
    /// </summary>
    /// <param name="reason">Причина ошибки для логирования</param>
    public void Fail(string reason)
    {
        if (Status is OrderStatus.Cancelled or OrderStatus.Confirmed)
            throw new InvalidOperationException(
                $"Невозможно завершить с ошибкой заказ в статусе {Status}.");
        
        Status = OrderStatus.Failed;
    }

    /// <summary>
    /// Вычисляет общую сумму заказа в копейках.
    /// </summary>
    private int CalculateTotalAmount() => PriceInKopecks * Quantity;

    // ========================================================================
    // Вспомогательные методы для тестирования/отладки
    // ========================================================================

    public override string ToString() => 
        $"Order[{Id:N}]: {Quantity} x {GetPriceAsMoney()} = {GetTotalAmountAsMoney()}, Status={Status}";
}
