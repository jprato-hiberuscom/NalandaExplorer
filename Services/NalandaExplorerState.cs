using NalandaExplorer.Models;

namespace NalandaExplorer.Services;

public sealed class NalandaExplorerState
{
    public string? LastRestOperationId { get; private set; }

    public NalandaApiResponse? LastRestResponse { get; private set; }

    public NalandaSoapResponse? LastSoapResponse { get; private set; }

    public void StoreRestResult(string operationId, NalandaApiResponse response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(response);
        LastRestOperationId = operationId;
        LastRestResponse = response;
    }

    public void StoreSoapResult(NalandaSoapResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        LastSoapResponse = response;
    }

    public void ClearRestResult()
    {
        LastRestOperationId = null;
        LastRestResponse = null;
    }
}
