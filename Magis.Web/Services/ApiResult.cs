using System.Text.Json;

namespace Magis.Web.Services;

// Resultado de una llamada a la API. ApiClient nunca lanza excepciones por errores de la API
// o de conexión: siempre devuelve un ApiResult para que el controlador decida qué mostrar.
public class ApiResult
{
    public bool Ok { get; init; }

    //Estado HTTP (200, 400, 404, 409, 500...). 503 también cuando no hay conexión con la API.
    public int StatusCode { get; init; }

    //Mensaje para el usuario (campo "error" de la API). Null si la llamada fue correcta
    public string? Error { get; init; }

    //Código de error de SQL Server (campo "codigo" de la API)
    public int? Codigo { get; init; }

    //JSON de la respuesta
    public JsonElement Raiz { get; init; }

    //Filas de la respuesta cuando es un arreglo de objetos
    public List<Fila> Filas { get; init; } = new();

    public Fila? Primera => Filas.Count > 0 ? Filas[0] : null;

    public bool EsConflicto => StatusCode == 409;
    public bool EsNoEncontrado => StatusCode == 404;
    public bool SinConexion => StatusCode == 503;

    //Busca una propiedad de primer nivel en la respuesta
    public JsonElement? Propiedad(string nombre)
    {
        if (Raiz.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var p in Raiz.EnumerateObject())
            if (string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase))
                return p.Value;

        return null;
    }

    public static ApiResult Falla(int status, string mensaje) =>
        new() { Ok = false, StatusCode = status, Error = mensaje };
}
