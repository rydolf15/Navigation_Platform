using FluentValidation;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NavigationPlatform.Api.Contracts.Journeys;
using NavigationPlatform.Api.Realtime;
using NavigationPlatform.Application.Abstractions.Identity;
using NavigationPlatform.Application.Journeys.Commands;
using NavigationPlatform.Infrastructure;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Sharing;
using Serilog;
using StackExchange.Redis;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

// ---------- Services ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateJourneyCommand).Assembly));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJourneyCommand>();

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default")!);

// ---------- Auth ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = true;
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

// ---------- Correlation ID ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();


builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddScoped<IUserPresenceWriter, RedisUserPresenceWriter>();

builder.Services.AddSignalR()
    .AddHubOptions<JourneyHub>(o =>
    {
        o.EnableDetailedErrors = false;
    });

builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

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

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.MapHub<JourneyHub>("/hubs/journeys");

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
    })
    .RequireAuthorization();

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


app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.Run();
