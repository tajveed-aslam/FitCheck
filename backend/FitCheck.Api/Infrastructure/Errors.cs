using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FitCheck.Api.Infrastructure;

/// <summary>The user's input can't be used (bad spec, too large, ...). Maps to 400.</summary>
public sealed class InputValidationException(string message) : Exception(message);

/// <summary>The LLM call failed or returned something unusable. Maps to 502.</summary>
public sealed class LlmException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            InputValidationException => (StatusCodes.Status400BadRequest, "Invalid input"),
            LlmException => (StatusCodes.Status502BadGateway, "Test generation failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning("{Title}: {Message}", title, exception.Message);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Never echo internal exception details for unexpected errors.
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            },
        });
    }
}
