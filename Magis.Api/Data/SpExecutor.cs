using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Magis.Api.Data;

/// <summary>
/// Unico punto de acceso a la base de datos. Ejecuta procedimientos almacenados con Dapper
/// y devuelve las filas como diccionarios (nombre de columna -> valor).
/// </summary>
public class SpExecutor
{
    private readonly string _cadena;

    public SpExecutor(IConfiguration configuracion)
    {
        _cadena = configuracion.GetConnectionString("MagisDb")
                  ?? throw new InvalidOperationException(
                      "Falta ConnectionStrings:MagisDb en appsettings.json (o en los secretos de usuario).");
    }

    public async Task<List<Dictionary<string, object?>>> EjecutarAsync(
        string procedimiento, Dictionary<string, object?>? parametros = null)
    {
        var dp = new DynamicParameters();
        if (parametros is not null)
            foreach (var par in parametros)
                dp.Add(par.Key, par.Value);

        await using var cn = new SqlConnection(_cadena);
        var filas = await cn.QueryAsync(procedimiento, dp,
                                        commandType: CommandType.StoredProcedure,
                                        commandTimeout: 60);
        return Convertir(filas);
    }

    /// <summary>Consulta de metadatos (nombres de reportes, parametros de un SP). Nunca recibe texto del usuario.</summary>
    public async Task<List<Dictionary<string, object?>>> ConsultarAsync(string sql, object? parametros = null)
    {
        await using var cn = new SqlConnection(_cadena);
        var filas = await cn.QueryAsync(sql, parametros);
        return Convertir(filas);
    }

    private static List<Dictionary<string, object?>> Convertir(IEnumerable<dynamic> filas) =>
        filas.Cast<IDictionary<string, object?>>()
             .Select(fila => new Dictionary<string, object?>(fila))
             .ToList();
}
