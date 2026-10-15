using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Magis.Web.Helpers;
using Magis.Web.Services;

namespace Magis.Web.Models;

//página de resultados de una lista
public class ListaVm
{
    public string? Criterio { get; set; }

    //Solo vehículos disponibles
    public bool Solo { get; set; }

    public List<Fila> Filas { get; set; } = new();
    public int Total { get; set; }
    public int Pagina { get; set; } = 1;
    public int Paginas { get; set; } = 1;
    public int PorPagina { get; set; } = 20;

    public int Desde => Total == 0 ? 0 : (Pagina - 1) * PorPagina + 1;
    public int Hasta => (Pagina - 1) * PorPagina + Filas.Count;
}

//Datos del paginador reutilizable
public class PaginadorVm
{
    public int Pagina { get; set; } = 1;
    public int Paginas { get; set; } = 1;
    public string Accion { get; set; } = "Index";

    //Valores que se conservan al cambiar de página
    public Dictionary<string, object?> Ruta { get; set; } = new();
}

//Referencias

//Formulario de referencia personal
public class ReferenciaForm
{
    public int? CodigoReferencia { get; set; }

    [Required(ErrorMessage = "El primer nombre es obligatorio.")]
    public string? PrimerNombre { get; set; }

    public string? SegundoNombre { get; set; }

    [Required(ErrorMessage = "El primer apellido es obligatorio.")]
    public string? PrimerApellido { get; set; }

    public string? SegundoApellido { get; set; }

    [Required(ErrorMessage = "Seleccione el tipo de referencia.")]
    public string? CodigoTipoReferencia { get; set; }

    public string? LugarTrabajo { get; set; }

    //Los teléfonos solo se registran al crear
    public string? TelefonoCelular { get; set; }
    public string? TelefonoCasa { get; set; }
    public string? TelefonoTrabajo { get; set; }

    public bool SinTelefonos =>
        string.IsNullOrWhiteSpace(TelefonoCelular) &&
        string.IsNullOrWhiteSpace(TelefonoCasa) &&
        string.IsNullOrWhiteSpace(TelefonoTrabajo);

    public Dictionary<string, object?> ADatos()
    {
        var d = new Dictionary<string, object?>();
        if (CodigoReferencia.HasValue)
            d["CodigoReferencia"] = CodigoReferencia.Value;

        DatosApi.Texto(d, "PrimerNombre", PrimerNombre);
        DatosApi.Texto(d, "SegundoNombre", SegundoNombre);
        DatosApi.Texto(d, "PrimerApellido", PrimerApellido);
        DatosApi.Texto(d, "SegundoApellido", SegundoApellido);
        DatosApi.Numero(d, "CodigoTipoReferencia", CodigoTipoReferencia);
        DatosApi.Texto(d, "LugarTrabajo", LugarTrabajo);
        DatosApi.Texto(d, "TelefonoCelular", TelefonoCelular);
        DatosApi.Texto(d, "TelefonoCasa", TelefonoCasa);
        DatosApi.Texto(d, "TelefonoTrabajo", TelefonoTrabajo);
        return d;
    }

    public static ReferenciaForm Desde(Fila f) => new()
    {
        CodigoReferencia = f.Entero("CodigoReferencia"),
        PrimerNombre = f["PrimerNombre"],
        SegundoNombre = f["SegundoNombre"],
        PrimerApellido = f["PrimerApellido"],
        SegundoApellido = f["SegundoApellido"],
        CodigoTipoReferencia = f["CodigoTipoReferencia"],
        LugarTrabajo = f["LugarTrabajo"]
    };
}

public class ReferenciaFormVm
{
    public ReferenciaForm Form { get; set; } = new();
    public List<Opcion> Tipos { get; set; } = new();
    public bool Edicion => Form.CodigoReferencia.HasValue;
}

//Garantías

//Formulario de vehículo en garantía
public class GarantiaForm
{
    public int? CodigoGarantia { get; set; }

    [Required(ErrorMessage = "El VIN es obligatorio.")]
    public string? VIN { get; set; }

    [Required(ErrorMessage = "Seleccione la marca.")]
    public string? CodigoMarca { get; set; }

    [Required(ErrorMessage = "Seleccione la línea.")]
    public string? CodigoLinea { get; set; }

    // Obligatorios al registrar Y al editar: sp_Garantia_Actualizar los exige
    [Required(ErrorMessage = "El año de fabricación es obligatorio.")]
    public string? AnioFabricacion { get; set; }

    [Required(ErrorMessage = "El valor de agencia es obligatorio.")]
    public string? ValorAgencia { get; set; }

    public string? Placa { get; set; }
    public string? Chasis { get; set; }
    public string? Serie { get; set; }

    //Entre 15 y 20, si se omite, el procedimiento usa su valor por defecto
    public string? PorcentajeDepreciacion { get; set; }

    public string? Descripcion { get; set; }

    //Tipo de garantía
    public string? TipoGarantia { get; set; }

    public Dictionary<string, object?> ADatos()
    {
        var d = new Dictionary<string, object?>();
        if (CodigoGarantia.HasValue)
            d["CodigoGarantia"] = CodigoGarantia.Value;

        DatosApi.Texto(d, "VIN", VIN);
        DatosApi.Numero(d, "CodigoMarca", CodigoMarca);
        DatosApi.Numero(d, "CodigoLinea", CodigoLinea);
        DatosApi.Numero(d, "AnioFabricacion", AnioFabricacion);
        DatosApi.Decimal(d, "ValorAgencia", ValorAgencia);
        DatosApi.Texto(d, "Placa", Placa);
        DatosApi.Texto(d, "Chasis", Chasis);
        DatosApi.Texto(d, "Serie", Serie);
        DatosApi.Decimal(d, "PorcentajeDepreciacion", PorcentajeDepreciacion);
        DatosApi.Texto(d, "Descripcion", Descripcion);
        DatosApi.Numero(d, "TipoGarantia", TipoGarantia);
        return d;
    }

    public static GarantiaForm Desde(Fila f) => new()
    {
        CodigoGarantia = f.Entero("CodigoGarantia"),
        VIN = f["VIN"],
        CodigoMarca = f["CodigoMarca"],
        CodigoLinea = f["CodigoLinea"],
        AnioFabricacion = f["AnioFabricacion"],
        ValorAgencia = f.Decimal("ValorAgencia")?.ToString("0.00", CultureInfo.InvariantCulture),
        Placa = f["Placa"],
        Chasis = f["Chasis"],
        Serie = f["Serie"],
        PorcentajeDepreciacion = f.Decimal("PorcentajeDepreciacion")?.ToString("0.##", CultureInfo.InvariantCulture),
        Descripcion = f["Descripcion"],
        TipoGarantia = f["TipoGarantia"]
    };
}

public class GarantiaFormVm
{
    public GarantiaForm Form { get; set; } = new();
    public List<Opcion> Marcas { get; set; } = new();
    public List<Opcion> Lineas { get; set; } = new();
    public List<Opcion> Tipos { get; set; } = new();
    public bool Edicion => Form.CodigoGarantia.HasValue;
}
