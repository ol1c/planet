using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Domain.Entities;

namespace University.Infrastructure.Persistence;

public class UniversityDbContext : DbContext, IUniversityDbContext
{
    public DbSet<Student> Studenci => Set<Student>();
    public DbSet<StudentStudiowMagisterskich> StudenciMagisterscy => Set<StudentStudiowMagisterskich>();
    public DbSet<Profesor> Profesorowie => Set<Profesor>();
    public DbSet<Kurs> Kursy => Set<Kurs>();
    public DbSet<Wydzial> Wydzialy => Set<Wydzial>();
    public DbSet<Gabinet> Gabinety => Set<Gabinet>();
    public DbSet<Enrollment> Zapisy => Set<Enrollment>();
    public DbSet<LicznikIndeksow> LicznikiIndeksow => Set<LicznikIndeksow>();

    public UniversityDbContext(DbContextOptions<UniversityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UniversityDbContext).Assembly);
    }
}
