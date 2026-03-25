namespace Web.Responses;

public record ApiResponse<T>(
    bool Success,
    int StatusCode,
    T? Data,
    string? Message = null
);
