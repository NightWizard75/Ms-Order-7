using System.Net.Http.Headers;
using Application.Shared.Interfaces;

namespace Infrastructure.Handlers;

/// <summary>
/// Добавляет Bearer-токен к исходящим запросам автоматически.
/// Используется с IHttpClientFactory через AddHttpMessageHandler.
/// </summary>
public class AuthDelegatingHandler(ITokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Если заголовок уже установлен — не перезаписываем
        if (request.Headers.Authorization != null)
            return await base.SendAsync(request, cancellationToken);

        // Получаем токен и добавляем к запросу
        var token = await tokenProvider.GetAccessTokenAsync("productservice.reserve", cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
