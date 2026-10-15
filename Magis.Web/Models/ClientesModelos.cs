using System.ComponentModel.DataAnnotations;
using Magis.Web.Helpers;
using Magis.Web.Services;

namespace Magis.Web.Models;

public class ClienteForm
{
    public int? CodigoDeudor { get; set; }

    [Required(ErrorMessage = "El primer nombre es obligatorio.")]
    public string? PrimerNombre { get; set; }

    public string? SegundoNombre { get; set; }

    [Required(ErrorMessage = "El primer apellido es obligatorio.")]
    public string? PrimerApellido { get; set; }

    public string? SegundoApellido { get; set; }

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Escriba un correo válido, por ejemplo nombre@correo.com.")]
    public string? Correo { get; set; }

    [Required(ErrorMessage = "El DPI es obligatorio.")]
    public string? DPI { get; set; }

    [Required(ErrorMessage = "El NIT es obligatorio.")]
    public string? NIT { get; set; }

    //Formato yyyy-MM-dd
    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    public string? FechaNacimiento { get; set; }

    public string? NoPasaporte { get; set; }

    public string? Genero { get; set; }

    public string? Telefono { get; set; }

    public string? CodigoTipoTelefono { get; set; }

    //Cuerpo JSON para POST/PUT api/cliente
    public Dictionary<string, object?> ADatos()
    {
        var d = new Dictionary<string, object?>();
        if (CodigoDeudor.HasValue)
            d["CodigoDeudor"] = CodigoDeudor.Value;

        DatosApi.Texto(d, "PrimerNombre", PrimerNombre);
        DatosApi.Texto(d, "SegundoNombre", SegundoNombre);
        DatosApi.Texto(d, "PrimerApellido", PrimerApellido);
        DatosApi.Texto(d, "SegundoApellido", SegundoApellido);
        DatosApi.Texto(d, "Correo", Correo);
        DatosApi.Texto(d, "DPI", DPI);
        DatosApi.Texto(d, "NIT", NIT);
        DatosApi.Texto(d, "FechaNacimiento", FechaNacimiento);
        DatosApi.Texto(d, "NoPasaporte", NoPasaporte);
        DatosApi.Texto(d, "Genero", Genero);
        DatosApi.Texto(d, "Telefono", Telefono);
        DatosApi.Numero(d, "CodigoTipoTelefono", CodigoTipoTelefono);
        return d;
    }

    public static ClienteForm Desde(Fila f) => new()
    {
        CodigoDeudor = f.Entero("CodigoDeudor"),
        PrimerNombre = f["PrimerNombre"],
        SegundoNombre = f["SegundoNombre"],
        PrimerApellido = f["PrimerApellido"],
        SegundoApellido = f["SegundoApellido"],
        Correo = f["Correo"],
        DPI = f["DPI"],
        NIT = f["NIT"],
        FechaNacimiento = DatosApi.FechaParaFormulario(f["FechaNacimiento"]),
        NoPasaporte = f["NoPasaporte"],
        Genero = f["Genero"],
        Telefono = f["Telefono"]
    };
}

//>Datos de la vista del formulario de cliente
public class ClienteFormVm
{
    public ClienteForm Form { get; set; } = new();
    public List<Opcion> TiposTelefono { get; set; } = new();
    public bool Edicion => Form.CodigoDeudor.HasValue;
}

//Datos de la lista de clientes
public class ClientesIndexVm
{
    public string? Criterio { get; set; }
    public List<Fila> Filas { get; set; } = new();
    public int Total { get; set; }
    public int Pagina { get; set; } = 1;
    public int Paginas { get; set; } = 1;
    public int PorPagina { get; set; } = 20;
}

public class TelefonoItem
{
    public string? Correlativo { get; set; }
    public string? Numero { get; set; }
    public string? CodigoTipo { get; set; }
    public string Tipo { get; set; } = "";
}

public class DireccionItem
{
    public string? Correlativo { get; set; }
    public string? Zona { get; set; }
    public string? Calle { get; set; }
    public string? Avenida { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string Departamento { get; set; } = "";
    public string? CodigoMunicipio { get; set; }
    public string Municipio { get; set; } = "";
    public string? CodigoTipo { get; set; }
    public string Tipo { get; set; } = "";
}

//Datos de la ficha de un cliente: datos personales, teléfonos y direcciones.
public class FichaClienteVm
{
    public Fila Cliente { get; set; } = new();
    public int CodigoDeudor { get; set; }
    public int CodigoPersona { get; set; }
    public List<TelefonoItem> Telefonos { get; set; } = new();
    public List<DireccionItem> Direcciones { get; set; } = new();
    public List<Opcion> TiposTelefono { get; set; } = new();
    public List<Opcion> TiposDireccion { get; set; } = new();
    public List<Opcion> Departamentos { get; set; } = new();
}
