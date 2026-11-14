using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TransactionReader.Finverse;

/// <summary>
/// Typed HttpClient for the Finverse API. The HttpClient's BaseAddress is set from
/// <see cref="FinverseOptions.BaseUrl"/> when it's registered in <see cref="AppHost"/>.
/// </summary>
public class FinverseClient(
    HttpClient httpClient,
    FinverseTokenCache tokenCache,
    IOptions<FinverseOptions> options,
    ILogger<FinverseClient> logger)
{

    /// <summary>
    /// Returns a customer access token, fetching a new one from Finverse only when the cached
    /// token is missing or about to expire. Send it as <c>Authorization: Bearer &lt;token&gt;</c>.
    /// </summary>
    private Task<string> GetCustomerAccessTokenAsync(CancellationToken cancellationToken = default) =>
        tokenCache.GetOrRefreshAsync(FetchCustomerAccessTokenAsync, cancellationToken);
    
    private string ClientId => options.Value.ClientId;
    
    /// <summary>Finverse uses snake_case JSON (client_id, access_token, ...).</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    
    private async Task<(string Token, TimeSpan Lifetime)> FetchCustomerAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        var request = new CustomerTokenRequest(ClientId, options.Value.ClientSecret, "client_credentials");

        using var response = await httpClient.PostAsJsonAsync(
            "auth/customer/token", request, JsonOptions, cancellationToken);

        await EnsureSuccessAsync(response, "customer token request", cancellationToken);

        var token = await response.Content.ReadFromJsonAsync<CustomerTokenResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Finverse returned an empty customer token response.");

        var lifetime = TimeSpan.FromSeconds(token.ExpiresIn);
        logger.LogDebug("Fetched new Finverse customer access token, valid for {Lifetime}.", lifetime);

        return (token.AccessToken, lifetime);
    }

    public async Task<string> GenerateLinkUrlAsync(
        string userId, string state, CancellationToken cancellationToken = default)
    {
        var customerToken = await GetCustomerAccessTokenAsync(cancellationToken);
        var payload = new LinkUrlRequest(ClientId, options.Value.RedirectUri, userId, state);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "link/token")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        await EnsureSuccessAsync(response, "link token request", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<LinkUrlResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Finverse did not return a link URL.");

        return result.LinkUrl;
    }

    /// <summary>Throws with Finverse's error body included when the response isn't a success.</summary>
    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Finverse {operation} failed with {(int)response.StatusCode} {response.StatusCode}: {body}",
            null,
            response.StatusCode);
    }

    private sealed record CustomerTokenRequest(string ClientId, string ClientSecret, string GrantType);

    private sealed record LinkUrlRequest(string ClientId, string RedirectUri, string UserId, string State) {
        public string GrantType => "client_credentials";
        public string ResponseMode => "form_post";
        public string ResponseType => "code";
    }

    private sealed record CustomerTokenResponse(
        string AccessToken,
        string TokenType,
        long ExpiresIn,
        DateTimeOffset IssuedAt);
    
    private sealed record LinkUrlResponse(
        string AccessToken,
        long ExpiresIn,
        DateTimeOffset IssuedAt,
        string LinkUrl,
        string TokenType);
}
