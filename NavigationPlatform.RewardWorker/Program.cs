using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NavigationPlatform.RewardWorker.Messaging;
using NavigationPlatform.RewardWorker.Persistence;
using Npgsql;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");

await EnsureDatabaseExistsAsync(connectionString, CancellationToken.None);
await EnsureSchemaAsync(connectionString, CancellationToken.None);

builder.Services.AddDbContext<RewardDbContext>(o =>
    o.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql =>
        {
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        }));

builder.Services.AddHostedService<JourneyEventsConsumer>();
builder.Services.AddHostedService<OutboxPublisher>();

builder.Services.AddSerilog(cfg =>
    cfg.WriteTo.Console());

var host = builder.Build();

await host.RunAsync();

static async Task EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct)
{
    var csb = new NpgsqlConnectionStringBuilder(connectionString);
    var dbName = csb.Database;

    if (string.IsNullOrWhiteSpace(dbName))
        throw new InvalidOperationException("ConnectionStrings:Default must include a Database name");

    // Connect to a known DB to create the target DB if missing.
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
                CREATE TABLE IF NOT EXISTS inbox_messages (
                    id uuid PRIMARY KEY,
                    type text NOT NULL,
                    occurred_utc timestamptz NOT NULL,
                    processed_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS outbox_messages (
                    id uuid PRIMARY KEY,
                    type text NOT NULL,
                    payload text NOT NULL,
                    occurred_utc timestamptz NOT NULL,
                    processed boolean NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_outbox_messages_processed ON outbox_messages(processed);

                CREATE TABLE IF NOT EXISTS journey_projection (
                    journey_id uuid PRIMARY KEY,
                    user_id uuid NOT NULL,
                    date date NOT NULL,
                    distance_km numeric(5,2) NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_journey_projection_user_date ON journey_projection(user_id, date);

                CREATE TABLE IF NOT EXISTS daily_distance_projection (
                    user_id uuid NOT NULL,
                    date date NOT NULL,
                    total_distance_km numeric(5,2) NOT NULL,
                    reward_granted boolean NOT NULL,
                    granted_by_journey_id uuid NULL,
                    CONSTRAINT pk_daily_distance_projection PRIMARY KEY (user_id, date)
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