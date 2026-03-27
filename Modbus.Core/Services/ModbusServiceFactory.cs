using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Protocol.Framing;
using Modbus.Core.Protocol.Rtu;
using Modbus.Core.Protocol.Tcp;
using Modbus.Core.Transport;
using Modbus.Core.Transport.Rtu;
using Modbus.Core.Transport.Tcp;

namespace Modbus.Core.Services;

public class ModbusServiceFactory : IModbusServiceFactory
{
    public IModbusService Create(ModbusDevice device)
    {
        var (transport, builder, parser) = device.TransportType switch
        {
            TransportType.Tcp => CreateTcp(device),
            TransportType.Rtu => CreateRtu(device),
            _ => throw new ArgumentOutOfRangeException(nameof(device), device.TransportType, "Unsupported transport type.")
        };
        return new ModbusService(transport, builder, parser);
    }

    private static (IModbusTransport, IModbusFrameBuilder, IModbusFrameParser) CreateTcp(ModbusDevice device)
    {
        if (device.Tcp is null)
            throw new InvalidOperationException($"Device '{device.Name}' is configured as TCP but has no TcpConfig.");

        return (new TcpModbusTransport(device.Tcp),
                new ModbusTcpFrameBuilder(),
                new ModbusTcpFrameParser());
    }

    private static (IModbusTransport, IModbusFrameBuilder, IModbusFrameParser) CreateRtu(ModbusDevice device)
    {
        if (device.Rtu is null)
            throw new InvalidOperationException($"Device '{device.Name}' is configured as RTU but has no RtuConfig.");

        return (new RtuModbusTransport(device.Rtu),
                new ModbusRtuFrameBuilder(),
                new ModbusRtuFrameParser());
    }
}
