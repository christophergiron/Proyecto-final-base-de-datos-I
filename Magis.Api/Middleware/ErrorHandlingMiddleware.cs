using Magis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Magis.Api.Middleware;

/// <summary>
/// Atrapa los errores y los convierte en JSON  { "error": "...", "codigo": 50002 }
/// con el estado HTTP que corresponde (400, 404, 409, 503, 500).
/// Los errores 5xxxx son los que lanzan los procedimientos almacenados con THROW.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ErrorHandlingMiddleware> _log;

    public ErrorHandlingMiddleware(RequestDelegate siguiente, ILogger<ErrorHandlingMiddleware> log)
    {
        _siguiente = siguiente;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _siguiente(contexto);
        }
        catch (ApiException ex)
        {
            await Responder(contexto, ex.Status, ex.Message, null);
        }
        catch (SqlException ex)
        {
            var (estado, mensaje) = Traducir(ex);
            if (estado >= 500)
                _log.LogError(ex, "Error SQL {Numero}", ex.Number);
            await Responder(contexto, estado, mensaje, ex.Number);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error no controlado");
            await Responder(contexto, 500, "Ocurrio un error inesperado en el servidor.", null);
        }
    }

    public static (int Estado, string Mensaje) Traducir(SqlException ex)
    {
        int n = ex.Number;

        // El registro no existe
        if (n is 50001 or 50040) return (404, ex.Message);

        // Datos duplicados (DPI, NIT, correo, VIN, comprobante...)
        if (n is 50002 or 50048) return (409, ex.Message);
        if (n is 2627 or 2601) return (409, "Ya existe un registro con ese valor (dato duplicado).");

        // Llave foranea: el registro esta en uso, o apunta a algo que no existe
        if (n == 547)
            return (409, "La operacion no se puede completar: el registro esta en uso por otros datos " +
                         "o hace referencia a un dato que no existe.");

        // Parametros mal enviados
        if (n is 201 or 8143 or 8144)
            return (400, "Parametros incorrectos para esta operacion. " + ex.Message);
        if (n is 220 or 232 or 241 or 242 or 245 or 295 or 8114 or 8115 or 8152)
            return (400, "Alguno de los valores enviados no tiene el formato o el tamano correcto. " + ex.Message);

        // Validaciones de negocio lanzadas por los SP (THROW 5xxxx)
        if (n >= 50000 && n < 60000) return (400, ex.Message);

        // Procedimiento inexistente: faltan scripts de instalacion
        if (n == 2812)
            return (500, "El procedimiento almacenado no existe. Verifique que ejecuto MagisV2_Instalacion_Completa.sql.");

        // No hay conexion con SQL Server o la base
        if (n is 2 or 53 or 40 or 4060 or 18456 or 10060 or -1)
            return (503, "No se pudo conectar con la base de datos. Revise la cadena de conexion y que SQL Server este activo.");

        return (500, "Error en la base de datos: " + ex.Message);
    }

    private static async Task Responder(HttpContext contexto, int estado, string mensaje, int? codigo)
    {
        if (contexto.Response.HasStarted) return;
        contexto.Response.StatusCode = estado;
        await contexto.Response.WriteAsJsonAsync(new { error = mensaje, codigo });
    }
}
