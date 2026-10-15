using Magis.Web.Services;

namespace Magis.Web.Models;

public record GrupoEntidades(string Titulo, List<EntidadInfo> Items);

public record FiltroConsulta(string Nombre, string Etiqueta, string? Valor);

public class ConsultaVm
{
    public List<GrupoEntidades> Grupos { get; set; } = new();

    public string? Entidad { get; set; }
    public string Titulo { get; set; } = "";
    public string? Criterio { get; set; }
    public bool TieneBuscar { get; set; }
    public List<FiltroConsulta> Filtros { get; set; } = new();
    public ListaVm Lista { get; set; } = new();
}
