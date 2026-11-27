using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class StudentStudiowMagisterskichConfiguration : IEntityTypeConfiguration<StudentStudiowMagisterskich>
{
    public void Configure(EntityTypeBuilder<StudentStudiowMagisterskich> builder)
    {
        builder.Property(m => m.TematPracyDyplomowej).HasMaxLength(300);

        builder.HasOne(m => m.Promotor)
            .WithMany(p => p.Dyplomanci)
            .HasForeignKey(m => m.PromotorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
