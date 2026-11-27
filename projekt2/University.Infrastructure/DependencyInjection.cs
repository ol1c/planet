using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using University.Application.Abstractions;
using University.Infrastructure.Persistence;

namespace University.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<UniversityDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUniversityDbContext>(provider => provider.GetRequiredService<UniversityDbContext>());
        return services;
    }
}
