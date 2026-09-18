using System.Text.Json;
using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;

namespace TaskManagement.Web.Middleware;

/// <summary>Turns domain exceptions raised under /api into consistent JSON error responses.</summary>
public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
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
        catch (Exception ex) when (context.Request.Path.StartsWithSegments("/api"))
        {
            var status = ex switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                ForbiddenException => StatusCodes.Status403Forbidden,
                DomainException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled API error on {Path}", context.Request.Path);

            var payload = new ApiError(status,
                status == StatusCodes.Status500InternalServerError
                    ? "Something went wrong while processing the request."
                    : ex.Message)
            {
                TraceId = context.TraceIdentifier
            };

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
