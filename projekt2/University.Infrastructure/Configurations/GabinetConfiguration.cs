using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class GabinetConfiguration : IEntityTypeConfiguration<Gabinet>
{
    public void Configure(EntityTypeBuilder<Gabinet> builder)
    {
        builder.ToTable("Gabinety");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.NumerPokoju).HasMaxLength(20).IsRequired();
        builder.Property(g => g.Budynek).HasMaxLength(40).IsRequired();
    }
}
