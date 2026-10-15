using System.Text.Json;
using Magis.Web.Services;

namespace Magis.Web.Models;

//Un parámetro de un procedimiento tal como lo describe GET /api/ayuda/{entidad}
public record ParametroDef(string Nombre, string Tipo)
{
    private static readonly HashSet<string> Enteros =
        new(StringComparer.OrdinalIgnoreCase) { "tinyint", "smallint", "int", "bigint" };

    private static readonly HashSet<string> Decimales =
        new(StringComparer.OrdinalIgnoreCase) { "decimal", "numeric", "money", "smallmoney", "float", "real" };

    public bool EsEntero => Enteros.Contains(Tipo);
    public bool EsDecimal => Decimales.Contains(Tipo);
    public bool EsFecha => string.Equals(Tipo, "date", StringComparison.OrdinalIgnoreCase);
    public bool EsFechaHora => Tipo.StartsWith("datetime", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(Tipo, "smalldatetime", StringComparison.OrdinalIgnoreCase);
    public bool EsBit => string.Equals(Tipo, "bit", StringComparison.OrdinalIgnoreCase);

    //convierte el texto del formulario al tipo del parámetro
    public bool TryConvertir(string? texto, out object? valor, out string? error)
    {
        valor = null;
        error = null;
        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto)) return true;

        if (EsEntero)
        {
            if (long.TryParse(texto, out var n)) { valor = n; return true; }
            error = $"{Etiqueta} debe ser un número entero.";
            return false;
        }

        if (EsDecimal)
        {
            if (decimal.TryParse(texto, System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowLeadingSign,
                                 System.Globalization.CultureInfo.InvariantCulture, out var d))
            { valor = d; return true; }
            error = $"{Etiqueta} debe ser un número.";
            return false;
        }

        if (EsBit)
        {
            valor = texto is "1" or "true" or "on" or "True";
            return true;
        }

        valor = texto;
        return true;
    }

    public string Etiqueta => Helpers.Formato.Encabezado(Nombre);
}

public record AccionDef(string Accion, string Metodo, List<ParametroDef> Parametros);

public class AyudaEntidad
{
    public string Entidad { get; init; } = "";
    public List<AccionDef> Acciones { get; init; } = new();

    public bool Tiene(string accion) =>
        Acciones.Any(a => string.Equals(a.Accion, accion, StringComparison.OrdinalIgnoreCase));

    public List<ParametroDef> Parametros(string accion) =>
        Acciones.FirstOrDefault(a => string.Equals(a.Accion, accion, StringComparison.OrdinalIgnoreCase))?.Parametros
        ?? new List<ParametroDef>();

    //Lee la ayuda de la API
    public static async Task<AyudaEntidad?> CargarAsync(ApiClient api, string entidad)
    {
        var resultado = await api.GetAsync("api/ayuda/" + Uri.EscapeDataString(entidad));
        if (!resultado.Ok) return null;

        var acciones = resultado.Propiedad("acciones");
        if (acciones is null || acciones.Value.ValueKind != JsonValueKind.Array) return null;

        var ayuda = new AyudaEntidad { Entidad = entidad };
        foreach (var elemento in acciones.Value.EnumerateArray())
        {
            var nombre = elemento.TryGetProperty("accion", out var a) ? a.GetString() : null;
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            var metodo = elemento.TryGetProperty("metodo", out var m) ? m.GetString() ?? "" : "";
            var parametros = new List<ParametroDef>();
            if (elemento.TryGetProperty("parametros", out var lista) && lista.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in lista.EnumerateArray())
                {
                    var n = p.TryGetProperty("Nombre", out var pn) ? pn.GetString() : null;
                    var t = p.TryGetProperty("Tipo", out var pt) ? pt.GetString() ?? "varchar" : "varchar";
                    if (!string.IsNullOrWhiteSpace(n)) parametros.Add(new ParametroDef(n, t));
                }
            }
            ayuda.Acciones.Add(new AccionDef(nombre, metodo, parametros));
        }
        return ayuda;
    }
}

//Una entidad de la API para las tarjetas de la pantalla de catálogos
public record EntidadInfo(string Nombre, string Titulo, bool EsCatalogo, int Operaciones);

//Un campo de formulario generado a partir de un parámetro
public class CampoVm
{
    public ParametroDef Def { get; set; } = new("", "varchar");
    public string? Valor { get; set; }
    public string Prefijo { get; set; } = "campo_";

    //Si es una llave foránea, muestra las opciones de la lista desplegable
    public List<Opcion>? Opciones { get; set; }

    //llaves del registro que no cambian al editar
    public bool SoloLectura { get; set; }
}

public class CatalogoVm
{
    public List<EntidadInfo> Entidades { get; set; } = new();

    public string? Entidad { get; set; }
    public string Titulo { get; set; } = "";
    public bool EsCatalogo { get; set; }
    public bool PuedeCrear { get; set; }
    public bool PuedeEditar { get; set; }
    public bool PuedeEliminar { get; set; }
    public bool TieneBuscar { get; set; }

    public string? Criterio { get; set; }
    public ListaVm Lista { get; set; } = new();

    //Parámetros de listar usados como filtros
    public List<CampoVm> Filtros { get; set; } = new();

    public List<CampoVm> CamposCrear { get; set; } = new();

    //Parámetros de eliminar identifican un registro
    public List<string> Llaves { get; set; } = new();
}

public class EditarCatalogoVm
{
    public string Entidad { get; set; } = "";
    public string Titulo { get; set; } = "";
    public List<CampoVm> Campos { get; set; } = new();
    public List<string> Llaves { get; set; } = new();
}
