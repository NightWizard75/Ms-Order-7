namespace Application.Shared.Interfaces;

/// <summary>
/// Контракт для получения access_token от централизованного провайдера аутентификации.
/// Используется для межсервисных вызовов по потоку client_credentials.
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Получает access_token для указанного scope.
    /// Реализация должна обеспечивать кэширование и обработку истечения срока действия.
    /// </summary>
    /// <param name="scope">Требуемый scope (например, "productservice.reserve")</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Строка access_token</returns>
    Task<string> GetAccessTokenAsync(string scope, CancellationToken ct = default);
}
