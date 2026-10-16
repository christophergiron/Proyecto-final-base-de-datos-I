namespace Magis.Web.Helpers;

//opción del menú lateral
public record MenuItem(string Texto, string Icono, string Controlador, string Accion = "Index", bool Listo = true);

public record MenuGrupo(string Titulo, MenuItem[] Items);
//Definición del menú de navegación. 
public static class Menu
{
    public static readonly MenuGrupo[] Grupos =
    {
        new("General", new[]
        {
            new MenuItem("Inicio", "bi-speedometer2", "Home")
        }),

        new("Operaciones", new[]
        {
            new MenuItem("Clientes",         "bi-people",          "Clientes"),
            new MenuItem("Referencias",      "bi-person-lines-fill","Referencias"),
            new MenuItem("Garantías",        "bi-car-front",       "Garantias"),
            new MenuItem("Créditos",         "bi-folder2-open",    "Creditos"),
            new MenuItem("Emitir crédito",   "bi-file-earmark-plus","Creditos", "Emitir"),
            new MenuItem("Pagos y cuotas",   "bi-cash-coin",       "Pagos"),
            new MenuItem("Registrar pago",   "bi-cash-stack",      "Pagos", "Registrar")
        }),

        new("Administración", new[]
        {
            new MenuItem("Catálogos y entidades", "bi-collection", "Catalogos")
        }),

        new("Consultas", new[]
        {
            new MenuItem("Consultas",  "bi-search",     "Consultas"),
            new MenuItem("Reportes",   "bi-bar-chart-line", "Reportes")
        })
    };

    public static MenuItem? Buscar(string controlador) =>
        Grupos.SelectMany(g => g.Items)
              .FirstOrDefault(i => string.Equals(i.Controlador, controlador, StringComparison.OrdinalIgnoreCase));
}
