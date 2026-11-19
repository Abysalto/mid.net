namespace AbySalto.Mid.Application.Common.Exceptions;

public class ConflictException(string message, Exception? inner = null, string? code = null)
    : Exception(message, inner), IApiError
{
    public string? Code { get; } = code;
    public IReadOnlyDictionary<string, object?>? Details => null;
}
