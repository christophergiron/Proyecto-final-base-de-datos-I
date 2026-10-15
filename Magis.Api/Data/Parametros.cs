using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Magis.Api.Data;

// Convierte el query string o el JSON recibido en parametros para un procedimiento almacenado
// El nombre de cada clave debe coincidir con el parametro del SP 
// Los valores nulos o vacios se omiten: el SP usa entonces su valor por defecto 
public static class Parametros
{
    private static readonly Regex NombreValido =
        new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly Regex FormatoFecha =
        new(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?)?$", RegexOptions.Compiled);

    public static Dictionary<string, object?> DesdeQuery(IQueryCollection query)
    {
        var resultado = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var par in query)
        {
            Validar(par.Key);
            var texto = par.Value.ToString();
            if (!string.IsNullOrWhiteSpace(texto))
                resultado[par.Key] = ConvertirTexto(texto);
        }
        return resultado;
    }

    public static Dictionary<string, object?> DesdeJson(JsonElement cuerpo)
    {
        if (cuerpo.ValueKind != JsonValueKind.Object)
            throw new ApiException(400, "El cuerpo de la solicitud debe ser un objeto JSON.");

        var resultado = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var propiedad in cuerpo.EnumerateObject())
        {
            Validar(propiedad.Name);
            var valor = ConvertirJson(propiedad.Value);
            if (valor is not null)
                resultado[propiedad.Name] = valor;
        }
        return resultado;
    }

    private static void Validar(string nombre)
    {
        if (!NombreValido.IsMatch(nombre))
            throw new ApiException(400, $"Nombre de parametro invalido: '{nombre}'.");
    }

    private static object? ConvertirJson(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => string.IsNullOrWhiteSpace(e.GetString()) ? null : ConvertirTexto(e.GetString()!),
        JsonValueKind.Number => e.TryGetInt64(out var entero) ? (object)entero : e.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        // [1,2,3] se envia como 1,2,3
        JsonValueKind.Array => string.Join(",", e.EnumerateArray().Select(x => x.ToString())),
        _ => null
    };

    private static object ConvertirTexto(string texto)
    {
        texto = texto.Trim();
        // Fechas ISO se envian como fecha real, sin ambiguedad de idioma
        if (FormatoFecha.IsMatch(texto) &&
            DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return fecha;
        return texto;
    }
}
