namespace Domain.Exceptions;

public abstract class DomainException(
    string message, 
    string errorCode, 
    Dictionary<string, object?>? context = null) 
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public Dictionary<string, object?> Context { get; } = context ?? new Dictionary<string, object?>();
}
