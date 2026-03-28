using System.Net.Sockets;
using Application.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Web.Middleware;

/// <summary>
/// Глобальный обработчик исключений через IExceptionHandler (ASP.NET Core 8+).
/// Маппит доменные и технические ошибки на ProblemDetails (RFC 7807).
/// </summary>
public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        // 👇 Временный лог для отладки
        logger.LogError(
            "🔍 DEBUG: Exception type={ExceptionType}, Message={Message}, StackTrace={StackTrace}",
            exception.GetType().FullName,  // 👈 Полное имя типа с неймспейсом!
            exception.Message,
            exception.StackTrace);
        var correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

        logger.LogError(exception, 
            "Необработанное исключение: {ExceptionType} - {Message} (Path: {RequestPath}, CorrelationId: {CorrelationId})",
            exception.GetType().Name,
            exception.Message,
            httpContext.Request.Path,
            correlationId);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Внутренняя ошибка сервера",
            Detail = "Произошла непредвиденная ошибка. Пожалуйста, попробуйте позже.",
            Instance = httpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId }
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        switch (exception)
        {
            // 🔹 404: Продукт не найден (бизнес-логика)
            case ProductNotFoundException notFound:
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Title = "Ресурс не найден";
                problemDetails.Detail = notFound.Message;
                problemDetails.Extensions["errorCode"] = notFound.ErrorCode;
                problemDetails.Extensions["context"] = notFound.Context;
                break;
            
            // 🔹 409: Конфликт бизнес-правил (сток, резерв и т.д.) — 👇 НОВЫЙ CASE
            case StockReservationException ex:
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Title = "Конфликт данных";
                problemDetails.Detail = ex.Message;
                problemDetails.Extensions["errorCode"] = ex.ErrorCode;
                problemDetails.Extensions["context"] = ex.Context;
                break;
            
            // 🔹 404: Сущность не найдена (бизнес-логика)
            case EntityNotFoundException notFound:
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Title = "Ресурс не найден";
                problemDetails.Detail = notFound.Message;
                problemDetails.Extensions["errorCode"] = notFound.ErrorCode;
                problemDetails.Extensions["context"] = notFound.Context;
                break;

            // 🔹 409: Конфликт бизнес-правил (сток, резерв и т.д.)
            case ConflictException conflict:
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Title = "Конфликт данных";
                problemDetails.Detail = conflict.Message;
                problemDetails.Extensions["errorCode"] = conflict.ErrorCode;
                problemDetails.Extensions["context"] = conflict.Context;
                break;

            // 🔹 400: Ошибка валидации FluentValidation
            case FluentValidation.ValidationException validation:
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Ошибка валидации данных";
                problemDetails.Detail = "Указанные данные не соответствуют требованиям";
                problemDetails.Extensions["errors"] = FormatValidationErrors(validation.Errors);
                break;

            // 🔹 400: Некорректный аргумент (ArgumentException)
            case ArgumentException argument:
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Некорректные параметры запроса";
                problemDetails.Detail = argument.Message;
                break;

            // 🔹 409: Уникальное нарушение в БД (дубликат)
            case DbUpdateException dbEx when dbEx.InnerException?.Message.Contains("23505") == true:
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Title = "Конфликт данных";
                problemDetails.Detail = "Запись с такими данными уже существует.";
                break;

            // 🔹 503: База данных или сеть недоступны
            case NpgsqlException npgsql when IsConnectionError(npgsql):
            case SocketException:
            case InvalidOperationException transient 
                when transient.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase):
                
                httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Status = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Title = "Сервис временно недоступен";
                problemDetails.Detail = "Пожалуйста, попробуйте позже.";
                httpContext.Response.Headers.RetryAfter = "30";
                break;
            
            case Polly.Timeout.TimeoutRejectedException timeout:
            case TaskCanceledException canceled 
                when canceled.InnerException is HttpRequestException || 
                     canceled.Message.Contains("The operation was canceled", StringComparison.OrdinalIgnoreCase):
    
                httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Status = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Title = "Сервис временно недоступен";
                problemDetails.Detail = "Внешний сервис не ответил в течение отведённого времени";
                problemDetails.Extensions["errorCode"] = "ExternalServiceTimeout";
                httpContext.Response.Headers.RetryAfter = "30";
                break;

            // 🔹 500: Всё остальное (без утечки деталей в продакшене)
            default:
                // problemDetails уже настроен на 500
                break;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    // ✅ Хелпер для форматирования ошибок FluentValidation
    private static Dictionary<string, string[]> FormatValidationErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> errors) =>
        errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

    // ✅ Хелпер для определения ошибок подключения к БД
    private static bool IsConnectionError(NpgsqlException ex)
    {
        var msg = ex.Message.ToLowerInvariant();
        return msg.Contains("failed to connect") ||
               msg.Contains("connection refused") ||
               msg.Contains("no connection could be made") ||
               msg.Contains("timeout") ||
               msg.Contains("host unreachable") ||
               ex.InnerException is SocketException;
    }
}
