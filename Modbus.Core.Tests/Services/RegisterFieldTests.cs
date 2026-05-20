using FluentAssertions;
using Modbus.Core.Services;

namespace Modbus.Core.Tests.Services;

public sealed class RegisterFieldTests
{
    // ── ExtractValue — whole register ─────────────────────────────────────────

    [Fact]
    public void ExtractValue_WholeRegister_ReturnsRawValue()
    {
        var field = new RegisterField(40001);
        var regs  = new Dictionary<ushort, ushort> { [40001] = 0xABCD };

        var result = field.ExtractValue(regs);

        result.Should().Be(0xABCD);
    }

    [Fact]
    public void ExtractValue_MissingRegister_ReturnsNull()
    {
        var field = new RegisterField(40001);
        var regs  = new Dictionary<ushort, ushort>();

        var result = field.ExtractValue(regs);

        result.Should().BeNull();
    }

    // ── ExtractValue — multi-word ─────────────────────────────────────────────

    [Fact]
    public void ExtractValue_TwoWordField_CombinesHighWordFirst()
    {
        var field = new RegisterField(40001, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort>
        {
            [40001] = 0x0001,
            [40002] = 0x0002,
        };

        var result = field.ExtractValue(regs);

        // high word << 16 | low word = 0x00010002
        result.Should().Be(0x00010002u);
    }

    [Fact]
    public void ExtractValue_TwoWordField_MissingSecondWord_ReturnsNull()
    {
        var field = new RegisterField(40001, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort> { [40001] = 0xFFFF };

        var result = field.ExtractValue(regs);

        result.Should().BeNull();
    }

    // ── ExtractValue — bit-field ──────────────────────────────────────────────

    [Fact]
    public void ExtractValue_BitField_ExtractsCorrectBits()
    {
        // BitOffset=12, BitWidth=1 → bit 12
        var field = new RegisterField(40007, BitOffset: 12, BitWidth: 1);
        var regs  = new Dictionary<ushort, ushort> { [40007] = 0x1000 }; // bit 12 set

        var result = field.ExtractValue(regs);

        result.Should().Be(1u);
    }

    [Fact]
    public void ExtractValue_BitField_BitClear_ReturnsZero()
    {
        var field = new RegisterField(40007, BitOffset: 12, BitWidth: 1);
        var regs  = new Dictionary<ushort, ushort> { [40007] = 0x0FFF }; // bit 12 clear

        var result = field.ExtractValue(regs);

        result.Should().Be(0u);
    }

    [Fact]
    public void ExtractValue_BitField_MultiBit_ExtractsCorrectValue()
    {
        // High byte (BitOffset=8, BitWidth=8) = byte value 0xAB
        var field = new RegisterField(40006, BitOffset: 8, BitWidth: 8);
        var regs  = new Dictionary<ushort, ushort> { [40006] = 0xAB00 };

        var result = field.ExtractValue(regs);

        result.Should().Be(0xABu);
    }

    [Fact]
    public void ExtractValue_BitField_LowByte_ExtractsCorrectValue()
    {
        // Low byte (BitOffset=0, BitWidth=8)
        var field = new RegisterField(40006, BitOffset: 0, BitWidth: 8);
        var regs  = new Dictionary<ushort, ushort> { [40006] = 0x00CD };

        var result = field.ExtractValue(regs);

        result.Should().Be(0xCDu);
    }

    [Fact]
    public void ExtractValue_TwoBitFieldsSameRegister_IndependentExtraction()
    {
        ushort regValue = 0x1234;
        var regs = new Dictionary<ushort, ushort> { [40007] = regValue };

        var highByte = new RegisterField(40007, BitOffset: 8, BitWidth: 8).ExtractValue(regs);
        var lowByte  = new RegisterField(40007, BitOffset: 0, BitWidth: 8).ExtractValue(regs);

        highByte.Should().Be(0x12u);
        lowByte.Should().Be(0x34u);
    }

    // ── ExtractString ─────────────────────────────────────────────────────────

    [Fact]
    public void ExtractString_AsciiWords_DecodesHighByteFirst()
    {
        // 'A'=0x41, 'B'=0x42, 'C'=0x43, 'D'=0x44
        // word0 = (A<<8)|B = 0x4142, word1 = (C<<8)|D = 0x4344
        var field = new RegisterField(43001, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort>
        {
            [43001] = 0x4142,
            [43002] = 0x4344,
        };

        var result = field.ExtractString(regs);

        result.Should().Be("ABCD");
    }

    [Fact]
    public void ExtractString_NullTerminated_TruncatesAtFirstNull()
    {
        // "AB\0X" — junk after null should be discarded
        var field = new RegisterField(43001, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort>
        {
            [43001] = 0x4142, // 'A','B'
            [43002] = 0x0058, // '\0','X' — X is junk
        };

        var result = field.ExtractString(regs);

        result.Should().Be("AB");
    }

    [Fact]
    public void ExtractString_AllNullBytes_ReturnsEmptyString()
    {
        var field = new RegisterField(43001, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort>
        {
            [43001] = 0x0000,
            [43002] = 0x0000,
        };

        var result = field.ExtractString(regs);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractString_MissingRegister_ReturnsNull()
    {
        var field = new RegisterField(43001, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort> { [43001] = 0x4142 }; // missing 43002

        var result = field.ExtractString(regs);

        result.Should().BeNull();
    }

    // ── ApplyBits ─────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyBits_SetsBitInCorrectPosition()
    {
        var field  = new RegisterField(40007, BitOffset: 12, BitWidth: 1);
        ushort reg = 0x0000;

        var result = field.ApplyBits(reg, 1);

        result.Should().Be(0x1000);
    }

    [Fact]
    public void ApplyBits_ClearsExistingBitBeforeSetting()
    {
        var field  = new RegisterField(40007, BitOffset: 12, BitWidth: 1);
        ushort reg = 0x1FFF; // bit 12 is set, others set too

        var result = field.ApplyBits(reg, 0); // clear bit 12

        result.Should().Be(0x0FFF); // bit 12 cleared, others preserved
    }

    [Fact]
    public void ApplyBits_MultiBitField_SetsCorrectBits()
    {
        // Write 0xAB to high byte (BitOffset=8, BitWidth=8)
        var field  = new RegisterField(40006, BitOffset: 8, BitWidth: 8);
        ushort reg = 0x00CD; // existing low byte

        var result = field.ApplyBits(reg, 0xAB);

        result.Should().Be(0xABCD); // high byte updated, low byte preserved
    }

    // ── BCD helpers ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0x00,  0)]
    [InlineData(0x09,  9)]
    [InlineData(0x10, 10)]
    [InlineData(0x25, 25)]
    [InlineData(0x59, 59)]
    [InlineData(0x99, 99)]
    public void BcdToByte_KnownValues_ReturnsDecimal(byte bcd, byte expected)
    {
        RegisterField.BcdToByte(bcd).Should().Be(expected);
    }

    [Theory]
    [InlineData( 0, 0x00)]
    [InlineData( 9, 0x09)]
    [InlineData(10, 0x10)]
    [InlineData(25, 0x25)]
    [InlineData(59, 0x59)]
    public void ByteToBcd_KnownValues_ReturnsBcd(byte value, byte expectedBcd)
    {
        RegisterField.ByteToBcd(value).Should().Be(expectedBcd);
    }

    // ── ExtractTime ───────────────────────────────────────────────────────────

    [Fact]
    public void ExtractTime_ValidRegisters_DecodesCorrectly()
    {
        // Addr+0: high=centésimo(0x00=0), low=segundo(0x30=30s)
        // Addr+1: high=minuto(0x15=15min), low=hora(0x10=10h)
        var field = new RegisterField(43200, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort>
        {
            [43200] = 0x0030, // centésimo=0x00→0, segundo=0x30→30
            [43201] = 0x1510, // minuto=0x15→15, hora=0x10→10
        };

        var result = field.ExtractTime(regs);

        result.Should().NotBeNull();
        result!.Value.Hora.Should().Be(10);
        result.Value.Minuto.Should().Be(15);
        result.Value.Segundo.Should().Be(30);
        result.Value.Centesimo.Should().Be(0);
    }

    [Fact]
    public void ExtractTime_MissingRegister_ReturnsNull()
    {
        var field = new RegisterField(43200, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort> { [43200] = 0x0000 };

        var result = field.ExtractTime(regs);

        result.Should().BeNull();
    }

    // ── ExtractDate ───────────────────────────────────────────────────────────

    [Fact]
    public void ExtractDate_ValidRegisters_DecodesCorrectly()
    {
        // Addr+0: high=diaSemana(3=terça), low=dia(0x20=20)
        // Addr+1: high=mês(0x05=maio), low=ano(0x26=26 → 2026)
        var field = new RegisterField(43202, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort>
        {
            [43202] = 0x0320, // diaSemana=3, dia=0x20→20
            [43203] = 0x0526, // mês=0x05→5, ano=0x26→2026
        };

        var result = field.ExtractDate(regs);

        result.Should().NotBeNull();
        result!.Value.DiaSemana.Should().Be(3);
        result.Value.Dia.Should().Be(20);
        result.Value.Mes.Should().Be(5);
        result.Value.Ano.Should().Be(2026);
    }

    [Fact]
    public void ExtractDate_MissingRegister_ReturnsNull()
    {
        var field = new RegisterField(43202, WordCount: 2);
        var regs  = new Dictionary<ushort, ushort> { [43202] = 0x0000 };

        var result = field.ExtractDate(regs);

        result.Should().BeNull();
    }
}
