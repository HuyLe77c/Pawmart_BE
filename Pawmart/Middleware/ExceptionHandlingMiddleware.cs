using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Pawmart.Middleware;

public sealed class ApiException(int statusCode, string message, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var (status, title, detail) = exception switch
            {
                ApiException api => (api.StatusCode, api.Title, api.Message),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict", "The resource changed before this request completed."),
                DbUpdateException => (StatusCodes.Status409Conflict, "Conflict", "The request conflicts with the current data."),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.")
            };

            if (status >= 500)
            {
                logger.LogError(exception, "Unhandled API error for {Path}", context.Request.Path);
            }
            else
            {
                logger.LogInformation(exception, "API request rejected for {Path}: {Message}", context.Request.Path, detail);
            }

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
        }
    }
}
