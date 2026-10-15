using System.Collections.Concurrent;
using System.Text.Json;

namespace Magis.Web.Services;

// Consulta a la API qué parámetros acepta cada operación y deja pasar solo esos. Así, si el procedimiento de actualizar acepta menos campos que el de insertar,
// la web no envía de más. El resultado se guarda en memoria
public class ParametrosApi
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ApiClient _api;

    public ParametrosApi(ApiClient api)
    {
        _api = api;
    }

    // Devuelve solo los datos cuyo nombre es un parámetro de la operación. Si no se puede consultar la ayuda, devuelve los datos tal como llegaron
    public async Task<Dictionary<string, object?>> FiltrarAsync(
        string entidad, string accion, Dictionary<string, object?> datos)
    {
        var permitidos = await ObtenerAsync(entidad, accion);
        if (permitidos is null)
            return datos;

        return datos
            .Where(d => permitidos.Contains(d.Key))
            .ToDictionary(d => d.Key, d => d.Value);
    }

    private async Task<HashSet<string>?> ObtenerAsync(string entidad, string accion)
    {
        var clave = entidad + "/" + accion;
        if (Cache.TryGetValue(clave, out var guardado))
            return guardado;

        var resultado = await _api.GetAsync("api/ayuda/" + entidad);
        var acciones = resultado.Ok ? resultado.Propiedad("acciones") : null;
        if (acciones is null || acciones.Value.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var elemento in acciones.Value.EnumerateArray())
        {
            if (!elemento.TryGetProperty("accion", out var nombre) ||
                !string.Equals(nombre.GetString(), accion, StringComparison.OrdinalIgnoreCase))
                continue;

            var parametros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (elemento.TryGetProperty("parametros", out var lista) && lista.ValueKind == JsonValueKind.Array)
            {
                foreach (var parametro in lista.EnumerateArray())
                {
                    if (parametro.TryGetProperty("Nombre", out var n) && n.GetString() is { } texto)
                        parametros.Add(texto);
                }
            }

            Cache[clave] = parametros;
            return parametros;
        }

        return null;
    }
}
