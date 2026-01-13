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
using NavigationPlatform.Infrastructure.Persistence.Rewards;
using NavigationPlatform.Infrastructure.Persistence.Sharing;
using NavigationPlatform.Infrastructure.Rewards;
using Npgsql;
using Serilog;

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

builder.Services.AddAuthorization();

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

app.MapGet("/api/users/me/daily-goal",
    async (IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(
            new GetDailyGoalStatusQuery(), ct)))
    .RequireAuthorization();


// ------------------------------------------------


app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.Run();

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
