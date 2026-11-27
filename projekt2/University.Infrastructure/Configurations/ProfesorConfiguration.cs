using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class ProfesorConfiguration : IEntityTypeConfiguration<Profesor>
{
    public void Configure(EntityTypeBuilder<Profesor> builder)
    {
        builder.ToTable("Profesorowie");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Imie).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Nazwisko).HasMaxLength(80).IsRequired();
        builder.Property(p => p.TytulNaukowy).HasMaxLength(40).IsRequired();
        builder.Property(p => p.IndeksUczelniany).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.IndeksUczelniany).IsUnique();

        builder.Property<DateTime>("OstatniaModyfikacja");

        builder.OwnsOne(p => p.AdresZamieszkania, adres =>
        {
            adres.Property(a => a.Ulica).HasMaxLength(100).HasColumnName("Ulica").IsRequired();
            adres.Property(a => a.Miasto).HasMaxLength(60).HasColumnName("Miasto").IsRequired();
            adres.Property(a => a.KodPocztowy).HasMaxLength(10).HasColumnName("KodPocztowy").IsRequired();
        });
        builder.Navigation(p => p.AdresZamieszkania).IsRequired();

        builder.HasOne(p => p.Gabinet)
            .WithOne(g => g.Profesor)
            .HasForeignKey<Gabinet>(g => g.ProfesorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
