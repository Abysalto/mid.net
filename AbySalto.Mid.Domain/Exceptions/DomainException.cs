namespace AbySalto.Mid.Domain.Exceptions;

public class DomainException(string message, string? code = null, IReadOnlyDictionary<string, object?>? details = null)
    : Exception(message)
{
    public string? Code { get; } = code;
    public IReadOnlyDictionary<string, object?>? Details { get; } = details;
}
