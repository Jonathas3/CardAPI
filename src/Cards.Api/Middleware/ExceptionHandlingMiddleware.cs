using System.Net;
using System.Text.Json;
using Cards.Api.Common;
using Cards.Application.Common;

namespace Cards.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (ApiException apiEx)
        {
            _logger.LogWarning("API error {ErrorCode}: {Message}", apiEx.ErrorCode, apiEx.Message);
            await WriteResponseAsync(context, (int)apiEx.StatusCode, apiEx.ErrorCode, apiEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception of type {ExceptionType}", ex.GetType().Name);
            await WriteResponseAsync(
                context,
                (int)HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, string errorCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var body = new ErrorResponse
        {
            ErrorCode = errorCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, ErrorResponse.JsonOptions));
    }
}
