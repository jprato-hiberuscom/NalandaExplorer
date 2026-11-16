using System.Text.Json;

namespace NalandaExplorer.Models;

public sealed record NalandaApiResponse(
    int StatusCode,
    string ReasonPhrase,
    long ElapsedMilliseconds,
    string ContentType,
    string Body,
    bool IsJson,
    JsonElement? Json);
