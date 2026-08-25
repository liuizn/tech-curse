using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Ocorreu uma exceção não tratada.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            BadRequestException => HttpStatusCode.BadRequest,           //400
            UnauthorizedException => HttpStatusCode.Unauthorized,       //401
            ForbiddenAccessException => HttpStatusCode.Forbidden,       //403
            NotFoundException => HttpStatusCode.NotFound,               //404
            NotAllowedException => HttpStatusCode.Conflict,             //409
            ConflictException => HttpStatusCode.Conflict,               //409
            ValidationException => HttpStatusCode.UnprocessableEntity,  //422
            GatewayTimeoutException => HttpStatusCode.GatewayTimeout,   //504
            _ => HttpStatusCode.InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Detail = exception.Message,
            Instance = context.Request.Path,
            Status = (int)statusCode,
            Title = statusCode.ToString(),
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("errors", validationException.Errors);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
