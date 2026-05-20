using FluentAssertions;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;
using Modbus.Core.Polling;
using Modbus.Core.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Modbus.Core.Tests.Polling;

public sealed class PollingEngineTests : IAsyncLifetime
{
    private readonly IModbusServiceFactory _factory = Substitute.For<IModbusServiceFactory>();
    private readonly IModbusService _svc = Substitute.For<IModbusService>();
    private PollingEngine _engine = null!;

    public Task InitializeAsync()
    {
        _factory.Create(Arg.Any<ModbusDevice>()).Returns(_svc);
        _engine = new PollingEngine(_factory, TimeSpan.FromMilliseconds(50));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try { await _engine.StopAsync(); } catch { }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ModbusDevice MakeTcpDevice(DeviceModel? model = null) => new()
    {
        Id       = 1,
        Name     = "TCP Device",
        SlaveId  = 1,
        TransportType = TransportType.Tcp,
        Tcp      = new TcpConfig { IpAddress = "127.0.0.1", Port = 502 },
        DeviceModel   = model,
        DeviceModelId = model?.Id,
        IsActive = true,
    };

    private static ModbusDevice MakeRtuDevice(DeviceModel? model = null) => new()
    {
        Id       = 2,
        Name     = "RTU Device",
        SlaveId  = 1,
        TransportType = TransportType.Rtu,
        Rtu      = new RtuConfig { PortName = "COM1" },
        DeviceModel   = model,
        DeviceModelId = model?.Id,
        IsActive = true,
    };

    private static DeviceModel MakeModelWithSqpf() => new()
    {
        Id   = 1,
        Name = "Test Model",
        SqpfRegisterAddress = 2900,
        Registers = new List<RegisterDefinition>
        {
            new()
            {
                Id           = 1,
                Name         = "U0",
                Address      = 2,
                RegisterType = RegisterType.Input,
                DataType     = DataType.Float32,
                WordOrder    = WordOrder.UseSqpf,
                ScaleFactor  = 1.0,
            }
        }
    };

    // ── Lifecycle tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task AddDevice_StartAsync_FiresRegisterValuesUpdated()
    {
        var device = MakeTcpDevice(); // no model → heartbeat poll
        _engine.AddDevice(device);

        var tcs = new TaskCompletionSource<RegisterValuesUpdatedEventArgs>();
        _engine.RegisterValuesUpdated += (_, e) => tcs.TrySetResult(e);

        await _engine.StartAsync();

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Device.Should().Be(device);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PollDevice_ConnectThrows_FiresDeviceConnectionFailed()
    {
        var device = MakeTcpDevice();
        _svc.ConnectAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("device unreachable"));

        _engine.AddDevice(device);

        var tcs = new TaskCompletionSource<DeviceConnectionFailedEventArgs>();
        _engine.DeviceConnectionFailed += (_, e) => tcs.TrySetResult(e);

        await _engine.StartAsync();

        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Device.Should().Be(device);
        result.Exception.Should().BeOfType<TimeoutException>();
    }

    [Fact]
    public void RemoveDevice_AfterAdd_DisposesService()
    {
        var device = MakeTcpDevice();
        _engine.AddDevice(device);
        _engine.RemoveDevice(device.Id);

        _svc.Received(1).Dispose();
    }

    [Fact]
    public async Task StopAsync_AfterStart_CompletesWithinTimeout()
    {
        var device = MakeTcpDevice();
        // Block ConnectAsync until cancelled so polling hangs
        _svc.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>()));

        _engine.AddDevice(device);
        await _engine.StartAsync();

        var stopTask = _engine.StopAsync();
        var completed = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().BeSameAs(stopTask, "StopAsync should complete within 5 seconds");
    }

    // ── RTU gate tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task SuspendRtuPollingAsync_BlocksRtuDeviceUntilResumed()
    {
        var device = MakeRtuDevice();
        _engine.AddDevice(device);

        await _engine.SuspendRtuPollingAsync();
        await _engine.StartAsync();

        // Wait longer than multiple poll cycles — gate is held, so ConnectAsync should not be called
        await Task.Delay(200);
        await _svc.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>());

        // Resume and wait for at least one poll
        var tcs = new TaskCompletionSource();
        _engine.DeviceConnectionFailed  += (_, _) => tcs.TrySetResult();
        _engine.RegisterValuesUpdated   += (_, _) => tcs.TrySetResult();
        _engine.ResumeRtuPolling();

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _svc.Received().ConnectAsync(Arg.Any<CancellationToken>());
    }

    // ── SQPF tests ────────────────────────────────────────────────────────────

    // IEEE 754 2.0 = 0x40000000.
    // With SQPF 0x3210 (identity): words=[0x0000, 0x0040] → 2.0
    // With SQPF 0x2301 (CDAB):     words=[0x0000, 0x4000] → 2.0

    [Fact]
    public async Task PollDevice_SqpfReadFails_FallsBackTo0x3210()
    {
        var model  = MakeModelWithSqpf();
        var device = MakeTcpDevice(model);

        // SQPF register read → throw (fallback 0x3210 used)
        _svc.ReadHoldingRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException());

        // Input register returns words for 2.0 encoded with SQPF 0x3210
        _svc.ReadInputRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(new ushort[] { 0x0000, 0x0040 });

        _engine.AddDevice(device);
        var tcs = new TaskCompletionSource<RegisterValuesUpdatedEventArgs>();
        _engine.RegisterValuesUpdated += (_, e) => tcs.TrySetResult(e);

        await _engine.StartAsync();
        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Values.Should().HaveCount(1);
        result.Values[0].Value.Should().BeApproximately(2.0, 1e-4);
    }

    [Fact]
    public async Task PollDevice_SqpfReadSucceeds_UsesReturnedSqpfValue()
    {
        var model  = MakeModelWithSqpf();
        var device = MakeTcpDevice(model);

        // SQPF register → returns 0x2301 (CDAB byte order)
        _svc.ReadHoldingRegistersAsync(Arg.Any<byte>(), (ushort)2900, Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(new ushort[] { 0x2301 });

        // Input register returns words for 2.0 encoded with SQPF 0x2301
        _svc.ReadInputRegistersAsync(Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<ushort>(), Arg.Any<CancellationToken>())
            .Returns(new ushort[] { 0x0000, 0x4000 });

        _engine.AddDevice(device);
        var tcs = new TaskCompletionSource<RegisterValuesUpdatedEventArgs>();
        _engine.RegisterValuesUpdated += (_, e) => tcs.TrySetResult(e);

        await _engine.StartAsync();
        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Values.Should().HaveCount(1);
        result.Values[0].Value.Should().BeApproximately(2.0, 1e-4);
    }

    // ── GroupRegisters unit tests (internal method) ────────────────────────────

    [Fact]
    public void GroupRegisters_ContiguousInputRegisters_MergedIntoOneBlock()
    {
        var model = new DeviceModel { Name = "M", Id = 1 };
        var registers = new List<RegisterDefinition>
        {
            Reg(model, 0,  DataType.Float32, RegisterType.Input),  // addr 0, count 2
            Reg(model, 2,  DataType.Float32, RegisterType.Input),  // addr 2, count 2
            Reg(model, 4,  DataType.Float32, RegisterType.Input),  // addr 4, count 2
        };

        var blocks = PollingEngine.GroupRegisters(registers, RegisterType.Input).ToList();

        blocks.Should().HaveCount(1);
        blocks[0].Start.Should().Be(0);
        blocks[0].Count.Should().Be(6);  // covers addr 0-5
    }

    [Fact]
    public void GroupRegisters_FiltersToRequestedRegisterType()
    {
        var model = new DeviceModel { Name = "M", Id = 1 };
        var registers = new List<RegisterDefinition>
        {
            Reg(model, 0, DataType.UInt16, RegisterType.Holding),
            Reg(model, 1, DataType.UInt16, RegisterType.Input),
        };

        var inputBlocks   = PollingEngine.GroupRegisters(registers, RegisterType.Input).ToList();
        var holdingBlocks = PollingEngine.GroupRegisters(registers, RegisterType.Holding).ToList();

        inputBlocks.Should().HaveCount(1);
        inputBlocks[0].Registers.Should().HaveCount(1);
        inputBlocks[0].Registers[0].RegisterType.Should().Be(RegisterType.Input);

        holdingBlocks.Should().HaveCount(1);
        holdingBlocks[0].Registers[0].RegisterType.Should().Be(RegisterType.Holding);
    }

    [Fact]
    public void GroupRegisters_GapBeyondMax_SplitsIntoTwoBlocks()
    {
        var model = new DeviceModel { Name = "M", Id = 1 };
        var registers = new List<RegisterDefinition>
        {
            Reg(model, 0,  DataType.UInt16, RegisterType.Input),  // ends at addr 1
            Reg(model, 20, DataType.UInt16, RegisterType.Input),  // gap = 20-1 = 19 > maxGap 5
        };

        var blocks = PollingEngine.GroupRegisters(registers, RegisterType.Input).ToList();

        blocks.Should().HaveCount(2);
        blocks[0].Start.Should().Be(0);
        blocks[1].Start.Should().Be(20);
    }

    [Fact]
    public void GroupRegisters_GapWithinMax_MergedIntoOneBlock()
    {
        var model = new DeviceModel { Name = "M", Id = 1 };
        var registers = new List<RegisterDefinition>
        {
            Reg(model, 0, DataType.UInt16, RegisterType.Input),  // ends at 1
            Reg(model, 4, DataType.UInt16, RegisterType.Input),  // gap = 4-1 = 3 ≤ maxGap 5
        };

        var blocks = PollingEngine.GroupRegisters(registers, RegisterType.Input).ToList();

        blocks.Should().HaveCount(1);
        blocks[0].Start.Should().Be(0);
        blocks[0].Count.Should().Be(5); // covers 0-4
    }

    [Fact]
    public void GroupRegisters_EmptyList_ReturnsEmpty()
    {
        var blocks = PollingEngine.GroupRegisters([], RegisterType.Input).ToList();
        blocks.Should().BeEmpty();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static RegisterDefinition Reg(DeviceModel model, ushort address, DataType dataType, RegisterType regType) => new()
    {
        Name         = $"R{address}",
        Address      = address,
        DataType     = dataType,
        RegisterType = regType,
        WordOrder    = WordOrder.BigEndian,
        ScaleFactor  = 1.0,
        DeviceModel  = model,
    };
}
