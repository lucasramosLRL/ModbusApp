using System.Collections.Generic;

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
    /// Merges <paramref name="fieldValue"/> into <paramref name="currentRegValue"/> by
    /// clearing the relevant bits and inserting the new value. Use before FC06 write.
    /// </summary>
    public ushort ApplyBits(ushort currentRegValue, ushort fieldValue) =>
        (ushort)((currentRegValue & ~(BitMask << BitOffset)) | ((fieldValue & BitMask) << BitOffset));

    /// <summary>
    /// Allows assigning a plain integer Modicon address in the profile registry
    /// without wrapping in <c>new RegisterField(...)</c>.
    /// Example: <c>AddrKe = 40005</c>  (whole register, WordCount = 1).
    /// For multi-word or bit-fields, use the explicit constructor.
    /// </summary>
    public static implicit operator RegisterField(int  addr) => new((ushort)addr);
    public static implicit operator RegisterField(uint addr) => new((ushort)addr);
}
