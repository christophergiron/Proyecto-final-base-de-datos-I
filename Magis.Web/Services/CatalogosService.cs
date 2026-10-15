using System.Text.Json;

namespace Magis.Web.Services;

public record Opcion(string Valor, string Texto);

// Lee los catálogos de la API  para llenar listas desplegables
// Cada catálogo nombra sus columnas a su manera
// el texto es la columna que indique quien llama; si no, Descripcion o Nombre; si no, la columna que se llama igual que el código sin el prefijo "Codigo" (CodigoLinea a Linea).
public class CatalogosService
{
    private readonly ApiClient _api;

    public CatalogosService(ApiClient api)
    {
        _api = api;
    }

    //Lista un catálogo
    public async Task<(List<Opcion> Opciones, string? Error)> ListarAsync(
        string entidad, string[] columnasCodigo, IDictionary<string, string?>? filtros = null,
        string? columnaTexto = null)
    {
        var resultado = await _api.GetAsync("api/" + entidad, filtros);
        if (!resultado.Ok)
            return (new List<Opcion>(), resultado.Error);

        var opciones = resultado.Filas
            .Select(f => Convertir(f, columnasCodigo, columnaTexto))
            .Where(o => o is not null)
            .Select(o => o!)
            .ToList();

        return (opciones, null);
    }

    public static Opcion? Convertir(Fila fila, string[] columnasCodigo, string? columnaTextoIndicada = null)
    {
        var columnaCodigo = columnasCodigo.FirstOrDefault(fila.Tiene)
                            ?? fila.Columnas.FirstOrDefault(c => c.StartsWith("Codigo", StringComparison.OrdinalIgnoreCase));
        if (columnaCodigo is null)
            return null;

        var valor = fila.Texto(columnaCodigo);
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var columnaTexto = ElegirColumnaTexto(fila, columnaCodigo, columnaTextoIndicada);
        var texto = columnaTexto is null ? valor : (fila.Texto(columnaTexto) ?? valor);
        return new Opcion(valor, texto);
    }

    private static string? ElegirColumnaTexto(Fila fila, string columnaCodigo, string? indicada)
    {
        if (!string.IsNullOrWhiteSpace(indicada) && fila.Tiene(indicada))
            return indicada;

        foreach (var candidata in new[] { "Descripcion", "Nombre" })
            if (fila.Tiene(candidata))
                return candidata;

        // CodigoLinea a Linea, CodigoMunicipio a Municipio y CodigoMarca a Marca
        const string prefijo = "Codigo";
        if (columnaCodigo.Length > prefijo.Length &&
            columnaCodigo.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
        {
            var sinPrefijo = columnaCodigo[prefijo.Length..];
            if (fila.Tiene(sinPrefijo))
                return sinPrefijo;
        }

        return fila.Columnas.FirstOrDefault(c =>
            !string.Equals(c, columnaCodigo, StringComparison.OrdinalIgnoreCase) &&
            fila.Valor(c)?.ValueKind == JsonValueKind.String);
    }

    //Texto de un código dentro de una lista
    public static string Texto(IEnumerable<Opcion> opciones, string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return "";
        return opciones.FirstOrDefault(o => o.Valor == codigo)?.Texto ?? codigo;
    }
}
