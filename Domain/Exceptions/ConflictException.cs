namespace Domain.Exceptions;

public class ConflictException(
    string message, 
    string errorCode, 
    Dictionary<string, object?>? context = null) 
    : DomainException(message, errorCode, context);
