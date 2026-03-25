namespace Application.Exceptions;

/// <summary>
/// Исключение: продукт не найден во внешнем Product Service.
/// НЕ использовать для внутренней БД Order Service!
/// </summary>
public class ProductNotFoundException(Guid productId, Dictionary<string, object?>? context = null)
    : ApplicationException($"Product {productId} not found in Product Service")
{
    public Guid ProductId { get; } = productId;
    public string ErrorCode { get; } = "PRODUCT_NOT_FOUND";
    public Dictionary<string, object?>? Context { get; } = context;
}
