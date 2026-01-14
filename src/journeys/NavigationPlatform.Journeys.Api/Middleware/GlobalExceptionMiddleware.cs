using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace NavigationPlatform.Api.Middleware;

internal sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.TraceIdentifier;
            var endpoint = context.GetEndpoint()?.DisplayName;
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "/";

            var requestBody =
                context.Items.TryGetValue(RequestBodyCaptureMiddleware.ItemKey, out var bodyObj)
                    ? bodyObj as string
                    : null;

            var truncated =
                context.Items.TryGetValue(RequestBodyCaptureMiddleware.TruncatedItemKey, out var truncObj) &&
                truncObj is bool b &&
                b;

            var (status, title) = ex switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
                _ => (StatusCodes.Status500InternalServerError, "Internal server error")
            };

            if (status >= 500)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception. CorrelationId={CorrelationId} Endpoint={Endpoint} Method={Method} Path={Path} RequestBody={RequestBody} RequestBodyTruncated={RequestBodyTruncated}",
                    correlationId,
                    endpoint,
                    method,
                    path,
                    requestBody,
                    truncated);
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Request failed with exception. CorrelationId={CorrelationId} Endpoint={Endpoint} Method={Method} Path={Path} RequestBody={RequestBody} RequestBodyTruncated={RequestBodyTruncated}",
                    correlationId,
                    endpoint,
                    method,
                    path,
                    requestBody,
                    truncated);
            }

            if (context.Response.HasStarted)
                throw;

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = status;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status >= 500 ? "An unexpected error occurred." : ex.Message,
                Instance = context.Request.Path,
                Extensions =
                {
                    ["correlationId"] = correlationId
                }
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}

