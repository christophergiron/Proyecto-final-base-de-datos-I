using Magis.Api.Data;
using Magis.Api.Middleware;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Api.Controllers;

// Modulo de reportes. Cualquier procedimiento llamado  sp_Reporte_NN_Nombre  que exista en la base
// aparece automaticamente aqui, sin tocar el codigo de la API.
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
        List<Dictionary<string, object?>> filas;
        try { filas = await _sp.ConsultarAsync(SqlReportes); }
        catch (SqlException ex) { return Error(ex); }
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
        List<Dictionary<string, object?>> filas;
        try { filas = await _sp.ConsultarAsync(SqlReportes); }
        catch (SqlException ex) { return Error(ex); }
        var existentes = filas.Select(f => (string)f["Reporte"]!).Distinct().ToList();

        var procedimiento = existentes.FirstOrDefault(n => n.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        if (procedimiento is null && int.TryParse(nombre, out var numero))
            procedimiento = existentes.FirstOrDefault(
                n => n.StartsWith($"sp_Reporte_{numero:00}_", StringComparison.OrdinalIgnoreCase));

        if (procedimiento is null)
            return NotFound(new { error = $"El reporte '{nombre}' no existe.", reportes = existentes });

        try
        {
            var resultado = await _sp.EjecutarAsync(procedimiento, Parametros.DesdeQuery(Request.Query));
            return Ok(resultado);
        }
        catch (SqlException ex)
        {
            return Error(ex);
        }
    }

    private IActionResult Error(SqlException ex)
    {
        var (estado, mensaje) = ErrorHandlingMiddleware.Traducir(ex);
        return StatusCode(estado, new { error = mensaje, codigo = ex.Number });
    }
}
