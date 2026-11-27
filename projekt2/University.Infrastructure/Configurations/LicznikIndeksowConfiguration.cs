using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Domain.Entities;

namespace University.Infrastructure.Configurations;

public class LicznikIndeksowConfiguration : IEntityTypeConfiguration<LicznikIndeksow>
{
    public void Configure(EntityTypeBuilder<LicznikIndeksow> builder)
    {
        builder.ToTable("LicznikiIndeksow");
        builder.HasKey(l => l.Prefix);
        builder.Property(l => l.Prefix).HasMaxLength(5);

        builder.HasData(
            new LicznikIndeksow { Prefix = "S", AktualnaWartosc = 1000 },
            new LicznikIndeksow { Prefix = "P", AktualnaWartosc = 100 });
    }
}
