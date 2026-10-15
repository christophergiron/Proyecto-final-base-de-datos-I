using System.Net.Security;
using Magis.Web.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    throw new InvalidOperationException(
        "Falta \"ApiBaseUrl\" en appsettings.json (por ejemplo \"https://localhost:7136\").");

builder.Services
    .AddHttpClient<ApiClient>(cliente =>
    {
        cliente.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        cliente.Timeout = TimeSpan.FromSeconds(90);
    })
    .ConfigurePrimaryHttpMessageHandler(servicios =>
    {
        var manejador = new HttpClientHandler();

       
        var entorno = servicios.GetRequiredService<IWebHostEnvironment>();
        if (entorno.IsDevelopment())
        {
            manejador.ServerCertificateCustomValidationCallback = (solicitud, certificado, cadena, errores) =>
                errores == SslPolicyErrors.None || solicitud.RequestUri?.IsLoopback == true;
        }

        return manejador;
    });

builder.Services.AddScoped<CatalogosService>();


builder.Services.AddScoped<ParametrosApi>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Estado/{0}");

app.UseHttpsRedirection();

var cultura = "es-GT";
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new[] { new System.Globalization.CultureInfo(cultura) },
    SupportedUICultures = new[] { new System.Globalization.CultureInfo(cultura) }
});

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
