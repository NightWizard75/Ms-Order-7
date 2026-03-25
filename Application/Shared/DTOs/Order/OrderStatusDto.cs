namespace Application.Shared.DTOs.Order;

/// <summary>
/// Минимальный контракт для ответа со статусом заказа.
/// Используется в легковесных эндпоинтах, где не нужны полные данные.
/// </summary>
/// <param name="Id">Идентификатор заказа</param>
/// <param name="Status">Текущий статус заказа (строковое представление)</param>
public record OrderStatusDto(
    Guid Id, 
    string Status
);
