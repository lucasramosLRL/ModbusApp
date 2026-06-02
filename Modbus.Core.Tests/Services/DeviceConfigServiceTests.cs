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

    // ── WriteMultipleRegistersAsync (FC16) ────────────────────────────────────

    [Fact]
    public async Task WriteMultipleRegistersAsync_HoldingAddress_CallsService()
    {
        var values = new ushort[] { 0x1111, 0x2222, 0x3333 };

        await _service.WriteMultipleRegistersAsync(Device, 40010, values);

        await _svc.Received(1).WriteMultipleRegistersAsync(
            Device.SlaveId,
            (ushort)(40010 - 40001), // raw 9
            Arg.Is<ushort[]>(a => a.Length == 3 && a[0] == 0x1111 && a[1] == 0x2222 && a[2] == 0x3333),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteMultipleRegistersAsync_InputAddress_ThrowsArgumentException()
    {
        var act = async () => await _service.WriteMultipleRegistersAsync(Device, 30001, [0x0000]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*4xxxx*");
    }

    [Fact]
    public async Task WriteMultipleRegistersAsync_Over22Words_SplitsIntoChunks()
    {
        // 35-word write must split into 22 + 13 (KS-3000 FC16 limit)
        var values = new ushort[35];
        for (int i = 0; i < 35; i++) values[i] = (ushort)(0x1000 + i);

        await _service.WriteMultipleRegistersAsync(Device, 43461, values);

        // First chunk: 22 words starting at raw (43461-40001=3460)
        await _svc.Received(1).WriteMultipleRegistersAsync(
            Device.SlaveId,
            (ushort)3460,
            Arg.Is<ushort[]>(a => a.Length == 22 && a[0] == 0x1000 && a[21] == 0x1015),
            Arg.Any<CancellationToken>());

        // Second chunk: 13 words starting at raw (3460+22=3482)
        await _svc.Received(1).WriteMultipleRegistersAsync(
            Device.SlaveId,
            (ushort)3482,
            Arg.Is<ushort[]>(a => a.Length == 13 && a[0] == 0x1016 && a[12] == 0x1022),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteMultipleRegistersAsync_EmptyValues_DoesNothing()
    {
        await _service.WriteMultipleRegistersAsync(Device, 40010, []);

        await _svc.DidNotReceive().WriteMultipleRegistersAsync(
            Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort[]>(), Arg.Any<CancellationToken>());
    }

    // ── SendCoilResetAsync (FC05 reset coil, wire address 5) ──────────────────

    [Fact]
    public async Task SendCoilResetAsync_WritesResetCoilTrue()
    {
        // Wire address 5 — matches the captured reset frame (02 05 00 05 FF 00 ...).
        await _service.SendCoilResetAsync(Device);

        await _svc.Received(1).WriteSingleCoilAsync(
            Device.SlaveId,
            (ushort)5,
            true,
            Arg.Any<CancellationToken>());
    }

    // ── WriteBatchAsync (resume-after-reboot semantics) ──────────────────────

    [Fact]
    public async Task WriteBatchAsync_AllOpsSucceed_ReturnsCompletedWithNoRemaining()
    {
        // Service uses IsConnected to decide whether to call ConnectAsync — true keeps it happy.
        _svc.IsConnected.Returns(true);
        var batch = new List<RegisterWrite>
        {
            new(40005, [ (ushort)11 ]),
            new(40010, [ (ushort)0x1111, (ushort)0x2222 ]),
        };

        var result = await _service.WriteBatchAsync(Device, batch, sendCoilResetAfter: false);

        result.Completed.Should().Be(2);
        result.Remaining.Should().BeEmpty();
        result.DeviceRebooted.Should().BeFalse();
        result.CoilResetSent.Should().BeFalse(); // sendCoilResetAfter was false
    }

    [Fact]
    public async Task WriteBatchAsync_IoFailurePostSuccess_ReturnsRemainingOps()
    {
        _svc.IsConnected.Returns(true);
        // First write succeeds (default Returns is Task.CompletedTask). Second write throws IOException.
        _svc.WriteSingleRegisterAsync(
                Arg.Any<byte>(),
                Arg.Is<ushort>(addr => addr == (ushort)(40010 - 40001)),
                Arg.Any<ushort>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new System.IO.IOException("device dropped socket"));

        var batch = new List<RegisterWrite>
        {
            new(40005, [ (ushort)11 ]),   // succeeds
            new(40010, [ (ushort)22 ]),   // fails (triggers reboot detection)
            new(40020, [ (ushort)33 ]),   // never attempted — should be in Remaining
        };

        var result = await _service.WriteBatchAsync(Device, batch, sendCoilResetAfter: true);

        result.Completed.Should().Be(1, "the first op should have committed before the IOException");
        result.DeviceRebooted.Should().BeTrue();
        result.Remaining.Should().HaveCount(2);
        result.Remaining[0].ModiconAddress.Should().Be((ushort)40010);
        result.Remaining[1].ModiconAddress.Should().Be((ushort)40020);
        result.CoilResetSent.Should().BeFalse("coil reset was deferred because the reboot interrupted the batch");
    }

    [Fact]
    public async Task WriteBatchAsync_AllOpsSucceedWithCoilReset_ReportsCoilSent()
    {
        _svc.IsConnected.Returns(true);
        var batch = new List<RegisterWrite> { new(43461, [ (ushort)0x4142 ]) };

        var result = await _service.WriteBatchAsync(Device, batch, sendCoilResetAfter: true);

        result.Completed.Should().Be(1);
        result.Remaining.Should().BeEmpty();
        result.DeviceRebooted.Should().BeFalse();
        result.CoilResetSent.Should().BeTrue();
        await _svc.Received(1).WriteSingleCoilAsync(
            Device.SlaveId, (ushort)5, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteBatchAsync_IotBufferCoil_SentBeforeResetCoil()
    {
        // When IoT grandezas / send interval change, the model-specific IoT buffer reset coil
        // (here wire 90 = KS-3000) must be pulsed BEFORE the commit/reset coil (wire 5).
        _svc.IsConnected.Returns(true);
        _service.IotBufferResetSettleMs = 0; // skip the multi-second settle wait in tests

        var coilCalls = new List<ushort>();
        _svc.When(s => s.WriteSingleCoilAsync(
                Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(ci => coilCalls.Add(ci.ArgAt<ushort>(1)));

        var batch = new List<RegisterWrite> { new(42101, [ (ushort)5 ]) }; // send interval

        var result = await _service.WriteBatchAsync(
            Device, batch, sendCoilResetAfter: true, iotBufferResetCoil: 90);

        result.Completed.Should().Be(1);
        result.CoilResetSent.Should().BeTrue();
        coilCalls.Should().Equal((ushort)90, (ushort)5); // buffer coil first, then reset coil
    }

    [Fact]
    public async Task WriteBatchAsync_NoIotBufferCoil_OnlySendsResetCoil()
    {
        _svc.IsConnected.Returns(true);
        var coilCalls = new List<ushort>();
        _svc.When(s => s.WriteSingleCoilAsync(
                Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(ci => coilCalls.Add(ci.ArgAt<ushort>(1)));

        var batch = new List<RegisterWrite> { new(40005, [ (ushort)11 ]) };

        await _service.WriteBatchAsync(Device, batch, sendCoilResetAfter: true); // iotBufferResetCoil null

        coilCalls.Should().Equal((ushort)5); // only the reset coil
    }

    [Fact]
    public async Task WriteBatchAsync_CoilResetTimesOut_StillReportsCoilSent()
    {
        // RTU: the meter applies the reset coil and reboots without echoing the FC05 response,
        // so the transport throws TimeoutException after 1s. That must be treated as success.
        _svc.IsConnected.Returns(true);
        _svc.WriteSingleCoilAsync(
                Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("RTU device did not respond"));

        var batch = new List<RegisterWrite> { new(40005, [ (ushort)11 ]) };

        var result = await _service.WriteBatchAsync(Device, batch, sendCoilResetAfter: true);

        result.Completed.Should().Be(1);
        result.DeviceRebooted.Should().BeFalse();
        result.CoilResetSent.Should().BeTrue("a TimeoutException on the commit coil means the meter rebooted without echoing");
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
