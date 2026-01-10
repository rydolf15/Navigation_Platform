using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace NavigationPlatform.Api.Auth;

internal sealed class CookieJwtBearerEvents : JwtBearerEvents
{
    public override Task MessageReceived(MessageReceivedContext ctx)
    {
        if (ctx.Request.Cookies.TryGetValue(
            AuthCookies.AccessToken, out var token))
        {
            ctx.Token = token;
        }
        return Task.CompletedTask;
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext ctx)
    {
        ctx.NoResult();
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
