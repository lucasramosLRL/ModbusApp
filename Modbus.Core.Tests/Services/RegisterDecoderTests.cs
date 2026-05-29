using FluentAssertions;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Services;

namespace Modbus.Core.Tests.Services;

public class RegisterDecoderTests
{
    // ── UInt16 ───────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> UInt16Cases()
    {
        yield return new object[] { new ushort[] { 0x0000 }, 1.0, 0.0 };
        yield return new object[] { new ushort[] { 0x0001 }, 1.0, 1.0 };
        yield return new object[] { new ushort[] { 0xFFFF }, 1.0, 65535.0 };
        yield return new object[] { new ushort[] { 0x1234 }, 1.0, 4660.0 };
        yield return new object[] { new ushort[] { 0x0001 }, 10.0, 10.0 };
        yield return new object[] { new ushort[] { 0x000A }, 0.1, 1.0 };
    }

    [Theory]
    [MemberData(nameof(UInt16Cases))]
    public void Decode_UInt16_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.UInt16, WordOrder.BigEndian, scale);
        result.Should().BeApproximately(expected, 1e-9);
    }

    // ── Int16 ────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> Int16Cases()
    {
        yield return new object[] { new ushort[] { 0x0000 }, 1.0, 0.0 };
        yield return new object[] { new ushort[] { 0x0001 }, 1.0, 1.0 };
        yield return new object[] { new ushort[] { 0x7FFF }, 1.0, 32767.0 };
        yield return new object[] { new ushort[] { 0x8000 }, 1.0, -32768.0 };
        yield return new object[] { new ushort[] { 0xFFFF }, 1.0, -1.0 };
        yield return new object[] { new ushort[] { 0xFFFE }, 0.1, -0.2 };
    }

    [Theory]
    [MemberData(nameof(Int16Cases))]
    public void Decode_Int16_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.Int16, WordOrder.BigEndian, scale);
        result.Should().BeApproximately(expected, 1e-9);
    }

    // ── UInt32 BigEndian ─────────────────────────────────────────────────────

    public static IEnumerable<object[]> UInt32BigEndianCases()
    {
        // BigEndian: words[0] is high word
        yield return new object[] { new ushort[] { 0x0000, 0x0000 }, 1.0, 0.0 };
        yield return new object[] { new ushort[] { 0x0000, 0x0001 }, 1.0, 1.0 };
        yield return new object[] { new ushort[] { 0x0001, 0x0000 }, 1.0, 65536.0 };
        yield return new object[] { new ushort[] { 0x0000, 0x000A }, 2.0, 20.0 };
    }

    [Theory]
    [MemberData(nameof(UInt32BigEndianCases))]
    public void Decode_UInt32_BigEndian_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.UInt32, WordOrder.BigEndian, scale);
        result.Should().BeApproximately(expected, 1e-9);
    }

    // ── UInt32 LittleEndian ──────────────────────────────────────────────────

    public static IEnumerable<object[]> UInt32LittleEndianCases()
    {
        // LittleEndian: words[1] is high word
        yield return new object[] { new ushort[] { 0x0001, 0x0000 }, 1.0, 1.0 };
        yield return new object[] { new ushort[] { 0x0000, 0x0001 }, 1.0, 65536.0 };
    }

    [Theory]
    [MemberData(nameof(UInt32LittleEndianCases))]
    public void Decode_UInt32_LittleEndian_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.UInt32, WordOrder.LittleEndian, scale);
        result.Should().BeApproximately(expected, 1e-9);
    }

    // ── UInt32 ByteSwapped ───────────────────────────────────────────────────

    public static IEnumerable<object[]> UInt32ByteSwappedCases()
    {
        // ByteSwapped (DCBA): SwapBytes each word, then words[1] is high
        // SwapBytes(0x0100) = 0x0001, SwapBytes(0x0000) = 0x0000 → high=0x0000, low=0x0001 → 1
        yield return new object[] { new ushort[] { 0x0100, 0x0000 }, 1.0, 1.0 };
        // SwapBytes(0x0000) = 0x0000, SwapBytes(0x0100) = 0x0001 → high=0x0001, low=0x0000 → 65536
        yield return new object[] { new ushort[] { 0x0000, 0x0100 }, 1.0, 65536.0 };
    }

    [Theory]
    [MemberData(nameof(UInt32ByteSwappedCases))]
    public void Decode_UInt32_ByteSwapped_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.UInt32, WordOrder.ByteSwapped, scale);
        result.Should().BeApproximately(expected, 1e-9);
    }

    // ── Int32 ────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> Int32Cases()
    {
        // BigEndian
        yield return new object[] { new ushort[] { 0x0000, 0x0001 }, WordOrder.BigEndian, 1.0, 1.0 };
        yield return new object[] { new ushort[] { 0xFFFF, 0xFFFF }, WordOrder.BigEndian, 1.0, -1.0 };
        yield return new object[] { new ushort[] { 0x8000, 0x0000 }, WordOrder.BigEndian, 1.0, -2147483648.0 };
        // LittleEndian
        yield return new object[] { new ushort[] { 0xFFFF, 0xFFFF }, WordOrder.LittleEndian, 1.0, -1.0 };
        yield return new object[] { new ushort[] { 0x0000, 0x8000 }, WordOrder.LittleEndian, 1.0, -2147483648.0 };
    }

    [Theory]
    [MemberData(nameof(Int32Cases))]
    public void Decode_Int32_ReturnsExpectedValue(ushort[] words, WordOrder order, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.Int32, order, scale);
        result.Should().BeApproximately(expected, 1e-9);
    }

    // ── Float32 BigEndian ────────────────────────────────────────────────────

    public static IEnumerable<object[]> Float32BigEndianCases()
    {
        // 42.0f = 0x42280000 → words[0]=0x4228, words[1]=0x0000
        yield return new object[] { new ushort[] { 0x4228, 0x0000 }, 1.0, 42.0 };
        // 1.0f = 0x3F800000 → words[0]=0x3F80, words[1]=0x0000
        yield return new object[] { new ushort[] { 0x3F80, 0x0000 }, 1.0, 1.0 };
        // -1.0f = 0xBF800000 → words[0]=0xBF80, words[1]=0x0000
        yield return new object[] { new ushort[] { 0xBF80, 0x0000 }, 1.0, -1.0 };
        // 0.0f = 0x00000000
        yield return new object[] { new ushort[] { 0x0000, 0x0000 }, 1.0, 0.0 };
        // 3.14f ≈ 0x4048F5C3 → words[0]=0x4048, words[1]=0xF5C3
        yield return new object[] { new ushort[] { 0x4048, 0xF5C3 }, 1.0, 3.14 };
        // Scale factor: 42.0 * 2.0 = 84.0
        yield return new object[] { new ushort[] { 0x4228, 0x0000 }, 2.0, 84.0 };
    }

    [Theory]
    [MemberData(nameof(Float32BigEndianCases))]
    public void Decode_Float32_BigEndian_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.Float32, WordOrder.BigEndian, scale);
        result.Should().BeApproximately(expected, 1e-2);
    }

    // ── Float32 LittleEndian ─────────────────────────────────────────────────

    public static IEnumerable<object[]> Float32LittleEndianCases()
    {
        // LittleEndian: words[1] is high → 42.0f: words[0]=0x0000, words[1]=0x4228
        yield return new object[] { new ushort[] { 0x0000, 0x4228 }, 1.0, 42.0 };
        yield return new object[] { new ushort[] { 0x0000, 0x3F80 }, 1.0, 1.0 };
        yield return new object[] { new ushort[] { 0x0000, 0xBF80 }, 1.0, -1.0 };
    }

    [Theory]
    [MemberData(nameof(Float32LittleEndianCases))]
    public void Decode_Float32_LittleEndian_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.Float32, WordOrder.LittleEndian, scale);
        result.Should().BeApproximately(expected, 1e-2);
    }

    // ── Float32 ByteSwapped ──────────────────────────────────────────────────

    public static IEnumerable<object[]> Float32ByteSwappedCases()
    {
        // ByteSwapped (DCBA): SwapBytes each word, then words[1] is high
        // 42.0f = bytes 42 28 00 00 (big-endian IEEE)
        // DCBA: reverse bytes → 00 00 28 42
        // As Modbus words: words[0]=0x0000, words[1]=0x2842
        // SwapBytes(0x2842)=0x4228 (high), SwapBytes(0x0000)=0x0000 (low) → 0x42280000 = 42.0
        yield return new object[] { new ushort[] { 0x0000, 0x2842 }, 1.0, 42.0 };
        // 1.0f = 0x3F800000, DCBA → 00 00 80 3F → words[0]=0x0000, words[1]=0x803F
        yield return new object[] { new ushort[] { 0x0000, 0x803F }, 1.0, 1.0 };
    }

    [Theory]
    [MemberData(nameof(Float32ByteSwappedCases))]
    public void Decode_Float32_ByteSwapped_ReturnsExpectedValue(ushort[] words, double scale, double expected)
    {
        var result = RegisterDecoder.Decode(words, DataType.Float32, WordOrder.ByteSwapped, scale);
        result.Should().BeApproximately(expected, 1e-2);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Decode_InvalidDataType_ThrowsArgumentOutOfRangeException()
    {
        var act = () => RegisterDecoder.Decode(new ushort[] { 0 }, (DataType)99, WordOrder.BigEndian);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Decode_InvalidWordOrder_ThrowsArgumentOutOfRangeException()
    {
        // WordOrder.UseSqpf is not handled by Combine32 — should throw
        var act = () => RegisterDecoder.Decode(new ushort[] { 0, 0 }, DataType.UInt32, (WordOrder)99);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Decode_DefaultScaleFactor_IsOne()
    {
        // Calling without scale factor should return raw value
        var result = RegisterDecoder.Decode(new ushort[] { 0x000A }, DataType.UInt16, WordOrder.BigEndian);
        result.Should().Be(10.0);
    }

    // ── DecodeFloat32WithSqpf ────────────────────────────────────────────────

    // SQPF nibble convention: nibble i = IEEE 754 float byte index at transmitted position i
    // Transmitted bytes: t[0]=words[0]Hi, t[1]=words[0]Lo, t[2]=words[1]Hi, t[3]=words[1]Lo
    // raw |= t[i] << (floatByteIdx * 8) where floatByteIdx = (sqpfValue >> (i*4)) & 0xF

    public static IEnumerable<object[]> SqpfCases()
    {
        // SQPF algorithm: for each transmitted byte t[i] (i=0..3),
        //   floatByteIdx = (sqpfValue >> (i*4)) & 0xF
        //   raw |= t[i] << (floatByteIdx * 8)
        // where byte0 = LSB, byte3 = MSB of the IEEE 754 float.
        //
        // For 1.0f = 0x3F800000: byte0(LSB)=0x00, byte1=0x00, byte2=0x80, byte3(MSB)=0x3F

        // SQPF=0x3210: t[0]→byte0, t[1]→byte1, t[2]→byte2, t[3]→byte3
        // t=[0x00,0x00,0x80,0x3F] → words[0]=0x0000, words[1]=0x803F
        yield return new object[] { new ushort[] { 0x0000, 0x803F }, (ushort)0x3210, 1.0, 1.0 };

        // SQPF=0x2301: t[0]→byte1, t[1]→byte0, t[2]→byte3, t[3]→byte2
        // byte0=0x00→t[1], byte1=0x00→t[0], byte2=0x80→t[3], byte3=0x3F→t[2]
        // t=[0x00,0x00,0x3F,0x80] → words[0]=0x0000, words[1]=0x3F80
        yield return new object[] { new ushort[] { 0x0000, 0x3F80 }, (ushort)0x2301, 1.0, 1.0 };

        // SQPF=0x0123: t[0]→byte3, t[1]→byte2, t[2]→byte1, t[3]→byte0
        // byte0=0x00→t[3], byte1=0x00→t[2], byte2=0x80→t[1], byte3=0x3F→t[0]
        // t=[0x3F,0x80,0x00,0x00] → words[0]=0x3F80, words[1]=0x0000
        yield return new object[] { new ushort[] { 0x3F80, 0x0000 }, (ushort)0x0123, 1.0, 1.0 };

        // Scale factor test: SQPF=0x3210, 1.0f * 5.0 = 5.0
        yield return new object[] { new ushort[] { 0x0000, 0x803F }, (ushort)0x3210, 5.0, 5.0 };

        // 42.0f = 0x42280000: byte0=0x00, byte1=0x00, byte2=0x28, byte3=0x42
        // SQPF=0x3210: t=[0x00,0x00,0x28,0x42] → words[0]=0x0000, words[1]=0x2842
        yield return new object[] { new ushort[] { 0x0000, 0x2842 }, (ushort)0x3210, 1.0, 42.0 };

        // 42.0f with SQPF=0x2301: t=[0x00,0x00,0x42,0x28] → words[0]=0x0000, words[1]=0x4228
        yield return new object[] { new ushort[] { 0x0000, 0x4228 }, (ushort)0x2301, 1.0, 42.0 };

        // 42.0f with SQPF=0x0123: t=[0x42,0x28,0x00,0x00] → words[0]=0x4228, words[1]=0x0000
        yield return new object[] { new ushort[] { 0x4228, 0x0000 }, (ushort)0x0123, 1.0, 42.0 };

        // Zero is always zero regardless of SQPF
        yield return new object[] { new ushort[] { 0x0000, 0x0000 }, (ushort)0x3210, 1.0, 0.0 };
        yield return new object[] { new ushort[] { 0x0000, 0x0000 }, (ushort)0x0123, 1.0, 0.0 };

        // ── Non-default SQPF values (KRON convention: nibble HIGH-to-LOW, floatByteIdx = 3-nibble) ──
        //   Device transmits t[j] = LE_float[ 3 - buffer_prog[j] ] where buffer_prog[j] = nibble(3-j).

        // SQPF=0x1230 ("F0 F1 F2 EXP"):
        //   buffer_prog=[nibble3,nibble2,nibble1,nibble0]=[1,2,3,0]
        //   t=[LE[3-1],LE[3-2],LE[3-3],LE[3-0]]=[LE[2],LE[1],LE[0],LE[3]]
        //   1.0f (LE=[00,00,80,3F]): t=[0x80,0x00,0x00,0x3F] → words=[0x8000,0x003F]
        yield return new object[] { new ushort[] { 0x8000, 0x003F }, (ushort)0x1230, 1.0, 1.0 };

        // SQPF=0x1230, 220.0f = 0x435C0000 (LE=[00,00,5C,43]):
        //   t=[0x5C,0x00,0x00,0x43] → words=[0x5C00,0x0043]
        yield return new object[] { new ushort[] { 0x5C00, 0x0043 }, (ushort)0x1230, 1.0, 220.0 };

        // SQPF=0x1302 ("F0 F2 EXP F1" — confirmed in KRON sw):
        //   buffer_prog=[1,3,0,2] → t=[LE[2],LE[0],LE[3],LE[1]]
        //   1.0f: t=[0x80,0x00,0x3F,0x00] → words=[0x8000,0x3F00]
        yield return new object[] { new ushort[] { 0x8000, 0x3F00 }, (ushort)0x1302, 1.0, 1.0 };

        // SQPF=0x2031 (non-self-inverse under KRON convention):
        //   buffer_prog=[nibble3,nibble2,nibble1,nibble0]=[2,0,3,1]
        //   t=[LE[3-2],LE[3-0],LE[3-3],LE[3-1]]=[LE[1],LE[3],LE[0],LE[2]]
        //   1.0f: t=[0x00,0x3F,0x00,0x80] → words=[0x003F,0x0080]
        yield return new object[] { new ushort[] { 0x003F, 0x0080 }, (ushort)0x2031, 1.0, 1.0 };
    }

    [Theory]
    [MemberData(nameof(SqpfCases))]
    public void DecodeFloat32WithSqpf_ReturnsExpectedValue(ushort[] words, ushort sqpf, double scale, double expected)
    {
        var result = RegisterDecoder.DecodeFloat32WithSqpf(words, sqpf, scale);
        result.Should().BeApproximately(expected, 1e-2);
    }

    // ── Negative float with SQPF ─────────────────────────────────────────────

    [Fact]
    public void DecodeFloat32WithSqpf_NegativeValue_DecodesCorrectly()
    {
        // -1.0f = 0xBF800000: byte0=0x00, byte1=0x00, byte2=0x80, byte3=0xBF
        // SQPF=0x3210: t=[0x00,0x00,0x80,0xBF] → words[0]=0x0000, words[1]=0x80BF
        var result = RegisterDecoder.DecodeFloat32WithSqpf(new ushort[] { 0x0000, 0x80BF }, 0x3210);
        result.Should().BeApproximately(-1.0, 1e-6);
    }
}
