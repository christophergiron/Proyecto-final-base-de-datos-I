using System.Text.Json;
using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

public class HomeController : MagisController
{
    private readonly ApiClient _api;

    public HomeController(ApiClient api)
    {
        _api = api;
    }


    // Página de inicio, comprueba la conexión con la API y muestra el estado

    public async Task<IActionResult> Index()
    {
        var resultado = await _api.GetAsync("api");

        var vm = new InicioVm
        {
            ApiUrl = _api.BaseUrl,
            Conectada = resultado.Ok,
            Error = resultado.Error
        };

        // GET /api devuelve { sistema, ayuda, entidades: { cliente: ["listar","buscar",...], ... } }
        if (resultado.Ok && resultado.Propiedad("entidades") is JsonElement entidades &&
            entidades.ValueKind == JsonValueKind.Object)
        {
            foreach (var entidad in entidades.EnumerateObject())
            {
                var operaciones = entidad.Value.ValueKind == JsonValueKind.Array
                    ? entidad.Value.GetArrayLength()
                    : 0;
                vm.Entidades.Add((entidad.Name, operaciones));
            }
        }

        return View(vm);
    }

    //Página que se muestra cuando ocurre una excepción no controlada.
    public IActionResult Error() =>
        View("Error", new ErrorVm
        {
            Codigo = 500,
            Titulo = "Algo salió mal",
            Mensaje = "Ocurrió un error inesperado en la página. Intente de nuevo; si persiste, avise al equipo."
        });

    //Páginas para los códigos HTTP (404, etc.)
    public IActionResult Estado(int id)
    {
        var vm = id switch
        {
            404 => new ErrorVm
            {
                Codigo = 404,
                Titulo = "Página no encontrada",
                Mensaje = "La dirección que busca no existe o la pantalla todavía no está disponible."
            },
            _ => new ErrorVm
            {
                Codigo = id,
                Titulo = "No se pudo completar la solicitud",
                Mensaje = $"El servidor respondió con el código {id}."
            }
        };

        Response.StatusCode = id;
        return View("Error", vm);
    }
}
