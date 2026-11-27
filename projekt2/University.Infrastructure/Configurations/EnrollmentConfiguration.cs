using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Zapisy");
        builder.HasKey(e => new { e.StudentId, e.KursId });
        builder.Property(e => e.Semestr).HasMaxLength(15).IsRequired();

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Zapisy)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Kurs)
            .WithMany(k => k.Zapisy)
            .HasForeignKey(e => e.KursId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
