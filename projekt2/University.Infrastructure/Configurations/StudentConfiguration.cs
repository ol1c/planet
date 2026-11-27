using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Studenci");
        builder.HasKey(s => s.Id);

        builder.HasDiscriminator<string>("TypStudenta")
            .HasValue<Student>("Inzynierski")
            .HasValue<StudentStudiowMagisterskich>("Magisterski");
        builder.Property<string>("TypStudenta").HasMaxLength(20);

        builder.Property(s => s.Imie).HasMaxLength(60).IsRequired();
        builder.Property(s => s.Nazwisko).HasMaxLength(80).IsRequired();
        builder.Property(s => s.IndeksUczelniany).HasMaxLength(20).IsRequired();
        builder.HasIndex(s => s.IndeksUczelniany).IsUnique();

        builder.Property<DateTime>("OstatniaModyfikacja");

        builder.OwnsOne(s => s.AdresZamieszkania, adres =>
        {
            adres.Property(a => a.Ulica).HasMaxLength(100).HasColumnName("Ulica").IsRequired();
            adres.Property(a => a.Miasto).HasMaxLength(60).HasColumnName("Miasto").IsRequired();
            adres.Property(a => a.KodPocztowy).HasMaxLength(10).HasColumnName("KodPocztowy").IsRequired();
        });
        builder.Navigation(s => s.AdresZamieszkania).IsRequired();
    }
}
