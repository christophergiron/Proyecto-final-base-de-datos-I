using System.Globalization;
using Magis.Web.Helpers;
using Magis.Web.Services;

namespace Magis.Web.Models;

//Formulario de registro de pago
public class PagoForm
{
    public string? NumeroCredito { get; set; }
    public string? Cuota { get; set; }
    public string? CodigoTipoPago { get; set; }
    public string? Monto { get; set; }
    public string? ReferenciaBancaria { get; set; }

    //Formato yyyy-MM-ddTHH:mm, si esta vacio usa la fecha del sistema
    public string? FechaPago { get; set; }

    public List<string> Validar(bool esEfectivo)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(NumeroCredito)) errores.Add("Elija el crédito.");
        if (!int.TryParse(Cuota, out _)) errores.Add("Elija la cuota que se va a pagar.");
        if (!int.TryParse(CodigoTipoPago, out _)) errores.Add("Elija el tipo de pago.");

        if (!decimal.TryParse(Monto?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var monto) || monto <= 0)
            errores.Add("Escriba un monto mayor que cero.");

        if (int.TryParse(CodigoTipoPago, out _) && !esEfectivo && string.IsNullOrWhiteSpace(ReferenciaBancaria))
            errores.Add("Los pagos que no son en efectivo necesitan la referencia bancaria (número de boleta, transferencia, autorización...).");

        var errorFecha = ErrorFecha(FechaPago);
        if (errorFecha is not null) errores.Add(errorFecha);

        return errores;
    }

    //Mensaje de error si la fecha no es válida o es futura
    public static string? ErrorFecha(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (!DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return "La fecha del pago no es válida.";
        if (fecha > DateTime.Now.AddMinutes(5))
            return "La fecha del pago no puede ser futura.";
        return null;
    }

    public static string? FechaParaApi(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? f.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
            : null;
    }

    public Dictionary<string, object?> ADatos(bool esEfectivo)
    {
        var d = new Dictionary<string, object?>();
        DatosApi.Texto(d, "NumeroCredito", NumeroCredito);
        DatosApi.Numero(d, "Cuota", Cuota);
        DatosApi.Numero(d, "CodigoTipoPago", CodigoTipoPago);
        DatosApi.Decimal(d, "Monto", Monto);
        if (!esEfectivo) DatosApi.Texto(d, "ReferenciaBancaria", ReferenciaBancaria);
        DatosApi.Texto(d, "FechaPago", FechaParaApi(FechaPago));
        return d;
    }
}

//Datos de la pantalla Registrar pago
public class PagoFormVm
{
    public PagoForm Form { get; set; } = new();

    //Créditos para el buscador
    public List<Opcion> Creditos { get; set; } = new();

    public Fila? Credito { get; set; }
    public List<Fila> Cuotas { get; set; } = new();
    public List<Opcion> TiposPago { get; set; } = new();
}

//Datos para la tabla de cuotas
public class CuotasVm
{
    public List<Fila> Filas { get; set; } = new();
    public string Numero { get; set; } = "";
    public string Id { get; set; } = "tCuotas";

    //Muestra el botón "Pagar" en las cuotas que aún tienen saldo
    public bool Pagar { get; set; }
}

//Datos de Pagos y cuotas de un crédito
public class CuentaVm
{
    public Fila Credito { get; set; } = new();
    public List<Fila> Cuotas { get; set; } = new();
    public List<Fila> Pagos { get; set; } = new();
}

//Datos del comprobante imprimible de un pago
public class ComprobanteVm
{
    public Fila Pago { get; set; } = new();
    public Fila? Credito { get; set; }
    public Fila? Cuota { get; set; }
    public bool Nuevo { get; set; }
}

//Edición de un pago
public class PagoEditForm
{
    public string? NumeroCredito { get; set; }
    public string? Cuota { get; set; }
    public string? NumeroPago { get; set; }
    public string? CodigoTipoPago { get; set; }
    public string? ReferenciaBancaria { get; set; }
    public string? FechaPago { get; set; }

    public List<string> Validar(bool esEfectivo)
    {
        var errores = new List<string>();
        if (!int.TryParse(CodigoTipoPago, out _)) errores.Add("Elija el tipo de pago.");
        else if (!esEfectivo && string.IsNullOrWhiteSpace(ReferenciaBancaria))
            errores.Add("Los pagos que no son en efectivo necesitan la referencia bancaria.");

        var errorFecha = PagoForm.ErrorFecha(FechaPago);
        if (errorFecha is not null) errores.Add(errorFecha);
        return errores;
    }

    public Dictionary<string, object?> ADatos(bool esEfectivo)
    {
        var d = new Dictionary<string, object?>();
        DatosApi.Texto(d, "NumeroCredito", NumeroCredito);
        DatosApi.Numero(d, "Cuota", Cuota);
        DatosApi.Numero(d, "NumeroPago", NumeroPago);
        DatosApi.Numero(d, "CodigoTipoPago", CodigoTipoPago);
        if (!esEfectivo) DatosApi.Texto(d, "ReferenciaBancaria", ReferenciaBancaria);
        DatosApi.Texto(d, "FechaPago", PagoForm.FechaParaApi(FechaPago));
        return d;
    }
}

public class EditarPagoVm
{
    public PagoEditForm Form { get; set; } = new();
    public Fila Pago { get; set; } = new();
    public List<Opcion> TiposPago { get; set; } = new();
}
