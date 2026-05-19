namespace Infrastructure.Options;

/// <summary>
/// Настройки клиента для получения токенов от IdentityServer.
/// Заполняется из appsettings.json через IOptions<T>.
/// </summary>
public record IdentityClientOptions
{
    /// <summary>Базовый адрес IdentityServer (например, "http://localhost:5265")</summary>
    public string Authority { get; init; } = string.Empty;
    
    /// <summary>Client ID этого сервиса</summary>
    public string ClientId { get; init; } = string.Empty;
    
    /// <summary>Client Secret этого сервиса</summary>
    public string ClientSecret { get; init; } = string.Empty;
    
    /// <summary>Таймаут запроса к /connect/token (секунды)</summary>
    public int RequestTimeoutSeconds { get; init; } = 10;
}
