using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace University.Infrastructure.Persistence;

public class UniversityDbContextFactory : IDesignTimeDbContextFactory<UniversityDbContext>
{
    public const string DomyslnyConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=UniversityCleanDb;Trusted_Connection=True";

    public UniversityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UniversityDbContext>();
        optionsBuilder.UseSqlServer(DomyslnyConnectionString);
        return new UniversityDbContext(optionsBuilder.Options);
    }
}
