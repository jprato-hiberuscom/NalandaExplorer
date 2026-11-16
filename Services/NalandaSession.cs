namespace NalandaExplorer.Services;

public sealed class NalandaSession(TimeProvider timeProvider)
{
    private string? _accessToken;
    private DateTimeOffset _expiresAtUtc;

    public event Action? Changed;

    public bool IsAuthenticated => TryGetAccessToken(out _);

    public DateTimeOffset? ExpiresAtUtc =>
        TryGetAccessToken(out _) ? _expiresAtUtc : null;

    public void SetAccessToken(string accessToken, int expiresInSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        if (expiresInSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresInSeconds));
        }

        _accessToken = accessToken;
        _expiresAtUtc = timeProvider.GetUtcNow().AddSeconds(expiresInSeconds);
        Changed?.Invoke();
    }

    public bool TryGetAccessToken(out string accessToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _expiresAtUtc > timeProvider.GetUtcNow())
        {
            accessToken = _accessToken;
            return true;
        }

        Clear();
        accessToken = string.Empty;
        return false;
    }

    public void Clear()
    {
        var hadToken = _accessToken is not null;
        _accessToken = null;
        _expiresAtUtc = default;

        if (hadToken)
        {
            Changed?.Invoke();
        }
    }
}
