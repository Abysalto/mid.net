using AbySalto.Mid.Application.Common.Exceptions;
using AbySalto.Mid.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AbySalto.Mid.Errors;

internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler 
{
    private const string UnexpectedErrorDetail = "An unexpected error occurred. Quote the traceId when reporting it.";

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ProductCatalogUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Product catalogue unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        Log(context, exception, status);

        var detail = status == StatusCodes.Status500InternalServerError ? UnexpectedErrorDetail : exception.Message;

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        AddCodeAndDetails(problem, exception, status);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    private static void AddCodeAndDetails(ProblemDetails problem, Exception exception, int status)
    {
        var (code, details) = exception switch
        {
            IApiError e => (e.Code, e.Details),
            DomainException e => (e.Code, e.Details),
            _ => (null, null),
        };

        problem.Extensions["code"] = code ?? exception switch
        {
            ValidationException => "validation_failed",
            DomainException => "business_rule_violated",
            AuthenticationFailedException => "auth.failed",
            NotFoundException => "not_found",
            ConflictException => "conflict",
            ProductCatalogUnavailableException => "product.catalog_unavailable",
            _ => "server_error",
        };

        if (details is null || status >= 500)
            return;

        foreach (var (key, value) in details)
            problem.Extensions.TryAdd(key, value);
    }

    private void Log(HttpContext context, Exception exception, int status)
    {
        var level = status switch
        {
            >= 500 when status != StatusCodes.Status503ServiceUnavailable => LogLevel.Error,
            StatusCodes.Status503ServiceUnavailable or StatusCodes.Status409Conflict => LogLevel.Warning,
            _ when exception.InnerException is not null => LogLevel.Warning,
            _ => LogLevel.Information,
        };

        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
        var userId = context.User.FindFirst("sub")?.Value;

        if (ExceptionRedaction.InvolvesDatabase(exception))
        {
            logger.Log(level,
                "{Method} {Path} -> {Status} for user {UserId}: {Message}. Cause: {Cause} (traceId {TraceId}){NewLine}{StackTrace}",
                context.Request.Method, context.Request.Path, status, userId,
                ExceptionRedaction.SafeMessage(exception), ExceptionRedaction.DescribeChain(exception), traceId,
                Environment.NewLine, ExceptionRedaction.StackTraces(exception));
            return;
        }

        logger.Log(level,
            level >= LogLevel.Warning ? exception : null,
            "{Method} {Path} -> {Status} for user {UserId}: {ExceptionType}: {Message} (traceId {TraceId})",
            context.Request.Method, context.Request.Path, status, userId,
            exception.GetType().Name, exception.Message, traceId);
    }
}
