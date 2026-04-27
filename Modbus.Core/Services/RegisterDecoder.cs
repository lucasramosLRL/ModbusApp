using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Services;

public static class RegisterDecoder
{
    /// <summary>
    /// Converts raw Modbus register words to a scaled engineering value.
    /// </summary>
    /// <param name="words">Raw 16-bit words as returned by the device.</param>
    /// <param name="dataType">How the words should be interpreted.</param>
    /// <param name="wordOrder">Word order for 32-bit types (which word is the high word).</param>
    /// <param name="scaleFactor">Multiplier applied after decoding. Defaults to 1.0.</param>
    public static double Decode(ushort[] words, DataType dataType, WordOrder wordOrder, double scaleFactor = 1.0)
    {
        double raw = dataType switch
        {
            DataType.UInt16  => words[0],
            DataType.Int16   => (short)words[0],
            DataType.UInt32  => Combine32(words, wordOrder),
            DataType.Int32   => (int)Combine32(words, wordOrder),
            DataType.Float32 => BitConverter.Int32BitsToSingle((int)Combine32(words, wordOrder)),
            _                => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
        };
        return raw * scaleFactor;
    }

    /// <summary>
    /// Decodes a Float32 using the raw SQPF register value as a byte-permutation table.
    /// Each nibble i of sqpfValue is the index into the transmitted byte stream for float byte i.
    /// </summary>
    public static double DecodeFloat32WithSqpf(ushort[] words, ushort sqpfValue, double scaleFactor = 1.0)
    {
        // Transmitted bytes in Modbus order: words[0] high, words[0] low, words[1] high, words[1] low
        Span<byte> t = [(byte)(words[0] >> 8), (byte)(words[0] & 0xFF), (byte)(words[1] >> 8), (byte)(words[1] & 0xFF)];

        // Reassemble float bytes: nibble i of sqpfValue = IEEE 754 float byte index at transmitted position i
        uint raw = 0;
        for (int i = 0; i < 4; i++)
        {
            int floatByteIdx = (sqpfValue >> (i * 4)) & 0xF;
            raw |= (uint)t[i] << (floatByteIdx * 8);
        }

        return BitConverter.Int32BitsToSingle((int)raw) * scaleFactor;
    }

    /// <summary>
    /// Combines two 16-bit words into a 32-bit unsigned integer respecting word order.
    /// BigEndian    = ABCD — words[0] is the high word.
    /// LittleEndian = CDAB — words[1] is the high word.
    /// ByteSwapped  = DCBA — bytes within each word are swapped, then words[1] is high.
    /// </summary>
    private static uint Combine32(ushort[] words, WordOrder wordOrder) => wordOrder switch
    {
        WordOrder.BigEndian    => ((uint)words[0] << 16) | words[1],
        WordOrder.LittleEndian => ((uint)words[1] << 16) | words[0],
        WordOrder.ByteSwapped  => ((uint)SwapBytes(words[1]) << 16) | SwapBytes(words[0]),
        _                      => throw new ArgumentOutOfRangeException(nameof(wordOrder), wordOrder, null)
    };

    private static ushort SwapBytes(ushort value) =>
        (ushort)((value >> 8) | (value << 8));
}
