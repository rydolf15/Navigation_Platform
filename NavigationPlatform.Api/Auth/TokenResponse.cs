using System.Text.Json.Serialization;

namespace NavigationPlatform.Api.Auth;

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = null!;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = null!;
}
