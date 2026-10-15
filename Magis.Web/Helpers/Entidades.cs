namespace Magis.Web.Helpers;

//Nombres para mostrar y agrupación de las entidades de la API 
public static class Entidades
{
    public static readonly string[] OrdenGrupos =
        { "Catálogos", "Personas y roles", "Vehículos", "Créditos y pagos", "Otras" };

    private static readonly Dictionary<string, (string Titulo, string Grupo)> Datos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["departamento"] = ("Departamentos", "Catálogos"),
            ["municipio"] = ("Municipios", "Catálogos"),
            ["marca"] = ("Marcas de vehículo", "Catálogos"),
            ["linea"] = ("Líneas de vehículo", "Catálogos"),
            ["tipodireccion"] = ("Tipos de dirección", "Catálogos"),
            ["tipogarantia"] = ("Tipos de garantía", "Catálogos"),
            ["tipopago"] = ("Tipos de pago", "Catálogos"),
            ["tiporeferencia"] = ("Tipos de referencia", "Catálogos"),
            ["tipotelefono"] = ("Tipos de teléfono", "Catálogos"),
            ["parametro"] = ("Parámetros del sistema", "Catálogos"),
            ["persona"] = ("Personas", "Personas y roles"),
            ["telefono"] = ("Teléfonos", "Personas y roles"),
            ["direccion"] = ("Direcciones", "Personas y roles"),
            ["cliente"] = ("Clientes", "Personas y roles"),
            ["vendedor"] = ("Vendedores", "Personas y roles"),
            ["referencia"] = ("Referencias", "Personas y roles"),
            ["fiador"] = ("Fiadores", "Personas y roles"),
            ["detallereferencia"] = ("Detalle de referencias", "Personas y roles"),
            ["garantia"] = ("Vehículos (garantías)", "Vehículos"),
            ["credito"] = ("Créditos", "Créditos y pagos"),
            ["cuota"] = ("Cuotas", "Créditos y pagos"),
            ["pago"] = ("Pagos", "Créditos y pagos")
        };

    public static string Titulo(string entidad) =>
        Datos.TryGetValue(entidad, out var d) ? d.Titulo : Formato.Encabezado(entidad);

    public static string Grupo(string entidad) =>
        Datos.TryGetValue(entidad, out var d) ? d.Grupo : "Otras";
}
