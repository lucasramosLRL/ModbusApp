using FluentAssertions;
using Modbus.Core.Services;

namespace Modbus.Core.Tests.Services;

public sealed class DeviceConfigProfileRegistryTests
{
    private const byte Ks3000 = 0xF2;
    private const byte Konect120 = 0xF3;

    [Theory]
    [InlineData(59, (ushort)90)]  // v5.9 — no mass memory → legacy coil 91 (wire 90)
    [InlineData(60, (ushort)79)]  // v6.0 — mass memory introduced → coil 80 (wire 79)
    [InlineData(61, (ushort)79)]  // v6.1 — mass memory → wire 79
    public void GetIotBufferResetCoil_Ks3000_DependsOnFirmware(byte firmware, ushort expected)
    {
        DeviceConfigProfileRegistry.GetIotBufferResetCoil(Ks3000, firmware)
            .Should().Be(expected);
    }

    [Fact]
    public void GetIotBufferResetCoil_Ks3000_UnknownFirmware_FallsBackToLegacyCoil()
    {
        DeviceConfigProfileRegistry.GetIotBufferResetCoil(Ks3000, null)
            .Should().Be(90);
    }

    [Fact]
    public void GetIotBufferResetCoil_Konect120_IsFixedRegardlessOfFirmware()
    {
        DeviceConfigProfileRegistry.GetIotBufferResetCoil(Konect120, null).Should().Be(79);
        DeviceConfigProfileRegistry.GetIotBufferResetCoil(Konect120, 60).Should().Be(79);
    }

    [Fact]
    public void GetIotBufferResetCoil_UnknownModel_ReturnsNull()
    {
        DeviceConfigProfileRegistry.GetIotBufferResetCoil(0x00, 60).Should().BeNull();
        DeviceConfigProfileRegistry.GetIotBufferResetCoil(null, null).Should().BeNull();
    }
}
