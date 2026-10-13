using Magis.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Api.Controllers;

/// <summary>
/// Modulo de reportes. Cualquier procedimiento llamado  sp_Reporte_NN_Nombre  que exista en la base
/// aparece automaticamente aqui, sin tocar el codigo de la API.
///   GET api/reportes            -> lista de reportes con sus parametros
///   GET api/reportes/7          -> ejecuta sp_Reporte_07_...   (tambien por nombre completo)
///   GET api/reportes/7?FechaInicio=2026-01-01&amp;FechaFin=2026-12-31   -> filtros como query string
/// </summary>
[ApiController]
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private const string SqlReportes =
        "SELECT pr.name AS Reporte, SUBSTRING(pa.name, 2, 200) AS Parametro, TYPE_NAME(pa.user_type_id) AS Tipo " +
        "FROM sys.procedures pr LEFT JOIN sys.parameters pa ON pa.object_id = pr.object_id " +
        "WHERE pr.name LIKE 'sp[_]Reporte[_]%' ORDER BY pr.name, pa.parameter_id";

    private readonly SpExecutor _sp;

    public ReportesController(SpExecutor sp)
    {
        _sp = sp;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var filas = await _sp.ConsultarAsync(SqlReportes);
        var reportes = filas
            .GroupBy(f => (string)f["Reporte"]!)
            .Select(g => new
            {
                nombre = g.Key,
                parametros = g.Where(f => f["Parametro"] is not null)
                              .Select(f => new { nombre = f["Parametro"], tipo = f["Tipo"] })
            });
        return Ok(reportes);
    }

    [HttpGet("{nombre}")]
    public async Task<IActionResult> Ejecutar(string nombre)
    {
        var filas = await _sp.ConsultarAsync(SqlReportes);
        var existentes = filas.Select(f => (string)f["Reporte"]!).Distinct().ToList();

        var procedimiento = existentes.FirstOrDefault(n => n.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        if (procedimiento is null && int.TryParse(nombre, out var numero))
            procedimiento = existentes.FirstOrDefault(
                n => n.StartsWith($"sp_Reporte_{numero:00}_", StringComparison.OrdinalIgnoreCase));

        if (procedimiento is null)
            return NotFound(new { error = $"El reporte '{nombre}' no existe.", reportes = existentes });

        var resultado = await _sp.EjecutarAsync(procedimiento, Parametros.DesdeQuery(Request.Query));
        return Ok(resultado);
    }
}
