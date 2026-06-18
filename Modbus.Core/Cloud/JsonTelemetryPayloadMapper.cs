using System.Globalization;
using System.Text.Json;
using Modbus.Core.Domain.Entities;

namespace Modbus.Core.Cloud;

/// <summary>
/// Maps KS telemetry JSON to register values so cloud devices feed the same reading pipeline as
/// local devices. Handles the KS data payload shape:
/// <code>[{ "variable":"data", "time":"2026-04-06 14:45:00", "metadata":{ "U0":201.8, "F1":59.99, … } }]</code>
/// (an array wrapping one object whose <c>metadata</c> holds the values). Log messages
/// (<c>{"param":"log",…}</c>) and any other shapes simply produce no values.
/// </summary>
/// <remarks>
/// Telemetry field names mostly equal the register definition names; a few differ and are aliased
/// (e.g. <c>F1</c>→<c>Freq</c>, <c>EA</c>→<c>EA+</c>). Telemetry values are already engineering values.
/// </remarks>
public class JsonTelemetryPayloadMapper : ITelemetryPayloadMapper
{
    private static readonly string[] TimeKeys = ["time", "timestamp", "ts", "datetime"];

    private static readonly string[] TimeFormats =
        ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-ddTHH:mm:ss"];

    /// <summary>Telemetry field name → register definition name, for fields whose names differ.</summary>
    private static readonly Dictionary<string, string> FieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F1"]  = "Freq",  // frequency
        ["EA"]  = "EA+",   // active energy (positive)
        ["ER"]  = "ER+",   // reactive energy (positive)
        ["EAN"] = "EA-",   // active energy (negative)
        ["ERN"] = "ER-",   // reactive energy (negative)
    };

    public IReadOnlyList<RegisterValue> Map(ModbusDevice device, string jsonPayload, DateTime fallbackTimestamp)
    {
        var definitions = device.DeviceModel?.Registers;
        if (definitions is null || definitions.Count == 0 || string.IsNullOrWhiteSpace(jsonPayload))
            return Array.Empty<RegisterValue>();

        JsonElement element;
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            // Clone so the element stays valid after the document is disposed.
            element = Unwrap(doc.RootElement).Clone();
        }
        catch (JsonException)
        {
            return Array.Empty<RegisterValue>();
        }

        if (element.ValueKind != JsonValueKind.Object)
            return Array.Empty<RegisterValue>();

        // Values live under "metadata" in the KS data payload; fall back to the element itself.
        var fieldSource = element.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            ? metadata
            : element;

        var fields = IndexFields(fieldSource);
        if (fields.Count == 0)
            return Array.Empty<RegisterValue>();

        var timestamp = ExtractTimestamp(element, fields, fallbackTimestamp);

        var results = new List<RegisterValue>();
        foreach (var def in definitions)
        {
            if (!fields.TryGetValue(def.Name, out var value))
                continue;

            results.Add(new RegisterValue
            {
                DeviceId     = device.Id,
                Address      = def.Address,
                RegisterType = def.RegisterType,
                Value        = value,
                RawWords     = Array.Empty<ushort>(), // telemetry is pre-decoded; no raw words available
                Timestamp    = timestamp
            });
        }

        return results;
    }

    /// <summary>If the payload is an array (KS data messages are), take its first object element.</summary>
    private static JsonElement Unwrap(JsonElement root) =>
        root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            ? root[0]
            : root;

    /// <summary>Indexes numeric fields case-insensitively, adding alias keys so register names resolve.</summary>
    private static Dictionary<string, double> IndexFields(JsonElement source)
    {
        var fields = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in source.EnumerateObject())
            if (TryGetNumber(prop.Value, out var value))
                fields[prop.Name] = value;

        foreach (var (alias, canonical) in FieldAliases)
            if (fields.TryGetValue(alias, out var value) && !fields.ContainsKey(canonical))
                fields[canonical] = value;

        return fields;
    }

    private static DateTime ExtractTimestamp(JsonElement element, Dictionary<string, double> fields, DateTime fallback)
    {
        foreach (var key in TimeKeys)
        {
            if (!element.TryGetProperty(key, out var prop))
                continue;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var unix))
                return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

            if (prop.ValueKind == JsonValueKind.String && prop.GetString() is { } text)
            {
                if (DateTime.TryParseExact(text, TimeFormats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var exact))
                    return exact;
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    return parsed;
            }
        }

        return fallback;
    }

    private static bool TryGetNumber(JsonElement element, out double value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDouble(out value);
            case JsonValueKind.String when double.TryParse(
                element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value):
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
