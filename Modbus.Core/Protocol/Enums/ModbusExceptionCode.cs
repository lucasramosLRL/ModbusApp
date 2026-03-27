namespace Modbus.Core.Protocol.Enums;

public enum ModbusExceptionCode : byte
{
    IllegalFunction                      = 0x01,
    IllegalDataAddress                   = 0x02,
    IllegalDataValue                     = 0x03,
    ServerDeviceFailure                  = 0x04,
    Acknowledge                          = 0x05,
    ServerDeviceBusy                     = 0x06,
    MemoryParityError                    = 0x08,
    GatewayPathUnavailable               = 0x0A,
    GatewayTargetDeviceFailedToRespond   = 0x0B
}
