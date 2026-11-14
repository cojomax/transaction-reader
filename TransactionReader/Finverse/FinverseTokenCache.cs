namespace TransactionReader.Finverse;

/// <summary>
/// Holds the current Finverse customer access token in memory and decides when it needs refreshing.
/// Registered as a singleton so the token is shared across every <see cref="FinverseClient"/> instance
/// (typed HttpClients are transient, so they can't hold the cache themselves).
/// </summary>
public sealed class FinverseTokenCache(TimeProvider timeProvider)
{
    /// <summary>Refresh this long before the token actually expires, to avoid using it as it lapses.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    // Only one caller fetches a new token at a time; others wait and reuse the result.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _token;
    private DateTimeOffset _refreshAt;

    public async Task<string> GetOrRefreshAsync(
        Func<CancellationToken, Task<(string Token, TimeSpan Lifetime)>> fetchToken,
        CancellationToken cancellationToken)
    {
        if (TryGetValidToken(out var token))
        {
            return token;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have refreshed it while we were waiting for the lock.
            if (TryGetValidToken(out token))
            {
                return token;
            }

            var fetchedAt = timeProvider.GetUtcNow();
            var (newToken, lifetime) = await fetchToken(cancellationToken);

            // Normally refresh 60s early; for very short-lived tokens, refresh halfway through instead.
            var usableFor = lifetime > RefreshMargin * 2 ? lifetime - RefreshMargin : lifetime / 2;

            _token = newToken;
            _refreshAt = fetchedAt + usableFor;
            return newToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool TryGetValidToken(out string token)
    {
        token = _token!;
        return _token is not null && timeProvider.GetUtcNow() < _refreshAt;
    }
}
