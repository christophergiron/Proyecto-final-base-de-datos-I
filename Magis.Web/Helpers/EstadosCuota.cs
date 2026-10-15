namespace Magis.Web.Helpers;

//Estados de una cuota que devuelve la API
public static class EstadosCuota
{
    public static bool EstaPagada(string? estado) =>
        string.Equals(estado?.Trim(), "Pagada", StringComparison.OrdinalIgnoreCase);

    public static bool EstaVencida(string? estado) =>
        string.Equals(estado?.Trim(), "Vencida", StringComparison.OrdinalIgnoreCase);

    //Clase de Bootstrap para la etiqueta de color del estado
    public static string Clase(string? estado) => (estado ?? "").Trim().ToLowerInvariant() switch
    {
        "pagada" => "text-bg-success",
        "parcial" => "text-bg-warning",
        "vencida" => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    //true si el tipo de pago es en efectivo
    public static bool EsEfectivo(string? descripcionTipoPago)
    {
        var texto = (descripcionTipoPago ?? "").Trim().ToLowerInvariant();
        return texto.StartsWith("efectivo", StringComparison.Ordinal);
    }
}
