using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NalandaExplorer.Models;

namespace NalandaExplorer.Services;

public sealed class NalandaApiClient(
    HttpClient httpClient,
    NalandaSession session,
    ILogger<NalandaApiClient> logger)
{
    private const int MaximumParameterLength = 500;
    private const int MaximumRequestBodyBytes = 32_768;
    private const int MaximumResponseBodyBytes = 2_097_152;
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };

    public async Task<NalandaApiResponse> GetAsync(
        NalandaApiOperation operation,
        IReadOnlyDictionary<string, string?> values,
        string? jsonBody,
        CancellationToken cancellationToken = default)
    {
        if (!session.TryGetAccessToken(out var accessToken))
        {
            throw new InvalidOperationException("La sesión REST ha caducado. Vuelve a conectar tu cuenta.");
        }

        ValidateRequiredGroups(operation, values);
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["service"] = "NalandaExplorer",
            ["correlation_id"] = correlationId
        });
        using var request = BuildRequest(operation, values, jsonBody, accessToken, correlationId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await NalandaHttpResponseReader.ReadBoundedBodyAsync(
                response.Content,
                MaximumResponseBodyBytes,
                "2 MB",
                cancellationToken);
            stopwatch.Stop();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                session.Clear();
            }

            LogResponse(operation, response, stopwatch.ElapsedMilliseconds);
            return CreateResponse(response, body, stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Nalanda REST query failed; action: verify Nalanda availability and retry");
            throw;
        }
    }

    private static HttpRequestMessage BuildRequest(
        NalandaApiOperation operation,
        IReadOnlyDictionary<string, string?> values,
        string? jsonBody,
        string accessToken,
        string correlationId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, BuildRelativeUri(operation, values));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd(operation.AcceptHeader);
        request.Headers.TryAddWithoutValidation("Correlation-Id", correlationId);

        if (!string.IsNullOrWhiteSpace(jsonBody))
        {
            ValidateJsonBody(operation, jsonBody);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string BuildRelativeUri(
        NalandaApiOperation operation,
        IReadOnlyDictionary<string, string?> values)
    {
        var path = operation.PathTemplate;
        foreach (var parameter in operation.Parameters.Where(
                     item => item.Location == NalandaApiParameterLocation.Path))
        {
            var value = GetValue(parameter, values);
            if (value is not null)
            {
                path = path.Replace(
                    $"{{{parameter.Name}}}", Uri.EscapeDataString(value), StringComparison.Ordinal);
            }
        }

        if (path.Contains('{') || path.Contains('}'))
        {
            throw new InvalidOperationException("La ruta configurada para la operación está incompleta.");
        }

        var query = operation.Parameters
            .Where(item => item.Location == NalandaApiParameterLocation.Query)
            .Select(item => (Parameter: item, Value: GetValue(item, values)))
            .Where(item => item.Value is not null)
            .Select(item =>
                $"{Uri.EscapeDataString(item.Parameter.Name)}={Uri.EscapeDataString(item.Value!)}");

        var queryString = string.Join("&", query);
        var relativePath = path.TrimStart('/');
        return queryString.Length == 0
            ? relativePath
            : $"{relativePath}?{queryString}";
    }

    private static string? GetValue(
        NalandaApiParameter parameter,
        IReadOnlyDictionary<string, string?> values)
    {
        if (!values.TryGetValue(parameter.Name, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            if (parameter.Required)
            {
                throw new ArgumentException($"{parameter.Label} es obligatorio.");
            }

            return null;
        }

        var value = rawValue.Trim();
        if (value.Length > MaximumParameterLength || value.Any(char.IsControl))
        {
            throw new ArgumentException($"{parameter.Label} tiene un formato o longitud no válida.");
        }

        if (parameter.Location == NalandaApiParameterLocation.Path &&
            (value is "." or ".." || value.IndexOfAny(['/', '\\', '?', '#']) >= 0))
        {
            throw new ArgumentException($"{parameter.Label} debe ser un único segmento de ruta.");
        }

        return value;
    }

    private static void ValidateRequiredGroups(
        NalandaApiOperation operation,
        IReadOnlyDictionary<string, string?> values)
    {
        if (operation.AtLeastOneRequired is not { Count: > 0 } group)
        {
            return;
        }

        if (!group.Any(name => values.TryGetValue(name, out var value) &&
                               !string.IsNullOrWhiteSpace(value)))
        {
            throw new ArgumentException(
                $"Debes indicar al menos uno de estos campos: {string.Join(" o ", group)}.");
        }
    }

    private static void ValidateJsonBody(NalandaApiOperation operation, string jsonBody)
    {
        if (!operation.SupportsJsonBody)
        {
            throw new ArgumentException("Esta consulta no admite cuerpo JSON.");
        }

        if (Encoding.UTF8.GetByteCount(jsonBody) > MaximumRequestBodyBytes)
        {
            throw new ArgumentException("El cuerpo JSON supera el límite de 32 KB.");
        }

        using var document = JsonDocument.Parse(jsonBody);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("El cuerpo de búsqueda debe ser un objeto JSON.");
        }
    }

    private static NalandaApiResponse CreateResponse(
        HttpResponseMessage response,
        string body,
        long elapsedMilliseconds)
    {
        var (formattedBody, json, isJson) = FormatBody(body);
        return new NalandaApiResponse(
            (int)response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            elapsedMilliseconds,
            response.Content.Headers.ContentType?.MediaType ?? "desconocido",
            formattedBody,
            isJson,
            json);
    }

    private static (string Body, JsonElement? Json, bool IsJson) FormatBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (string.Empty, null, true);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var json = document.RootElement.Clone();
            return (JsonSerializer.Serialize(json, PrettyJsonOptions), json, true);
        }
        catch (JsonException)
        {
            return (body, null, false);
        }
    }

    private void LogResponse(
        NalandaApiOperation operation,
        HttpResponseMessage response,
        long elapsedMilliseconds)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Nalanda REST query returned HTTP {StatusCode} for operation {OperationId}",
                (int)response.StatusCode,
                operation.Id);
            return;
        }

        logger.LogInformation(
            "Nalanda REST query completed with HTTP {StatusCode} in {ElapsedMilliseconds} ms for operation {OperationId}",
            (int)response.StatusCode,
            elapsedMilliseconds,
            operation.Id);
    }
}
