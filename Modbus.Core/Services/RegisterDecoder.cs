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
    /// Combines two 16-bit words into a 32-bit unsigned integer respecting word order.
    /// BigEndian = words[0] is the high word (most common in energy meters).
    /// </summary>
    private static uint Combine32(ushort[] words, WordOrder wordOrder) => wordOrder switch
    {
        WordOrder.BigEndian    => ((uint)words[0] << 16) | words[1],
        WordOrder.LittleEndian => ((uint)words[1] << 16) | words[0],
        _                      => throw new ArgumentOutOfRangeException(nameof(wordOrder), wordOrder, null)
    };
}
