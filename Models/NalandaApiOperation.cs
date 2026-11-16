namespace NalandaExplorer.Models;

public sealed record NalandaApiOperation(
    string Id,
    string Category,
    string Name,
    string Description,
    string PathTemplate,
    IReadOnlyList<NalandaApiParameter> Parameters,
    string AcceptHeader,
    bool SupportsJsonBody = false,
    IReadOnlyList<string>? AtLeastOneRequired = null);
