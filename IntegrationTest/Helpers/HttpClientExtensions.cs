using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace IntegrationTest.Helpers;

public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> PostJsonAsync<TRequest>(
        this HttpClient client, 
        string requestUri, 
        TRequest content, 
        CancellationToken ct = default)
    {
        return await HttpClientJsonExtensions
            .PostAsJsonAsync(client, requestUri, content, ct);
    }

    public static async Task<TResponse?> ReadFromJsonAsync<TResponse>(
        this HttpResponseMessage response, 
        CancellationToken ct = default) where TResponse : class
    {
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
    }

    public static async Task<TestProblemDetails?> ReadProblemDetailsAsync(
        this HttpResponseMessage response, 
        CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        return await response.Content.ReadFromJsonAsync<TestProblemDetails>(options, ct);
    }
}
