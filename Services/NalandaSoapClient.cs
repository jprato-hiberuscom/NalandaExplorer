using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using NalandaExplorer.Models;

namespace NalandaExplorer.Services;

public sealed class NalandaSoapClient(
    HttpClient httpClient,
    ILogger<NalandaSoapClient> logger)
{
    private const int MaximumCredentialLength = 1024;
    private const int MaximumParameterLength = 500;
    private const int MaximumResponseBodyBytes = 20 * 1_048_576;
    private const string MaximumResponseDisplay = "20 MB";
    private static readonly XNamespace SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace SchemaNamespace = "http://www.obralia.com/integration/schemas";
    private static readonly XNamespace CommonNamespace = "http://www.obralia.com/integration/schemas/common";

    public async Task<NalandaSoapResponse> ExportProjectAsync(
        string username,
        string password,
        string projectCode,
        string ownerTaxId,
        CancellationToken cancellationToken = default)
    {
        username = ValidateValue(username, "Usuario", MaximumCredentialLength);
        password = ValidateValue(password, "Contraseña", MaximumCredentialLength, true);
        projectCode = ValidateValue(projectCode, "Código de obra", MaximumParameterLength);
        ownerTaxId = ValidateValue(ownerTaxId, "CIF de la empresa propietaria", MaximumParameterLength);

        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["service"] = "NalandaExplorer",
            ["correlation_id"] = correlationId
        });
        using var request = BuildRequest(username, password, projectCode, ownerTaxId, correlationId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await NalandaHttpResponseReader.ReadBoundedBodyAsync(
                response.Content,
                MaximumResponseBodyBytes,
                MaximumResponseDisplay,
                cancellationToken);
            stopwatch.Stop();
            LogResponse(response, stopwatch.ElapsedMilliseconds);
            var parsed = NalandaSoapResponseParser.Parse(body, MaximumResponseBodyBytes);

            return new NalandaSoapResponse(
                (int)response.StatusCode,
                response.ReasonPhrase ?? string.Empty,
                stopwatch.ElapsedMilliseconds,
                response.Content.Headers.ContentType?.MediaType ?? "desconocido",
                body,
                parsed.IsXml,
                parsed.FaultMessage,
                parsed.Project);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "Nalanda SOAP export failed; action: {Action}",
                "Verify Nalanda availability and retry");
            throw;
        }
    }

    private static HttpRequestMessage BuildRequest(
        string username,
        string password,
        string projectCode,
        string ownerTaxId,
        string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = new StringContent(
                BuildEnvelope(username, password, projectCode, ownerTaxId),
                Encoding.UTF8,
                "text/xml")
        };
        request.Headers.Accept.ParseAdd("text/xml");
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
        request.Headers.TryAddWithoutValidation("Correlation-Id", correlationId);
        return request;
    }

    private static string BuildEnvelope(
        string username,
        string password,
        string projectCode,
        string ownerTaxId)
    {
        var envelope = new XElement(
            SoapNamespace + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", SoapNamespace.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sch", SchemaNamespace.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "com", CommonNamespace.NamespaceName),
            new XElement(SoapNamespace + "Header"),
            new XElement(
                SoapNamespace + "Body",
                new XElement(
                    SchemaNamespace + "ExportarObraRequest",
                    new XElement(
                        SchemaNamespace + "authObralia",
                        new XElement(CommonNamespace + "username", username),
                        new XElement(CommonNamespace + "password", password)),
                    new XElement(SchemaNamespace + "codigoObra", projectCode),
                    new XElement(SchemaNamespace + "cifEmpresaPropietaria", ownerTaxId))));

        return new XDocument(envelope).ToString(SaveOptions.DisableFormatting);
    }

    private static string ValidateValue(
        string? value,
        string label,
        int maximumLength,
        bool preserveWhitespace = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} es obligatorio.");
        }

        if (value.Length > maximumLength || value.Any(char.IsControl))
        {
            throw new ArgumentException($"{label} tiene un formato o longitud no válida.");
        }

        return preserveWhitespace ? value : value.Trim();
    }

    private void LogResponse(HttpResponseMessage response, long elapsedMilliseconds)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Nalanda SOAP export returned HTTP {StatusCode} in {ElapsedMilliseconds} ms",
                (int)response.StatusCode,
                elapsedMilliseconds);
            return;
        }

        logger.LogInformation(
            "Nalanda SOAP export completed with HTTP {StatusCode} in {ElapsedMilliseconds} ms",
            (int)response.StatusCode,
            elapsedMilliseconds);
    }
}
