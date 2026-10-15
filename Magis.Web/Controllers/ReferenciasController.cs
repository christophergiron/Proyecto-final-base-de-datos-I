using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

// Referencias personales (se exigen mínimo 3 por crédito)  no acepta teléfonos se piden solo al registrar
[AutoValidateAntiforgeryToken]
public class ReferenciasController : MagisController
{
    private static readonly string[] CodigoTipo = { "CodigoTipoReferencia", "Codigo" };

    private readonly ApiClient _api;
    private readonly CatalogosService _catalogos;
    private readonly ParametrosApi _parametros;

    public ReferenciasController(ApiClient api, CatalogosService catalogos, ParametrosApi parametros)
    {
        _api = api;
        _catalogos = catalogos;
        _parametros = parametros;
    }

    //Lista y búsqueda

    public async Task<IActionResult> Index(string? q, int pagina = 1)
    {
        var criterio = q?.Trim();

        var resultado = string.IsNullOrEmpty(criterio)
            ? await _api.GetAsync("api/referencia")
            : await _api.GetAsync("api/referencia/buscar", new Dictionary<string, string?> { ["Criterio"] = criterio });

        var vm = new ListaVm { Criterio = criterio };
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View(vm);
        }

        Paginar(vm, resultado.Filas, pagina);
        return View(vm);
    }

    //Registrar

    [HttpGet]
    public async Task<IActionResult> Crear() =>
        View("Formulario", await ArmarFormularioAsync(new ReferenciaForm()));

    [HttpPost]
    public async Task<IActionResult> Crear([Bind(Prefix = "Form")] ReferenciaForm form)
    {
        if (form.SinTelefonos)
            ModelState.AddModelError(string.Empty, "Escriba al menos un teléfono (celular, casa o trabajo).");

        if (!ModelState.IsValid)
            return View("Formulario", await ArmarFormularioAsync(form));

        var datos = await _parametros.FiltrarAsync("referencia", "insertar", form.ADatos());
        var resultado = await _api.PostAsync("api/referencia", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Formulario", await ArmarFormularioAsync(form));
        }

        Exito("Referencia registrada correctamente.");
        return RedirectToAction(nameof(Index));
    }

    //Editar

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var lista = await _api.GetAsync("api/referencia");
        var fila = lista.Ok ? lista.Filas.FirstOrDefault(f => f.Entero("CodigoReferencia") == id) : null;

        if (fila is null)
        {
            if (!lista.Ok)
                Fallo(lista);
            else
                Fallo("La referencia solicitada no existe.");
            return RedirectToAction(nameof(Index));
        }

        return View("Formulario", await ArmarFormularioAsync(ReferenciaForm.Desde(fila)));
    }

    [HttpPost]
    public async Task<IActionResult> Editar([Bind(Prefix = "Form")] ReferenciaForm form)
    {
        if (!form.CodigoReferencia.HasValue)
        {
            Fallo("No se indicó qué referencia se debe actualizar.");
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
            return View("Formulario", await ArmarFormularioAsync(form));

        // Solo se envía lo que sp_Referencia_Actualizar declara
        var datos = await _parametros.FiltrarAsync("referencia", "actualizar", form.ADatos());
        var resultado = await _api.PutAsync("api/referencia", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Formulario", await ArmarFormularioAsync(form));
        }

        Exito("Referencia actualizada.");
        return RedirectToAction(nameof(Index));
    }

    //Eliminar

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        var resultado = await _api.DeleteAsync("api/referencia",
            new Dictionary<string, string?> { ["CodigoReferencia"] = id.ToString() });

        if (resultado.Ok)
            Exito("Referencia eliminada.");
        else
            Fallo(resultado); // 409 si la referencia ya está asignada a un crédito

        return RedirectToAction(nameof(Index));
    }

    // Utilidades

    private async Task<ReferenciaFormVm> ArmarFormularioAsync(ReferenciaForm form)
    {
        var (tipos, error) = await _catalogos.ListarAsync("tiporeferencia", CodigoTipo);
        if (error is not null)
            ViewData["Aviso"] = "No se pudo cargar la lista de tipos de referencia: " + error;

        return new ReferenciaFormVm { Form = form, Tipos = tipos };
    }
}
