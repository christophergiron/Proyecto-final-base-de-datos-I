namespace Magis.Api.Data;

/// <summary>Error controlado de la API (se convierte en una respuesta JSON con el estado HTTP indicado).</summary>
public class ApiException : Exception
{
    public int Status { get; }

    public ApiException(int status, string mensaje) : base(mensaje)
    {
        Status = status;
    }
}
