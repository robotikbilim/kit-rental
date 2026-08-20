using KitRental.SharedKernel;

namespace KitRental.Identity.Api.Middleware;

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
            var status = exception is DomainException
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status500InternalServerError;
            context.Response.StatusCode = status;

            await Results.Problem(
                statusCode: status,
                title: status == StatusCodes.Status400BadRequest ? "Kimlik işlemi başarısız" : "Beklenmeyen hata",
                detail: exception.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = exception is DomainException domain ? domain.Code : "server.error"
                }).ExecuteAsync(context);
        }
    }
}
