namespace ECom.Shared.Web;

using ECom.Shared.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException nfe => (StatusCodes.Status404NotFound, "Not Found", nfe.Message),
            ConflictException ce => (StatusCodes.Status409Conflict, ce.ConflictTitle ?? "Conflict", ce.Message),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Unauthorized", "Invalid email or password."),
            DomainException de => (StatusCodes.Status400BadRequest, "Bad Request", de.Message),
            GatewayException => (StatusCodes.Status503ServiceUnavailable, "Payment service unavailable",
                "The payment service is temporarily unavailable. Please retry with the same idempotency key."),
            DownstreamServiceException => (StatusCodes.Status503ServiceUnavailable, "Service unavailable",
                "A dependent service is temporarily unavailable. Please retry."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Payment state changed",
                "The record was modified by another request. Please reload and retry."),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
        else if (exception is DownstreamServiceException dse)
            _logger.LogWarning(exception, "Downstream service {Service} failed. TraceId: {TraceId}", dse.ServiceName, httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
