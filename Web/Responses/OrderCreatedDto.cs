namespace Web.Responses;

public record OrderCreatedDto(
    Guid Id,
    string Status,
    int TotalAmount
);
