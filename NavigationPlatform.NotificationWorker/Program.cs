using Microsoft.EntityFrameworkCore;
using NavigationPlatform.NotificationWorker.Messaging;
using NavigationPlatform.NotificationWorker.Persistence;
using NavigationPlatform.NotificationWorker.Processing;
using Npgsql;
using Serilog;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");

await EnsureDatabaseExistsAsync(connectionString, CancellationToken.None);
await EnsureSchemaAsync(connectionString, CancellationToken.None);

// ---------- Logging ----------
builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext());

// ---------- Database ----------
builder.Services.AddDbContext<NotificationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ---------- Messaging ----------
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<IUserPresence, RedisUserPresence>(); // or stub

builder.Services.AddSingleton<ISignalRNotifier>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var hubUrl = cfg["SignalR:HubUrl"];

    if (string.IsNullOrWhiteSpace(hubUrl))
        throw new InvalidOperationException("SignalR HubUrl is not configured");

    return new SignalRNotifier(hubUrl);
});


builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// ---------- Processing ----------
builder.Services.AddScoped<NotificationEventProcessor>();

// ---------- Worker ----------
builder.Services.AddHostedService<NotificationEventsConsumer>();

var host = builder.Build();
await host.RunAsync();

static async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct)
{
    var csb = new NpgsqlConnectionStringBuilder(connectionString);
    var dbName = csb.Database;

    if (string.IsNullOrWhiteSpace(dbName))
        throw new InvalidOperationException("ConnectionStrings:Default must include a Database name");

    var admin = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Database = "postgres"
    };

    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            await using var conn = new NpgsqlConnection(admin.ConnectionString);
            await conn.OpenAsync(ct);

            await using var existsCmd = conn.CreateCommand();
            existsCmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name;";
            existsCmd.Parameters.AddWithValue("name", dbName);

            var exists = await existsCmd.ExecuteScalarAsync(ct) is not null;

            if (!exists)
            {
                var safe = dbName.Replace("\"", "\"\"");
                await using var createCmd = conn.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE \"{safe}\";";
                await createCmd.ExecuteNonQueryAsync(ct);
            }

            return;
        }
        catch when (attempt < 30)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}

static async Task EnsureSchemaAsync(string connectionString, CancellationToken ct)
{
    for (var attempt = 1; attempt <= 30; attempt++)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);

            var sql = """
                CREATE TABLE IF NOT EXISTS journey_favourites (
                    journey_id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    CONSTRAINT pk_journey_favourites PRIMARY KEY (journey_id, user_id)
                );

                CREATE TABLE IF NOT EXISTS inbox_messages (
                    id uuid PRIMARY KEY,
                    type text NOT NULL,
                    occurred_utc timestamptz NOT NULL,
                    processed_utc timestamptz NOT NULL
                );
            """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);

            return;
        }
        catch when (attempt < 30)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
