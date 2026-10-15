using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Magis.Web.Helpers;

//Formato de toda la web: quetzales "Q 1,234.56", fechas "dd/MM/yyyy" y encabezados legibles.
//Se usa cultura invariante así el resultado es idéntico en cualquier computadora
public static class Formato
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    //Números y dinero

    public static string Quetzales(decimal? valor)
    {
        if (valor is null) return "";
        var v = valor.Value;
        var texto = Math.Abs(v).ToString("N2", Inv);
        return v < 0 ? $"-Q {texto}" : $"Q {texto}";
    }

    public static string Porcentaje(decimal? valor) =>
        valor is null ? "" : $"{valor.Value.ToString("0.##", Inv)} %";

    //Número con separador de miles y 2 decimales
    public static string Numero(decimal? valor, int decimales = 2) =>
        valor is null ? "" : valor.Value.ToString("N" + decimales, Inv);

    //Fechas

    //dd/MM/yyyy
    public static string Fecha(DateTime? fecha) =>
        fecha is null ? "" : fecha.Value.ToString("dd/MM/yyyy", Inv);

    //dd/MM/yyyy HH:mm
    public static string FechaHora(DateTime? fecha) =>
        fecha is null ? "" : fecha.Value.ToString("dd/MM/yyyy HH:mm", Inv);

    //yyyy-MM-dd: el formato que la API espera para las fechas
    public static string Iso(DateTime fecha) => fecha.ToString("yyyy-MM-dd", Inv);

    private static readonly Regex FechaIso =
        new(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?)?$", RegexOptions.Compiled);

    //Celdas de tablas

    // Las columnas de dinero se reconocen por su nombre.
    private static readonly Regex NoEsDinero = new(
        "Codigo|Numero|Nro|Dias|Cantidad|Anio|Plazo|Porcentaje|Tasa|Correlativo|Meses|Edad",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EsDinero = new(
        "Monto|Valor|Saldo|Capital|Interes|Enganche|Seguro|Pendiente|Pagado|Comision|Costo|" +
        "Perdida|Precio|Manejo|Recaudable|Financiado|Deuda|Abono|Neto|Depreciacion|Mora$|Ingreso",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TotalDeDinero = new(
        "^Total(?!.*(Creditos|Cuotas|Pagos|Clientes|Vendedores|Vehiculos|Registros|Referencias))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EsPorcentaje = new(
        "Porcentaje|Tasa", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool ColumnaDeDinero(string columna) =>
        !NoEsDinero.IsMatch(columna) && (EsDinero.IsMatch(columna) || TotalDeDinero.IsMatch(columna));

    //Convierte un valor JSON en el texto que se muestra en una celda.
    public static string Celda(string columna, JsonElement? valor)
    {
        if (valor is null) return "";
        var v = valor.Value;

        switch (v.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return "";

            case JsonValueKind.True:
                return "Sí";

            case JsonValueKind.False:
                return "No";

            case JsonValueKind.Number:
                if (!v.TryGetDecimal(out var d)) return v.GetRawText();
                if (EsPorcentaje.IsMatch(columna)) return Porcentaje(d);
                if (ColumnaDeDinero(columna)) return Quetzales(d);
                return d == Math.Truncate(d) ? d.ToString("0", Inv) : d.ToString("N2", Inv);

            case JsonValueKind.String:
                var texto = v.GetString() ?? "";
                if (FechaIso.IsMatch(texto) &&
                    DateTime.TryParse(texto, Inv, DateTimeStyles.None, out var fecha))
                    return fecha.TimeOfDay == TimeSpan.Zero ? Fecha(fecha) : FechaHora(fecha);
                return texto;

            default:
                return v.GetRawText();
        }
    }

    //Encabezados

    private static readonly Dictionary<string, string> Acentos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Codigo"] = "Código", ["Numero"] = "Número", ["Telefono"] = "Teléfono", ["Direccion"] = "Dirección",
        ["Descripcion"] = "Descripción", ["Anio"] = "Año", ["Interes"] = "Interés", ["Garantia"] = "Garantía",
        ["Linea"] = "Línea", ["Parametro"] = "Parámetro", ["Comision"] = "Comisión",
        ["Depreciacion"] = "Depreciación", ["Region"] = "Región", ["Genero"] = "Género",
        ["Fabricacion"] = "Fabricación", ["Credito"] = "Crédito", ["Creditos"] = "Créditos",
        ["Emision"] = "Emisión", ["Cumpleanos"] = "Cumpleaños", ["Dias"] = "Días",
        ["Vehiculo"] = "Vehículo", ["Vehiculos"] = "Vehículos", ["Autorizacion"] = "Autorización",
        ["Ultimo"] = "Último", ["Periodo"] = "Período", ["Maximo"] = "Máximo", ["Minimo"] = "Mínimo",
        ["Pagina"] = "Página", ["Correo"] = "Correo", ["Electronico"] = "Electrónico",
        ["Informacion"] = "Información", ["Aprobacion"] = "Aprobación", ["Ubicacion"] = "Ubicación",
        ["Telefonos"] = "Teléfonos", ["Cobranza"] = "Cobranza", ["Poliza"] = "Póliza",
        ["Cuota"] = "Cuota", ["Mas"] = "Más", ["Rapido"] = "Rápido", ["Sera"] = "Será"
    };

    private static readonly Regex CortePalabras = new(
        "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

    //"CodigoDeudor" a "Código Deudor", "PrimerNombre" a "Primer Nombre"y "DPI" a "DPI"
    public static string Encabezado(string columna)
    {
        if (string.IsNullOrWhiteSpace(columna)) return "";
        if (columna.Contains(' ')) return columna;

        var palabras = CortePalabras
            .Replace(columna.Replace('_', ' '), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Acentos.TryGetValue(p, out var conAcento) ? conAcento : p);

        return string.Join(" ", palabras);
    }
}
