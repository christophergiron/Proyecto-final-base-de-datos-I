using System.Text.Json;
using Magis.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Api.Controllers;

/// Un solo controlador para todas las entidades del sistema (ver Data/Catalogo.cs).
///   GET    api/{entidad}                 -> sp_{Entidad}_Listar     (filtros opcionales en el query string)
[ApiController]
[Route("api/{entidad}")]
public class EntidadController : ControllerBase
{
    private static readonly HashSet<string> ExtrasGet =
        new(StringComparer.OrdinalIgnoreCase) { "obtener", "simular", "simular-plan" };

    private static readonly HashSet<string> ExtrasPost =
        new(StringComparer.OrdinalIgnoreCase) { "registrar" };

    private readonly SpExecutor _sp;

    public EntidadController(SpExecutor sp)
    {
        _sp = sp;
    }

    [HttpGet]
    public Task<IActionResult> Listar(string entidad) =>
        Ejecutar(entidad, "listar", Parametros.DesdeQuery(Request.Query));

    [HttpGet("buscar")]
    public Task<IActionResult> Buscar(string entidad) =>
        Ejecutar(entidad, "buscar", Parametros.DesdeQuery(Request.Query));

    [HttpGet("{accion}")]
    public Task<IActionResult> AccionGet(string entidad, string accion)
    {
        if (!ExtrasGet.Contains(accion))
            return Task.FromResult<IActionResult>(NotFound(new { error = $"La accion '{accion}' no existe." }));
        return Ejecutar(entidad, accion, Parametros.DesdeQuery(Request.Query));
    }

    [HttpPost]
    public Task<IActionResult> Insertar(string entidad, [FromBody] JsonElement cuerpo) =>
        Ejecutar(entidad, "insertar", Parametros.DesdeJson(cuerpo));

    [HttpPost("{accion}")]
    public Task<IActionResult> AccionPost(string entidad, string accion, [FromBody] JsonElement cuerpo)
    {
        if (!ExtrasPost.Contains(accion))
            return Task.FromResult<IActionResult>(NotFound(new { error = $"La accion '{accion}' no existe." }));
        return Ejecutar(entidad, accion, Parametros.DesdeJson(cuerpo));
    }

    [HttpPut]
    public Task<IActionResult> Actualizar(string entidad, [FromBody] JsonElement cuerpo) =>
        Ejecutar(entidad, "actualizar", Parametros.DesdeJson(cuerpo));

    [HttpDelete]
    public Task<IActionResult> Eliminar(string entidad) =>
        Ejecutar(entidad, "eliminar", Parametros.DesdeQuery(Request.Query));

    private async Task<IActionResult> Ejecutar(string entidad, string accion, Dictionary<string, object?> parametros)
    {
        if (!Catalogo.Entidades.TryGetValue(entidad, out var definicion))
            return NotFound(new { error = $"La entidad '{entidad}' no existe.", entidades = Catalogo.Entidades.Keys });

        if (!definicion.Sp.TryGetValue(accion, out var procedimiento))
            return StatusCode(405, new { error = $"La entidad '{entidad}' no permite la accion '{accion}'." });

        var filas = await _sp.EjecutarAsync(procedimiento, parametros);
        return Ok(filas);
    }
}
