using System.Text.Json;
using System.Text.RegularExpressions;
using Magis.Web.Models;
using Magis.Web.Services;

namespace Magis.Web.Helpers;

//Formato de las celdas de los reportes según la columna 
public static class ReportesFormato
{
    public static readonly string[] Meses =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
    };

    public static string FechaLarga(DateTime fecha) => $"{fecha.Day} de {Meses[fecha.Month - 1]} de {fecha.Year}";

    public static bool EsNumerico(string formato) =>
        formato is "dinero" or "porcentaje" or "entero" or "decimal";

    //Texto listo para mostrar: Q 1,234.56, 12.50 %, dd/MM/yyyy, etc. Vacío si el valor es NULL
    public static string Celda(Fila fila, string columna, string formato)
    {
        var valor = fila.Valor(columna);
        if (valor is null || valor.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "";

        if (valor.Value.ValueKind == JsonValueKind.True) return "Sí";
        if (valor.Value.ValueKind == JsonValueKind.False) return "No";

        return formato switch
        {
            "dinero" => Formato.Quetzales(fila.Decimal(columna)),
            "porcentaje" => Formato.Porcentaje(fila.Decimal(columna)),
            "entero" => fila.Decimal(columna) is { } e ? Formato.Numero(e, 0) : fila[columna] ?? "",
            "decimal" => Formato.Numero(fila.Decimal(columna)),
            "fecha" => fila.Fecha(columna) is { } f ? Formato.Fecha(f) : fila[columna] ?? "",
            "fechahora" => fila.Fecha(columna) is { } fh ? Formato.FechaHora(fh) : fila[columna] ?? "",
            _ => fila[columna] ?? ""
        };
    }

    private static readonly Regex EsFecha = new("^Fecha|Vencimiento$", RegexOptions.Compiled);

    //>Si el reporte no tiene descripción en el JSON, se adivina el formato por el nombre de la columna
    public static string FormatoPorNombre(string columna)
    {
        if (columna.Equals("TotalFilasDisponibles", StringComparison.OrdinalIgnoreCase)) return "oculta";
        if (Regex.IsMatch(columna, "Porcentaje|Tasa|Pct", RegexOptions.IgnoreCase)) return "porcentaje";
        if (Formato.ColumnaDeDinero(columna)) return "dinero";
        if (EsFecha.IsMatch(columna)) return "fecha";
        return "texto";
    }

    //Primera frase de la explicación para las tarjetas
    public static string Corto(string? texto, int maximo = 170)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var limpio = texto.Trim();
        var punto = limpio.IndexOf(". ", StringComparison.Ordinal);
        var primera = punto > 0 ? limpio[..(punto + 1)] : limpio;
        return primera.Length <= maximo ? primera : primera[..(maximo - 1)].TrimEnd() + "…";
    }
}

//Lee wwwroot/data/reportes-info.json y lo vuelve a leer si el archivo cambia
public static class ReportesInfoStore
{
    private static readonly object Candado = new();
    private static Dictionary<int, ReporteInfo> _cache = new();
    private static DateTimeOffset _version = DateTimeOffset.MinValue;

    public static Dictionary<int, ReporteInfo> Obtener(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        var archivo = env.WebRootFileProvider.GetFileInfo("data/reportes-info.json");
        if (!archivo.Exists) return new Dictionary<int, ReporteInfo>();

        lock (Candado)
        {
            if (archivo.LastModified == _version && _cache.Count > 0) return _cache;

            try
            {
                using var lector = new StreamReader(archivo.CreateReadStream(), System.Text.Encoding.UTF8, true);
                var lista = JsonSerializer.Deserialize<List<ReporteInfo>>(lector.ReadToEnd(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ReporteInfo>();
                _cache = lista.GroupBy(r => r.Numero).ToDictionary(g => g.Key, g => g.First());
                _version = archivo.LastModified;
            }
            catch (JsonException)
            {
                // Si el archivo está mal escrito, los reportes siguen funcionando
                _cache = new Dictionary<int, ReporteInfo>();
            }
            return _cache;
        }
    }
}
