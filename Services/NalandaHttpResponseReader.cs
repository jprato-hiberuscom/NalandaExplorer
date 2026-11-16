using System.Text;

namespace NalandaExplorer.Services;

internal static class NalandaHttpResponseReader
{
    public static async Task<string> ReadBoundedBodyAsync(
        HttpContent content,
        int maximumBytes,
        string maximumDisplay,
        CancellationToken cancellationToken)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        if (content.Headers.ContentLength is long contentLength &&
            contentLength > maximumBytes)
        {
            throw new InvalidDataException(
                $"La respuesta supera el límite de {maximumDisplay}.");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        while (true)
        {
            var count = await stream.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }

            if (buffer.Length + count > maximumBytes)
            {
                throw new InvalidDataException(
                    $"La respuesta supera el límite de {maximumDisplay}.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF');
    }
}
