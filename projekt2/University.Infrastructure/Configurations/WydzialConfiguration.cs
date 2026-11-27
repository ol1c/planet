using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class WydzialConfiguration : IEntityTypeConfiguration<Wydzial>
{
    public void Configure(EntityTypeBuilder<Wydzial> builder)
    {
        builder.ToTable("Wydzialy");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Nazwa).HasMaxLength(150).IsRequired();
    }
}
