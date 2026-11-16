namespace NalandaExplorer.Models;

public sealed record NalandaSoapResponse(
    int StatusCode,
    string ReasonPhrase,
    long ElapsedMilliseconds,
    string ContentType,
    string Body,
    bool IsXml,
    string? FaultMessage,
    NalandaSoapProject? Project);

public sealed record NalandaSoapProject(
    string? Code,
    string? OwnerTaxId,
    IReadOnlyList<NalandaSoapContractor> Contractors);

public sealed record NalandaSoapContractor(
    string? Id,
    string? TaxId,
    string? ParentId,
    string? CompanyName,
    string? EntryDate,
    string? ExitDate,
    IReadOnlyList<NalandaSoapWorker> Workers,
    IReadOnlyList<NalandaSoapMachinery> Machinery,
    IReadOnlyList<NalandaSoapDocument> Documents);

public sealed record NalandaSoapWorker(
    string? Name,
    string? TaxId,
    string? EntryDate,
    string? ExitDate,
    int DocumentCount);

public sealed record NalandaSoapMachinery(
    string? Registration,
    int DocumentCount);

public sealed record NalandaSoapDocument(
    string? Url,
    string? Type,
    string? Status,
    string? Description,
    string? UploadedAt,
    string? IssuedAt,
    string? ExpiresAt);
