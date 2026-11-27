using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using University.Domain.Entities;

namespace University.Application.Abstractions;

public interface IUniversityDbContext
{
    DbSet<Student> Studenci { get; }
    DbSet<StudentStudiowMagisterskich> StudenciMagisterscy { get; }
    DbSet<Profesor> Profesorowie { get; }
    DbSet<Kurs> Kursy { get; }
    DbSet<Wydzial> Wydzialy { get; }
    DbSet<Gabinet> Gabinety { get; }
    DbSet<Enrollment> Zapisy { get; }
    DbSet<LicznikIndeksow> LicznikiIndeksow { get; }

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
