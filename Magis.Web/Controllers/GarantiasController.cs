using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

// Garantías. La línea depende de la marca el código de línea se repite entre marcas, así que siempre se envían CodigoMarca y CodigoLinea juntos.

[AutoValidateAntiforgeryToken]
public class GarantiasController : MagisController
{
    private static readonly string[] CodigoMarca = { "CodigoMarca", "Codigo" };
    private static readonly string[] CodigoLinea = { "CodigoLinea", "Codigo" };
    private static readonly string[] CodigoTipoGarantia = { "CodigoTipoGarantia", "Codigo" };

    private readonly ApiClient _api;
    private readonly CatalogosService _catalogos;
    private readonly ParametrosApi _parametros;

    public GarantiasController(ApiClient api, CatalogosService catalogos, ParametrosApi parametros)
    {
        _api = api;
        _catalogos = catalogos;
        _parametros = parametros;
    }

    //Lista, búsqueda y disponibles

    public async Task<IActionResult> Index(string? q, bool solo = false, int pagina = 1)
    {
        var criterio = q?.Trim();

        ApiResult resultado;
        if (!string.IsNullOrEmpty(criterio))
            resultado = await _api.GetAsync("api/garantia/buscar", new Dictionary<string, string?> { ["Criterio"] = criterio });
        else if (solo)
            resultado = await _api.GetAsync("api/garantia", new Dictionary<string, string?> { ["SoloDisponibles"] = "1" });
        else
            resultado = await _api.GetAsync("api/garantia");

        var vm = new ListaVm { Criterio = criterio, Solo = solo };
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View(vm);
        }

     
        var filas = solo && !string.IsNullOrEmpty(criterio)
            ? resultado.Filas.Where(f => !f.Si("Asignada")).ToList()
            : resultado.Filas;

        Paginar(vm, filas, pagina);
        return View(vm);
    }

    //Registrar

    [HttpGet]
    public async Task<IActionResult> Crear() =>
        View("Formulario", await ArmarFormularioAsync(new GarantiaForm()));

    [HttpPost]
    public async Task<IActionResult> Crear([Bind(Prefix = "Form")] GarantiaForm form)
    {
        if (!ModelState.IsValid)
            return View("Formulario", await ArmarFormularioAsync(form));

        var datos = await _parametros.FiltrarAsync("garantia", "insertar", form.ADatos());
        var resultado = await _api.PostAsync("api/garantia", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Formulario", await ArmarFormularioAsync(form));
        }

        Exito("Vehículo registrado correctamente.");
        return RedirectToAction(nameof(Index));
    }

    //Editar

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var lista = await _api.GetAsync("api/garantia");
        var fila = lista.Ok ? lista.Filas.FirstOrDefault(f => f.Entero("CodigoGarantia") == id) : null;

        if (fila is null)
        {
            if (!lista.Ok)
                Fallo(lista);
            else
                Fallo("El vehículo solicitado no existe.");
            return RedirectToAction(nameof(Index));
        }

        var form = GarantiaForm.Desde(fila);
        var vm = await ArmarFormularioAsync(form);

        // Los vehículos cargados con datos de prueba no tienen año ni valor de agencia, y el sistema
        // los exige para guardar cualquier cambio: se avisa para que el usuario los complete.
        if (string.IsNullOrWhiteSpace(form.AnioFabricacion) || string.IsNullOrWhiteSpace(form.ValorAgencia))
        {
            var aviso = "Este vehículo no tiene registrado el año de fabricación o el valor de agencia. " +
                        "Complete ambos datos para poder guardar los cambios.";
            var previo = ViewData["Aviso"] as string;
            ViewData["Aviso"] = string.IsNullOrWhiteSpace(previo) ? aviso : previo + " " + aviso;
        }

        return View("Formulario", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Editar([Bind(Prefix = "Form")] GarantiaForm form)
    {
        if (!form.CodigoGarantia.HasValue)
        {
            Fallo("No se indicó qué vehículo se debe actualizar.");
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
            return View("Formulario", await ArmarFormularioAsync(form));

        var datos = await _parametros.FiltrarAsync("garantia", "actualizar", form.ADatos());
        var resultado = await _api.PutAsync("api/garantia", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Formulario", await ArmarFormularioAsync(form));
        }

        Exito("Vehículo actualizado.");
        return RedirectToAction(nameof(Index));
    }

    //Eliminar

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        var resultado = await _api.DeleteAsync("api/garantia",
            new Dictionary<string, string?> { ["CodigoGarantia"] = id.ToString() });

        if (resultado.Ok)
            Exito("Vehículo eliminado.");
        else
            Fallo(resultado); // 409 si el vehículo respalda un crédito

        return RedirectToAction(nameof(Index));
    }

    //Líneas en cascada
    [HttpGet]
    public async Task<IActionResult> Lineas(string? marca)
    {
        if (string.IsNullOrWhiteSpace(marca))
            return Json(Array.Empty<object>());

        var (opciones, error) = await _catalogos.ListarAsync("linea", CodigoLinea,
            new Dictionary<string, string?> { ["CodigoMarca"] = marca });

        if (error is not null)
            return StatusCode(503, new { error });

        return Json(opciones.Select(o => new { valor = o.Valor, texto = o.Texto }));
    }

    // Utilidades

    private async Task<List<Opcion>> LineasAsync(string? marca)
    {
        if (string.IsNullOrWhiteSpace(marca))
            return new List<Opcion>();

        var (opciones, _) = await _catalogos.ListarAsync("linea", CodigoLinea,
            new Dictionary<string, string?> { ["CodigoMarca"] = marca });
        return opciones;
    }

    private async Task<GarantiaFormVm> ArmarFormularioAsync(GarantiaForm form)
    {
        // Las tres consultas son independientes
        var tMarcas = _catalogos.ListarAsync("marca", CodigoMarca);
        var tTipos = _catalogos.ListarAsync("tipogarantia", CodigoTipoGarantia);
        var tLineas = LineasAsync(form.CodigoMarca);
        await Task.WhenAll(tMarcas, tTipos, tLineas);

        var (marcas, errorMarcas) = await tMarcas;
        var (tipos, _) = await tTipos;

        if (errorMarcas is not null)
            ViewData["Aviso"] = "No se pudo cargar la lista de marcas: " + errorMarcas;

        return new GarantiaFormVm
        {
            Form = form,
            Marcas = marcas,
            Tipos = tipos,
            Lineas = await tLineas
        };
    }
}
