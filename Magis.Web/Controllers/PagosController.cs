using Magis.Web.Helpers;
using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;


// Pagos y cuotas.
// Los pagos que no son en efectivo exigen referencia bancaria.
[AutoValidateAntiforgeryToken]
public class PagosController : MagisController
{
    private const int PorPagina = 20;

    private readonly ApiClient _api;
    private readonly CatalogosService _catalogos;
    private readonly ParametrosApi _parametros;

    public PagosController(ApiClient api, CatalogosService catalogos, ParametrosApi parametros)
    {
        _api = api;
        _catalogos = catalogos;
        _parametros = parametros;
    }

    //Lista de pagos

    public async Task<IActionResult> Index(string? q, int pagina = 1)
    {
        var criterio = q?.Trim();

        var resultado = string.IsNullOrEmpty(criterio)
            ? await _api.GetAsync("api/pago")
            : await _api.GetAsync("api/pago/buscar", new Dictionary<string, string?> { ["Criterio"] = criterio });

        var vm = new ListaVm { Criterio = criterio };
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View(vm);
        }

        Paginar(vm, resultado.Filas, pagina);
        return View(vm);
    }

    //Pagos y cuotas de un crédito

    public async Task<IActionResult> Cuenta(string? numero)
    {
        numero = LimpiarNumero(numero);
        if (string.IsNullOrWhiteSpace(numero))
        {
            Fallo("No se indicó el número de crédito.");
            return RedirectToAction(nameof(Index));
        }

        var filtro = new Dictionary<string, string?> { ["NumeroCredito"] = numero };
        var tCredito = _api.GetAsync("api/credito/obtener", filtro);
        var tCuotas = _api.GetAsync("api/cuota", filtro);
        var tPagos = _api.GetAsync("api/pago", filtro);
        await Task.WhenAll(tCredito, tCuotas, tPagos);

        var credito = await tCredito;
        var cuotas = await tCuotas;
        var pagos = await tPagos;

        if (!credito.Ok)
        {
            Fallo(credito);
            return RedirectToAction(nameof(Index));
        }
        if (credito.Primera is null)
        {
            Fallo($"El crédito {numero} no existe.");
            return RedirectToAction(nameof(Index));
        }

        var avisos = new List<string>();
        if (!cuotas.Ok) avisos.Add("No se pudieron cargar las cuotas: " + cuotas.Error);
        if (!pagos.Ok) avisos.Add("No se pudieron cargar los pagos: " + pagos.Error);
        if (avisos.Count > 0) ViewData["Aviso"] = string.Join(" ", avisos);

        return View(new CuentaVm
        {
            Credito = credito.Primera,
            Cuotas = cuotas.Ok ? cuotas.Filas : new List<Fila>(),
            Pagos = pagos.Ok
                ? pagos.Filas.OrderByDescending(p => p.Fecha("FechaPago")).ThenByDescending(p => p.Entero("NumeroPago") ?? 0).ToList()
                : new List<Fila>()
        });
    }

    // Registrar un pago

    [HttpGet]
    public async Task<IActionResult> Registrar(string? numero, string? cuota) =>
        View("Registrar", await ArmarRegistroAsync(new PagoForm { NumeroCredito = LimpiarNumero(numero), Cuota = cuota }));

    [HttpPost]
    public async Task<IActionResult> Registrar([Bind(Prefix = "Form")] PagoForm form)
    {
        form.NumeroCredito = LimpiarNumero(form.NumeroCredito);

        var (tipos, _) = await TiposAsync();
        var esEfectivo = EsEfectivo(tipos, form.CodigoTipoPago);

        foreach (var error in form.Validar(esEfectivo))
            ModelState.AddModelError(string.Empty, error);

        if (!ModelState.IsValid)
            return View("Registrar", await ArmarRegistroAsync(form));

        var datos = await _parametros.FiltrarAsync("pago", "insertar", form.ADatos(esEfectivo));
        var resultado = await _api.PostAsync("api/pago", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Registrar", await ArmarRegistroAsync(form));
        }

        decimal? montoPagado = decimal.TryParse(form.Monto, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : null;
        Exito($"Pago de {Formato.Quetzales(montoPagado)} registrado en el crédito {form.NumeroCredito}.");

        // Se busca el pago recién creado para abrir su comprobante
        var cuotaNumero = int.TryParse(form.Cuota, out var c) ? c : 0;
        var comprobante = resultado.Primera?["NumeroComprobante"];
        var pagos = await _api.GetAsync("api/pago", new Dictionary<string, string?> { ["NumeroCredito"] = form.NumeroCredito });

        string? numeroPago = null;
        if (pagos.Ok)
        {
            Fila? nuevo = null;
            if (!string.IsNullOrWhiteSpace(comprobante))
                nuevo = pagos.Filas.FirstOrDefault(p => p["NumeroComprobante"] == comprobante);
            nuevo ??= pagos.Filas
                .Where(p => p.Entero("Cuota") == cuotaNumero)
                .OrderByDescending(p => p.Entero("NumeroPago") ?? 0)
                .FirstOrDefault();
            numeroPago = nuevo?["NumeroPago"];
        }

        if (string.IsNullOrWhiteSpace(numeroPago))
            return RedirectToAction(nameof(Cuenta), new { numero = form.NumeroCredito });

        return RedirectToAction(nameof(Comprobante),
            new { numero = form.NumeroCredito, cuota = form.Cuota, pago = numeroPago, nuevo = true });
    }

    // Comprobante

    public async Task<IActionResult> Comprobante(string? numero, string? cuota, string? pago, bool nuevo = false)
    {
        numero = LimpiarNumero(numero);
        if (string.IsNullOrWhiteSpace(numero))
        {
            Fallo("No se indicó el crédito del comprobante.");
            return RedirectToAction(nameof(Index));
        }

        var filtro = new Dictionary<string, string?> { ["NumeroCredito"] = numero };
        var tPagos = _api.GetAsync("api/pago", filtro);
        var tCredito = _api.GetAsync("api/credito/obtener", filtro);
        var tCuotas = _api.GetAsync("api/cuota", filtro);
        await Task.WhenAll(tPagos, tCredito, tCuotas);

        var pagos = await tPagos;
        var credito = await tCredito;
        var cuotas = await tCuotas;

        if (!pagos.Ok)
        {
            Fallo(pagos);
            return RedirectToAction(nameof(Index));
        }

        var fila = pagos.Filas.FirstOrDefault(p => p["Cuota"] == cuota && p["NumeroPago"] == pago);
        if (fila is null)
        {
            Fallo("No se encontró ese pago.");
            return RedirectToAction(nameof(Cuenta), new { numero });
        }

        return View(new ComprobanteVm
        {
            Pago = fila,
            Credito = credito.Ok ? credito.Primera : null,
            Cuota = cuotas.Ok ? cuotas.Filas.FirstOrDefault(x => x["Cuota"] == cuota) : null,
            Nuevo = nuevo
        });
    }

    //Editar un pago

    [HttpGet]
    public async Task<IActionResult> Editar(string? numero, string? cuota, string? pago)
    {
        numero = LimpiarNumero(numero);
        var fila = await BuscarPagoAsync(numero, cuota, pago);
        if (fila is null)
        {
            Fallo("No se encontró ese pago.");
            return RedirectToAction(nameof(Index));
        }

        var (tipos, errorTipos) = await TiposAsync();
        if (errorTipos is not null) ViewData["Aviso"] = "No se pudieron cargar los tipos de pago: " + errorTipos;

        return View("Editar", new EditarPagoVm
        {
            Pago = fila,
            TiposPago = tipos,
            Form = new PagoEditForm
            {
                NumeroCredito = numero,
                Cuota = cuota,
                NumeroPago = pago,
                CodigoTipoPago = fila["CodigoTipoPago"],
                ReferenciaBancaria = fila["ReferenciaBancaria"],
                FechaPago = fila.Fecha("FechaPago")?.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture)
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Editar([Bind(Prefix = "Form")] PagoEditForm form)
    {
        form.NumeroCredito = LimpiarNumero(form.NumeroCredito);
        var (tipos, _) = await TiposAsync();
        var esEfectivo = EsEfectivo(tipos, form.CodigoTipoPago);

        foreach (var error in form.Validar(esEfectivo))
            ModelState.AddModelError(string.Empty, error);

        if (!ModelState.IsValid)
            return await VistaEditarAsync(form, tipos);

        var datos = await _parametros.FiltrarAsync("pago", "actualizar", form.ADatos(esEfectivo));
        var resultado = await _api.PutAsync("api/pago", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return await VistaEditarAsync(form, tipos);
        }

        Exito("Pago actualizado correctamente.");
        return RedirectToAction(nameof(Comprobante),
            new { numero = form.NumeroCredito, cuota = form.Cuota, pago = form.NumeroPago });
    }

    private async Task<IActionResult> VistaEditarAsync(PagoEditForm form, List<Opcion> tipos)
    {
        var fila = await BuscarPagoAsync(form.NumeroCredito, form.Cuota, form.NumeroPago);
        if (fila is null)
        {
            Fallo("No se encontró ese pago.");
            return RedirectToAction(nameof(Index));
        }
        return View("Editar", new EditarPagoVm { Form = form, Pago = fila, TiposPago = tipos });
    }

    // Eliminar un pago

    [HttpPost]
    public async Task<IActionResult> Eliminar(string? numero, string? cuota, string? pago, string? volver)
    {
        numero = LimpiarNumero(numero);
        var resultado = await _api.DeleteAsync("api/pago", new Dictionary<string, string?>
        {
            ["NumeroCredito"] = numero,
            ["Cuota"] = cuota,
            ["NumeroPago"] = pago
        });

        if (resultado.Ok) Exito("Pago eliminado. La cuota y el saldo del crédito se recalcularon.");
        else Fallo(resultado);

        return volver == "cuenta" && !string.IsNullOrWhiteSpace(numero)
            ? RedirectToAction(nameof(Cuenta), new { numero })
            : RedirectToAction(nameof(Index));
    }

    //Utilidades
    private static string? LimpiarNumero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var corte = texto.Trim().Split(new[] { ' ', '·' }, StringSplitOptions.RemoveEmptyEntries);
        return corte.Length == 0 ? null : corte[0].Trim();
    }

    private async Task<(List<Opcion> Opciones, string? Error)> TiposAsync() =>
        await _catalogos.ListarAsync("tipopago", new[] { "Codigo", "CodigoTipoPago" });

    private static bool EsEfectivo(IEnumerable<Opcion> tipos, string? codigo) =>
        EstadosCuota.EsEfectivo(tipos.FirstOrDefault(t => t.Valor == codigo)?.Texto);

    private async Task<Fila?> BuscarPagoAsync(string? numero, string? cuota, string? pago)
    {
        if (string.IsNullOrWhiteSpace(numero)) return null;
        var pagos = await _api.GetAsync("api/pago", new Dictionary<string, string?> { ["NumeroCredito"] = numero });
        return pagos.Ok ? pagos.Filas.FirstOrDefault(p => p["Cuota"] == cuota && p["NumeroPago"] == pago) : null;
    }

    //Carga lo que necesita la pantalla de registro créditos o el crédito elegido con sus cuotas
    private async Task<PagoFormVm> ArmarRegistroAsync(PagoForm form)
    {
        var vm = new PagoFormVm { Form = form };
        var avisos = new List<string>();

        var (tipos, errorTipos) = await TiposAsync();
        vm.TiposPago = tipos;
        if (errorTipos is not null) avisos.Add("No se pudieron cargar los tipos de pago: " + errorTipos);

        if (!string.IsNullOrWhiteSpace(form.NumeroCredito))
        {
            var filtro = new Dictionary<string, string?> { ["NumeroCredito"] = form.NumeroCredito };
            var tCredito = _api.GetAsync("api/credito/obtener", filtro);
            var tCuotas = _api.GetAsync("api/cuota", filtro);
            await Task.WhenAll(tCredito, tCuotas);

            var credito = await tCredito;
            var cuotas = await tCuotas;

            if (credito.Ok && credito.Primera is not null)
            {
                vm.Credito = credito.Primera;
                vm.Cuotas = cuotas.Ok ? cuotas.Filas : new List<Fila>();
                if (!cuotas.Ok) avisos.Add("No se pudieron cargar las cuotas: " + cuotas.Error);

                // Cuota y monto por defecto
                var pendientes = vm.Cuotas.Where(x => !EstadosCuota.EstaPagada(x["EstadoCuota"])).ToList();
                var elegida = pendientes.FirstOrDefault(x => x["Cuota"] == form.Cuota) ?? pendientes.FirstOrDefault();
                if (elegida is not null && string.IsNullOrWhiteSpace(form.Cuota))
                    form.Cuota = elegida["Cuota"];
                if (elegida is not null && string.IsNullOrWhiteSpace(form.Monto))
                    form.Monto = DatosApi.NumeroParaFormulario(elegida.Decimal("Pendiente"));
                if (pendientes.Count == 0 && vm.Cuotas.Count > 0)
                    avisos.Add("Este crédito no tiene cuotas pendientes: todas están pagadas.");

                if (string.IsNullOrWhiteSpace(form.FechaPago))
                    form.FechaPago = DateTime.Now.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                avisos.Add(credito.Ok ? $"El crédito {form.NumeroCredito} no existe." : (credito.Error ?? "No se pudo cargar el crédito."));
                form.NumeroCredito = null;
            }
        }

        if (vm.Credito is null)
        {
            var creditos = await _api.GetAsync("api/credito");
            if (creditos.Ok)
            {
                vm.Creditos = creditos.Filas
                    .Where(f => f.Tiene("NumeroCredito"))
                    .Where(f => !f.Tiene("SaldoPendiente") || (f.Decimal("SaldoPendiente") ?? 1m) > 0m)
                    .Select(f => new Opcion(f["NumeroCredito"]!, TextoCredito(f)))
                    .ToList();
            }
            else
            {
                avisos.Add("No se pudo cargar la lista de créditos: " + creditos.Error);
            }
        }

        if (avisos.Count > 0)
            ViewData["Aviso"] = string.Join(" ", avisos);

        return vm;
    }

    //Texto del buscador de créditos
    private static string TextoCredito(Fila f)
    {
        var texto = f["NumeroCredito"] ?? "";
        if (!string.IsNullOrWhiteSpace(f["Cliente"])) texto += " · " + f["Cliente"];
        if (f.Tiene("SaldoPendiente")) texto += " · saldo " + Formato.Quetzales(f.Decimal("SaldoPendiente"));
        return texto;
    }
}
