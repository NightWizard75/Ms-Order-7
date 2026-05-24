using Web.Responses;
using Application.Shared.DTOs.Order;

namespace IntegrationTest.Helpers;

/// <summary>
/// Хелпер для ожидания завершения Saga в интеграционных тестах.
/// </summary>
public static class SagaWaiter
{
    private const int MaxWaitSeconds = 30;
    private const int PollIntervalMs = 500;

    /// <summary>
    /// Ждёт, пока статус заказа изменится с Pending на конечный.
    /// </summary>
    public static async Task<OrderDto> WaitForOrderCompletionAsync(
        HttpClient client, 
        Guid orderId, 
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (stopwatch.Elapsed.TotalSeconds < MaxWaitSeconds)
        {
            var response = await client.GetAsync($"/api/orders/{orderId}", ct);
            response.EnsureSuccessStatusCode();
            
            var result = await response.ReadFromJsonAsync<ApiResponse<OrderDto>>(ct: ct);
            var order = result?.Data;
            
            if (order != null && order.Status != Domain.Enums.OrderStatus.Pending)
                return order;
            
            await Task.Delay(PollIntervalMs, ct);
        }
        
        throw new TimeoutException(
            $"Заказ {orderId} не завершил обработку за {MaxWaitSeconds} сек. Текущий статус: {await GetCurrentStatusAsync(client, orderId, ct)}");
    }

    private static async Task<Domain.Enums.OrderStatus> GetCurrentStatusAsync(
        HttpClient client, Guid orderId, CancellationToken ct)
    {
        var response = await client.GetAsync($"/api/orders/{orderId}", ct);
        var result = await response.ReadFromJsonAsync<ApiResponse<OrderDto>>(ct: ct);
        return result?.Data?.Status ?? Domain.Enums.OrderStatus.Pending;
    }
}
