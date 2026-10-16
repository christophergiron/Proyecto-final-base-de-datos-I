using Magis.Web.Services;

namespace Magis.Web.Models;

//explicación, filtros y formato de cada columna

public class ReporteParamInfo
{
    public string Nombre { get; set; } = "";
    public string Etiqueta { get; set; } = "";
    public string Tipo { get; set; } = "text";
    public bool Obligatorio { get; set; }
}

public class ReporteColInfo
{
    public string Nombre { get; set; } = "";

    public string Formato { get; set; } = "texto";
}

public class ReporteInfo
{
    public int Numero { get; set; }
    public string Procedimiento { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Razon { get; set; } = "";
    public List<ReporteParamInfo> Parametros { get; set; } = new();
    public bool LimitaFilas { get; set; }
    public List<ReporteColInfo> Columnas { get; set; } = new();
}

// Reporte ya combinado

public class ReporteParam
{
    public string Nombre { get; set; } = "";
    public string Etiqueta { get; set; } = "";

    //date, int, decimal, boolean o text
    public string Tipo { get; set; } = "text";
    public bool Obligatorio { get; set; }

    //Control del formulario: texto, fecha, entero, decimal, booleano, mes, cliente, credito, vendedor o marca
    public string Control { get; set; } = "texto";
}

public class ReporteDef
{
    public int Numero { get; set; }
    public string Nombre { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Razon { get; set; } = "";
    public List<ReporteParam> Parametros { get; set; } = new();
    public List<ReporteColInfo>? Columnas { get; set; }
    public bool LimitaFilas { get; set; }
}

public class CampoReporteVm
{
    public ReporteParam Param { get; set; } = new();
    public string? Valor { get; set; }
    public List<Opcion> Opciones { get; set; } = new();
}

public class TablaReporteVm
{
    public List<Fila> Filas { get; set; } = new();
    public List<ReporteColInfo> Columnas { get; set; } = new();
    public string Id { get; set; } = "tReporte";

    //Columnas de dinero o número que llevan fila de totales.
    public HashSet<string> Totales { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string AccionFila { get; set; } = "";

    //Valores que se conservan al abrir la vista imprimible de una fila
    public Dictionary<string, string> RutaParams { get; set; } = new();
}

public class ReporteVm
{
    public ReporteDef Def { get; set; } = new();
    public Dictionary<string, string> Valores { get; set; } = new();
    public Dictionary<string, List<Opcion>> Opciones { get; set; } = new();
    public bool Ejecutado { get; set; }
    public List<Fila> Filas { get; set; } = new();
    public List<ReporteColInfo> Columnas { get; set; } = new();

    //Filas que cumplen el filtro
    public int? TotalDisponible { get; set; }

    //Filtros aplicados en texto
    public string Resumen { get; set; } = "";
}

public class EstadoCuentaVm
{
    public List<Fila> Creditos { get; set; } = new();
    public string CodigoDeudor { get; set; } = "";
}

public class PlanPagosVm
{
    public string NumeroCredito { get; set; } = "";
    public List<Fila> Cuotas { get; set; } = new();
    public Fila? Credito { get; set; }
    public List<ReporteColInfo> Columnas { get; set; } = new();
}

public class CartasVm
{
    public List<Fila> Clientes { get; set; } = new();
    public string FechaTexto { get; set; } = "";
}
