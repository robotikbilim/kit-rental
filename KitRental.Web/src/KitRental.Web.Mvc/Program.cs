using KitRental.Web.Mvc.Branding;
using KitRental.Web.Mvc.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.Configure<BrandingOptions>(builder.Configuration.GetSection("Branding"));
builder.Services.AddScoped<IBrandResolver, HostBrandResolver>();
builder.Services.AddHttpClient<KitRentalApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["GatewayUrl"] ?? "https://localhost:61327"));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/access-denied";
        options.Cookie.Name = "KitRental.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/home/error");
    app.UseHsts();
}
app.UseResponseCompression();
// PDF.js modules and font/colour-map assets are served locally with the label renderer.
var staticContentTypes = new FileExtensionContentTypeProvider();
staticContentTypes.Mappings[".mjs"] = "text/javascript";
staticContentTypes.Mappings[".bcmap"] = "application/octet-stream";
staticContentTypes.Mappings[".pfb"] = "application/octet-stream";
staticContentTypes.Mappings[".icc"] = "application/vnd.iccprofile";
staticContentTypes.Mappings[".wasm"] = "application/wasm";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticContentTypes,
    OnPrepareResponse = context =>
        context.Context.Response.Headers.CacheControl = "public,max-age=604800"
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program;
