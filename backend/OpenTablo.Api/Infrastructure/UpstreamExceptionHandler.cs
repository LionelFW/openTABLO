using Microsoft.AspNetCore.Diagnostics;
using OpenTablo.Api.Prim;

namespace OpenTablo.Api.Infrastructure;

/// <summary>Turns quota exhaustion and upstream failures into ProblemDetails instead of a bare 500.</summary>
public sealed class UpstreamExceptionHandler(IProblemDetailsService problemDetails, ILogger<UpstreamExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        (int Status, string Title)? mapped = exception switch
        {
            QuotaExceededException => (StatusCodes.Status429TooManyRequests, "Live data quota reached, try again later."),
            HttpRequestException or TaskCanceledException when !ct.IsCancellationRequested => (StatusCodes.Status502BadGateway, "An upstream data provider is unavailable."),
            _ => null,
        };
        if (mapped is not { } m) return false;

        logger.LogWarning(exception, "Upstream failure mapped to {Status}", m.Status);
        context.Response.StatusCode = m.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = m.Status, Title = m.Title },
        });
    }
}
