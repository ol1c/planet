using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using University.Application;
using University.Infrastructure;
using University.Infrastructure.Persistence;
using University.UI.Menu;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var connectionString = builder.Configuration.GetConnectionString("DefaultSqlServer")
    ?? UniversityDbContextFactory.DomyslnyConnectionString;

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddApplication();
builder.Services.AddSingleton<MenuGlowne>();

using var host = builder.Build();

Console.WriteLine("Aktualizacja schematu bazy danych (MigrateAsync)...");
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UniversityDbContext>();
    await dbContext.Database.MigrateAsync();
}
Console.WriteLine("Baza danych gotowa.");

await host.Services.GetRequiredService<MenuGlowne>().UruchomAsync();
