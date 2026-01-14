using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using NavigationPlatform.Gateway.Admin;
using NavigationPlatform.Gateway.Auth;
using NavigationPlatform.Gateway.Middleware;
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
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// ---------- OpenTelemetry Tracing ----------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "navigation-gateway"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["Jaeger:OtlpEndpoint"] ?? "http://jaeger:4317");
        }));

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

                // Extract realm roles from token payload and add them as role claims
                // Keycloak sends realm_access as a JSON object: {"roles": ["admin", ...]}
                // The JWT handler might not extract nested JSON objects, so we read the token payload directly
                try
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Auth");
                    
                    // Try to get realm_access from claim first (JWT handler might have extracted it)
                    var realmAccessClaim = ctx.Principal?.FindFirst("realm_access");
                    string? realmAccessJson = null;
                    
                    if (realmAccessClaim != null && !string.IsNullOrWhiteSpace(realmAccessClaim.Value))
                    {
                        realmAccessJson = realmAccessClaim.Value;
                        logger.LogDebug("Found realm_access in claims: {RealmAccess}", realmAccessJson);
                    }
                    // If not found in claims, try to read from token payload directly
                    else if (ctx.SecurityToken is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtToken)
                    {
                        // JwtSecurityToken has a Payload property that contains all claims
                        if (jwtToken.Payload.TryGetValue("realm_access", out var realmAccessObj) && realmAccessObj != null)
                        {
                            // realm_access might be stored as a JsonElement or Dictionary, try to serialize it
                            if (realmAccessObj is System.Text.Json.JsonElement jsonElement)
                            {
                                realmAccessJson = jsonElement.GetRawText();
                            }
                            else
                            {
                                // Try to serialize as JSON
                                realmAccessJson = System.Text.Json.JsonSerializer.Serialize(realmAccessObj);
                            }
                            logger.LogDebug("Found realm_access in token payload: {RealmAccess}", realmAccessJson);
                        }
                        else
                        {
                            logger.LogWarning("realm_access not found in token payload. Available keys: {Keys}", 
                                string.Join(", ", jwtToken.Payload.Keys));
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(realmAccessJson))
                    {
                        using var doc = JsonDocument.Parse(realmAccessJson);
                        if (doc.RootElement.TryGetProperty("roles", out var roles) &&
                            roles.ValueKind == JsonValueKind.Array)
                        {
                            var identity = ctx.Principal?.Identity as ClaimsIdentity;
                            if (identity != null)
                            {
                                var rolesAdded = new List<string>();
                                foreach (var role in roles.EnumerateArray())
                                {
                                    if (role.ValueKind == JsonValueKind.String)
                                    {
                                        var roleValue = role.GetString();
                                        if (!string.IsNullOrWhiteSpace(roleValue))
                                        {
                                            // Add as both ClaimTypes.Role and "roles" for compatibility
                                            identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                                            identity.AddClaim(new Claim("roles", roleValue));
                                            rolesAdded.Add(roleValue);
                                        }
                                    }
                                }
                                logger.LogInformation("Added {Count} roles to identity: {Roles}", rolesAdded.Count, string.Join(", ", rolesAdded));
                            }
                        }
                    }
                    else
                    {
                        logger.LogWarning("realm_access JSON is null or empty");
                    }
                }
                catch (Exception ex)
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Auth");
                    logger.LogError(ex, "Failed to extract realm_access roles from token");
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
        policy.RequireAssertion(ctx =>
        {
            Microsoft.Extensions.Logging.ILogger? log = null;
            if (ctx.Resource is HttpContext httpContext)
            {
                var logFactory = httpContext.RequestServices?.GetService<ILoggerFactory>();
                log = logFactory?.CreateLogger("Auth");
            }
            
            // Check if user is authenticated
            if (ctx.User?.Identity?.IsAuthenticated != true)
            {
                log?.LogWarning("User is not authenticated");
                return false;
            }
            
            var result = HasRealmRole(ctx.User, "admin", log);
            if (!result && log != null)
            {
                var allClaims = ctx.User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
                log.LogWarning("Admin authorization failed. User claims: {Claims}", string.Join(", ", allClaims));
            }
            else if (result && log != null)
            {
                log.LogInformation("Admin authorization succeeded");
            }
            return result;
        });
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
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestBodyCaptureMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ---------- Endpoints ----------
app.MapAuth();

app.MapPatch("/admin/users/{id:guid}/status",
    async (Guid id, UpdateUserStatusRequest body, HttpContext http, GatewayDbContext db, KeycloakAdminClient keycloak, CancellationToken ct) =>
    {
        var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AdminEndpoint");
        logger.LogInformation("Endpoint handler reached. User authenticated: {IsAuthenticated}, Identity: {IdentityName}", 
            http.User?.Identity?.IsAuthenticated, http.User?.Identity?.Name);
        
        // Log all claims for debugging
        var allClaims = http.User?.Claims.Select(c => $"{c.Type}={c.Value}").ToList() ?? new List<string>();
        logger.LogInformation("All user claims: {Claims}", string.Join(", ", allClaims));
        
        // Try multiple ways to find the sub claim
        var actor = http.User?.FindFirst("sub")?.Value 
            ?? http.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? http.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? http.User?.Identity?.Name;
        
        logger.LogInformation("Sub claim value: {Sub}", actor);
        
        if (string.IsNullOrWhiteSpace(actor) || !Guid.TryParse(actor, out var actorUserId))
        {
            logger.LogWarning("Failed to parse sub claim as Guid. Sub: {Sub}. Available claims: {Claims}", actor, string.Join(", ", allClaims));
            return Results.Unauthorized();
        }
        
        logger.LogInformation("Processing status update for user {UserId} to {Status} by actor {ActorUserId}", id, body.Status, actorUserId);

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

app.UseMetricServer();
app.UseHttpMetrics();

app.MapReverseProxy();

app.Run();

static bool HasRealmRole(ClaimsPrincipal user, string role, Microsoft.Extensions.Logging.ILogger? logger = null)
{
    // First check: Look for roles we added as ClaimTypes.Role or "roles" claims
    // (These are added in OnTokenValidated from realm_access)
    var roleClaims = user.Claims
        .Where(c => string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase))
        .ToList();
    
    if (logger != null)
    {
        logger.LogDebug("Checking for role '{Role}'. Found {Count} role claims: {Roles}", 
            role, roleClaims.Count, string.Join(", ", roleClaims.Select(c => $"{c.Type}={c.Value}")));
    }
    
    var hasRole = roleClaims.Any(c => string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase));
    
    if (hasRole)
    {
        logger?.LogInformation("Found role '{Role}' in role claims", role);
        return true;
    }

    // Second check: Try to parse realm_access claim if it exists
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
                    {
                        logger?.LogInformation("Found role '{Role}' in realm_access claim", role);
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to parse realm_access claim");
        }
    }

    logger?.LogWarning("Role '{Role}' not found in user claims", role);
    return false;
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
