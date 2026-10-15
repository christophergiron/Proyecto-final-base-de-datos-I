using Magis.Web.Models;
using Magis.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magis.Web.Controllers;

// Base de todos los controladores de la web. Centraliza cómo se muestran los avisos para que toda la página se comporte igual.
//   - Exito,Fallo y Aviso usan TempData, FalloAhora usa ViewData, para mostrar el error en la misma vista. El mensaje de error SIEMPRE es el campo "error" que devuelve la API.
public abstract class MagisController : Controller
{
    protected void Exito(string mensaje) => TempData["Ok"] = mensaje;

    protected void Aviso(string mensaje) => TempData["Aviso"] = mensaje;

    protected void Fallo(string mensaje) => TempData["Error"] = mensaje;

    protected void Fallo(ApiResult resultado) =>
        TempData["Error"] = resultado.Error ?? "La operación no se pudo completar.";

    protected void FalloAhora(ApiResult resultado) =>
        ViewData["Error"] = resultado.Error ?? "La operación no se pudo completar.";

    // Reparte una lista completa de la API en páginas
    protected static void Paginar(ListaVm vm, IReadOnlyList<Fila> filas, int pagina)
    {
        vm.Total = filas.Count;
        vm.Paginas = Math.Max(1, (int)Math.Ceiling(vm.Total / (double)vm.PorPagina));
        vm.Pagina = Math.Clamp(pagina, 1, vm.Paginas);
        vm.Filas = filas.Skip((vm.Pagina - 1) * vm.PorPagina).Take(vm.PorPagina).ToList();
    }
}
