using FluentAssertions;
using Modbus.Core.Cloud;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Tests.Cloud;

public class JsonTelemetryPayloadMapperTests
{
    private readonly JsonTelemetryPayloadMapper _mapper = new();

    private static ModbusDevice MakeDevice(params RegisterDefinition[] registers) => new()
    {
        Id            = 7,
        Name          = "Cloud Meter",
        SlaveId       = 1,
        TransportType = TransportType.MqttCloud,
        SerialNumber  = 12345,
        DeviceModel   = new DeviceModel { Id = 1, Name = "KS-3000", Registers = registers }
    };

    private static RegisterDefinition Def(string name, ushort address, RegisterType type = RegisterType.Input) =>
        new() { Name = name, Address = address, RegisterType = type, DataType = DataType.Float32 };

    [Fact]
    public void Map_ReadsKsDataPayload_ArrayWithMetadata()
    {
        var device = MakeDevice(Def("U0", 2), Def("I0", 16), Def("P0", 34));
        const string json =
            """[{ "variable":"data", "time":"2026-04-06 14:45:00", "metadata":{ "U0":201.80, "I0":1.25, "P0":250.0 } }]""";

        var values = _mapper.Map(device, json, DateTime.UtcNow);

        values.Should().HaveCount(3);
        values.Should().ContainSingle(v => v.Address == 2 && v.Value == 201.80);
        values.Should().ContainSingle(v => v.Address == 16 && v.Value == 1.25);
        values.Should().OnlyContain(v => v.DeviceId == 7);
    }

    [Fact]
    public void Map_UsesTimeFieldFromPayload()
    {
        var device = MakeDevice(Def("U0", 2));
        const string json = """[{ "variable":"data", "time":"2026-04-06 14:45:00", "metadata":{ "U0":201.8 } }]""";

        var values = _mapper.Map(device, json, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        values.Single().Timestamp.Should().Be(new DateTime(2026, 4, 6, 14, 45, 0));
    }

    [Fact]
    public void Map_AliasesTelemetryFieldNamesToRegisterNames()
    {
        // Telemetry uses F1/EA/ER, register map uses Freq/EA+/ER+.
        var device = MakeDevice(Def("Freq", 26), Def("EA+", 200), Def("ER+", 202));
        const string json =
            """[{ "variable":"data", "metadata":{ "F1":59.99, "EA":12.5, "ER":3.4 } }]""";

        var values = _mapper.Map(device, json, DateTime.UtcNow);

        values.Should().ContainSingle(v => v.Address == 26 && v.Value == 59.99);
        values.Should().ContainSingle(v => v.Address == 200 && v.Value == 12.5);
        values.Should().ContainSingle(v => v.Address == 202 && v.Value == 3.4);
    }

    [Fact]
    public void Map_IgnoresLogMessages()
    {
        var device = MakeDevice(Def("U0", 2));
        const string json = """{ "param":"log", "ID":"4002892", "msg":"Connection Started" }""";

        _mapper.Map(device, json, DateTime.UtcNow).Should().BeEmpty();
    }

    [Fact]
    public void Map_IgnoresUnknownMetadataFields()
    {
        var device = MakeDevice(Def("U0", 2));
        const string json = """[{ "variable":"data", "metadata":{ "U0":230, "CE":1, "LSTS":0 } }]""";

        _mapper.Map(device, json, DateTime.UtcNow).Should().ContainSingle(v => v.Address == 2 && v.Value == 230);
    }

    [Fact]
    public void Map_ReturnsEmpty_WhenModelHasNoRegisters()
    {
        var device = new ModbusDevice { Name = "No Model", SlaveId = 1, TransportType = TransportType.MqttCloud };
        _mapper.Map(device, """[{ "variable":"data", "metadata":{ "U0":1 } }]""", DateTime.UtcNow).Should().BeEmpty();
    }

    [Fact]
    public void MapReadings_ReturnsOnlyPublishedFields_InPayloadOrder()
    {
        // Model defines many quantities; the payload publishes only three.
        var device = MakeDevice(Def("U0", 2), Def("I0", 16), Def("P0", 34), Def("Q0", 42));
        const string json =
            """[{ "variable":"data", "metadata":{ "U0":201.8, "I0":1.25, "P0":250.0 } }]""";

        var readings = _mapper.MapReadings(device, json);

        readings.Select(r => r.Code).Should().Equal("U0", "I0", "P0");
        readings.Should().OnlyContain(r => r.Definition != null);
    }

    [Fact]
    public void MapReadings_SurfacesUnknownFields_WithNullDefinition()
    {
        // "CE" has no register definition — it must still appear, carrying its raw code.
        var device = MakeDevice(Def("U0", 2));
        const string json = """[{ "variable":"data", "metadata":{ "U0":230, "CE":97 } }]""";

        var readings = _mapper.MapReadings(device, json);

        readings.Should().ContainSingle(r => r.Code == "U0" && r.Definition!.Address == 2);
        readings.Should().ContainSingle(r => r.Code == "CE" && r.Value == 97 && r.Definition == null);
    }

    [Fact]
    public void MapReadings_ResolvesAliasedField_KeepsRawCode()
    {
        // Telemetry "F1" resolves to the "Freq" definition, but the reading keeps the payload code.
        var device = MakeDevice(Def("Freq", 26));
        const string json = """[{ "variable":"data", "metadata":{ "F1":59.98 } }]""";

        var reading = _mapper.MapReadings(device, json).Single();

        reading.Code.Should().Be("F1");
        reading.Definition!.Name.Should().Be("Freq");
        reading.Definition.Address.Should().Be(26);
    }

    [Fact]
    public void MapReadings_SurfacesFields_EvenWhenModelHasNoRegisters()
    {
        var device = new ModbusDevice { Name = "No Model", SlaveId = 1, TransportType = TransportType.MqttCloud };
        const string json = """[{ "variable":"data", "metadata":{ "U0":1, "CE":97 } }]""";

        var readings = _mapper.MapReadings(device, json);

        readings.Should().HaveCount(2);
        readings.Should().OnlyContain(r => r.Definition == null);
    }

    [Fact]
    public void MapReadings_IgnoresLogMessages()
    {
        var device = MakeDevice(Def("U0", 2));
        const string json = """{ "param":"log", "ID":"4002892", "msg":"Connection Started" }""";

        _mapper.MapReadings(device, json).Should().BeEmpty();
    }
}
