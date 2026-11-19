using System.Data.Common;
using System.Text;
using AbySalto.Mid.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AbySalto.Mid.Errors;

internal static class ExceptionRedaction
{
    public static bool InvolvesDatabase(Exception exception) =>
        Chain(exception).Any(e => e is DbException or DbUpdateException);

    public static string SafeMessage(Exception exception) =>
        IsOurs(exception) ? exception.Message : "(database error, message redacted)";

    public static string DescribeChain(Exception exception) =>
        string.Join(" > ", Chain(exception).Select(e => e switch
        {
            DbException db when db.SqlState is not null => $"{db.GetType().Name} SQLSTATE {db.SqlState}",
            _ => e.GetType().Name,
        }));

    public static string StackTraces(Exception exception)
    {
        var sb = new StringBuilder();
        foreach (var e in Chain(exception))
            if (e.StackTrace is { } trace)
                sb.AppendLine($"--- {e.GetType().Name}").AppendLine(trace);
        return sb.ToString();
    }

    private static bool IsOurs(Exception e) =>
        e is ConflictException or ValidationException or NotFoundException
            or AuthenticationFailedException or ProductCatalogUnavailableException
            or Domain.Exceptions.DomainException;

    private static IEnumerable<Exception> Chain(Exception? e)
    {
        for (; e is not null; e = e.InnerException)
            yield return e;
    }
}
