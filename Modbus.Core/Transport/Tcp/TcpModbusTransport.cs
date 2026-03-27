using System.Net.Sockets;
using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Transport.Tcp;

public class TcpModbusTransport : IModbusTransport
{
    private readonly TcpConfig _config;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected => _client?.Connected ?? false;

    public TcpModbusTransport(TcpConfig config) => _config = config;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_config.IpAddress, _config.Port, cancellationToken);
        _stream = _client.GetStream();
    }

    public Task DisconnectAsync()
    {
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ignores <paramref name="expectedResponseLength"/> — response length is read
    /// from the MBAP Length field in the first 6 bytes of the response.
    /// </summary>
    public async Task<byte[]> SendAsync(byte[] request, int expectedResponseLength, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var stream = _stream ?? throw new InvalidOperationException("Transport is not connected.");

            await stream.WriteAsync(request, cancellationToken);

            // Read the first 6 MBAP bytes: TxId(2) + ProtocolId(2) + Length(2)
            var header = new byte[6];
            await ReadExactAsync(stream, header, cancellationToken);

            // Length field = remaining bytes after it (UnitId + PDU)
            int remaining = (header[4] << 8) | header[5];
            var rest = new byte[remaining];
            await ReadExactAsync(stream, rest, cancellationToken);

            // Combine into the full frame the parser expects
            var response = new byte[6 + remaining];
            header.CopyTo(response, 0);
            rest.CopyTo(response, 6);
            return response;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int received = 0;
        while (received < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(received), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("Connection closed by remote device.");
            received += read;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _lock.Dispose();
    }
}
