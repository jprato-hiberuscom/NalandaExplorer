namespace NalandaExplorer.Models;

public enum NalandaApiParameterLocation
{
    Path,
    Query
}

public sealed record NalandaApiParameter(
    string Name,
    string Label,
    NalandaApiParameterLocation Location,
    bool Required = false,
    string InputType = "text",
    string? Placeholder = null,
    string? DefaultValue = null,
    IReadOnlyList<string>? Options = null,
    string? Description = null);
