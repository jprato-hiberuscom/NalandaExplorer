using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NalandaExplorer.Services;

public sealed class NalandaAuthenticationService(
    HttpClient httpClient,
    NalandaSession session,
    ILogger<NalandaAuthenticationService> logger)
{
    public async Task SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["service"] = "NalandaExplorer",
            ["correlation_id"] = correlationId
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "open/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password
            })
        };
        request.Headers.Accept.ParseAdd("application/vnd.nalanda.api-v1.0+json");
        request.Headers.TryAddWithoutValidation("Correlation-Id", correlationId);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Nalanda REST authentication was rejected with HTTP {StatusCode}",
                    (int)response.StatusCode);
                throw new NalandaAuthenticationException(response.StatusCode);
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken);

            if (token is null ||
                string.IsNullOrWhiteSpace(token.AccessToken) ||
                !string.Equals(token.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                token.ExpiresIn <= 0)
            {
                throw new InvalidDataException("Nalanda devolvió una respuesta de token no válida.");
            }

            session.SetAccessToken(token.AccessToken, token.ExpiresIn);
            logger.LogInformation("Nalanda REST authentication succeeded");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Nalanda REST authentication request failed; action: verify Nalanda availability and retry");
            throw;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
