using System.Net;
using Messenger.Api.Common;

namespace Messenger.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(
                context,
                "forbidden",
                ex.Message,
                HttpStatusCode.Forbidden);
        }
        catch (InvalidOperationException ex)
        {
            await WriteErrorAsync(
                context,
                "bad_request",
                ex.Message,
                HttpStatusCode.BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred.");

            await WriteErrorAsync(
                context,
                "internal_server_error",
                "An unexpected error occurred.",
                HttpStatusCode.InternalServerError);
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        string code,
        string message,
        HttpStatusCode statusCode)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ApiErrorResponse(
            code,
            message,
            (int)statusCode,
            DateTime.UtcNow);

        await context.Response.WriteAsJsonAsync(response);
    }
}