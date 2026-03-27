using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Persistence.Configurations;

public class DeviceModelConfiguration : IEntityTypeConfiguration<DeviceModel>
{
    public void Configure(EntityTypeBuilder<DeviceModel> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Manufacturer).HasMaxLength(100);
        builder.Property(m => m.DeviceCode).IsRequired(false);

        builder.HasMany(m => m.Registers)
               .WithOne(r => r.DeviceModel)
               .HasForeignKey(r => r.DeviceModelId);
    }
}
