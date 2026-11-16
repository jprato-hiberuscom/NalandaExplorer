using System.Net;

namespace NalandaExplorer.Services;

public sealed class NalandaAuthenticationException(HttpStatusCode statusCode)
    : Exception($"Nalanda rechazó la autenticación con HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
