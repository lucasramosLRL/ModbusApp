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

        builder.OwnsOne(d => d.Mqtt, mqtt =>
        {
            mqtt.Property(m => m.BrokerHost).HasColumnName("MqttBrokerHost").HasMaxLength(255);
            mqtt.Property(m => m.Port).HasColumnName("MqttPort");
            mqtt.Property(m => m.UseTls).HasColumnName("MqttUseTls");
            mqtt.Property(m => m.ClientId).HasColumnName("MqttClientId").HasMaxLength(128);
            mqtt.Property(m => m.Username).HasColumnName("MqttUsername").HasMaxLength(128);
            mqtt.Property(m => m.Password).HasColumnName("MqttPassword").HasMaxLength(256);
            mqtt.Property(m => m.TelemetryTopic).HasColumnName("MqttTelemetryTopic").HasMaxLength(255);
            mqtt.Property(m => m.CommandTopic).HasColumnName("MqttCommandTopic").HasMaxLength(255);
            mqtt.Property(m => m.ReplyTopic).HasColumnName("MqttReplyTopic").HasMaxLength(255);
        });

        builder.HasOne(d => d.DeviceModel)
               .WithMany(m => m.Devices)
               .HasForeignKey(d => d.DeviceModelId)
               .IsRequired(false);
    }
}
