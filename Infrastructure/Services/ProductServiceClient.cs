using System.Diagnostics;
using Application.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Application.Shared.DTOs.External;
using Newtonsoft.Json;

namespace Infrastructure.Services;

/// <summary>
/// Клиент для взаимодействия с Product Service.
/// Реализует политику повторных попыток (Polly Retry) для отказоустойчивости.
/// </summary>
public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceClient> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;
    
    // Метрики
    private static readonly Meter Meter = new("ProductServiceClient", "1.0");
    
    private static readonly Histogram<double> RequestDuration = 
        Meter.CreateHistogram<double>("product_service.request.duration_seconds", 
            description: "Product Service request duration in seconds");
    
    private static readonly Counter<int> RequestsTotal = 
        Meter.CreateCounter<int>("product_service.requests.total", 
            description: "Total requests to Product Service");
    
    private static readonly Counter<int> RequestsFailed = 
        Meter.CreateCounter<int>("product_service.requests.failed", 
            description: "Failed requests to Product Service");
    

    public ProductServiceClient(
        HttpClient httpClient,
        IOptions<ProductServiceClientSettings> settings,
        ResiliencePipeline<HttpResponseMessage> resiliencePipeline,
        ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _resiliencePipeline = resiliencePipeline;
        
        // 👇 Читаем настройки
        var config = settings.Value;
        
        // 👇 Настраиваем базовый адрес
        if (!string.IsNullOrEmpty(config.BaseAddress))
            _httpClient.BaseAddress = new Uri(config.BaseAddress);
    }

    public async Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default)
    {
        // 👇 Замер времени 
        // 👇 Замер времени через Stopwatch и счётчики
        var stopwatch = StartMetrics("ProductExistsAsync");
        
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async (cancelToken) => await _httpClient.GetAsync($"/api/products/{productId}", cancelToken),
                ct
            );

            return response.StatusCode switch
            {
                HttpStatusCode.OK => true,
                HttpStatusCode.NotFound => false,
                _ => throw new HttpRequestException($"Product Service вернул неожиданный статус: {response.StatusCode}")
            };
        }
        catch (HttpRequestException ex)
        {
            RecordFailure("ProductExistsAsync", ex.GetType().Name);
            _logger.LogError(ex, "Ошибка при проверке существования продукта {ProductId}", productId);
            throw;
        }
        finally
        {
            // 👇 Записываем длительность в секундах (Histogram принимает double)
            StopMetrics(stopwatch, "ProductExistsAsync");
        }
    }
    
    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        var stopwatch = StartMetrics("GetProductAsync");
    
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async (cancelToken) => await _httpClient.GetAsync($"/api/products/{productId}", cancelToken),
                ct
            );

            return response.StatusCode switch
            {
                HttpStatusCode.OK => (await response.Content.ReadFromJsonAsync<ShowProductResponseData>(ct))!.Data,
                HttpStatusCode.NotFound => null,  
                _ => throw new HttpRequestException($"Unexpected status: {response.StatusCode}")
            };
        }
        catch (HttpRequestException ex)
        {
            RecordFailure("GetProductAsync", ex.GetType().Name);
            _logger.LogError(ex, "Ошибка при получении продукта {ProductId}", productId);
            throw;
        }
        finally
        {
            StopMetrics(stopwatch, "GetProductAsync");
        }
    }

    public async Task<bool> ReserveStockAsync(Guid productId, int quantity, string correlationId, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        RequestsTotal.Add(1, new[] { 
            new KeyValuePair<string, object?>("method", "ReserveStockAsync") 
        });
    
        try
        {
            var request = new ReserveStockRequest(productId, quantity, correlationId);
        
            // 👇 Newtonsoft.Json сериализация
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _resiliencePipeline.ExecuteAsync(
                async (cancelToken) => await _httpClient.PostAsync("/api/products/reserve", content, cancelToken),
                ct
            );

            return response.StatusCode switch
            {
                HttpStatusCode.OK or HttpStatusCode.NoContent => true,
                HttpStatusCode.NotFound => false,
                HttpStatusCode.Conflict => LogAndReturnFalse(productId, quantity),
                _ => throw new HttpRequestException($"Product Service вернул неожиданный статус при резерве: {response.StatusCode}")
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            RequestsFailed.Add(1, new[] { 
                new KeyValuePair<string, object?>("method", "ReserveStockAsync"),
                new KeyValuePair<string, object?>("error", ex.GetType().Name)
            });
            _logger.LogError(ex, "Ошибка при резервировании стока для продукта {ProductId}", productId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            RequestDuration.Record(
                stopwatch.Elapsed.TotalSeconds, 
                new[] { new KeyValuePair<string, object?>("method", "ReserveStockAsync") });
        }
    }

    // 👇 Вынесенный метод для логирования
    private bool LogAndReturnFalse(Guid productId, int quantity)
    {
        _logger.LogWarning(
            "Недостаточно стока для продукта {ProductId}. Запрошено: {Quantity}",
            productId,
            quantity
        );
        return false;
    }
    
    // ========================================================================
    // 👇 Приватные хелперы для метрик
    // ========================================================================
    private Stopwatch StartMetrics(string methodName)
    {
        var stopwatch = Stopwatch.StartNew();
        RequestsTotal.Add(1, new[] { 
            new KeyValuePair<string, object?>("method", methodName) 
        });
        
        return stopwatch;
    }

    private void StopMetrics(Stopwatch stopwatch, string methodName)
    {
        stopwatch.Stop();
        RequestDuration.Record(
            stopwatch.Elapsed.TotalSeconds, 
            new[] { new KeyValuePair<string, object?>("method", methodName) });
    }

    private void RecordFailure(string methodName, string errorType)
    {
        RequestsFailed.Add(1, new[] { 
            new KeyValuePair<string, object?>("method", methodName),
            new KeyValuePair<string, object?>("error", errorType)
        });
    }
}
