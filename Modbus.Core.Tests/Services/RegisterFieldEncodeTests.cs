using FluentAssertions;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Services;

namespace Modbus.Core.Tests.Services;

public sealed class RegisterFieldEncodeTests
{
    // ── EncodeFloat32 — round-trip against RegisterDecoder.Decode ─────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-1f)]
    [InlineData(220.5f)]
    [InlineData(3.14159f)]
    [InlineData(-273.15f)]
    [InlineData(float.Epsilon)]
    public void EncodeFloat32_ByteSwapped_RoundTripsThroughDecoder(float value)
    {
        var words = RegisterDecoder.EncodeFloat32(value, WordOrder.ByteSwapped);

        var decoded = RegisterDecoder.Decode(words, DataType.Float32, WordOrder.ByteSwapped);

        ((float)decoded).Should().BeApproximately(value, 1e-6f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(220.5f)]
    [InlineData(-1.5f)]
    public void EncodeFloat32_BigEndian_RoundTripsThroughDecoder(float value)
    {
        var words = RegisterDecoder.EncodeFloat32(value, WordOrder.BigEndian);

        var decoded = RegisterDecoder.Decode(words, DataType.Float32, WordOrder.BigEndian);

        ((float)decoded).Should().BeApproximately(value, 1e-6f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(220.5f)]
    public void EncodeFloat32_LittleEndian_RoundTripsThroughDecoder(float value)
    {
        var words = RegisterDecoder.EncodeFloat32(value, WordOrder.LittleEndian);

        var decoded = RegisterDecoder.Decode(words, DataType.Float32, WordOrder.LittleEndian);

        ((float)decoded).Should().BeApproximately(value, 1e-6f);
    }

    [Fact]
    public void EncodeFloat32_ByteSwapped_ProducesTwoWords()
    {
        var words = RegisterDecoder.EncodeFloat32(1f, WordOrder.ByteSwapped);

        words.Should().HaveCount(2);
    }

    // ── EncodeString — round-trip against ExtractString ───────────────────────

    [Fact]
    public void EncodeString_AsciiText_RoundTripsThroughExtract()
    {
        var field = new RegisterField(43461, WordCount: 4); // 8 chars capacity

        var words = field.EncodeString("HELLO");
        var regs  = ToDict(field.Addr, words);

        field.ExtractString(regs).Should().Be("HELLO");
    }

    [Fact]
    public void EncodeString_NullOrEmpty_ProducesAllZeroWords()
    {
        var field = new RegisterField(43461, WordCount: 3);

        var fromNull  = field.EncodeString(null);
        var fromEmpty = field.EncodeString(string.Empty);

        fromNull.Should().Equal(0, 0, 0);
        fromEmpty.Should().Equal(0, 0, 0);
    }

    [Fact]
    public void EncodeString_HighByteIsFirstChar()
    {
        // 'A'=0x41, 'B'=0x42 → word = (A<<8)|B = 0x4142
        var field = new RegisterField(43461, WordCount: 1);

        var words = field.EncodeString("AB");

        words.Should().Equal((ushort)0x4142);
    }

    [Fact]
    public void EncodeString_ShorterThanCapacity_PadsRemainderWithZeros()
    {
        // "A" in a 3-word field (6 bytes): word0=(0x41,0x00)=0x4100, word1=0, word2=0
        var field = new RegisterField(43461, WordCount: 3);

        var words = field.EncodeString("A");

        words.Should().Equal((ushort)0x4100, (ushort)0x0000, (ushort)0x0000);
    }

    [Fact]
    public void EncodeString_LongerThanCapacity_Truncates()
    {
        // 1-word field (2 bytes) given 4-char string → keeps first 2 chars
        var field = new RegisterField(43461, WordCount: 1);

        var words = field.EncodeString("ABCD");

        // 'A'=0x41, 'B'=0x42 → 0x4142 (no room for null terminator either)
        words.Should().Equal((ushort)0x4142);
    }

    // ── EncodeTime — round-trip against ExtractTime ───────────────────────────

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10, 15, 30, 0)]
    [InlineData(23, 59, 59, 99)]
    public void EncodeTime_RoundTripsThroughExtractTime(int hour, int minute, int second, int centesimo)
    {
        var (w0, w1) = RegisterField.EncodeTime(hour, minute, second, centesimo);

        var field = new RegisterField(42001, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort> { [42001] = w0, [42002] = w1 };
        var t     = field.ExtractTime(regs);

        t.Should().NotBeNull();
        t!.Value.Hora.Should().Be((byte)hour);
        t.Value.Minuto.Should().Be((byte)minute);
        t.Value.Segundo.Should().Be((byte)second);
        t.Value.Centesimo.Should().Be((byte)centesimo);
    }

    [Fact]
    public void EncodeTime_ProducesExpectedBcdLayout()
    {
        // 10h 15min 30s 0cent → word0=(0x00 cent, 0x30 sec)=0x0030; word1=(0x15 min, 0x10 hour)=0x1510
        var (w0, w1) = RegisterField.EncodeTime(10, 15, 30, 0);

        w0.Should().Be(0x0030);
        w1.Should().Be(0x1510);
    }

    // ── EncodeDate — round-trip against ExtractDate ───────────────────────────

    [Theory]
    [InlineData(2026, 5, 20, 3)]
    [InlineData(2000, 1, 1, 6)]
    [InlineData(2099, 12, 31, 5)]
    public void EncodeDate_RoundTripsThroughExtractDate(int year, int month, int day, int weekday)
    {
        var (w0, w1) = RegisterField.EncodeDate(year, month, day, weekday);

        var field = new RegisterField(42003, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort> { [42003] = w0, [42004] = w1 };
        var d     = field.ExtractDate(regs);

        d.Should().NotBeNull();
        d!.Value.Ano.Should().Be(year);
        d.Value.Mes.Should().Be((byte)month);
        d.Value.Dia.Should().Be((byte)day);
        d.Value.DiaSemana.Should().Be((byte)weekday);
    }

    [Fact]
    public void EncodeDate_ProducesExpectedBcdLayout()
    {
        // 2026-05-20, weekday=3 → word0=(0x03 weekday, 0x20 day)=0x0320; word1=(0x05 month, 0x26 year)=0x0526
        var (w0, w1) = RegisterField.EncodeDate(2026, 5, 20, 3);

        w0.Should().Be(0x0320);
        w1.Should().Be(0x0526);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<ushort, ushort> ToDict(ushort baseAddr, ushort[] words)
    {
        var d = new Dictionary<ushort, ushort>(words.Length);
        for (int i = 0; i < words.Length; i++)
            d[(ushort)(baseAddr + i)] = words[i];
        return d;
    }
}
