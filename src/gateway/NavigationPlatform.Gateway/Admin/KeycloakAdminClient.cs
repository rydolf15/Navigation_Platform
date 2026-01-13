using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace NavigationPlatform.Gateway.Admin;

internal sealed class KeycloakAdminClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;

    private string? _accessToken;
    private DateTime _accessTokenExpiresUtc;

    public KeycloakAdminClient(IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _httpFactory = httpFactory;
        _cfg = cfg;
    }

    public async Task SetUserStatusAsync(Guid userId, string status, CancellationToken ct)
    {
        var baseUrl = _cfg["KeycloakAdmin:BaseUrl"]
            ?? throw new InvalidOperationException("KeycloakAdmin:BaseUrl is not configured");
        var realm = _cfg["KeycloakAdmin:Realm"]
            ?? throw new InvalidOperationException("KeycloakAdmin:Realm is not configured");

        var enabled = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

        var token = await GetAdminAccessTokenAsync(ct);

        var url = $"{baseUrl}/admin/realms/{realm}/users/{userId}";

        var client = _httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Keycloak attributes are string -> string[].
        var body = new
        {
            enabled,
            attributes = new Dictionary<string, string[]>
            {
                ["account_status"] = [status]
            }
        };

        req.Content = JsonContent.Create(body);

        using var resp = await client.SendAsync(req, ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new KeyNotFoundException("User not found in Keycloak.");

        resp.EnsureSuccessStatusCode();
    }

    private async Task<string> GetAdminAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && DateTime.UtcNow < _accessTokenExpiresUtc)
            return _accessToken!;

        var baseUrl = _cfg["KeycloakAdmin:BaseUrl"]
            ?? throw new InvalidOperationException("KeycloakAdmin:BaseUrl is not configured");
        var adminRealm = _cfg["KeycloakAdmin:AdminRealm"] ?? "master";
        var clientId = _cfg["KeycloakAdmin:ClientId"] ?? "admin-cli";

        // Prefer env vars so we don't commit secrets.
        var username =
            _cfg["KeycloakAdmin:Username"]
            ?? Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN")
            ?? "admin";

        var password =
            _cfg["KeycloakAdmin:Password"]
            ?? Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD")
            ?? "admin";

        var tokenUrl = $"{baseUrl}/realms/{adminRealm}/protocol/openid-connect/token";

        var client = _httpFactory.CreateClient();
        using var resp = await client.PostAsync(
            tokenUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["username"] = username,
                ["password"] = password
            }),
            ct);

        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<AdminTokenResponse>(cancellationToken: ct);
        if (json == null || string.IsNullOrWhiteSpace(json.AccessToken))
            throw new InvalidOperationException("Keycloak admin token response is invalid.");

        _accessToken = json.AccessToken;
        // subtract a small safety window
        _accessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(10, json.ExpiresIn - 30));

        return _accessToken;
    }

    private sealed record AdminTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

