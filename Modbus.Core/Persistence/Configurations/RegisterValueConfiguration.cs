using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Persistence.Configurations;

public class RegisterValueConfiguration : IEntityTypeConfiguration<RegisterValue>
{
    public void Configure(EntityTypeBuilder<RegisterValue> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RegisterType).HasConversion<string>();

        // ushort[] has no native SQLite representation — store as BLOB
        builder.Property(r => r.RawWords)
            .HasConversion(
                v => v.SelectMany(BitConverter.GetBytes).ToArray(),
                v => Enumerable.Range(0, v.Length / 2)
                               .Select(i => BitConverter.ToUInt16(v, i * 2))
                               .ToArray(),
                new ValueComparer<ushort[]>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (h, e) => HashCode.Combine(h, e.GetHashCode())),
                    v => v.ToArray()));

        // One latest value per register per device
        builder.HasIndex(r => new { r.DeviceId, r.Address, r.RegisterType }).IsUnique();

        builder.HasOne(r => r.Device)
               .WithMany(d => d.RegisterValues)
               .HasForeignKey(r => r.DeviceId);
    }
}
