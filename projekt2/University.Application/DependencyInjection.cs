using Microsoft.Extensions.DependencyInjection;
using University.Application.Generators;
using University.Application.Services;

namespace University.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IndeksUczelnianyService>();
        services.AddScoped<StudentService>();
        services.AddScoped<ProfesorService>();
        services.AddScoped<WydzialService>();
        services.AddScoped<KursService>();
        services.AddScoped<LicznikIndeksowService>();
        services.AddScoped<UniversityQueryService>();
        services.AddScoped<UniversityBogusGenerator>();
        return services;
    }
}
