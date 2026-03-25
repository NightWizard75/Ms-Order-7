namespace Domain.Exceptions;

public class EntityNotFoundException(
    string resource, 
    Dictionary<string, object?> searchCriteria) 
    : DomainException(
        BuildMessage(resource, searchCriteria), 
        "NOT_FOUND",
        BuildContext(resource, searchCriteria))
{
    private static string BuildMessage(string resource, Dictionary<string, object?> criteria)
    {
        var criteriaString = string.Join(", ", 
            criteria.Select(kvp => $"{kvp.Key}='{kvp.Value}'"));
        
        return $"Сущность '{resource}' не найдена по критериям: [{criteriaString}]";
    }

    private static Dictionary<string, object?> BuildContext(
        string resource, 
        Dictionary<string, object?> criteria)
    {
        return new Dictionary<string, object?>
        {
            ["resource"] = resource,
            ["searchCriteria"] = criteria
        };
    }
}
