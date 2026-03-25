namespace Domain.Enums;

public enum OrderStatus
{
    Pending,      // 👈 Заказ создан, ждёт резервирования стока
    Processing,   // 👈 Резервирование в процессе
    Confirmed,    // 👈 Успешно завершён
    Cancelled,    // 👈 Отменён (компенсация)
    Failed        // 👈 Завершён с ошибкой
}
