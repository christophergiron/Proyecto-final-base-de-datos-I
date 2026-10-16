using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Magis.Web.Helpers;
using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

// Módulo de reportes
public class ReportesController : MagisController
{
    private const string Prefijo = "p_";
    private static readonly Regex NombreReporte = new(@"^sp_Reporte_(\d+)_(.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ApiClient _api;
    private readonly CatalogosService _catalogos;
    private readonly IWebHostEnvironment _env;

    public ReportesController(ApiClient api, CatalogosService catalogos, IWebHostEnvironment env)
    {
        _api = api;
        _catalogos = catalogos;
        _env = env;
    }

    //Menú de reportes

    public async Task<IActionResult> Index()
    {
        var (reportes, error) = await CargarReportesAsync();
        if (error is not null)
            ViewData["Error"] = error;
        else if (reportes.Count == 0)
            ViewData["Aviso"] = "La API todavía no tiene reportes publicados (procedimientos sp_Reporte_NN_...). " +
                                "Cuando existan aparecerán aquí automáticamente.";
        return View(reportes);
    }

    //filtros y resultado

    public async Task<IActionResult> Ver(int numero)
    {
        var def = await ObtenerAsync(numero);
        if (def is null) return NoExiste(numero);

        var vm = new ReporteVm { Def = def, Valores = LeerValores(def) };
        vm.Opciones = await OpcionesAsync(def);

        // Con filtros opcionales se muestra el resultado de una vez
        var enviado = Request.Query.ContainsKey("ejecutar");
        var autoejecutar = !def.Parametros.Any(p => p.Obligatorio);
        if (!enviado && !autoejecutar) return View(vm);

        var errores = Validar(def, vm.Valores);
        if (errores.Count > 0)
        {
            ViewData["Error"] = string.Join(" ", errores);
            return View(vm);
        }

        var resultado = await EjecutarAsync(numero, Normalizar(def, vm.Valores));
        vm.Ejecutado = true;
        vm.Resumen = ResumenDe(def, vm.Valores, vm.Opciones);

        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            vm.Ejecutado = false;
            return View(vm);
        }

        vm.Filas = resultado.Filas;
        vm.Columnas = ColumnasDe(def, resultado.Filas);
        vm.TotalDisponible = resultado.Primera?.Entero("TotalFilasDisponibles");
        return View(vm);
    }

    //Descarga el resultado con los mismos filtros como CSV
    public async Task<IActionResult> Exportar(int numero)
    {
        var def = await ObtenerAsync(numero);
        if (def is null) return NoExiste(numero);

        var valores = LeerValores(def);
        var errores = Validar(def, valores);
        if (errores.Count > 0)
        {
            Fallo(string.Join(" ", errores));
            return RedirectToAction(nameof(Ver), new { numero });
        }

        var resultado = await EjecutarAsync(numero, Normalizar(def, valores));
        if (!resultado.Ok)
        {
            Fallo(resultado);
            return RedirectToAction(nameof(Ver), new { numero });
        }

        var columnas = ColumnasDe(def, resultado.Filas);
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", columnas.Select(c => Celda(Formato.Encabezado(c.Nombre)))));
        foreach (var fila in resultado.Filas)
            csv.AppendLine(string.Join(",", columnas.Select(c => Celda(ReportesFormato.Celda(fila, c.Nombre, c.Formato)))));

        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"reporte_{numero:00}_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    //Reporte 5: cartas de cumpleaños

    public async Task<IActionResult> Cartas(string? codigo)
    {
        var filtros = new Dictionary<string, string?>
        {
            ["Mes"] = Request.Query[Prefijo + "Mes"].ToString(),
            ["SoloConCredito"] = Request.Query[Prefijo + "SoloConCredito"].ToString(),
            ["MaxFilas"] = "0"   // 0 = todas
        };

        var resultado = await EjecutarAsync(5, filtros);
        if (!resultado.Ok)
        {
            Fallo(resultado);
            return RedirectToAction(nameof(Ver), new { numero = 5 });
        }

        var clientes = resultado.Filas;
        if (!string.IsNullOrWhiteSpace(codigo))
            clientes = clientes.Where(f => f["CodigoDeudor"] == codigo).ToList();

        if (clientes.Count == 0)
        {
            Aviso("No hay cumpleañeros para generar cartas con esos filtros.");
            return RedirectToAction(nameof(Ver), new { numero = 5 });
        }

        return View(new CartasVm { Clientes = clientes, FechaTexto = ReportesFormato.FechaLarga(DateTime.Now) });
    }

    //Reporte 6: estado de cuenta

    public async Task<IActionResult> EstadoCuenta()
    {
        var codigo = PrimerToken(Request.Query[Prefijo + "CodigoDeudor"].ToString());
        if (!int.TryParse(codigo, out _))
        {
            Fallo("Elija el cliente del estado de cuenta.");
            return RedirectToAction(nameof(Ver), new { numero = 6 });
        }

        var resultado = await EjecutarAsync(6, new Dictionary<string, string?> { ["CodigoDeudor"] = codigo });
        if (!resultado.Ok)
        {
            Fallo(resultado);
            return RedirectToAction(nameof(Ver), new { numero = 6 });
        }

        if (resultado.Filas.Count == 0)
        {
            Aviso("Ese cliente no tiene créditos para mostrar en el estado de cuenta.");
            return RedirectToAction(nameof(Ver), new { numero = 6 });
        }

        return View(new EstadoCuentaVm { Creditos = resultado.Filas, CodigoDeudor = codigo! });
    }

    //Reporte 7: plan de pagos

    public async Task<IActionResult> PlanPagos()
    {
        var numeroCredito = PrimerToken(Request.Query[Prefijo + "NumeroCredito"].ToString());
        if (string.IsNullOrWhiteSpace(numeroCredito))
        {
            Fallo("Elija el crédito del plan de pagos.");
            return RedirectToAction(nameof(Ver), new { numero = 7 });
        }

        var tPlan = EjecutarAsync(7, new Dictionary<string, string?> { ["NumeroCredito"] = numeroCredito });
        var tCredito = _api.GetAsync("api/credito/obtener", new Dictionary<string, string?> { ["NumeroCredito"] = numeroCredito });
        await Task.WhenAll(tPlan, tCredito);

        var plan = await tPlan;
        var credito = await tCredito;
        if (!plan.Ok)
        {
            Fallo(plan);
            return RedirectToAction(nameof(Ver), new { numero = 7 });
        }

        if (plan.Filas.Count == 0)
        {
            Aviso($"El crédito {numeroCredito} no tiene plan de pagos.");
            return RedirectToAction(nameof(Ver), new { numero = 7 });
        }

        var def = await ObtenerAsync(7);
        return View(new PlanPagosVm
        {
            NumeroCredito = numeroCredito,
            Cuotas = plan.Filas,
            Credito = credito.Ok ? credito.Primera : null,
            Columnas = ColumnasDe(def, plan.Filas)
        });
    }

    //Carga y combinación de reportes

    private async Task<(List<ReporteDef> Reportes, string? Error)> CargarReportesAsync()
    {
        var resultado = await _api.GetAsync("api/reportes");
        if (!resultado.Ok) return (new List<ReporteDef>(), resultado.Error);

        var info = ReportesInfoStore.Obtener(_env);
        var reportes = new List<ReporteDef>();

        if (resultado.Raiz.ValueKind == JsonValueKind.Array)
        {
            foreach (var elemento in resultado.Raiz.EnumerateArray())
            {
                var nombre = Texto(elemento, "nombre");
                var m = nombre is null ? null : NombreReporte.Match(nombre);
                if (m is null || !m.Success) continue;

                var numero = int.Parse(m.Groups[1].Value);
                info.TryGetValue(numero, out var datos);

                var def = new ReporteDef
                {
                    Numero = numero,
                    Nombre = nombre!,
                    Titulo = datos?.Titulo is { Length: > 0 } t ? t : Formato.Encabezado(m.Groups[2].Value),
                    Razon = datos?.Razon ?? "",
                    LimitaFilas = datos?.LimitaFilas ?? false,
                    Columnas = datos?.Columnas.Count > 0 ? datos.Columnas : null
                };

                if (elemento.TryGetProperty("parametros", out var ps) && ps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in ps.EnumerateArray())
                    {
                        var pn = Texto(p, "nombre");
                        if (string.IsNullOrWhiteSpace(pn)) continue;

                        var pi = datos?.Parametros.FirstOrDefault(x => x.Nombre.Equals(pn, StringComparison.OrdinalIgnoreCase));
                        var tipo = pi?.Tipo is { Length: > 0 } ti ? ti : TipoDesdeSql(Texto(p, "tipo"));

                        def.Parametros.Add(new ReporteParam
                        {
                            Nombre = pn,
                            Etiqueta = pi?.Etiqueta is { Length: > 0 } e ? e : Formato.Encabezado(pn),
                            Tipo = tipo,
                            Obligatorio = pi?.Obligatorio ?? false,
                            Control = ControlDe(pn, tipo)
                        });
                    }
                }

                reportes.Add(def);
            }
        }

        return (reportes.OrderBy(r => r.Numero).ToList(), null);
    }

    private async Task<ReporteDef?> ObtenerAsync(int numero)
    {
        var (reportes, _) = await CargarReportesAsync();
        return reportes.FirstOrDefault(r => r.Numero == numero);
    }

    private IActionResult NoExiste(int numero)
    {
        Fallo($"El reporte {numero} no existe en la API.");
        return RedirectToAction(nameof(Index));
    }

    private static string TipoDesdeSql(string? sql) => (sql ?? "").ToLowerInvariant() switch
    {
        "date" or "datetime" or "datetime2" or "smalldatetime" => "date",
        "int" or "smallint" or "tinyint" or "bigint" => "int",
        "bit" => "boolean",
        "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real" => "decimal",
        _ => "text"
    };

    private static string ControlDe(string nombre, string tipo)
    {
        switch (nombre)
        {
            case "CodigoDeudor": return "cliente";
            case "NumeroCredito": return "credito";
            case "CodigoVendedor": return "vendedor";
            case "CodigoMarca": return "marca";
            case "Mes": return "mes";
        }

        return tipo switch
        {
            "date" => "fecha",
            "boolean" => "booleano",
            "int" => "entero",
            "decimal" => "decimal",
            _ => "texto"
        };
    }

    //Filtros

    private Dictionary<string, string> LeerValores(ReporteDef def) =>
        def.Parametros.ToDictionary(p => p.Nombre, p => Request.Query[Prefijo + p.Nombre].ToString().Trim());

    private static List<string> Validar(ReporteDef def, Dictionary<string, string> valores)
    {
        var errores = new List<string>();
        foreach (var p in def.Parametros)
        {
            var texto = valores.GetValueOrDefault(p.Nombre) ?? "";
            if (string.IsNullOrWhiteSpace(texto))
            {
                if (p.Obligatorio) errores.Add($"{p.Etiqueta} es obligatorio.");
                continue;
            }

            switch (p.Control)
            {
                case "cliente":
                    if (!int.TryParse(PrimerToken(texto), out _)) errores.Add("Elija un cliente de la lista.");
                    break;
                case "credito":
                    break;
                case "entero" or "vendedor" or "marca" or "mes":
                    if (!int.TryParse(texto, out _)) errores.Add($"{p.Etiqueta} debe ser un número entero.");
                    break;
                case "decimal":
                    if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                        errores.Add($"{p.Etiqueta} debe ser un número.");
                    break;
                case "fecha":
                    if (!DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        errores.Add($"{p.Etiqueta} no es una fecha válida.");
                    break;
            }
        }

        // Rango de fechas coherente
        if (valores.TryGetValue("FechaInicio", out var inicio) && valores.TryGetValue("FechaFin", out var fin) &&
            DateTime.TryParse(inicio, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1) &&
            DateTime.TryParse(fin, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2) && d1 > d2)
            errores.Add("La fecha inicial no puede ser posterior a la fecha final.");

        return errores;
    }

    //Valores listos para la API
    private static Dictionary<string, string?> Normalizar(ReporteDef def, Dictionary<string, string> valores)
    {
        var filtros = new Dictionary<string, string?>();
        foreach (var p in def.Parametros)
        {
            var texto = valores.GetValueOrDefault(p.Nombre) ?? "";
            if (string.IsNullOrWhiteSpace(texto)) continue;

            filtros[p.Nombre] = p.Control is "cliente" or "credito" ? PrimerToken(texto) : texto.Trim();
        }
        return filtros;
    }

    private Task<ApiResult> EjecutarAsync(int numero, Dictionary<string, string?> filtros) =>
        _api.GetAsync($"api/reportes/{numero}", filtros);

    //Listas para los controles inteligentes
    private async Task<Dictionary<string, List<Opcion>>> OpcionesAsync(ReporteDef def)
    {
        var opciones = new Dictionary<string, List<Opcion>>();

        foreach (var p in def.Parametros)
        {
            List<Opcion>? lista = null;

            switch (p.Control)
            {
                case "cliente":
                {
                    var r = await _api.GetAsync("api/cliente");
                    if (r.Ok)
                        lista = r.Filas.Where(f => f.Tiene("CodigoDeudor"))
                            .Select(f => new Opcion(f["CodigoDeudor"]!, $"{f["CodigoDeudor"]} · {f["NombreCompleto"]} · DPI {f["DPI"]}"))
                            .ToList();
                    break;
                }
                case "credito":
                {
                    var r = await _api.GetAsync("api/credito");
                    if (r.Ok)
                        lista = r.Filas.Where(f => f.Tiene("NumeroCredito"))
                            .Select(f => new Opcion(f["NumeroCredito"]!, $"{f["NumeroCredito"]} · {f["Cliente"]}"))
                            .ToList();
                    break;
                }
                case "vendedor":
                {
                    var r = await _api.GetAsync("api/vendedor");
                    if (r.Ok)
                        lista = r.Filas.Where(f => f.Tiene("CodigoVendedor"))
                            .Select(f => new Opcion(f["CodigoVendedor"]!, f["NombreCompleto"] ?? f["CodigoVendedor"]!))
                            .ToList();
                    break;
                }
                case "marca":
                {
                    var (marcas, _) = await _catalogos.ListarAsync("marca", new[] { "Codigo" });
                    lista = marcas;
                    break;
                }
            }

            if (lista is not null) opciones[p.Nombre] = lista;
        }

        return opciones;
    }

    //Columnas y textos
    private static List<ReporteColInfo> ColumnasDe(ReporteDef? def, List<Fila> filas)
    {
        var enFila = filas.FirstOrDefault()?.Columnas ?? new List<string>();
        var resultado = new List<ReporteColInfo>();

        if (def?.Columnas is not null)
        {
            foreach (var c in def.Columnas)
            {
                if (c.Formato == "oculta") continue;
                if (enFila.Count == 0 || enFila.Contains(c.Nombre, StringComparer.OrdinalIgnoreCase))
                    resultado.Add(c);
            }
        }

        foreach (var nombre in enFila)
        {
            if (resultado.Any(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase))) continue;
            if (def?.Columnas?.Any(c => c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)) == true) continue; // oculta

            var formato = ReportesFormato.FormatoPorNombre(nombre);
            if (formato != "oculta") resultado.Add(new ReporteColInfo { Nombre = nombre, Formato = formato });
        }

        return resultado;
    }

    private static string ResumenDe(ReporteDef def, Dictionary<string, string> valores, Dictionary<string, List<Opcion>> opciones)
    {
        var partes = new List<string>();
        foreach (var p in def.Parametros)
        {
            var texto = valores.GetValueOrDefault(p.Nombre) ?? "";
            if (string.IsNullOrWhiteSpace(texto)) continue;

            var mostrado = p.Control switch
            {
                "fecha" when DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) => Formato.Fecha(f),
                "booleano" => texto == "1" ? "Sí" : "No",
                "mes" when int.TryParse(texto, out var m) && m is >= 1 and <= 12 => ReportesFormato.Meses[m - 1],
                "vendedor" or "marca" => opciones.GetValueOrDefault(p.Nombre)?.FirstOrDefault(o => o.Valor == texto)?.Texto ?? texto,
                _ => texto
            };
            partes.Add($"{p.Etiqueta}: {mostrado}");
        }
        return partes.Count == 0 ? "Sin filtros (todos los registros)" : string.Join(" · ", partes);
    }

    private static string? PrimerToken(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var partes = texto.Trim().Split(new[] { ' ', '·' }, StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? null : partes[0];
    }

    private static string? Texto(JsonElement elemento, string propiedad)
    {
        foreach (var p in elemento.EnumerateObject())
            if (p.Name.Equals(propiedad, StringComparison.OrdinalIgnoreCase))
                return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
        return null;
    }

    private static string Celda(string? valor)
    {
        valor ??= "";
        return valor.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + valor.Replace("\"", "\"\"") + "\""
            : valor;
    }
}
