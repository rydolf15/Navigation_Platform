using Microsoft.AspNetCore.Http.HttpResults;

namespace NavigationPlatform.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/login", (HttpContext ctx, IConfiguration cfg) =>
        {
            var verifier = Pkce.GenerateCodeVerifier();
            var challenge = Pkce.GenerateCodeChallenge(verifier);

            ctx.Response.Cookies.Append(
                "pkce_verifier",
                verifier,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(5)
                });

            var url =
                $"{cfg["Auth:Authority"]}/protocol/openid-connect/auth" +
                $"?client_id={cfg["Auth:ClientId"]}" +
                $"&response_type=code" +
                $"&scope=openid profile offline_access" +
                $"&redirect_uri={cfg["Auth:RedirectUri"]}" +
                $"&code_challenge={challenge}" +
                $"&code_challenge_method=S256";

            return Results.Redirect(url);
        });

        app.MapGet("/api/auth/callback", async Task<Results<RedirectHttpResult, UnauthorizedHttpResult>> (HttpContext ctx, IConfiguration cfg, IHttpClientFactory http) =>
        {
            var code = ctx.Request.Query["code"].ToString();
            var verifier = ctx.Request.Cookies["pkce_verifier"];

            if (string.IsNullOrEmpty(code) || verifier == null)
                return TypedResults.Unauthorized();

            var client = http.CreateClient();

            var response = await client.PostAsync(
                $"{cfg["Auth:Authority"]}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = cfg["Auth:ClientId"]!,
                    ["code"] = code,
                    ["redirect_uri"] = cfg["Auth:RedirectUri"]!,
                    ["code_verifier"] = verifier
                }));

            if (!response.IsSuccessStatusCode)
                return TypedResults.Unauthorized();

            var json = await response.Content.ReadFromJsonAsync<TokenResponse>();

            ctx.Response.Cookies.Append(
                AuthCookies.AccessToken,
                json!.AccessToken,
                CookieOptions());

            ctx.Response.Cookies.Append(
                AuthCookies.RefreshToken,
                json.RefreshToken,
                CookieOptions(days: 30));

            return TypedResults.Redirect("/");
        });

        app.MapPost("/api/auth/refresh", async (HttpContext ctx, IConfiguration cfg, IHttpClientFactory http) =>
        {
            var refresh = ctx.Request.Cookies[AuthCookies.RefreshToken];
            if (refresh == null) return Results.Unauthorized();

            var client = http.CreateClient();

            var response = await client.PostAsync(
                $"{cfg["Auth:Authority"]}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = cfg["Auth:ClientId"]!,
                    ["refresh_token"] = refresh
                }));

            if (!response.IsSuccessStatusCode)
                return Results.Unauthorized();

            var json = await response.Content.ReadFromJsonAsync<TokenResponse>();

            ctx.Response.Cookies.Append(
                AuthCookies.AccessToken,
                json!.AccessToken,
                CookieOptions());

            return Results.NoContent();
        });

        app.MapPost("/api/auth/logout", async (HttpContext ctx, IConfiguration cfg, IHttpClientFactory http) =>
        {
            var refreshToken = ctx.Request.Cookies[AuthCookies.RefreshToken];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var client = http.CreateClient();

                // RFC 7009 token revocation
                await client.PostAsync(
                    $"{cfg["Auth:Authority"]}/protocol/openid-connect/revoke",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client_id"] = cfg["Auth:ClientId"]!,
                        ["token"] = refreshToken,
                        ["token_type_hint"] = "refresh_token"
                    }));
                // Ignore response intentionally (idempotent logout)
            }

            // Clear cookies (local logout)
            ctx.Response.Cookies.Delete(AuthCookies.AccessToken);
            ctx.Response.Cookies.Delete(AuthCookies.RefreshToken);

            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static CookieOptions CookieOptions(int days = 1) =>
        new()
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(days)
        };
}
