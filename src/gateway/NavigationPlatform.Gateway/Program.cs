using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using NavigationPlatform.Gateway.Auth;
using NavigationPlatform.Gateway.Realtime;
using NavigationPlatform.Gateway.Realtime.Hubs;
using NavigationPlatform.Gateway.Realtime.Presence;
using Serilog;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// ---------- Services ----------
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();

// ---------- Reverse proxy (Gateway -> Journey service) ----------
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        // Copy access-token cookie into Authorization header for downstream services.
        builderContext.AddRequestTransform(transformContext =>
        {
            if (!transformContext.HttpContext.Request.Headers.ContainsKey("Authorization") &&
                transformContext.HttpContext.Request.Cookies.TryGetValue(
                    AuthCookies.AccessToken,
                    out var token) &&
                !string.IsNullOrWhiteSpace(token))
            {
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                    "Authorization",
                    $"Bearer {token}");
            }

            return ValueTask.CompletedTask;
        });
    });

// ---------- Auth ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // MUST match `iss` in token
        o.Authority = builder.Configuration["Auth:AuthorityPublic"];
        o.RequireHttpsMetadata = false;

        // FORCE metadata/JWKS retrieval via Docker DNS
        o.MetadataAddress =
            $"{builder.Configuration["Auth:AuthorityInternal"]}/.well-known/openid-configuration";

        o.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "sub",
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Auth:AuthorityPublic"],
            ValidateAudience = true,
            ValidAudiences = new[]
            {
                "navigation-api",
                "account"
            },

            ValidateLifetime = true
        };

        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue(
                    AuthCookies.AccessToken,
                    out var cookieToken))
                {
                    ctx.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------- Rate limiting ----------
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// ---------- Realtime (SignalR + presence) ----------
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddScoped<IUserPresenceWriter, RedisUserPresenceWriter>();

var app = builder.Build();

// ---------- Middleware ----------
app.UseSerilogRequestLogging();

app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();

    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    await next();
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var correlationId = context.TraceIdentifier;

        context.Response.ContentType = "application/problem+json";

        var (status, title) = exception switch
        {
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Invalid operation"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid argument"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        context.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = "An unexpected error occurred.",
            Instance = context.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId
            }
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ---------- Endpoints ----------
app.MapAuth();

app.MapHub<JourneyHub>("/hubs/journeys").RequireAuthorization();
app.MapHub<NotificationsHub>("/hubs/notifications").RequireAuthorization();
app.MapHub<InternalNotificationHub>("/hubs/internal-notifications");

app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.MapReverseProxy();

app.Run();
