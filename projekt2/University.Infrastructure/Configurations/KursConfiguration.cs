using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class KursConfiguration : IEntityTypeConfiguration<Kurs>
{
    public void Configure(EntityTypeBuilder<Kurs> builder)
    {
        builder.ToTable("Kursy");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Nazwa).HasMaxLength(150).IsRequired();
        builder.Property(k => k.KodKursu).HasMaxLength(30).IsRequired();

        builder.HasOne(k => k.Wydzial)
            .WithMany(w => w.Kursy)
            .HasForeignKey(k => k.WydzialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(k => k.Prowadzacy)
            .WithMany(p => p.ProwadzoneKursy)
            .HasForeignKey(k => k.ProwadzacyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(k => k.WymaganiaWstepne)
            .WithMany(k => k.KursyZalezne)
            .UsingEntity<Dictionary<string, object>>(
                "WymaganiaWstepneKursow",
                wymaganie => wymaganie.HasOne<Kurs>().WithMany().HasForeignKey("WymaganieWstepneId")
                    .OnDelete(DeleteBehavior.Restrict),
                kurs => kurs.HasOne<Kurs>().WithMany().HasForeignKey("KursId")
                    .OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("KursId", "WymaganieWstepneId"));
    }
}
