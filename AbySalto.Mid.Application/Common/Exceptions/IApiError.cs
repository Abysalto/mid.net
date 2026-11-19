namespace AbySalto.Mid.Application.Common.Exceptions;

public interface IApiError
{
    string? Code { get; }

    IReadOnlyDictionary<string, object?>? Details { get; }
}
