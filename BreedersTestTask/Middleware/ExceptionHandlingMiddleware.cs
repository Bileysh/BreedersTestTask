using System.Text.Json;
using BreedersTestTask.DTOs;
using BreedersTestTask.Exceptions;

namespace BreedersTestTask.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, errorType) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, nameof(NotFoundException)),
            ForbiddenException => (StatusCodes.Status403Forbidden, nameof(ForbiddenException)),
            ValidationException => (StatusCodes.Status400BadRequest, nameof(ValidationException)),
            DomainException => (StatusCodes.Status400BadRequest, nameof(DomainException)),
            _ => (StatusCodes.Status500InternalServerError, "InternalServerError")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Handled {ErrorType} on {Path}: {Message}", errorType, context.Request.Path, ex.Message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var message = statusCode == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred. Please try again later."
            : ex.Message;

        var payload = new ErrorResponse(new ErrorDetails(errorType, message));

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions)); 
    }
}