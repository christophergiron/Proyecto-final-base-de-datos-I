using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Magis.Web.Services;

public class ApiClient
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _log;

    public ApiClient(HttpClient http, ILogger<ApiClient> log)
    {
        _http = http;
        _log = log;
    }

    //URL base de la API configurada
    public string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    //GET / POST / PUT / DELETE

    //Los filtros van al query string y los vacíos se omiten
    public Task<ApiResult> GetAsync(string ruta, IDictionary<string, string?>? filtros = null) =>
        EnviarAsync(HttpMethod.Get, ConQuery(ruta, filtros), null);

    //El cuerpo se envía como JSON
    public Task<ApiResult> PostAsync(string ruta, object cuerpo) =>
        EnviarAsync(HttpMethod.Post, Limpiar(ruta), cuerpo);

    //El cuerpo debe incluir las llaves del registro
    public Task<ApiResult> PutAsync(string ruta, object cuerpo) =>
        EnviarAsync(HttpMethod.Put, Limpiar(ruta), cuerpo);

    public Task<ApiResult> DeleteAsync(string ruta, IDictionary<string, string?> llaves) =>
        EnviarAsync(HttpMethod.Delete, ConQuery(ruta, llaves), null);

    //Núcleo

    private async Task<ApiResult> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(metodo, ruta);
            if (cuerpo is not null)
            {
                var json = JsonSerializer.Serialize(cuerpo, OpcionesJson);
                solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var respuesta = await _http.SendAsync(solicitud);
            var texto = await respuesta.Content.ReadAsStringAsync();
            return Interpretar((int)respuesta.StatusCode, texto);
        }
        catch (TaskCanceledException ex)
        {
            _log.LogWarning(ex, "La API no respondió a tiempo: {Metodo} {Ruta}", metodo, ruta);
            return ApiResult.Falla(503, "La API tardó demasiado en responder. Intente de nuevo en unos segundos.");
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "No hay conexión con la API: {Metodo} {Ruta}", metodo, ruta);
            return ApiResult.Falla(503,
                $"No se pudo conectar con la API ({BaseUrl}). Verifique que el proyecto Magis.Api esté en ejecución.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error inesperado al llamar a la API: {Metodo} {Ruta}", metodo, ruta);
            return ApiResult.Falla(500, "Ocurrió un error inesperado al comunicarse con la API.");
        }
    }

    private static ApiResult Interpretar(int estado, string texto)
    {
        JsonElement raiz = default;
        var esJson = false;

        if (!string.IsNullOrWhiteSpace(texto))
        {
            try
            {
                using var documento = JsonDocument.Parse(texto);
                raiz = documento.RootElement.Clone();
                esJson = true;
            }
            catch (JsonException)
            {
            }
        }

        if (estado is >= 200 and < 300)
        {
            var filas = new List<Fila>();
            if (esJson && raiz.ValueKind == JsonValueKind.Array)
            {
                foreach (var elemento in raiz.EnumerateArray())
                    if (elemento.ValueKind == JsonValueKind.Object)
                        filas.Add(Fila.Desde(elemento));
            }
            return new ApiResult { Ok = true, StatusCode = estado, Raiz = raiz, Filas = filas };
        }

        string? mensaje = null;
        int? codigo = null;

        if (esJson && raiz.ValueKind == JsonValueKind.Object)
        {
            foreach (var propiedad in raiz.EnumerateObject())
            {
                if (string.Equals(propiedad.Name, "error", StringComparison.OrdinalIgnoreCase) &&
                    propiedad.Value.ValueKind == JsonValueKind.String)
                    mensaje = propiedad.Value.GetString();
                else if (string.Equals(propiedad.Name, "codigo", StringComparison.OrdinalIgnoreCase) &&
                         propiedad.Value.ValueKind == JsonValueKind.Number &&
                         propiedad.Value.TryGetInt32(out var n))
                    codigo = n;
            }
        }

        mensaje ??= MensajePorEstado(estado);

        return new ApiResult { Ok = false, StatusCode = estado, Error = mensaje, Codigo = codigo, Raiz = raiz };
    }

    private static string MensajePorEstado(int estado) => estado switch
    {
        400 => "Los datos enviados no son válidos. Revise el formulario.",
        404 => "El registro o la operación solicitada no existe.",
        405 => "Esta operación no está permitida para esta entidad.",
        409 => "El dato ya existe o el registro está en uso y no se puede modificar o eliminar.",
        503 => "No hay conexión con la base de datos o con la API.",
        _ => $"La API respondió con un error inesperado (HTTP {estado})."
    };

    //Utilidades

    private static string Limpiar(string ruta) => ruta.TrimStart('/');

    //Agrega al query string solo los filtros que tengan valor
    private static string ConQuery(string ruta, IDictionary<string, string?>? filtros)
    {
        ruta = Limpiar(ruta);
        if (filtros is null)
            return ruta;

        var partes = filtros
            .Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value!.Trim())}")
            .ToList();

        return partes.Count == 0 ? ruta : $"{ruta}?{string.Join("&", partes)}";
    }
}
