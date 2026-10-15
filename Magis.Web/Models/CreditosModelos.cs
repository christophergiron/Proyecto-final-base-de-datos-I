using Magis.Web.Helpers;
using Magis.Web.Services;

namespace Magis.Web.Models;

// Datos de un fiador nuevo
// Crea a la persona y, si se indica NumeroCredito, la liga a ese crédito.
public class FiadorNuevoForm
{
    public string? PrimerNombre { get; set; }
    public string? SegundoNombre { get; set; }
    public string? PrimerApellido { get; set; }
    public string? SegundoApellido { get; set; }
    public string? DPI { get; set; }
    public string? NIT { get; set; }
    public string? Correo { get; set; }
    public string? Telefono { get; set; }

    //Formato yyyy-MM-dd
    public string? FechaNacimiento { get; set; }

    //true si la persona no escribió nada en este bloque
    public bool Vacio =>
        string.IsNullOrWhiteSpace(PrimerNombre) && string.IsNullOrWhiteSpace(SegundoNombre) &&
        string.IsNullOrWhiteSpace(PrimerApellido) && string.IsNullOrWhiteSpace(SegundoApellido) &&
        string.IsNullOrWhiteSpace(DPI) && string.IsNullOrWhiteSpace(NIT) &&
        string.IsNullOrWhiteSpace(Correo) && string.IsNullOrWhiteSpace(Telefono) &&
        string.IsNullOrWhiteSpace(FechaNacimiento);

    //Nombre para mensajes
    public string Nombre => $"{PrimerNombre} {PrimerApellido}".Trim();

    //Campos obligatorios que faltan
    public List<string> Faltantes()
    {
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(PrimerNombre)) faltan.Add("primer nombre");
        if (string.IsNullOrWhiteSpace(PrimerApellido)) faltan.Add("primer apellido");
        if (string.IsNullOrWhiteSpace(DPI)) faltan.Add("DPI");
        if (string.IsNullOrWhiteSpace(NIT)) faltan.Add("NIT");
        if (string.IsNullOrWhiteSpace(Correo)) faltan.Add("correo");
        if (string.IsNullOrWhiteSpace(Telefono)) faltan.Add("teléfono");
        return faltan;
    }

    public Dictionary<string, object?> ADatos(string? numeroCredito)
    {
        var d = new Dictionary<string, object?>();
        DatosApi.Texto(d, "PrimerNombre", PrimerNombre);
        DatosApi.Texto(d, "SegundoNombre", SegundoNombre);
        DatosApi.Texto(d, "PrimerApellido", PrimerApellido);
        DatosApi.Texto(d, "SegundoApellido", SegundoApellido);
        DatosApi.Texto(d, "DPI", DPI);
        DatosApi.Texto(d, "NIT", NIT);
        DatosApi.Texto(d, "Correo", Correo);
        DatosApi.Texto(d, "Telefono", Telefono);
        DatosApi.Texto(d, "FechaNacimiento", FechaNacimiento);
        DatosApi.Texto(d, "NumeroCredito", numeroCredito);
        return d;
    }
}

//Formulario de emisión de crédito
public class CreditoForm
{
    // Cliente y vehículo se eligen con un buscador
    public string? CodigoDeudor { get; set; }
    public string? TextoCliente { get; set; }
    public string? CodigoGarantia { get; set; }
    public string? TextoGarantia { get; set; }

    public string? CodigoVendedor { get; set; }
    public string? Enganche { get; set; }

    // Datos Opcionales si se dejan vacíos, el procedimiento usa sus valores por defecto
    public string? FechaCredito { get; set; }
    public string? Plazo { get; set; }
    public string? TasaInteres { get; set; }
    public string? PorcentajeComision { get; set; }

    //codigoReferencia de las referencias elegidas
    public List<int> Referencias { get; set; } = new();

    //Códigos de persona de fiadores que ya existen, separados por coma
    public string? FiadoresExistentes { get; set; }

    //Fiadores nuevos: se registran y se ligan al crédito justo después de emitirlo
    public List<FiadorNuevoForm> NuevosFiadores { get; set; } = new();

    public List<int> CodigosFiadoresExistentes() =>
        (FiadoresExistentes ?? "")
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

    //Validaciones previas al envío
    public List<string> Validar()
    {
        var errores = new List<string>();

        if (!long.TryParse(CodigoDeudor, out _)) errores.Add("Elija un cliente de la lista.");
        if (!long.TryParse(CodigoGarantia, out _)) errores.Add("Elija un vehículo disponible de la lista.");
        if (!long.TryParse(CodigoVendedor, out _)) errores.Add("Elija un vendedor.");

        if (!decimal.TryParse(Enganche?.Trim(), System.Globalization.NumberStyles.Number,
                              System.Globalization.CultureInfo.InvariantCulture, out var enganche) || enganche < 0)
            errores.Add("Escriba el enganche (0 o más).");

        if (Referencias.Distinct().Count() < 3)
            errores.Add("Seleccione al menos 3 referencias distintas.");

        foreach (var texto in (FiadoresExistentes ?? "")
                     .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!int.TryParse(texto, out _))
                errores.Add($"El código de fiador \"{texto}\" no es un número.");

        var numero = 0;
        foreach (var fiador in NuevosFiadores.Where(f => !f.Vacio))
        {
            numero++;
            var faltan = fiador.Faltantes();
            if (faltan.Count > 0)
                errores.Add($"Fiador nuevo {numero}: falta {string.Join(", ", faltan)}.");
        }

        return errores;
    }

    //Cuerpo JSON para POST api/credito
    public Dictionary<string, object?> ADatos()
    {
        var d = new Dictionary<string, object?>();
        DatosApi.Numero(d, "CodigoDeudor", CodigoDeudor);
        DatosApi.Numero(d, "CodigoGarantia", CodigoGarantia);
        DatosApi.Numero(d, "CodigoVendedor", CodigoVendedor);
        DatosApi.Decimal(d, "Enganche", Enganche);

        // La API acepta [1,2,3] y lo convierte en "1,2,3" para el procedimiento
        d["Referencias"] = Referencias.Distinct().ToList();

        var fiadores = CodigosFiadoresExistentes();
        if (fiadores.Count > 0)
            d["Fiadores"] = fiadores;

        DatosApi.Texto(d, "FechaCredito", FechaCredito);
        DatosApi.Numero(d, "Plazo", Plazo);
        DatosApi.Decimal(d, "TasaInteres", TasaInteres);
        DatosApi.Decimal(d, "PorcentajeComision", PorcentajeComision);
        return d;
    }
}

//Vehículo disponible para el buscador del formulario, con su valor de agencia
public record GarantiaOpcion(string Valor, string Texto, string? ValorAgencia);

public class CreditoFormVm
{
    public CreditoForm Form { get; set; } = new();
    public List<Opcion> Clientes { get; set; } = new();
    public List<GarantiaOpcion> Garantias { get; set; } = new();
    public List<Opcion> Vendedores { get; set; } = new();

    //Referencias libres
    public List<Fila> Referencias { get; set; } = new();
}

//>Datos de la vista de detalle de un crédito
public class DetalleCreditoVm
{
    public Fila Credito { get; set; } = new();
    public List<Fila> Cuotas { get; set; } = new();
    public List<Fila> Fiadores { get; set; } = new();

    //>true justo después de emitir: se muestra el aviso grande con el número y la cuota
    public bool Nuevo { get; set; }

    //Cuota mensual ya formateada
    public string? CuotaMensual { get; set; }

    public FiadorNuevoForm FiadorNuevo { get; set; } = new();
}

//>Datos para el bloque de campos de un fiador
public class FiadorCamposVm
{
    // <summary>Prefijo de los nombres de campo: Fiador o Form.NuevosFiadores[0]
    public string Prefijo { get; set; } = "Fiador";

    public FiadorNuevoForm Datos { get; set; } = new();
}
