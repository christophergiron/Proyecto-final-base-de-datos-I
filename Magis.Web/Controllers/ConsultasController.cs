using System.Text;
using System.Text.Json;
using Magis.Web.Helpers;
using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

// Módulo de consultas: todas las entidades y catálogos de la API en tablas con buscador.
// Solo consulta: para modificar datos están las pantallas de cada módulo y la de catálogos.
public class ConsultasController : MagisController
{
    private const int PorPagina = 25;
    private const string PrefijoFiltro = "f_";

    private readonly ApiClient _api;

    public ConsultasController(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index(string? entidad, string? q, int pagina = 1)
    {
        var (conocidas, errorRaiz) = await EntidadesAsync();
        var vm = new ConsultaVm { Grupos = Agrupar(conocidas) };

        if (string.IsNullOrWhiteSpace(entidad))
        {
            if (errorRaiz is not null) ViewData["Error"] = errorRaiz;
            return View(vm);
        }

        entidad = entidad.Trim().ToLowerInvariant();
        if (!conocidas.ContainsKey(entidad))
        {
            Fallo($"La entidad \"{entidad}\" no existe en la API.");
            return RedirectToAction(nameof(Index));
        }

        var ayuda = await AyudaEntidad.CargarAsync(_api, entidad);
        vm.Entidad = entidad;
        vm.Titulo = Entidades.Titulo(entidad);
        vm.Criterio = q?.Trim();
        vm.TieneBuscar = ayuda?.Tiene("buscar") ?? true;
        vm.Filtros = (ayuda?.Parametros("listar") ?? new List<ParametroDef>())
            .Select(p => new FiltroConsulta(p.Nombre, p.Etiqueta, Request.Query[PrefijoFiltro + p.Nombre].ToString()))
            .ToList();

        var resultado = await ConsultarAsync(entidad, vm.Criterio, vm.TieneBuscar, vm.Filtros);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View(vm);
        }

        Paginar(vm.Lista, resultado.Filas, pagina);
        return View(vm);
    }

    //Descarga el resultado de la consulta (todas las filas, no solo la página) como CSV para Excel
    public async Task<IActionResult> Exportar(string? entidad, string? q)
    {
        entidad = entidad?.Trim().ToLowerInvariant();
        var (conocidas, _) = await EntidadesAsync();
        if (string.IsNullOrWhiteSpace(entidad) || !conocidas.ContainsKey(entidad))
        {
            Fallo("Elija una entidad para exportar.");
            return RedirectToAction(nameof(Index));
        }

        var ayuda = await AyudaEntidad.CargarAsync(_api, entidad);
        var tieneBuscar = ayuda?.Tiene("buscar") ?? true;
        var filtros = (ayuda?.Parametros("listar") ?? new List<ParametroDef>())
            .Select(p => new FiltroConsulta(p.Nombre, p.Etiqueta, Request.Query[PrefijoFiltro + p.Nombre].ToString()))
            .ToList();

        var resultado = await ConsultarAsync(entidad, q?.Trim(), tieneBuscar, filtros);
        if (!resultado.Ok)
        {
            Fallo(resultado);
            return RedirectToAction(nameof(Index), new { entidad });
        }

        var csv = new StringBuilder();
        var columnas = resultado.Filas.FirstOrDefault()?.Columnas.ToList() ?? new List<string>();
        csv.AppendLine(string.Join(",", columnas.Select(c => Celda(Formato.Encabezado(c)))));
        foreach (var fila in resultado.Filas)
            csv.AppendLine(string.Join(",", columnas.Select(c => Celda(fila.Mostrar(c)))));

        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"{entidad}_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    //Utilidades

    private async Task<ApiResult> ConsultarAsync(string entidad, string? criterio, bool tieneBuscar, List<FiltroConsulta> filtros)
    {
        if (!string.IsNullOrEmpty(criterio) && tieneBuscar)
            return await _api.GetAsync($"api/{entidad}/buscar", new Dictionary<string, string?> { ["Criterio"] = criterio });

        var filtrosApi = filtros
            .Where(f => !string.IsNullOrWhiteSpace(f.Valor))
            .ToDictionary(f => f.Nombre, f => f.Valor);
        return await _api.GetAsync($"api/{entidad}", filtrosApi);
    }

    private async Task<(Dictionary<string, int> Entidades, string? Error)> EntidadesAsync()
    {
        var entidades = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var resultado = await _api.GetAsync("api");

        if (resultado.Ok && resultado.Propiedad("entidades") is JsonElement raiz && raiz.ValueKind == JsonValueKind.Object)
        {
            foreach (var e in raiz.EnumerateObject())
                entidades[e.Name] = e.Value.ValueKind == JsonValueKind.Array ? e.Value.GetArrayLength() : 0;
            return (entidades, null);
        }

        return (entidades, resultado.Ok ? "La API no devolvió la lista de entidades." : resultado.Error);
    }

    private static List<GrupoEntidades> Agrupar(Dictionary<string, int> entidades) =>
        Helpers.Entidades.OrdenGrupos
            .Select(grupo => new GrupoEntidades(
                grupo,
                entidades
                    .Where(e => Helpers.Entidades.Grupo(e.Key) == grupo)
                    .Select(e => new EntidadInfo(e.Key, Helpers.Entidades.Titulo(e.Key), grupo == "Catálogos", e.Value))
                    .OrderBy(e => e.Titulo)
                    .ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();

    //Escapa un valor para CSV
    private static string Celda(string? valor)
    {
        valor ??= "";
        return valor.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + valor.Replace("\"", "\"\"") + "\""
            : valor;
    }
}
