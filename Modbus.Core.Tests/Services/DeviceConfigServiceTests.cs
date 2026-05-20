using FluentAssertions;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Exceptions;
using Modbus.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Modbus.Core.Tests.Services;

public sealed class DeviceConfigServiceTests
{
    private readonly IModbusServiceFactory _factory = Substitute.For<IModbusServiceFactory>();
    private readonly IModbusService _svc = Substitute.For<IModbusService>();
    private readonly DeviceConfigService _service;

    private static readonly ModbusDevice Device = new()
    {
        Id            = 1,
        Name          = "Test Device",
        SlaveId       = 1,
        TransportType = TransportType.Tcp,
        Tcp           = new TcpConfig { IpAddress = "127.0.0.1", Port = 502 },
    };

    public DeviceConfigServiceTests()
    {
        _factory.Create(Arg.Any<ModbusDevice>()).Returns(_svc);
        _service = new DeviceConfigService(_factory);
    }

    // ── FC03 / FC04 routing ───────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_HoldingField_IssuesFC03Request()
    {
        RegisterField field = 40005; // 4xxxx → holding
        _svc.ReadHoldingRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(new ushort[] { 0x1234 });

        await _service.ReadAsync(Device, [field]);

        await _svc.Received(1).ReadHoldingRegistersAsync(
            Device.SlaveId,
            (ushort)(40005 - 40001),  // raw 0-based address = 4
            1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadAsync_InputField_IssuesFC04Request()
    {
        RegisterField field = 30001; // 3xxxx → input
        _svc.ReadInputRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(new ushort[] { 0xABCD });

        await _service.ReadAsync(Device, [field]);

        await _svc.Received(1).ReadInputRegistersAsync(
            Device.SlaveId,
            (ushort)(30001 - 30001),  // raw 0-based address = 0
            1,
            Arg.Any<CancellationToken>());
    }

    // ── Block merging — bit-fields sharing same address ───────────────────────

    [Fact]
    public async Task ReadAsync_TwoBitFieldsSameAddress_IssuedAsOneSingleRead()
    {
        var sntp    = new RegisterField(40007, BitOffset: 12, BitWidth: 1);
        var current = new RegisterField(40007, BitOffset: 15, BitWidth: 1);

        _svc.ReadHoldingRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(new ushort[] { 0x9000 });

        await _service.ReadAsync(Device, [sntp, current]);

        await _svc.Received(1).ReadHoldingRegistersAsync(
            Arg.Any<byte>(),
            Arg.Any<ushort>(),
            Arg.Any<ushort>(),
            Arg.Any<CancellationToken>());
    }

    // ── Large field splitting (> 32 words) ────────────────────────────────────

    [Fact]
    public async Task ReadAsync_StringFieldOver32Words_SplitsIntoTwoChunks()
    {
        // 35-word string field → first chunk = 32 words, second = 3 words
        var bigField = new RegisterField(43461, WordCount: 35);

        // Return an array large enough for any chunk the service might request
        _svc.ReadHoldingRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ushort[32]));

        await _service.ReadAsync(Device, [bigField]);

        // Expect exactly 2 ReadHoldingRegisters calls
        await _svc.Received(2).ReadHoldingRegistersAsync(
            Arg.Any<byte>(),
            Arg.Any<ushort>(),
            Arg.Any<ushort>(),
            Arg.Any<CancellationToken>());
    }

    // ── Retry logic ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_TransientError_RetriesUpTo3Times()
    {
        RegisterField field = 40001;
        int callCount = 0;

        // When...Do runs before the configured return value — throw on first 2 calls
        _svc.When(s => s.ReadHoldingRegistersAsync(
                Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                callCount++;
                if (callCount < 3)
                    throw new TimeoutException("transient");
            });
        _svc.ReadHoldingRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ushort[] { 0xABCD }));

        var result = await _service.ReadAsync(Device, [field]);

        callCount.Should().Be(3, "should have retried twice (3 total attempts)");
        result.FailedBlocks.Should().BeEmpty();
        result.Values.Should().ContainKey(40001);
    }

    [Fact]
    public async Task ReadAsync_ModbusProtocolException_NoRetry_BlockMarkedFailed()
    {
        RegisterField field = 40001;
        int callCount = 0;

        _svc.When(s => s.ReadHoldingRegistersAsync(
                Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                callCount++;
                throw new ModbusProtocolException(FunctionCode.ReadHoldingRegisters, ModbusExceptionCode.IllegalDataAddress);
            });
        _svc.ReadHoldingRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<ushort>())); // unreachable — Do always throws

        var result = await _service.ReadAsync(Device, [field]);

        callCount.Should().Be(1, "ModbusProtocolException must not be retried");
        result.FailedBlocks.Should().HaveCount(1);
    }

    // ── Partial failure resilience ────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_OneBlockFails_OtherBlockSucceeds()
    {
        // Two different address fields — each is an independent block
        RegisterField goodField = 40001;
        RegisterField badField  = 40005;

        // NSubstitute requires all-or-nothing on matchers — use Arg.Is<T> for specific values
        _svc.ReadHoldingRegistersAsync(
                Arg.Any<byte>(),
                Arg.Is<ushort>(0),  // raw 0 = 40001 - 40001
                Arg.Any<ushort>(),
                Arg.Any<CancellationToken>())
            .Returns(new ushort[] { 0x1234 });

        _svc.ReadHoldingRegistersAsync(
                Arg.Any<byte>(),
                Arg.Is<ushort>(4),  // raw 4 = 40005 - 40001
                Arg.Any<ushort>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException());

        var result = await _service.ReadAsync(Device, [goodField, badField]);

        result.Values.Should().ContainKey(40001, "good block should be in result");
        result.Values.Should().NotContainKey(40005, "failed block should not be in result");
        result.FailedBlocks.Should().HaveCount(1);
    }

    // ── WriteAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_HoldingAddress_CallsWriteSingleRegister()
    {
        await _service.WriteAsync(Device, 40005, 0x1234);

        await _svc.Received(1).WriteSingleRegisterAsync(
            Device.SlaveId,
            (ushort)(40005 - 40001), // raw 4
            0x1234,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_InputAddress_ThrowsArgumentException()
    {
        var act = async () => await _service.WriteAsync(Device, 30001, 0x0000);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*4xxxx*");
    }

    // ── Empty fields list ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_EmptyFieldList_ReturnsEmptyResult()
    {
        var result = await _service.ReadAsync(Device, []);

        result.Values.Should().BeEmpty();
        result.FailedBlocks.Should().BeEmpty();
        await _svc.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>());
    }
}
