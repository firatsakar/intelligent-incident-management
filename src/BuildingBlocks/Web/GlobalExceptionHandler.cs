using BuildingBlocks.SharedKernel.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web;

// Maps unhandled exceptions to RFC 9457 problem details. Services opt in with
// AddExceptionHandler<GlobalExceptionHandler>() and derive their not-found exceptions from
// NotFoundException.
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var problemDetails = exception switch
        {
            ValidationException validationEx => CreateValidationProblem(validationEx),
            NotFoundException notFoundEx => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = notFoundEx.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = "An internal server error has occurred.",
            },
        };

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception occurred");
        else
            _logger.LogWarning("Handled exception: {Message}", exception.Message);

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateValidationProblem(ValidationException ex)
    {
        var errors = ex
            .Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred.",
        };

        problemDetails.Extensions["errors"] = errors;

        return problemDetails;
    }
}
