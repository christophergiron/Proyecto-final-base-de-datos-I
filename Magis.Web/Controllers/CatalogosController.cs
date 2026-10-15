using System.Text.Json;
using Magis.Web.Helpers;
using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Magis.Web.Controllers;

// Los catálogos se pueden administrar, el resto de entidades aquí son solo de consulta.
[AutoValidateAntiforgeryToken]
public class CatalogosController : MagisController
{
    private const int PorPagina = 20;
    private const string PrefijoCampo = "campo_";
    private const string PrefijoLlave = "k_";
    private const string PrefijoFiltro = "f_";

    private static readonly string[] Catalogos =
    {
        "departamento", "municipio", "marca", "linea", "tipodireccion",
        "tipogarantia", "tipopago", "tiporeferencia", "tipotelefono", "parametro"
    };

    private static readonly Dictionary<string, string> Titulos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["departamento"] = "Departamentos",
        ["municipio"] = "Municipios",
        ["marca"] = "Marcas de vehículo",
        ["linea"] = "Líneas de vehículo",
        ["tipodireccion"] = "Tipos de dirección",
        ["tipogarantia"] = "Tipos de garantía",
        ["tipopago"] = "Tipos de pago",
        ["tiporeferencia"] = "Tipos de referencia",
        ["tipotelefono"] = "Tipos de teléfono",
        ["parametro"] = "Parámetros del sistema",
        ["persona"] = "Personas",
        ["telefono"] = "Teléfonos",
        ["direccion"] = "Direcciones",
        ["cliente"] = "Clientes",
        ["vendedor"] = "Vendedores",
        ["referencia"] = "Referencias",
        ["garantia"] = "Garantías",
        ["credito"] = "Créditos",
        ["cuota"] = "Cuotas",
        ["fiador"] = "Fiadores",
        ["detallereferencia"] = "Detalle de referencias",
        ["pago"] = "Pagos"
    };

    private readonly ApiClient _api;
    private readonly CatalogosService _catalogos;

    public CatalogosController(ApiClient api, CatalogosService catalogos)
    {
        _api = api;
        _catalogos = catalogos;
    }

    // Lista

    public async Task<IActionResult> Index(string? entidad, string? q, int pagina = 1)
    {
        var (conocidas, errorRaiz) = await EntidadesAsync();
        var vm = new CatalogoVm
        {
            Entidades = conocidas
                .Select(e => new EntidadInfo(e.Key, TituloDe(e.Key), EsCatalogo(e.Key), e.Value))
                .OrderBy(e => e.EsCatalogo ? 0 : 1).ThenBy(e => e.Titulo)
                .ToList()
        };

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
        if (ayuda is null)
        {
            Fallo("No se pudo leer la descripción de la entidad desde la API.");
            return RedirectToAction(nameof(Index));
        }

        var esCatalogo = EsCatalogo(entidad);
        vm.Entidad = entidad;
        vm.Titulo = TituloDe(entidad);
        vm.EsCatalogo = esCatalogo;
        vm.PuedeCrear = esCatalogo && ayuda.Tiene("insertar");
        vm.PuedeEditar = esCatalogo && ayuda.Tiene("actualizar");
        vm.PuedeEliminar = esCatalogo && ayuda.Tiene("eliminar");
        vm.TieneBuscar = ayuda.Tiene("buscar");
        vm.Llaves = ayuda.Parametros("eliminar").Select(p => p.Nombre).ToList();
        vm.Criterio = q?.Trim();

        // Campos de filtro y de creación
        var filtros = ayuda.Parametros("listar");
        var crear = vm.PuedeCrear ? ayuda.Parametros("insertar") : new List<ParametroDef>();
        var opciones = await OpcionesAsync(filtros.Concat(crear), entidad, conocidas.Keys);

        vm.Filtros = filtros.Select(p => new CampoVm
        {
            Def = p,
            Prefijo = PrefijoFiltro,
            Valor = Request.Query[PrefijoFiltro + p.Nombre].ToString(),
            Opciones = opciones.GetValueOrDefault(p.Nombre)
        }).ToList();

        vm.CamposCrear = crear.Select(p => new CampoVm
        {
            Def = p,
            Opciones = opciones.GetValueOrDefault(p.Nombre),
          
            Valor = vm.Filtros.FirstOrDefault(f => f.Def.Nombre == p.Nombre)?.Valor
        }).ToList();

        // Consulta
        ApiResult resultado;
        if (!string.IsNullOrEmpty(vm.Criterio) && vm.TieneBuscar)
        {
            resultado = await _api.GetAsync($"api/{entidad}/buscar",
                new Dictionary<string, string?> { ["Criterio"] = vm.Criterio });
        }
        else
        {
            var filtrosApi = vm.Filtros
                .Where(f => !string.IsNullOrWhiteSpace(f.Valor))
                .ToDictionary(f => f.Def.Nombre, f => (string?)f.Valor);
            resultado = await _api.GetAsync($"api/{entidad}", filtrosApi);
        }

        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View(vm);
        }

        Paginar(vm.Lista, resultado.Filas, pagina);
        return View(vm);
    }

    // Crear

    [HttpPost]
    public async Task<IActionResult> Crear(string? entidad)
    {
        entidad = entidad?.Trim().ToLowerInvariant();
        var ayuda = string.IsNullOrWhiteSpace(entidad) || !EsCatalogo(entidad)
            ? null
            : await AyudaEntidad.CargarAsync(_api, entidad);

        if (ayuda is null || !ayuda.Tiene("insertar"))
        {
            Fallo("Esta entidad no se puede administrar desde aquí.");
            return RedirectToAction(nameof(Index));
        }

        var (datos, errores) = LeerCampos(ayuda.Parametros("insertar"));
        if (errores.Count > 0)
        {
            Fallo(string.Join(" ", errores));
            return RedirectToAction(nameof(Index), RutaLista(entidad!));
        }

        var resultado = await _api.PostAsync($"api/{entidad}", datos);
        if (resultado.Ok) Exito($"{TituloDe(entidad!)}: registro agregado correctamente.");
        else Fallo(resultado);

        return RedirectToAction(nameof(Index), RutaLista(entidad!));
    }

    // Editar

    [HttpGet]
    public async Task<IActionResult> Editar(string? entidad)
    {
        entidad = entidad?.Trim().ToLowerInvariant();
        var ayuda = string.IsNullOrWhiteSpace(entidad) || !EsCatalogo(entidad)
            ? null
            : await AyudaEntidad.CargarAsync(_api, entidad);

        if (ayuda is null || !ayuda.Tiene("actualizar"))
        {
            Fallo("Esta entidad no se puede editar desde aquí.");
            return RedirectToAction(nameof(Index));
        }

        var llaves = ayuda.Parametros("eliminar").Select(p => p.Nombre).ToList();
        var valoresLlave = llaves.ToDictionary(k => k, k => Request.Query[PrefijoLlave + k].ToString());

        // Se busca el registro en la lista usando los filtros que acepta
        var filtrosListar = ayuda.Parametros("listar").Select(p => p.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filtros = valoresLlave
            .Where(v => filtrosListar.Contains(v.Key) && !string.IsNullOrWhiteSpace(v.Value))
            .ToDictionary(v => v.Key, v => (string?)v.Value);

        var lista = await _api.GetAsync($"api/{entidad}", filtros);
        if (!lista.Ok)
        {
            Fallo(lista);
            return RedirectToAction(nameof(Index), RutaLista(entidad!));
        }

        var fila = lista.Filas.FirstOrDefault(f =>
            valoresLlave.All(v => !f.Tiene(v.Key) || string.Equals(f[v.Key], v.Value, StringComparison.OrdinalIgnoreCase)));
        if (fila is null)
        {
            Fallo("No se encontró el registro que quería editar.");
            return RedirectToAction(nameof(Index), RutaLista(entidad!));
        }

        var (conocidas, _) = await EntidadesAsync();
        var parametros = ayuda.Parametros("actualizar");
        var opciones = await OpcionesAsync(parametros, entidad!, conocidas.Keys);

        return View(new EditarCatalogoVm
        {
            Entidad = entidad!,
            Titulo = TituloDe(entidad!),
            Llaves = llaves,
            Campos = parametros.Select(p => new CampoVm
            {
                Def = p,
                Valor = ValorDeFila(fila, p, entidad!),
                Opciones = opciones.GetValueOrDefault(p.Nombre),
                SoloLectura = llaves.Contains(p.Nombre, StringComparer.OrdinalIgnoreCase)
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Editar(string? entidad, bool guardar = true)
    {
        entidad = entidad?.Trim().ToLowerInvariant();
        var ayuda = string.IsNullOrWhiteSpace(entidad) || !EsCatalogo(entidad)
            ? null
            : await AyudaEntidad.CargarAsync(_api, entidad);

        if (ayuda is null || !ayuda.Tiene("actualizar"))
        {
            Fallo("Esta entidad no se puede editar desde aquí.");
            return RedirectToAction(nameof(Index));
        }

        var (datos, errores) = LeerCampos(ayuda.Parametros("actualizar"));
        var llaves = ayuda.Parametros("eliminar").Select(p => p.Nombre).ToList();
        var ruta = new RouteValueDictionary { ["entidad"] = entidad };
        foreach (var llave in llaves)
            ruta[PrefijoLlave + llave] = Request.Form[PrefijoCampo + llave].ToString();

        if (errores.Count > 0)
        {
            Fallo(string.Join(" ", errores));
            return RedirectToAction(nameof(Editar), ruta);
        }

        var resultado = await _api.PutAsync($"api/{entidad}", datos);
        if (!resultado.Ok)
        {
            Fallo(resultado);
            return RedirectToAction(nameof(Editar), ruta);
        }

        Exito($"{TituloDe(entidad!)}: registro actualizado correctamente.");
        return RedirectToAction(nameof(Index), RutaLista(entidad!));
    }

    //Eliminar

    [HttpPost]
    public async Task<IActionResult> Eliminar(string? entidad)
    {
        entidad = entidad?.Trim().ToLowerInvariant();
        var ayuda = string.IsNullOrWhiteSpace(entidad) || !EsCatalogo(entidad)
            ? null
            : await AyudaEntidad.CargarAsync(_api, entidad);

        if (ayuda is null || !ayuda.Tiene("eliminar"))
        {
            Fallo("Esta entidad no se puede eliminar desde aquí.");
            return RedirectToAction(nameof(Index));
        }

        var llaves = ayuda.Parametros("eliminar")
            .ToDictionary(p => p.Nombre, p => (string?)Request.Form[PrefijoLlave + p.Nombre].ToString());

        var resultado = await _api.DeleteAsync($"api/{entidad}", llaves);
        if (resultado.Ok) Exito($"{TituloDe(entidad!)}: registro eliminado.");
        else Fallo(resultado);   // 409: el registro está en uso y no se puede borrar

        return RedirectToAction(nameof(Index), RutaLista(entidad!));
    }

    // Utilidades

    private static bool EsCatalogo(string? entidad) =>
        entidad is not null && Catalogos.Contains(entidad, StringComparer.OrdinalIgnoreCase);

    private static string TituloDe(string entidad) =>
        Titulos.TryGetValue(entidad, out var titulo) ? titulo : Formato.Encabezado(entidad);

    private static RouteValueDictionary RutaLista(string entidad) => new() { ["entidad"] = entidad };

    //Entidades de la API con su cantidad de operaciones
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

        // Si GET api no respondió, al menos se ofrecen los catálogos conocidos
        foreach (var catalogo in Catalogos) entidades[catalogo] = 0;
        return (entidades, resultado.Ok ? null : resultado.Error);
    }

    // carga las opciones de esa entidad para mostrar una lista desplegable en lugar de un número
    private async Task<Dictionary<string, List<Opcion>>> OpcionesAsync(
        IEnumerable<ParametroDef> parametros, string entidadActual, IEnumerable<string> conocidas)
    {
        var existentes = new HashSet<string>(conocidas, StringComparer.OrdinalIgnoreCase);
        var destinos = new Dictionary<string, string>();

        foreach (var p in parametros)
        {
            if (!p.EsEntero || destinos.ContainsKey(p.Nombre)) continue;
            if (!p.Nombre.StartsWith("Codigo", StringComparison.OrdinalIgnoreCase) || p.Nombre.Length <= 6) continue;

            var destino = p.Nombre[6..].ToLowerInvariant();
            if (destino != entidadActual && existentes.Contains(destino))
                destinos[p.Nombre] = destino;
        }

        var resultado = new Dictionary<string, List<Opcion>>();
        foreach (var (nombre, destino) in destinos)
        {
            var (lista, _) = await _catalogos.ListarAsync(destino, new[] { nombre, "Codigo" });
            resultado[nombre] = lista;
        }
        return resultado;
    }

    //Lee del formulario los campos que declara el procedimiento y los convierte a su tipo
    private (Dictionary<string, object?> Datos, List<string> Errores) LeerCampos(IEnumerable<ParametroDef> parametros)
    {
        var datos = new Dictionary<string, object?>();
        var errores = new List<string>();

        foreach (var p in parametros)
        {
            var texto = Request.Form[PrefijoCampo + p.Nombre].ToString();
            if (!p.TryConvertir(texto, out var valor, out var error))
            {
                errores.Add(error!);
                continue;
            }
            if (valor is not null) datos[p.Nombre] = valor;
        }
        return (datos, errores);
    }


    // Valor actual de un campo en la fila de la lista. Si la columna no se llama igual que el parámetro
    private static string? ValorDeFila(Fila fila, ParametroDef parametro, string entidad)
    {
        if (fila.Tiene(parametro.Nombre))
            return fila[parametro.Nombre];

        if (string.Equals(parametro.Nombre, "Descripcion", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var candidata in new[] { entidad, "Nombre" })
            {
                var columna = fila.Columnas.FirstOrDefault(c => string.Equals(c, candidata, StringComparison.OrdinalIgnoreCase));
                if (columna is not null) return fila[columna];
            }
        }
        return null;
    }
}
