using System.Text.Encodings.Web;
using Magis.Api.Data;
using Magis.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(opciones =>
    {
        // Acentos legibles en el JSON (en vez de \u00F3)
        opciones.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

builder.Services.AddCors(opciones =>
    opciones.AddPolicy("Libre", politica => politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton<SpExecutor>();

var app = builder.Build();

app.UseCors("Libre");
app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapControllers();

// Pagina de inicio: muestra las entidades disponibles y sus acciones
app.MapGet("/", () => Results.Redirect("/api"));
app.MapGet("/api", () => Results.Ok(new
{
    sistema = "MAGIS-AUTO API",
    ayuda = "GET /api/ayuda/{entidad} muestra los parametros de cada operacion; GET /api/reportes lista los reportes.",
    entidades = Catalogo.Entidades.ToDictionary(e => e.Key, e => e.Value.Sp.Keys.ToArray())
}));

app.Run();
