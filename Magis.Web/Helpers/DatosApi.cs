using System.Globalization;

namespace Magis.Web.Helpers;


public static class DatosApi
{
    //Agrega un campo de texto si no está vacío.
    public static void Texto(IDictionary<string, object?> datos, string campo, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
            datos[campo] = valor.Trim();
    }

    //Agrega un código numérico si el texto es un entero válido
    public static void Numero(IDictionary<string, object?> datos, string campo, string? valor)
    {
        if (long.TryParse(valor?.Trim(), out var numero))
            datos[campo] = numero;
    }

    //Agrega un monto o porcentaje si el texto es un decimal válido
    public static void Decimal(IDictionary<string, object?> datos, string campo, string? valor)
    {
        if (decimal.TryParse(valor?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var numero))
            datos[campo] = numero;
    }

    //Convierte "1990-04-14T00:00:00" en "1990-04-14"
    public static string? FechaParaFormulario(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        return valor.Length >= 10 ? valor[..10] : valor;
    }

    //Número para un campo de formulario HTML
    public static string? NumeroParaFormulario(decimal? valor) =>
        valor?.ToString("0.##", CultureInfo.InvariantCulture);
}
