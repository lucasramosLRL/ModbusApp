using System.IO.Ports;
using Modbus.Core.Domain.ValueObjects;
using DomainParity = Modbus.Core.Domain.Enums.Parity;
using DomainStopBits = Modbus.Core.Domain.Enums.StopBits;

namespace Modbus.Core.Transport.Rtu;

public class RtuModbusTransport : IModbusTransport
{
    private readonly RtuConfig _config;
    private SerialPort? _port;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Timeout in milliseconds used when reading variable-length responses (expectedResponseLength = 0).
    /// Covers the Modbus inter-frame silence requirement plus USB-serial adapter latency.
    /// </summary>
    private const int VariableLengthReadTimeoutMs = 100;

    public bool IsConnected => _port?.IsOpen ?? false;

    public RtuModbusTransport(RtuConfig config) => _config = config;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _port = new SerialPort(
            _config.PortName,
            _config.BaudRate,
            MapParity(_config.Parity),
            _config.DataBits,
            MapStopBits(_config.StopBits))
        {
            ReadTimeout  = 1000,
            WriteTimeout = 1000
        };
        _port.Open();
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _port?.Close();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a request and reads the response.
    /// When <paramref name="expectedResponseLength"/> is 0, reads until the inter-frame
    /// timeout fires — use this for FC17 (Report Slave ID) whose response length is unknown.
    /// </summary>
    public async Task<byte[]> SendAsync(byte[] request, int expectedResponseLength, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var port = _port ?? throw new InvalidOperationException("Serial port is not open.");

            port.DiscardInBuffer();
            await port.BaseStream.WriteAsync(request, cancellationToken);

            return expectedResponseLength > 0
                ? await ReadExactAsync(port.BaseStream, expectedResponseLength, cancellationToken)
                : await ReadUntilSilenceAsync(port.BaseStream, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        int received = 0;
        while (received < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(received), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("Serial port stream ended unexpectedly.");
            received += read;
        }
        return buffer;
    }

    /// <summary>
    /// Reads bytes until no new data arrives within <see cref="VariableLengthReadTimeoutMs"/>.
    /// Used for responses where the length is not known ahead of time (e.g. FC17).
    /// </summary>
    private static async Task<byte[]> ReadUntilSilenceAsync(Stream stream, CancellationToken cancellationToken)
    {
        var chunks = new List<byte[]>();
        var temp = new byte[256];

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(VariableLengthReadTimeoutMs);

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(temp.AsMemory(), cts.Token);
                if (read == 0) break;
                chunks.Add(temp[..read].ToArray());

                // Reset timeout after each received chunk
                cts.CancelAfter(VariableLengthReadTimeoutMs);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Inter-frame silence detected — this is the expected end of the RTU frame.
        }

        return chunks.Count == 0 ? [] : [.. chunks.SelectMany(c => c)];
    }

    private static Parity MapParity(DomainParity parity) => parity switch
    {
        DomainParity.Even => Parity.Even,
        DomainParity.Odd  => Parity.Odd,
        _                 => Parity.None
    };

    private static StopBits MapStopBits(DomainStopBits stopBits) => stopBits switch
    {
        DomainStopBits.Two => StopBits.Two,
        _                  => StopBits.One
    };

    public void Dispose()
    {
        _port?.Dispose();
        _lock.Dispose();
    }
}
