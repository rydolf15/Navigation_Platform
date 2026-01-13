using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NavigationPlatform.Application.Abstractions.Messaging;
using NavigationPlatform.Application.Abstractions.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Analytics;
using NavigationPlatform.Infrastructure.Persistence;
using NavigationPlatform.Infrastructure.Persistence.Journeys;
using NavigationPlatform.Infrastructure.Persistence.Repositories;

namespace NavigationPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(connectionString));

        services.AddDbContext<AnalyticsDbContext>(o =>
            o.UseNpgsql(connectionString));

        services.AddScoped<IJourneyRepository, JourneyRepository>();
        services.AddScoped<IJourneyReadRepository, JourneyReadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}