using Magis.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Api.Controllers;

// GET api/ayuda/cliente muestra, para cada accion de la entidad,
// el metodo HTTP, el procedimiento almacenado y los nombres/tipos de sus parametros.
// Los parametros que se envian en el JSON deben llamarse exactamente asi (sin la arroba).
[ApiController]
[Route("api/ayuda")]
public class AyudaController : ControllerBase
{
    private const string SqlParametros =
        "SELECT SUBSTRING(p.name, 2, 200) AS Nombre, TYPE_NAME(p.user_type_id) AS Tipo " +
        "FROM sys.parameters p WHERE p.object_id = OBJECT_ID(@sp) ORDER BY p.parameter_id";

    private static readonly Dictionary<string, string> Metodos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["listar"] = "GET", ["buscar"] = "GET", ["obtener"] = "GET", ["simular"] = "GET", ["simular-plan"] = "GET",
        ["insertar"] = "POST", ["registrar"] = "POST", ["actualizar"] = "PUT", ["eliminar"] = "DELETE",
    };

    private readonly SpExecutor _sp;

    public AyudaController(SpExecutor sp)
    {
        _sp = sp;
    }

    [HttpGet("{entidad}")]
    public async Task<IActionResult> Obtener(string entidad)
    {
        if (!Catalogo.Entidades.TryGetValue(entidad, out var definicion))
            return NotFound(new { error = $"La entidad '{entidad}' no existe.", entidades = Catalogo.Entidades.Keys });

        var acciones = new List<object>();
        foreach (var par in definicion.Sp)
        {
            var parametros = await _sp.ConsultarAsync(SqlParametros, new { sp = "dbo." + par.Value });
            acciones.Add(new
            {
                accion = par.Key,
                metodo = Metodos.GetValueOrDefault(par.Key, "GET"),
                procedimiento = par.Value,
                parametros
            });
        }
        return Ok(new { entidad, acciones });
    }
}
