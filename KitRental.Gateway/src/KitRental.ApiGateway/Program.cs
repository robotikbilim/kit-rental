using KitRental.Observability;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKitRentalObservability();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KitRental API Gateway",
        Version = "v1",
        Description = "KitRental Identity ve Core servislerine açılan merkezi API giriş noktası."
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Identity API'den alınan erişim belirtecini girin."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});
builder.Services.AddHttpClient("identity", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Identity"] ?? "https://localhost:59592"));
builder.Services.AddHttpClient("core", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Core"] ?? "https://localhost:59590"));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "KitRental Gateway API v1");
    options.SwaggerEndpoint("/identity/swagger/v1/swagger.json", "KitRental Identity API v1");
    options.SwaggerEndpoint("/core/swagger/v1/swagger.json", "KitRental Core API v1");
    options.DocumentTitle = "KitRental API Dokümantasyonu";
    options.DisplayRequestDuration();
    options.EnablePersistAuthorization();
});
app.UseKitRentalObservability();

var proxyMethods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" };
app.MapMethods("/identity/{**path}", proxyMethods, (HttpContext context, IHttpClientFactory clients, string? path) =>
    ForwardAsync(context, clients.CreateClient("identity"), path))
    .WithTags("Identity Proxy")
    .WithSummary("İsteği Identity API'ye yönlendirir.");
app.MapMethods("/core/{**path}", proxyMethods, (HttpContext context, IHttpClientFactory clients, string? path) =>
    ForwardAsync(context, clients.CreateClient("core"), path))
    .WithTags("Core Proxy")
    .WithSummary("İsteği Core API'ye yönlendirir.");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithTags("Health")
    .WithSummary("Gateway sağlık durumunu döndürür.");
app.Run();

static async Task ForwardAsync(HttpContext context, HttpClient client, string? path)
{
    var target = $"/{path}{context.Request.QueryString}";
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
    if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        request.Content = new StreamContent(context.Request.Body);

    foreach (var header in context.Request.Headers)
    {
        if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
            continue;
        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && request.Content is not null)
            request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    foreach (var header in response.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();
    foreach (var header in response.Content.Headers)
        context.Response.Headers[header.Key] = header.Value.ToArray();
    context.Response.Headers.Remove("transfer-encoding");
    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
}

public partial class Program;
