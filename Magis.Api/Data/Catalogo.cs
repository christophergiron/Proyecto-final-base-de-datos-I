namespace Magis.Api.Data;

// Una entidad de la API y los procedimientos almacenados que se pueden ejecutar sobre ella
public class EntidadDef
{
    public Dictionary<string, string> Sp { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Crea acciones estandar: la accion "listar" de "Cliente" -> sp_Cliente_Listar
    public EntidadDef(string nombre, params string[] acciones)
    {
        foreach (var accion in acciones)
            Sp[accion] = $"sp_{nombre}_{char.ToUpper(accion[0]) + accion[1..]}";
    }

    // Agrega o reemplaza una accion con un procedimiento de otro nombre
    public EntidadDef Con(string accion, string procedimiento)
    {
        Sp[accion] = procedimiento;
        return this;
    }
}

// Lista cerrada de lo que la API puede ejecutar.
public static class Catalogo
{
    private static readonly string[] Crud = { "listar", "buscar", "insertar", "actualizar", "eliminar" };

    public static readonly Dictionary<string, EntidadDef> Entidades = new(StringComparer.OrdinalIgnoreCase)
    {
        // Catalogos
        ["departamento"]     = new EntidadDef("Departamento", Crud),
        ["municipio"]        = new EntidadDef("Municipio", Crud),
        ["marca"]            = new EntidadDef("Marca", Crud),
        ["linea"]            = new EntidadDef("Linea", Crud),
        ["tipodireccion"]    = new EntidadDef("TipoDireccion", Crud),
        ["tipogarantia"]     = new EntidadDef("TipoGarantia", Crud),
        ["tipopago"]         = new EntidadDef("TipoPago", Crud),
        ["tiporeferencia"]   = new EntidadDef("TipoReferencia", Crud),
        ["tipotelefono"]     = new EntidadDef("TipoTelefono", Crud),
        ["parametro"]        = new EntidadDef("Parametro", Crud),

        // Personas y roles
        ["persona"]          = new EntidadDef("Persona", Crud),
        ["telefono"]         = new EntidadDef("Telefono", Crud),
        ["direccion"]        = new EntidadDef("Direccion", Crud),
        ["cliente"]          = new EntidadDef("Cliente", Crud),
        ["vendedor"]         = new EntidadDef("Vendedor", Crud),
        ["referencia"]       = new EntidadDef("Referencia", Crud),
        ["garantia"]         = new EntidadDef("Garantia", Crud),

        // Creditos y pagos
        ["credito"]          = new EntidadDef("Credito", Crud)
                                   .Con("insertar", "sp_Credito_Emitir")
                                   .Con("obtener", "sp_Credito_Obtener")
                                   .Con("simular", "sp_Credito_Simular")
                                   .Con("simular-plan", "sp_Credito_Simular_Plan"),
        ["cuota"]            = new EntidadDef("Cuota", "listar", "buscar", "actualizar"),
        ["fiador"]           = new EntidadDef("Fiador", Crud)
                                   .Con("registrar", "sp_Fiador_Registrar"),
        ["detallereferencia"]= new EntidadDef("DetalleReferencia", Crud),
        ["pago"]             = new EntidadDef("Pago", Crud)
                                   .Con("insertar", "sp_Pago_Registrar"),
    };
}
