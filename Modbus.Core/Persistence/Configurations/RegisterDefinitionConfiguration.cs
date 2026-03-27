using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Persistence.Configurations;

public class RegisterDefinitionConfiguration : IEntityTypeConfiguration<RegisterDefinition>
{
    public void Configure(EntityTypeBuilder<RegisterDefinition> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Unit).HasMaxLength(20);

        // Store enums as strings for readability in the SQLite file
        builder.Property(r => r.RegisterType).HasConversion<string>();
        builder.Property(r => r.DataType).HasConversion<string>();
        builder.Property(r => r.WordOrder).HasConversion<string>();

        // Computed — not stored
        builder.Ignore(r => r.RegisterCount);
    }
}
