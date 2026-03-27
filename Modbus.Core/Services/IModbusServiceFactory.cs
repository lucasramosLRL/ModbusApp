using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Services;

public interface IModbusServiceFactory
{
    IModbusService Create(ModbusDevice device);
}
