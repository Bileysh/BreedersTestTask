using BreedersTestTask.Exceptions;
using BreedersTestTask.Services.Implementation;

namespace BreedersTestTask.Middleware;

public class BreederAuthMiddleware
{
    private readonly RequestDelegate _next;

    public BreederAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, BreederContext breederContext)
    {
        bool isPreflight = HttpMethods.IsOptions(context.Request.Method);

        
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            if (!context.Request.Headers.TryGetValue("X-Breeder-Id", out var headerValue) || 
                !int.TryParse(headerValue, out var breederId) || 
                breederId <= 0)
            {
                throw new ValidationException("A valid 'X-Breeder-Id' header is required.");
            }

            breederContext.BreederId = breederId;
        }

        await _next(context);
    }
}