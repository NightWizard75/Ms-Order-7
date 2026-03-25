namespace Infrastructure.Services;

/// <summary>
/// Настройки для подключения к Product Service.
/// Выносятся в appsettings.json для гибкости конфигурации.
/// </summary>
public class ProductServiceClientSettings
{
    /// <summary>
    /// Базовый URL Product Service (например, "http://localhost:5001").
    /// </summary>
    public string BaseAddress { get; set; } = string.Empty;

    /// <summary>
    /// Таймаут на один HTTP-запрос (в секундах).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Максимальное количество повторных попыток при временных ошибках.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Время разрыва цепи (Circuit Breaker) при частых ошибках (в секундах).
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
