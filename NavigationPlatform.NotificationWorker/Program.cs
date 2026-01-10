using Microsoft.EntityFrameworkCore;
using NavigationPlatform.NotificationWorker.Messaging;
using NavigationPlatform.NotificationWorker.Persistence;
using NavigationPlatform.NotificationWorker.Processing;
using Serilog;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// ---------- Logging ----------
builder.Services.AddSerilog(c => c.WriteTo.Console());

// ---------- Database ----------
builder.Services.AddDbContext<NotificationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ---------- Messaging ----------
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<IUserPresence, RedisUserPresence>(); // or stub
builder.Services.AddSingleton<SignalRNotifier>(_ =>
    new SignalRNotifier(builder.Configuration["SignalR:HubUrl"]!));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// ---------- Processing ----------
builder.Services.AddScoped<NotificationOutboxProcessor>();

// ---------- Worker ----------
builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();
host.Run();
