using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Persistence.Configurations;

public class ModbusDeviceConfiguration : IEntityTypeConfiguration<ModbusDevice>
{
    public void Configure(EntityTypeBuilder<ModbusDevice> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.TransportType).HasConversion<string>();

        builder.OwnsOne(d => d.Tcp, tcp =>
        {
            tcp.Property(t => t.IpAddress).HasColumnName("TcpIpAddress").HasMaxLength(50);
            tcp.Property(t => t.Port).HasColumnName("TcpPort");
        });

        builder.OwnsOne(d => d.Rtu, rtu =>
        {
            rtu.Property(r => r.PortName).HasColumnName("RtuPortName").HasMaxLength(50);
            rtu.Property(r => r.BaudRate).HasColumnName("RtuBaudRate");
            rtu.Property(r => r.DataBits).HasColumnName("RtuDataBits");
            rtu.Property(r => r.Parity).HasColumnName("RtuParity").HasConversion<string>();
            rtu.Property(r => r.StopBits).HasColumnName("RtuStopBits").HasConversion<string>();
        });

        builder.HasOne(d => d.DeviceModel)
               .WithMany(m => m.Devices)
               .HasForeignKey(d => d.DeviceModelId)
               .IsRequired(false);
    }
}
