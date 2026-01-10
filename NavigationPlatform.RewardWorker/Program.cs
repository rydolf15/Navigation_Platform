using Microsoft.EntityFrameworkCore;
using NavigationPlatform.RewardWorker.Persistence;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddSingleton<OutboxProcessor>();

builder.Services.AddSerilog(cfg =>
    cfg.WriteTo.Console());

var host = builder.Build();

while (!host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested)
{
    using var scope = host.Services.CreateScope();
    var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
    await processor.ProcessAsync(CancellationToken.None);

    await Task.Delay(TimeSpan.FromSeconds(5));
}