using Magis.Web.Helpers;
using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

// Créditos lista, emisión con simulador, y detalle.

[AutoValidateAntiforgeryToken]
public class CreditosController : MagisController
{
    private const int PorPagina = 20;

    // Si la columna de cuota mensual no se llama igual en todas las respuestas, se prueban estos nombres
    private static readonly string[] ColumnasCuotaMensual = { "TotalCuota", "CuotaMensual", "MontoCuota", "ValorCuota", "Total", "Monto" };

    private readonly ApiClient _api;
    private readonly ParametrosApi _parametros;

    public CreditosController(ApiClient api, ParametrosApi parametros)
    {
        _api = api;
        _parametros = parametros;
    }

    // Lista y búsqueda

    public async Task<IActionResult> Index(string? q, int pagina = 1)
    {
        var criterio = q?.Trim();

        var resultado = string.IsNullOrEmpty(criterio)
            ? await _api.GetAsync("api/credito")
            : await _api.GetAsync("api/credito/buscar", new Dictionary<string, string?> { ["Criterio"] = criterio });

        var vm = new ListaVm { Criterio = criterio };
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View(vm);
        }

        Paginar(vm, resultado.Filas, pagina);
        return View(vm);
    }

    //Emitir

    [HttpGet]
    public async Task<IActionResult> Emitir() =>
        View("Emitir", await ArmarFormularioAsync(new CreditoForm()));

    [HttpPost]
    public async Task<IActionResult> Emitir([Bind(Prefix = "Form")] CreditoForm form)
    {
        form.Referencias = form.Referencias.Distinct().ToList();
        form.NuevosFiadores = form.NuevosFiadores.Where(f => !f.Vacio).ToList();

        foreach (var error in form.Validar())
            ModelState.AddModelError(string.Empty, error);

        if (!ModelState.IsValid)
            return View("Emitir", await ArmarFormularioAsync(form));

        var datos = await _parametros.FiltrarAsync("credito", "insertar", form.ADatos());
        var resultado = await _api.PostAsync("api/credito", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Emitir", await ArmarFormularioAsync(form));
        }

        // El crédito ya existe. La API devuelve la fila con el número nuevo.
        var numero = resultado.Primera?["NumeroCredito"];
        var avisos = new List<string>();

        if (!string.IsNullOrWhiteSpace(numero))
        {
            // Fiadores nuevos se registran ligados al crédito recién emitido
            foreach (var fiador in form.NuevosFiadores)
            {
                var registro = await _api.PostAsync("api/fiador/registrar", fiador.ADatos(numero));
                if (!registro.Ok)
                    avisos.Add($"No se pudo registrar al fiador {fiador.Nombre}: {registro.Error}");
            }

            Exito($"Crédito {numero} emitido correctamente.");
            if (avisos.Count > 0)
                Aviso(string.Join(" ", avisos) + " Puede agregarlo de nuevo desde el detalle del crédito.");

            return RedirectToAction(nameof(Detalle), new { numero, nuevo = true });
        }

        Exito("Crédito emitido correctamente.");
        if (form.NuevosFiadores.Count > 0)
            Aviso("La API no devolvió el número del crédito, así que los fiadores nuevos no se registraron. " +
                  "Búsquelo en la lista de créditos y agréguelos desde su detalle.");

        return RedirectToAction(nameof(Index));
    }

    //Simulador (los llama creditos.js)

    [HttpGet]
    public async Task<IActionResult> Simular(
        string? valorAgencia, string? enganche, string? plazo, string? tasaInteres, string? porcentajeDepreciacion)
    {
        if (string.IsNullOrWhiteSpace(valorAgencia) || string.IsNullOrWhiteSpace(enganche))
            return PartialView("_AlertaSimple", "Escriba el valor de agencia y el enganche para simular.");

        var resultado = await _api.GetAsync("api/credito/simular", new Dictionary<string, string?>
        {
            ["ValorAgencia"] = valorAgencia,
            ["Enganche"] = enganche,
            ["Plazo"] = plazo,
            ["TasaInteres"] = tasaInteres,
            ["PorcentajeDepreciacion"] = porcentajeDepreciacion
        });

        if (!resultado.Ok)
            return PartialView("_AlertaSimple", resultado.Error ?? "No se pudo simular.");

        if (resultado.Primera is null)
            return PartialView("_AlertaSimple", "La API no devolvió resultados para esos datos.");

        return PartialView("_Simulacion", resultado.Primera);
    }


    [HttpGet]
    public async Task<IActionResult> SimularPlan(string? valorAgencia, string? enganche, string? plazo, string? tasaInteres)
    {
        if (string.IsNullOrWhiteSpace(valorAgencia) || string.IsNullOrWhiteSpace(enganche))
            return PartialView("_AlertaSimple", "Escriba el valor de agencia y el enganche para ver el plan de pagos.");

        var resultado = await _api.GetAsync("api/credito/simular-plan", new Dictionary<string, string?>
        {
            ["ValorAgencia"] = valorAgencia,
            ["Enganche"] = enganche,
            ["Plazo"] = plazo,
            ["TasaInteres"] = tasaInteres
        });

        if (!resultado.Ok)
            return PartialView("_AlertaSimple", resultado.Error ?? "No se pudo calcular el plan de pagos.");

        return PartialView("_PlanPagos", resultado.Filas);
    }

    // Detalle

    public async Task<IActionResult> Detalle(string? numero, bool nuevo = false)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            Fallo("No se indicó el número de crédito.");
            return RedirectToAction(nameof(Index));
        }

        var filtro = new Dictionary<string, string?> { ["NumeroCredito"] = numero };
        var tCredito = _api.GetAsync("api/credito/obtener", filtro);
        var tCuotas = _api.GetAsync("api/cuota", filtro);
        var tFiadores = _api.GetAsync("api/fiador", filtro);
        await Task.WhenAll(tCredito, tCuotas, tFiadores);

        var credito = await tCredito;
        var cuotas = await tCuotas;
        var fiadores = await tFiadores;

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

        var vm = new DetalleCreditoVm
        {
            Credito = credito.Primera,
            Cuotas = cuotas.Ok ? cuotas.Filas : new List<Fila>(),
            Fiadores = fiadores.Ok ? fiadores.Filas : new List<Fila>(),
            Nuevo = nuevo
        };

        var avisos = new List<string>();
        if (!cuotas.Ok) avisos.Add("No se pudieron cargar las cuotas: " + cuotas.Error);
        if (!fiadores.Ok) avisos.Add("No se pudieron cargar los fiadores: " + fiadores.Error);
        if (avisos.Count > 0)
            ViewData["Aviso"] = string.Join(" ", avisos);

        // Cuota mensual
        var primera = vm.Cuotas.FirstOrDefault();
        if (primera is not null)
        {
            var columna = ColumnasCuotaMensual.FirstOrDefault(primera.Tiene);
            var importe = columna is null ? null : primera.Decimal(columna);
            if (importe.HasValue)
                vm.CuotaMensual = Formato.Quetzales(importe);
        }

        return View(vm);
    }

    //Agregar un fiador a un crédito ya emitido

    [HttpPost]
    public async Task<IActionResult> AgregarFiador(string numero, [Bind(Prefix = "Fiador")] FiadorNuevoForm fiador)
    {
        var faltan = fiador.Faltantes();
        if (faltan.Count > 0)
        {
            Fallo($"Para registrar al fiador falta: {string.Join(", ", faltan)}.");
            return RedirectToAction(nameof(Detalle), new { numero });
        }

        var resultado = await _api.PostAsync("api/fiador/registrar", fiador.ADatos(numero));
        if (resultado.Ok)
            Exito($"Fiador {fiador.Nombre} registrado y ligado al crédito {numero}.");
        else
            Fallo(resultado);

        return RedirectToAction(nameof(Detalle), new { numero });
    }

    // UCarga clientes, vehículos disponibles, vendedores y referencias libres para el formulario
    private async Task<CreditoFormVm> ArmarFormularioAsync(CreditoForm form)
    {
        // Las cuatro consultas son independientes
        var tClientes = _api.GetAsync("api/cliente");
        var tGarantias = _api.GetAsync("api/garantia", new Dictionary<string, string?> { ["SoloDisponibles"] = "1" });
        var tVendedores = _api.GetAsync("api/vendedor");
        var tReferencias = _api.GetAsync("api/referencia");
        await Task.WhenAll(tClientes, tGarantias, tVendedores, tReferencias);

        var clientes = await tClientes;
        var garantias = await tGarantias;
        var vendedores = await tVendedores;
        var referencias = await tReferencias;

        var avisos = new List<string>();
        if (!clientes.Ok) avisos.Add("No se pudieron cargar los clientes: " + clientes.Error);
        if (!garantias.Ok) avisos.Add("No se pudieron cargar los vehículos: " + garantias.Error);
        if (!vendedores.Ok) avisos.Add("No se pudieron cargar los vendedores: " + vendedores.Error);
        if (!referencias.Ok) avisos.Add("No se pudieron cargar las referencias: " + referencias.Error);
        if (avisos.Count > 0)
            ViewData["Aviso"] = string.Join(" ", avisos);

        return new CreditoFormVm
        {
            Form = form,

            Clientes = clientes.Filas
                .Where(f => f.Entero("CodigoDeudor").HasValue)
                .Select(f => new Opcion(
                    f["CodigoDeudor"]!,
                    $"{f["NombreCompleto"]} · DPI {f["DPI"]} (#{f["CodigoDeudor"]})"))
                .ToList(),

            // Por si "SoloDisponibles" no se aplicara, se descartan las asignadas
            Garantias = garantias.Filas
                .Where(f => f.Entero("CodigoGarantia").HasValue && !f.Si("Asignada"))
                .Select(f => new GarantiaOpcion(
                    f["CodigoGarantia"]!,
                    $"{f["Marca"]} {f["Linea"]} {f["AnioFabricacion"]} · VIN {f["VIN"]} · Placa {f["Placa"]} (#{f["CodigoGarantia"]})"
                        .Replace("  ", " "),
                    DatosApi.NumeroParaFormulario(f.Decimal("ValorAgencia"))))
                .ToList(),

            Vendedores = vendedores.Filas
                .Where(f => f.Entero("CodigoVendedor").HasValue)
                .Select(f => new Opcion(
                    f["CodigoVendedor"]!,
                    $"{f["NombreCompleto"]} (#{f["CodigoVendedor"]})" +
                    (string.IsNullOrWhiteSpace(f["Region"]) ? "" : $" · {f["Region"]}")))
                .ToList(),

            // Una referencia ya asignada a otro crédito no se puede volver a usar
            Referencias = referencias.Filas
                .Where(f => f.Entero("CodigoReferencia").HasValue && !f.Si("Asignada"))
                .ToList()
        };
    }
}
