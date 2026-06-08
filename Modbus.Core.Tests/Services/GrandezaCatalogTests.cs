using FluentAssertions;
using Modbus.Core.Services;

namespace Modbus.Core.Tests.Services;

public sealed class GrandezaCatalogTests
{
    [Fact]
    public void ForDeviceCode_Ks3000_ContainsExpectedMinCount()
    {
        var list = GrandezaCatalog.ForDeviceCode(0xF2);
        list.Should().HaveCountGreaterThan(60);
    }

    [Fact]
    public void ForDeviceCode_Ks3000_U0_HasMqttId2()
    {
        var u0 = GrandezaCatalog.ForDeviceCode(0xF2).Single(g => g.Code == "U0");
        u0.MqttId.Should().Be(2);
    }

    [Fact]
    public void ForDeviceCode_Konect120_MirrorsKs3000()
    {
        var ks  = GrandezaCatalog.ForDeviceCode(0xF2);
        var k120 = GrandezaCatalog.ForDeviceCode(0xF3);
        k120.Should().BeEquivalentTo(ks);
    }

    [Fact]
    public void ForDeviceCode_UnknownModel_ReturnsEmpty()
    {
        GrandezaCatalog.ForDeviceCode(0xAA).Should().BeEmpty();
        GrandezaCatalog.ForDeviceCode(null).Should().BeEmpty();
    }

    [Fact]
    public void ForDeviceCode_AllMqttIdsUnique()
    {
        var list = GrandezaCatalog.ForDeviceCode(0xF2);
        list.Select(g => g.MqttId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Limit_DefaultsTo50()
    {
        GrandezaCatalog.Limit(0xF2, null).Should().Be(50);
        GrandezaCatalog.Limit(0xF3, 20).Should().Be(50);
    }
}
