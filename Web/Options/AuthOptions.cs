namespace Web.Options;

/// <summary>
/// Настройки аутентификации для валидации токенов от внешнего провайдера (IdentityServer).
/// </summary>
public record AuthOptions
{
    /// <summary>
    /// URL IdentityServer для получения метаданных (/.well-known/openid-configuration).
    /// Пример: "http://localhost:5000"
    /// </summary>
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// Имя защищаемого API (должно совпадать с ApiResource.Name в IdentityServer).
    /// Пример: "orderservice"
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Требовать HTTPS для метаданных (включать только в продакшене).
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = false;
    
    public bool AllowInsecureHttps { get; init; } = false;
}
