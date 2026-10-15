using Magis.Web.Services;

namespace Magis.Web.Models;

//Datos para la vista de inicio
public class InicioVm
{
    public string ApiUrl { get; set; } = "";
    public bool Conectada { get; set; }
    public string? Error { get; set; }

    //Entidades que expone la API, con la cantidad de operaciones de cada una
    public List<(string Nombre, int Operaciones)> Entidades { get; set; } = new();
}

//atos para la página de error
public class ErrorVm
{
    public int Codigo { get; set; } = 500;
    public string Titulo { get; set; } = "Algo salió mal";
    public string Mensaje { get; set; } = "Ocurrió un error inesperado. Intente de nuevo.";
}

// Configuración de la tabla reutilizable, Las columnas se toman de los datos que devuelve la API, así que sirve para cualquier entidad o reporte.

public class TablaModelo
{
    public List<Fila> Filas { get; init; } = new();

    //¿Columnas a mostrar, en orden. Si es null se usan todas las de la primera fila
    public List<string>? Columnas { get; init; }

    //Columnas que no se deben mostrar
    public HashSet<string>? Ocultas { get; init; }

    //Identificador HTML de la tabla
    public string Id { get; init; } = "tabla";

    //Muestra un cuadro para filtrar las filas mientras se escribe
    public bool Buscador { get; init; } = true;

    public string Vacio { get; init; } = "No hay registros para mostrar.";

    //Columnas visibles
    public List<string> ColumnasVisibles()
    {
        var origen = Columnas ?? Filas.FirstOrDefault()?.Columnas.ToList() ?? new List<string>();
        return origen.Where(c => Ocultas is null || !Ocultas.Contains(c)).ToList();
    }
}
