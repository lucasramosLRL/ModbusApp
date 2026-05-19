using System.Collections.Generic;
using System.Text;

namespace Modbus.Core.Services;

/// <summary>
/// Describes one configuration field in terms of Modicon register numbers.
///
/// Three usage patterns:
///   • Whole register    new RegisterField(40005)                     — single 16-bit word
///   • Multi-word        new RegisterField(40001, WordCount: 2)       — e.g. Float32 or 32-bit uint
///   • Bit-field         new RegisterField(40007, BitOffset: 12, BitWidth: 1)
///
/// Addresses follow the Modicon convention: 4xxxx = FC03 holding, 3xxxx = FC04 input.
/// </summary>
public readonly record struct RegisterField(
    ushort Addr,
    int    WordCount  = 1,
    int    BitOffset  = 0,
    int    BitWidth   = 16)
{
    public bool IsMultiWord  => WordCount > 1;
    public bool IsBitField   => BitWidth < 16 || BitOffset > 0;

    private int BitMask => (1 << BitWidth) - 1;

    /// <summary>
    /// All Modicon register addresses this field occupies (used to build the bulk-read list).
    /// </summary>
    public IEnumerable<ushort> AllAddresses()
    {
        for (int i = 0; i < WordCount; i++)
            yield return (ushort)(Addr + i);
    }

    /// <summary>
    /// Extracts the field value from a register dictionary (keyed by Modicon address).
    /// Returns null if any required register is missing.
    /// For WordCount == 2: returns a 32-bit value (high word first, big-endian).
    /// For bit-fields: returns only the relevant bits, shifted to position 0.
    /// </summary>
    public uint? ExtractValue(IReadOnlyDictionary<ushort, ushort> regs)
    {
        if (!regs.TryGetValue(Addr, out var word0)) return null;

        if (WordCount == 2)
        {
            if (!regs.TryGetValue((ushort)(Addr + 1), out var word1)) return null;
            return (uint)(word0 << 16 | word1);
        }

        return (uint)((word0 >> BitOffset) & BitMask);
    }

    /// <summary>
    /// Decodes a string field from the register dictionary (high byte first per word).
    /// Reads <see cref="WordCount"/> registers starting at <see cref="Addr"/>,
    /// interprets them as ASCII bytes, and trims null terminators.
    /// Returns null if any register is missing.
    /// </summary>
    public string? ExtractString(IReadOnlyDictionary<ushort, ushort> regs)
    {
        var bytes = new byte[WordCount * 2];
        for (int i = 0; i < WordCount; i++)
        {
            if (!regs.TryGetValue((ushort)(Addr + i), out var word)) return null;
            bytes[i * 2]     = (byte)(word >> 8);
            bytes[i * 2 + 1] = (byte)(word & 0xFF);
        }
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    /// <summary>
    /// Merges <paramref name="fieldValue"/> into <paramref name="currentRegValue"/> by
    /// clearing the relevant bits and inserting the new value. Use before FC06 write.
    /// </summary>
    public ushort ApplyBits(ushort currentRegValue, ushort fieldValue) =>
        (ushort)((currentRegValue & ~(BitMask << BitOffset)) | ((fieldValue & BitMask) << BitOffset));

    /// <summary>
    /// Decodes a BCD byte into its decimal value. E.g. 0x25 → 25.
    /// </summary>
    public static byte BcdToByte(byte bcd) => (byte)((bcd >> 4) * 10 + (bcd & 0x0F));

    /// <summary>
    /// Encodes a decimal byte (0–99) into BCD. E.g. 25 → 0x25.
    /// </summary>
    public static byte ByteToBcd(byte value) => (byte)(((value / 10) << 4) | (value % 10));

    /// <summary>
    /// Decodes an RTC time from two consecutive registers starting at <see cref="Addr"/>.
    ///   Addr+0: high = centésimo BCD, low = segundo BCD
    ///   Addr+1: high = minuto BCD,    low = hora BCD
    /// Returns null if any register is missing.
    /// </summary>
    public (byte Hora, byte Minuto, byte Segundo, byte Centesimo)? ExtractTime(
        IReadOnlyDictionary<ushort, ushort> regs)
    {
        if (!regs.TryGetValue(Addr, out var r0) ||
            !regs.TryGetValue((ushort)(Addr + 1), out var r1)) return null;

        return (
            Hora:      BcdToByte((byte)(r1 & 0xFF)),
            Minuto:    BcdToByte((byte)(r1 >> 8)),
            Segundo:   BcdToByte((byte)(r0 & 0xFF)),
            Centesimo: BcdToByte((byte)(r0 >> 8))
        );
    }

    /// <summary>
    /// Decodes an RTC date from two consecutive registers starting at <see cref="Addr"/>.
    ///   Addr+0: high = dia da semana raw (KS: 01=dom..07=sáb), low = dia BCD
    ///   Addr+1: high = mês BCD, low = ano BCD (2 dígitos; base = 2000)
    /// Returns null if any register is missing.
    /// </summary>
    public (byte DiaSemana, byte Dia, byte Mes, int Ano)? ExtractDate(
        IReadOnlyDictionary<ushort, ushort> regs)
    {
        if (!regs.TryGetValue(Addr, out var r0) ||
            !regs.TryGetValue((ushort)(Addr + 1), out var r1)) return null;

        return (
            DiaSemana: (byte)(r0 >> 8),
            Dia:       BcdToByte((byte)(r0 & 0xFF)),
            Mes:       BcdToByte((byte)(r1 >> 8)),
            Ano:       2000 + BcdToByte((byte)(r1 & 0xFF))
        );
    }

    /// <summary>
    /// Allows assigning a plain integer Modicon address in the profile registry
    /// without wrapping in <c>new RegisterField(...)</c>.
    /// Example: <c>AddrKe = 40005</c>  (whole register, WordCount = 1).
    /// For multi-word or bit-fields, use the explicit constructor.
    /// </summary>
    public static implicit operator RegisterField(int  addr) => new((ushort)addr);
    public static implicit operator RegisterField(uint addr) => new((ushort)addr);
}
