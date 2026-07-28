using System.Text.Json;
using Esotera.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Esotera.Api.Middleware;

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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problemDetails) = exception switch
        {
            NotFoundException notFound => (StatusCodes.Status404NotFound, new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Recurso não encontrado",
                Detail = notFound.Message
            }),
            ConflictException conflict => (StatusCodes.Status409Conflict, new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito",
                Detail = conflict.Message
            }),
            Application.Exceptions.ValidationException validation => (StatusCodes.Status400BadRequest, new ValidationProblemDetails(validation.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Erro de validação",
                Detail = validation.Message
            }),
            ForbiddenException forbidden => (StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acesso negado",
                Detail = forbidden.Message
            }),
            UnauthorizedAppException unauthorized => (StatusCodes.Status401Unauthorized, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado",
                Detail = unauthorized.Message
            }),
            _ => (StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno do servidor",
                Detail = context.RequestServices.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing")
                    || context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
                    ? $"{exception.GetType().Name}: {exception.Message}"
                    : "Ocorreu um erro inesperado. Tente novamente mais tarde."
            })
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Erro não tratado: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("Exceção de aplicação: {Type} - {Message}", exception.GetType().Name, exception.Message);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), options));
    }
}
