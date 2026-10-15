using Magis.Web.Helpers;
using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

// Clientes : listar, buscar, registrar, editar, eliminar, y su ficha con teléfonos y direcciones.
// Los catálogos  se leen de la API.

[AutoValidateAntiforgeryToken]
public class ClientesController : MagisController
{
    private const int PorPagina = 20;

    private static readonly string[] CodigoTipoTelefono = { "CodigoTipoTelefono", "Codigo" };
    private static readonly string[] CodigoTipoDireccion = { "CodigoTipoDireccion", "Codigo" };
    private static readonly string[] CodigoDepartamento = { "CodigoDepartamento", "Codigo" };
    private static readonly string[] CodigoMunicipio = { "CodigoMunicipio", "Codigo" };

    private readonly ApiClient _api;
    private readonly CatalogosService _catalogos;
    private readonly ParametrosApi _parametros;

    public ClientesController(ApiClient api, CatalogosService catalogos, ParametrosApi parametros)
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
            ? await _api.GetAsync("api/cliente")
            : await _api.GetAsync("api/cliente/buscar", new Dictionary<string, string?> { ["Criterio"] = criterio });

        var vm = new ClientesIndexVm { Criterio = criterio, PorPagina = PorPagina };

        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View(vm);
        }

        vm.Total = resultado.Filas.Count;
        vm.Paginas = Math.Max(1, (int)Math.Ceiling(vm.Total / (double)PorPagina));
        vm.Pagina = Math.Clamp(pagina, 1, vm.Paginas);
        vm.Filas = resultado.Filas.Skip((vm.Pagina - 1) * PorPagina).Take(PorPagina).ToList();
        return View(vm);
    }

    //Registrar

    [HttpGet]
    public async Task<IActionResult> Crear() =>
        View("Formulario", await ArmarFormularioAsync(new ClienteForm()));

    [HttpPost]
    public async Task<IActionResult> Crear([Bind(Prefix = "Form")] ClienteForm form)
    {
        if (!ModelState.IsValid)
            return View("Formulario", await ArmarFormularioAsync(form));

        var resultado = await _api.PostAsync("api/cliente", form.ADatos());
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Formulario", await ArmarFormularioAsync(form));
        }

        Exito("Cliente registrado correctamente. Ahora puede agregar sus teléfonos y direcciones.");

        // El insertar devuelve la fila con el código nuevo
        var codigo = resultado.Primera?.Entero("CodigoDeudor");
        return codigo.HasValue
            ? RedirectToAction(nameof(Ficha), new { id = codigo.Value })
            : RedirectToAction(nameof(Index));
    }

    //Editar

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var (cliente, resultado) = await BuscarClienteAsync(id);
        if (cliente is null)
            return NoEncontrado(resultado);

        // Los teléfonos se editan desde la ficha del cliente
        return View("Formulario", await ArmarFormularioAsync(ClienteForm.Desde(cliente)));
    }

    [HttpPost]
    public async Task<IActionResult> Editar([Bind(Prefix = "Form")] ClienteForm form)
    {
        if (!form.CodigoDeudor.HasValue)
        {
            Fallo("No se indicó qué cliente se debe actualizar.");
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
            return View("Formulario", await ArmarFormularioAsync(form));


        var datos = await _parametros.FiltrarAsync("cliente", "actualizar", form.ADatos());
        var resultado = await _api.PutAsync("api/cliente", datos);
        if (!resultado.Ok)
        {
            FalloAhora(resultado);
            return View("Formulario", await ArmarFormularioAsync(form));
        }

        Exito("Datos del cliente actualizados.");
        return RedirectToAction(nameof(Ficha), new { id = form.CodigoDeudor.Value });
    }

    //Eliminar

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        var resultado = await _api.DeleteAsync("api/cliente",
            new Dictionary<string, string?> { ["CodigoDeudor"] = id.ToString() });

        if (resultado.Ok)
            Exito("Cliente eliminado.");
        else
            Fallo(resultado); // por ejemplo 409: el cliente tiene créditos y no se puede borrar

        return RedirectToAction(nameof(Index));
    }

    //Ficha: datos, teléfonos, direcciones

    public async Task<IActionResult> Ficha(int id)
    {
        var (cliente, resultado) = await BuscarClienteAsync(id);
        if (cliente is null)
            return NoEncontrado(resultado);

        var persona = cliente.Entero("CodigoPersona") ?? 0;
        var filtro = new Dictionary<string, string?> { ["CodigoPersona"] = persona.ToString() };

        // Las cinco consultas son independientes
        var tTelefonos = _api.GetAsync("api/telefono", filtro);
        var tDirecciones = _api.GetAsync("api/direccion", filtro);
        var tTiposTelefono = _catalogos.ListarAsync("tipotelefono", CodigoTipoTelefono);
        var tTiposDireccion = _catalogos.ListarAsync("tipodireccion", CodigoTipoDireccion);
        var tDepartamentos = _catalogos.ListarAsync("departamento", CodigoDepartamento);
        await Task.WhenAll(tTelefonos, tDirecciones, tTiposTelefono, tTiposDireccion, tDepartamentos);

        var telefonos = await tTelefonos;
        var direcciones = await tDirecciones;

        var vm = new FichaClienteVm
        {
            Cliente = cliente,
            CodigoDeudor = id,
            CodigoPersona = persona,
            TiposTelefono = (await tTiposTelefono).Opciones,
            TiposDireccion = (await tTiposDireccion).Opciones,
            Departamentos = (await tDepartamentos).Opciones
        };

        var avisos = new List<string>();

        if (telefonos.Ok)
        {
            vm.Telefonos = telefonos.Filas.Select(f => new TelefonoItem
            {
                Correlativo = f["Correlativo"],
                Numero = f["Numero"],
                CodigoTipo = f["CodigoTipoTelefono"],
                Tipo = CatalogosService.Texto(vm.TiposTelefono, f["CodigoTipoTelefono"])
            }).ToList();
        }
        else
        {
            avisos.Add("No se pudieron cargar los teléfonos: " + telefonos.Error);
        }

        if (direcciones.Ok)
        {
            // Nombre del municipio
            var municipiosPorDepartamento = new Dictionary<string, List<Opcion>>();
            foreach (var codigo in direcciones.Filas
                         .Select(f => f["CodigoDepartamento"])
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct())
            {
                var (opciones, _) = await _catalogos.ListarAsync("municipio", CodigoMunicipio,
                    new Dictionary<string, string?> { ["CodigoDepartamento"] = codigo });
                municipiosPorDepartamento[codigo!] = opciones;
            }

            vm.Direcciones = direcciones.Filas.Select(f =>
            {
                var depto = f["CodigoDepartamento"];
                var municipios = depto is not null && municipiosPorDepartamento.TryGetValue(depto, out var lista)
                    ? lista
                    : new List<Opcion>();

                return new DireccionItem
                {
                    Correlativo = f["Correlativo"],
                    Zona = f["Zona"],
                    Calle = f["Calle"],
                    Avenida = f["Avenida"],
                    CodigoDepartamento = depto,
                    Departamento = CatalogosService.Texto(vm.Departamentos, depto),
                    CodigoMunicipio = f["CodigoMunicipio"],
                    Municipio = CatalogosService.Texto(municipios, f["CodigoMunicipio"]),
                    CodigoTipo = f["CodigoTipoDireccion"],
                    Tipo = CatalogosService.Texto(vm.TiposDireccion, f["CodigoTipoDireccion"])
                };
            }).ToList();
        }
        else
        {
            avisos.Add("No se pudieron cargar las direcciones: " + direcciones.Error);
        }

        if (avisos.Count > 0)
            ViewData["Aviso"] = string.Join(" ", avisos);

        return View(vm);
    }

    //Teléfonos

    [HttpPost]
    public async Task<IActionResult> GuardarTelefono(
        int codigoDeudor, int codigoPersona, string? correlativo, string? numero, string? codigoTipoTelefono)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            Fallo("Escriba el número de teléfono.");
            return RedirectToAction(nameof(Ficha), new { id = codigoDeudor });
        }

        var datos = new Dictionary<string, object?> { ["CodigoPersona"] = codigoPersona };
        DatosApi.Numero(datos, "Correlativo", correlativo);
        DatosApi.Texto(datos, "Numero", numero);
        DatosApi.Numero(datos, "CodigoTipoTelefono", codigoTipoTelefono);

        var edicion = datos.ContainsKey("Correlativo");
        var resultado = edicion
            ? await _api.PutAsync("api/telefono", datos)
            : await _api.PostAsync("api/telefono", datos);

        if (resultado.Ok)
            Exito(edicion ? "Teléfono actualizado." : "Teléfono agregado.");
        else
            Fallo(resultado);

        return RedirectToAction(nameof(Ficha), new { id = codigoDeudor });
    }

    [HttpPost]
    public async Task<IActionResult> EliminarTelefono(int codigoDeudor, int codigoPersona, int correlativo)
    {
        var resultado = await _api.DeleteAsync("api/telefono", new Dictionary<string, string?>
        {
            ["CodigoPersona"] = codigoPersona.ToString(),
            ["Correlativo"] = correlativo.ToString()
        });

        if (resultado.Ok)
            Exito("Teléfono eliminado.");
        else
            Fallo(resultado);

        return RedirectToAction(nameof(Ficha), new { id = codigoDeudor });
    }

    //Direcciones

    [HttpPost]
    public async Task<IActionResult> GuardarDireccion(
        int codigoDeudor, int codigoPersona, string? correlativo, string? zona, string? calle, string? avenida,
        string? codigoDepartamento, string? codigoMunicipio, string? codigoTipoDireccion)
    {
        var datos = new Dictionary<string, object?> { ["CodigoPersona"] = codigoPersona };
        DatosApi.Numero(datos, "Correlativo", correlativo);
        DatosApi.Texto(datos, "Zona", zona);
        DatosApi.Texto(datos, "Calle", calle);
        DatosApi.Texto(datos, "Avenida", avenida);
        DatosApi.Numero(datos, "CodigoDepartamento", codigoDepartamento);
        DatosApi.Numero(datos, "CodigoMunicipio", codigoMunicipio);
        DatosApi.Numero(datos, "CodigoTipoDireccion", codigoTipoDireccion);

        var edicion = datos.ContainsKey("Correlativo");
        var resultado = edicion
            ? await _api.PutAsync("api/direccion", datos)
            : await _api.PostAsync("api/direccion", datos);

        if (resultado.Ok)
            Exito(edicion ? "Dirección actualizada." : "Dirección agregada.");
        else
            Fallo(resultado);

        return RedirectToAction(nameof(Ficha), new { id = codigoDeudor });
    }

    [HttpPost]
    public async Task<IActionResult> EliminarDireccion(int codigoDeudor, int codigoPersona, int correlativo)
    {
        var resultado = await _api.DeleteAsync("api/direccion", new Dictionary<string, string?>
        {
            ["CodigoPersona"] = codigoPersona.ToString(),
            ["Correlativo"] = correlativo.ToString()
        });

        if (resultado.Ok)
            Exito("Dirección eliminada.");
        else
            Fallo(resultado);

        return RedirectToAction(nameof(Ficha), new { id = codigoDeudor });
    }

    //Municipios en cascada
    [HttpGet]
    public async Task<IActionResult> Municipios(string? departamento)
    {
        if (string.IsNullOrWhiteSpace(departamento))
            return Json(Array.Empty<object>());

        var (opciones, error) = await _catalogos.ListarAsync("municipio", CodigoMunicipio,
            new Dictionary<string, string?> { ["CodigoDepartamento"] = departamento });

        if (error is not null)
            return StatusCode(503, new { error });

        return Json(opciones.Select(o => new { valor = o.Valor, texto = o.Texto }));
    }

    //Utilidades

    private async Task<(Fila? Cliente, ApiResult Resultado)> BuscarClienteAsync(int id)
    {
        var resultado = await _api.GetAsync("api/cliente");
        var cliente = resultado.Ok
            ? resultado.Filas.FirstOrDefault(f => f.Entero("CodigoDeudor") == id)
            : null;
        return (cliente, resultado);
    }

    private IActionResult NoEncontrado(ApiResult resultado)
    {
        if (!resultado.Ok)
            Fallo(resultado);
        else
            Fallo("El cliente solicitado no existe.");

        return RedirectToAction(nameof(Index));
    }

    private async Task<ClienteFormVm> ArmarFormularioAsync(ClienteForm form)
    {
        var (tipos, error) = await _catalogos.ListarAsync("tipotelefono", CodigoTipoTelefono);
        if (error is not null)
            ViewData["Aviso"] = "No se pudo cargar la lista de tipos de teléfono: " + error;

        return new ClienteFormVm { Form = form, TiposTelefono = tipos };
    }
}
