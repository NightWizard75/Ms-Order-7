using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Shared.Interfaces;
using Infrastructure.Options;
using Infrastructure.Shared;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

/// <summary>
/// Получает токены через grant_type=client_credentials и кэширует их in-memory.
/// </summary>
public class ClientCredentialsTokenService(
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityClientOptions> options)
    : ITokenProvider
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("IdentityServer");
    private readonly IdentityClientOptions _options = options.Value;
    
    // 🔹 Потокобезопасный кэш: Scope -> CachedToken
    private readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new();

    public async Task<string> GetAccessTokenAsync(string scope, CancellationToken ct = default)
    {
        // 1️⃣ Проверка кэша (с запасом 30 сек до реального exp)
        if (_tokenCache.TryGetValue(scope, out var cached) && !cached.IsExpired)
            return cached.Token;

        // 2️⃣ Создание линкованного CancellationToken с тайм-аутом из конфига
        using var tokenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tokenCts.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

        // 3️⃣ Формирование тела запроса (x-www-form-urlencoded)
        var requestContent = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _options.ClientId),
            new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            new KeyValuePair<string, string>("scope", scope)
        ]);

        // 4️⃣ Запрос к /connect/token
        var response = await _httpClient.PostAsync(OidcEndpoints.Token, requestContent, tokenCts.Token);
        response.EnsureSuccessStatusCode();

        // 5️⃣ Парсинг ответа
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(tokenCts.Token)
            ?? throw new InvalidOperationException("IdentityServer returned empty token response");
        
        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new InvalidOperationException("Access token is missing in IdentityServer response");

        // 6️⃣ Сохранение в кэш и возврат
        var newToken = new CachedToken(tokenResponse.AccessToken, tokenResponse.ExpiresIn);
        _tokenCache[scope] = newToken;

        return newToken.Token;
    }

    // 🔹 Внутренние DTO (скрыты от внешнего кода)
    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("scope")] string Scope);
    
    private record CachedToken(string Token, int ExpiresInSeconds)
    {
        private readonly DateTime _acquiredAt = DateTime.UtcNow;
        
        // ⏱️ Считаем токен "истёкшим" за 30 секунд до фактического exp
        public bool IsExpired => DateTime.UtcNow > _acquiredAt.AddSeconds(ExpiresInSeconds - 30);
    }
}
