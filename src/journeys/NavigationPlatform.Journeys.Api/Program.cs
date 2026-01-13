using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NavigationPlatform.Api.Contracts.Journeys;
using NavigationPlatform.Api.Messaging;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Abstractions.Rewards;
using NavigationPlatform.Application.Journeys.Commands;
using NavigationPlatform.Application.Journeys.Queries;
using NavigationPlatform.Infrastructure;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Analytics;
using NavigationPlatform.Infrastructure.Persistence.Rewards;
using NavigationPlatform.Infrastructure.Persistence.Sharing;
using NavigationPlatform.Infrastructure.Rewards;
using Npgsql;
using Serilog;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(_ => { });
});

// ---------- Logging ----------
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

// ---------- Services ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

builder.Services.AddMediatR(cfg =>
{
    // Application handlers (commands/queries)
    cfg.RegisterServicesFromAssembly(typeof(CreateJourneyCommand).Assembly);
    // Infrastructure handlers (some commands are currently handled there)
    cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJourneyCommand>();

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default")!);

// ---------- Messaging (Outbox -> RabbitMQ) ----------
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<DailyGoalAchievedConsumer>();
builder.Services.AddHostedService<MonthlyDistanceProjectionConsumer>();


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
            OnTokenValidated = ctx =>
            {
                var expectedClientId = builder.Configuration["Auth:ClientId"];
                var azp = ctx.Principal?.FindFirst("azp")?.Value;

                if (string.IsNullOrWhiteSpace(expectedClientId) ||
                    string.IsNullOrWhiteSpace(azp) ||
                    !string.Equals(azp, expectedClientId, StringComparison.Ordinal))
                {
                    ctx.Fail("Invalid token (azp).");
                }

                return Task.CompletedTask;
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

// ---------- Correlation ID ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddDbContext<RewardReadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IDailyGoalStatusReader, DailyGoalStatusReader>();


var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    var rewardsDb = scope.ServiceProvider.GetRequiredService<RewardReadDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILogger<Program>>();

    var journeysConnectionString = builder.Configuration.GetConnectionString("Default")!;

    const int maxRetries = 10;
    var delay = TimeSpan.FromSeconds(3);

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            EnsureDatabaseExists(journeysConnectionString);
            db.Database.Migrate();

            // Create reward read-model table (separate context; not covered by migrations)
            rewardsDb.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS daily_distance_projection (
                    user_id uuid NOT NULL,
                    date date NOT NULL,
                    total_distance_km numeric(5,2) NOT NULL,
                    reward_granted boolean NOT NULL,
                    CONSTRAINT pk_daily_distance_projection PRIMARY KEY (user_id, date)
                );
            """);

            // Admin analytics projection tables (CQRS read model; not covered by migrations)
            analyticsDb.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS analytics_inbox_messages (
                    id uuid PRIMARY KEY,
                    type text NOT NULL,
                    occurred_utc timestamptz NOT NULL,
                    processed_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS journey_distance_projection (
                    journey_id uuid PRIMARY KEY,
                    user_id uuid NOT NULL,
                    year int NOT NULL,
                    month int NOT NULL,
                    distance_km numeric(12,2) NOT NULL,
                    start_time timestamptz NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_journey_distance_projection_user_year_month
                    ON journey_distance_projection (user_id, year, month);

                CREATE TABLE IF NOT EXISTS monthly_distance_projection (
                    user_id uuid NOT NULL,
                    year int NOT NULL,
                    month int NOT NULL,
                    total_distance_km numeric(14,2) NOT NULL,
                    CONSTRAINT pk_monthly_distance_projection PRIMARY KEY (user_id, year, month)
                );

                CREATE INDEX IF NOT EXISTS ix_monthly_distance_projection_year_month
                    ON monthly_distance_projection (year, month);
            """);

            logger.LogInformation("Database migration completed");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(
                ex,
                "Database not ready (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                attempt,
                maxRetries,
                delay.TotalSeconds);

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

app.UseHttpsRedirection();

// ----------- Error Handling -----------
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
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
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

app.UseSwagger();
app.UseSwaggerUI();

// ---------- Endpoints ----------

app.MapPost("/api/journeys",
    async Task<Results<Created<Guid>, BadRequest>> (
        CreateJourneyCommand cmd,
        IMediator mediator,
        CancellationToken ct) =>
    {
        var id = await mediator.Send(cmd, ct);
        return TypedResults.Created($"/api/journeys/{id}", id);
    }).RequireAuthorization();

app.MapPost("/api/journeys/{id:guid}/favorite",
    async (Guid id, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.Send(new FavoriteJourneyCommand(id), ct);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapDelete("/api/journeys/{id:guid}/favorite",
    async (Guid id, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.Send(new UnfavoriteJourneyCommand(id), ct);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapPost("/api/journeys/{id:guid}/share",
    async (Guid id, ShareJourneyRequest req, IMediator m, CancellationToken ct) =>
    {
        await m.Send(new ShareJourneyCommand(id, req.UserIds), ct);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapGet("/api/journeys/{id:guid}/share",
    async (Guid id, AppDbContext db, ICurrentUser currentUser, CancellationToken ct) =>
    {
        var journey = await db.Journeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (journey == null)
            return Results.NotFound();

        // Only owners can manage/view the recipient list.
        if (journey.UserId != currentUser.UserId)
            return Results.Forbid();

        var userIds = await db.Set<JourneyShare>()
            .AsNoTracking()
            .Where(x => x.JourneyId == id)
            .OrderBy(x => x.SharedAtUtc)
            .Select(x => x.SharedWithUserId)
            .ToListAsync(ct);

        return Results.Ok(new { userIds });
    }).RequireAuthorization();

app.MapPost("/api/journeys/{id:guid}/public-link",
    async (Guid id, IMediator m, CancellationToken ct) =>
    {
        var linkId = await m.Send(new CreatePublicLinkCommand(id), ct);
        return Results.Ok(new { url = $"/public/journeys/{linkId}" });
    }).RequireAuthorization();

app.MapDelete("/api/journeys/public-link/{id:guid}",
    async (Guid id, IMediator m, CancellationToken ct) =>
    {
        await m.Send(new RevokePublicLinkCommand(id), ct);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapGet("/public/journeys/{linkId:guid}",
    async (Guid linkId, AppDbContext db, CancellationToken ct) =>
    {
        var link = await db.Set<JourneyPublicLink>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == linkId, ct);

        if (link == null || link.RevokedUtc != null)
            return Results.StatusCode(410);

        var journey = await db.Journeys
            .AsNoTracking()
            .Where(j => j.Id == link.JourneyId)
            .Select(j => new
            {
                j.StartLocation,
                j.ArrivalLocation,
                j.StartTime,
                j.ArrivalTime,
                DistanceKm = (decimal)j.DistanceKm
            })
            .FirstAsync(ct);

        return Results.Ok(journey);
    });

app.MapGet("/api/journeys",
    async (IMediator mediator, CancellationToken ct, [FromQuery] int page, [FromQuery] int pageSize) =>
    {
        if (page <= 0 || pageSize <= 0 || pageSize > 100)
            return Results.BadRequest(new
            {
                error = "Invalid paging parameters",
                page,
                pageSize
            });

        return Results.Ok(
            await mediator.Send(
                new GetJourneysPagedQuery(page, pageSize), ct));
    })
    .RequireAuthorization();

app.MapGet("/api/journeys/{id}",
    async (Guid id, IMediator mediator, CancellationToken ct) =>
    {
        var result = await mediator.Send(
            new GetJourneyByIdQuery(id), ct);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    })
    .RequireAuthorization();

app.MapGet("/admin/journeys",
    async (
        HttpContext http,
        IMediator mediator,
        CancellationToken ct,
        [FromQuery] Guid? userId,
        [FromQuery] string? transportType,
        [FromQuery] DateTime? startDateFrom,
        [FromQuery] DateTime? startDateTo,
        [FromQuery] DateTime? arrivalDateFrom,
        [FromQuery] DateTime? arrivalDateTo,
        [FromQuery] decimal? minDistance,
        [FromQuery] decimal? maxDistance,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? direction = null) =>
    {
        if (page <= 0 || pageSize <= 0 || pageSize > 200)
            return Results.BadRequest(new { error = "Invalid paging parameters", page, pageSize });

        var result = await mediator.Send(
            new GetAdminJourneysQuery(
                userId,
                transportType,
                startDateFrom,
                startDateTo,
                arrivalDateFrom,
                arrivalDateTo,
                minDistance,
                maxDistance,
                page,
                pageSize,
                orderBy,
                direction),
            ct);

        http.Response.Headers["XTotalCount"] = result.TotalCount.ToString();

        return Results.Ok(new
        {
            items = result.Items,
            page = result.Page,
            pageSize = result.PageSize
        });
    })
    .RequireAuthorization("Admin");

app.MapGet("/admin/statistics/monthly-distance",
    async (
        AnalyticsDbContext db,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? orderBy = "UserId",
        [FromQuery] string? direction = "asc") =>
    {
        if (page <= 0 || pageSize <= 0 || pageSize > 500)
            return Results.BadRequest(new { error = "Invalid paging parameters", page, pageSize });

        var query = db.MonthlyDistances.AsNoTracking();
        var totalCount = await query.CountAsync(ct);

        var asc = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        if (!asc && !desc) return Results.BadRequest(new { error = "Invalid Direction. Use asc or desc." });

        query = (orderBy?.Trim().ToLowerInvariant(), asc) switch
        {
            ("totaldistancekm", true) => query.OrderBy(x => x.TotalDistanceKm),
            ("totaldistancekm", false) => query.OrderByDescending(x => x.TotalDistanceKm),
            ("userid", true) => query.OrderBy(x => x.UserId),
            ("userid", false) => query.OrderByDescending(x => x.UserId),
            (null, _) => query.OrderBy(x => x.UserId),
            ("", _) => query.OrderBy(x => x.UserId),
            _ => throw new ArgumentException("Invalid OrderBy.")
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.UserId,
                x.Year,
                x.Month,
                TotalDistanceKm = x.TotalDistanceKm
            })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            items,
            page,
            pageSize,
            totalCount
        });
    })
    .RequireAuthorization("Admin");

app.MapPut("/api/journeys/{id:guid}",
    async (Guid id, UpdateJourneyCommand body, IMediator mediator, CancellationToken ct) =>
    {
        // Route id is the source of truth.
        await mediator.Send(body with { JourneyId = id }, ct);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapDelete("/api/journeys/{id:guid}",
    async (Guid id, IMediator mediator, CancellationToken ct) =>
    {
        await mediator.Send(new DeleteJourneyCommand(id), ct);
        return Results.NoContent();
    }).RequireAuthorization();

app.MapGet("/api/users/me/daily-goal",
    async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(
            new GetDailyGoalStatusQuery(), ct)))
    .RequireAuthorization();


// ------------------------------------------------


app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.Run();

static bool HasRealmRole(ClaimsPrincipal user, string role)
{
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
            // ignore
        }
    }

    return user.Claims.Any(c =>
        (string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase)) &&
        string.Equals(c.Value, role, StringComparison.OrdinalIgnoreCase));
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
