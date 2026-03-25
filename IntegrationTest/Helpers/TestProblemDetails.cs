using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntegrationTest.Helpers;

/// <summary>
/// DTO для десериализации ошибок в тестах.
/// Точно соответствует формату, который возвращает ExceptionHandler.
/// </summary>
public class TestProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
    
    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}
