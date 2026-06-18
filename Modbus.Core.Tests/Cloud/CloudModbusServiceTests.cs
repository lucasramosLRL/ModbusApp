using FluentAssertions;
using Modbus.Core.Cloud;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using NSubstitute;

namespace Modbus.Core.Tests.Cloud;

public class CloudModbusServiceTests
{
    private readonly ICloudCommandService _commands = Substitute.For<ICloudCommandService>();
    private readonly ModbusDevice _device = new()
    {
        Id = 3, Name = "Cloud", SlaveId = 1, TransportType = TransportType.MqttCloud
    };

    private CloudModbusService CreateSut() => new(_device, _commands);

    [Fact]
    public async Task ReadHoldingRegisters_DelegatesToCommandService_AsHolding()
    {
        _commands.ReadRegistersAsync(_device, RegisterType.Holding, (ushort)10, (ushort)2, Arg.Any<CancellationToken>())
            .Returns([0x1234, 0x5678]);

        var result = await CreateSut().ReadHoldingRegistersAsync(slaveId: 1, startAddress: 10, quantity: 2);

        result.Should().Equal(0x1234, 0x5678);
    }

    [Fact]
    public async Task ReadInputRegisters_DelegatesToCommandService_AsInput()
    {
        await CreateSut().ReadInputRegistersAsync(slaveId: 1, startAddress: 5, quantity: 4);

        await _commands.Received(1).ReadRegistersAsync(
            _device, RegisterType.Input, (ushort)5, (ushort)4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteSingleRegister_SendsSingleValueWriteCommand()
    {
        await CreateSut().WriteSingleRegisterAsync(slaveId: 1, address: 200, value: 99);

        await _commands.Received(1).WriteRegistersAsync(
            _device, (ushort)200, Arg.Is<ushort[]>(v => v.Length == 1 && v[0] == 99), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteMultipleRegisters_ForwardsValues()
    {
        var values = new ushort[] { 1, 2, 3 };

        await CreateSut().WriteMultipleRegistersAsync(slaveId: 1, startAddress: 300, values: values);

        await _commands.Received(1).WriteRegistersAsync(_device, (ushort)300, values, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportSlaveId_IsNotSupportedOverCloud()
    {
        var act = () => CreateSut().ReportSlaveIdAsync(1);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Connect_SetsIsConnected()
    {
        var sut = CreateSut();
        sut.IsConnected.Should().BeFalse();

        await sut.ConnectAsync();
        sut.IsConnected.Should().BeTrue();

        await sut.DisconnectAsync();
        sut.IsConnected.Should().BeFalse();
    }
}
