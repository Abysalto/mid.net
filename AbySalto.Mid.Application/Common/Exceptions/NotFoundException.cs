namespace AbySalto.Mid.Application.Common.Exceptions;

public class NotFoundException(string message, string? code = null, IReadOnlyDictionary<string, object?>? details = null)
    : Exception(message), IApiError
{
    public string? Code { get; } = code;
    public IReadOnlyDictionary<string, object?>? Details { get; } = details;
}
