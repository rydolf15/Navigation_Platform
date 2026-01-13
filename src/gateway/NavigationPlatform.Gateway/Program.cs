using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NavigationPlatform.Gateway.Admin;
using NavigationPlatform.Gateway.Auth;
using NavigationPlatform.Gateway.Messaging;
using NavigationPlatform.Gateway.Persistence;
using NavigationPlatform.Gateway.Realtime;
using NavigationPlatform.Gateway.Realtime.Hubs;
using NavigationPlatform.Gateway.Realtime.Presence;
using Npgsql;
using Serilog;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// ---------- Services ----------
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();

// ---------- Persistence (Gateway-owned DB for A3) ----------
builder.Services.AddDbContext<GatewayDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<KeycloakAdminClient>();
builder.Services.AddHostedService<GatewayOutboxPublisher>();

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
            // Keycloak (by default) does not include an `aud` claim in access tokens for this client.
            // Validate the client via `azp` in OnTokenValidated instead.
            ValidateAudience = false,

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
            },
            OnTokenValidated = async ctx =>
            {
                var expectedClientId = builder.Configuration["Auth:ClientId"];
                var azp = ctx.Principal?.FindFirst("azp")?.Value;

                if (string.IsNullOrWhiteSpace(expectedClientId) ||
                    string.IsNullOrWhiteSpace(azp) ||
                    !string.Equals(azp, expectedClientId, StringComparison.Ordinal))
                {
                    ctx.Fail("Invalid token (azp).");
                    return;
                }

                // Enforce user status (A3): if suspended/deactivated, fail auth immediately.
                try
                {
                    var sub = ctx.Principal?.FindFirst("sub")?.Value;
                    if (Guid.TryParse(sub, out var userId))
                    {
                        var db = ctx.HttpContext.RequestServices.GetRequiredService<GatewayDbContext>();

                        var status = await db.UserAccountStatuses
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.UserId == userId, ctx.HttpContext.RequestAborted);

                        if (status != null &&
                            !string.Equals(status.Status, "Active", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.Fail($"Account {status.Status}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Auth");

                    logger.LogWarning(ex, "Failed to check account status");
                }
            },
            OnChallenge = async ctx =>
            {
                // Provide a clear error message for suspended/deactivated accounts (A3).
                var failure = ctx.AuthenticateFailure?.Message;

                if (!string.IsNullOrWhiteSpace(failure) &&
                    failure.StartsWith("Account ", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.HandleResponse();

                    var statusText = failure.Replace("Account ", "", StringComparison.OrdinalIgnoreCase)
                        .Trim()
                        .TrimEnd('.');

                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    ctx.Response.ContentType = "application/problem+json";

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = $"Account {statusText}",
                        Detail = $"Your account is {statusText}. Please contact an administrator.",
                        Instance = ctx.Request.Path
                    };

                    await ctx.Response.WriteAsJsonAsync(problem);
                }
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Admin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx => HasRealmRole(ctx.User, "admin"));
    });
});

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

// ---------- Database bootstrap ----------
using (var scope = app.Services.CreateScope())
{
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("GatewayDb");

    var connectionString = cfg.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");

    const int maxRetries = 10;
    var delay = TimeSpan.FromSeconds(3);

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            EnsureDatabaseExists(connectionString);
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex, "Gateway DB not ready (attempt {Attempt}/{Max}). Retrying in {Delay}s...", attempt, maxRetries, delay.TotalSeconds);
            Thread.Sleep(delay);
        }
    }
}

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

app.MapPatch("/admin/users/{id:guid}/status",
    async (Guid id, UpdateUserStatusRequest body, HttpContext http, GatewayDbContext db, KeycloakAdminClient keycloak, CancellationToken ct) =>
    {
        var actor = http.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(actor, out var actorUserId))
            return Results.Unauthorized();

        if (!TryNormalizeStatus(body.Status, out var newStatus))
            return Results.BadRequest(new { error = "Invalid status. Use Active, Suspended, or Deactivated." });

        var existing = await db.UserAccountStatuses
            .FirstOrDefaultAsync(x => x.UserId == id, ct);

        var oldStatus = existing?.Status ?? "Active";

        if (string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            return Results.NoContent();

        // Update Keycloak (source of truth for login ability)
        await keycloak.SetUserStatusAsync(id, newStatus, ct);

        if (existing == null)
        {
            existing = new UserAccountStatus { UserId = id };
            db.UserAccountStatuses.Add(existing);
        }

        existing.Status = newStatus;
        existing.UpdatedUtc = DateTime.UtcNow;

        db.UserStatusAudits.Add(new UserStatusAudit
        {
            UserId = id,
            ChangedByUserId = actorUserId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            OccurredUtc = DateTime.UtcNow
        });

        db.OutboxMessages.Add(
            GatewayOutboxMessage.From(
                new UserStatusChanged(
                    id,
                    newStatus,
                    actorUserId,
                    DateTime.UtcNow)));

        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    })
    .RequireAuthorization("Admin");

app.MapHub<JourneyHub>("/hubs/journeys").RequireAuthorization();
app.MapHub<NotificationsHub>("/hubs/notifications").RequireAuthorization();
app.MapHub<InternalNotificationHub>("/hubs/internal-notifications");

app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.MapReverseProxy();

app.Run();

static bool HasRealmRole(ClaimsPrincipal user, string role)
{
    // Keycloak realm roles are typically in: realm_access.roles = ["admin", ...]
    var realmAccess = user.FindFirst("realm_access")?.Value;

    if (!string.IsNullOrWhiteSpace(realmAccess))
    {
        try
        {
            using var doc = JsonDocument.Parse(realmAccess);
            if (doc.RootElement.TryGetProperty("roles", out var roles) &&
                roles.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in roles.EnumerateArray())
                {
                    if (r.ValueKind == JsonValueKind.String &&
                        string.Equals(r.GetString(), role, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch
        {
            // ignore parse errors; fall back below
        }
    }

    // Fallback: some setups map roles directly as repeated "roles" claims.
    return user.Claims.Any(c =>
        string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase))
      && user.Claims.Any(c =>
        (string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase)) &&
        string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase));
}

static bool TryNormalizeStatus(string? input, out string normalized)
{
    normalized = string.Empty;
    if (string.IsNullOrWhiteSpace(input))
        return false;

    var s = input.Trim();

    if (string.Equals(s, "Active", StringComparison.OrdinalIgnoreCase))
    {
        normalized = "Active";
        return true;
    }

    if (string.Equals(s, "Suspended", StringComparison.OrdinalIgnoreCase))
    {
        normalized = "Suspended";
        return true;
    }

    if (string.Equals(s, "Deactivated", StringComparison.OrdinalIgnoreCase))
    {
        normalized = "Deactivated";
        return true;
    }

    return false;
}

static void EnsureDatabaseExists(string connectionString)
{
    var csb = new NpgsqlConnectionStringBuilder(connectionString);
    var dbName = csb.Database;

    if (string.IsNullOrWhiteSpace(dbName) || dbName == "postgres")
        return;

    var admin = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Database = "postgres"
    };

    using var conn = new NpgsqlConnection(admin.ConnectionString);
    conn.Open();

    using var existsCmd = conn.CreateCommand();
    existsCmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name;";
    existsCmd.Parameters.AddWithValue("name", dbName);

    var exists = existsCmd.ExecuteScalar() is not null;

    if (!exists)
    {
        var safe = dbName.Replace("\"", "\"\"");
        using var createCmd = conn.CreateCommand();
        createCmd.CommandText = $"CREATE DATABASE \"{safe}\";";
        createCmd.ExecuteNonQuery();
    }
}

internal sealed record UpdateUserStatusRequest(string Status);
