using Application.Features.Orders.CreateOrder;
using System.Globalization;

namespace Web.Requests;

/// <summary>
/// DTO для запроса создания заказа.
/// Используется только на границе API (Web слой).
/// Поддерживает два формата цены: копейки (int) или денежная строка (decimal).
/// Требуется ровно одно из двух полей.
/// </summary>
public record CreateOrderRequest(
    Guid ProductId,
    int Quantity,
    string CustomerEmail
)
{
    /// <summary>
    /// Конвертирует запрос в команду.
    /// Вызывается ПОСЛЕ валидации (гарантировано одно поле заполнено).
    /// </summary>
    /// <param name="correlationId">ID корреляции для трассировки (из middleware)</param>
    /// <returns>Команда для MediatR</returns>
    public CreateOrderCommand ToCommand(string correlationId)
    {
        return new CreateOrderCommand(
            ProductId: ProductId,
            Quantity: Quantity,
            CustomerEmail: CustomerEmail,
            CorrelationId: correlationId
        );
    }
}
