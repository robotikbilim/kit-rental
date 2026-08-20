using KitRental.Core.Application.Common;
using KitRental.SharedKernel;

namespace KitRental.Core.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await WriteErrorResponseAsync(context, exception);
        }
    }

    private static Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var (status, title, code) = exception switch
        {
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı", "resource.not_found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Bu kayda erişim izniniz yok", "resource.forbidden"),
            ConflictException conflict => (StatusCodes.Status409Conflict, "İş kuralı çakışması", conflict.Code),
            DomainException domain => (StatusCodes.Status400BadRequest, "Geçersiz istek", domain.Code),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata", "server.error")
        };

        context.Response.StatusCode = status;
        return Results.Problem(
            statusCode: status,
            title: title,
            detail: exception.Message,
            extensions: new Dictionary<string, object?> { ["code"] = code })
            .ExecuteAsync(context);
    }
}
