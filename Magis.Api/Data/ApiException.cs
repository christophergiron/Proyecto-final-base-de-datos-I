namespace Magis.Api.Data;

// Error controlado de la API (se convierte en una respuesta JSON con el estado HTTP indicado).

public class ApiException : Exception
{
    public int Status { get; }

    public ApiException(int status, string mensaje) : base(mensaje)
    {
        Status = status;
    }
}
