using System.Globalization;
using System.Text.Json;
using Magis.Web.Helpers;

namespace Magis.Web.Services;

// Una fila devuelta por la API se usa en lugar de una clase por entidad: si la API se cambia un
// procedimiento almacenado, la web sigue funcionando sin recompilar modelos.
public class Fila
{
    private readonly Dictionary<string, JsonElement> _valores = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _columnas = new();

    public static Fila Desde(JsonElement objeto)
    {
        var fila = new Fila();
        if (objeto.ValueKind != JsonValueKind.Object)
            return fila;

        foreach (var propiedad in objeto.EnumerateObject())
        {
            if (!fila._valores.ContainsKey(propiedad.Name))
                fila._columnas.Add(propiedad.Name);
            fila._valores[propiedad.Name] = propiedad.Value.Clone();
        }
        return fila;
    }

    //Nombres de las columnas
    public IReadOnlyList<string> Columnas => _columnas;

    public bool Tiene(string columna) => _valores.ContainsKey(columna);

    //Valor del JSON
    public JsonElement? Valor(string columna) =>
        _valores.TryGetValue(columna, out var valor) ? valor : null;

    //Valor como texto sin formato
    public string? Texto(string columna)
    {
        if (!_valores.TryGetValue(columna, out var valor))
            return null;

        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public string? this[string columna] => Texto(columna);

    //true si la columna vale 1 o true
    public bool Si(string columna) =>
        string.Equals(Texto(columna), "1", StringComparison.Ordinal) ||
        string.Equals(Texto(columna), "true", StringComparison.OrdinalIgnoreCase);

    public decimal? Decimal(string columna) =>
        decimal.TryParse(Texto(columna), NumberStyles.Number | NumberStyles.AllowLeadingSign,
                         CultureInfo.InvariantCulture, out var d) ? d : null;

    public int? Entero(string columna)
    {
        var d = Decimal(columna);
        return d.HasValue && d.Value == Math.Truncate(d.Value) ? (int)d.Value : null;
    }

    public DateTime? Fecha(string columna) =>
        DateTime.TryParse(Texto(columna), CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    // Valor listo para mostrar en pantalla
    public string Mostrar(string columna) => Formato.Celda(columna, Valor(columna));
}
