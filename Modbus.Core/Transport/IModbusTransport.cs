namespace Modbus.Core.Transport;

public interface IModbusTransport : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();

    /// <summary>
    /// Sends a request frame and returns the complete response frame.
    /// </summary>
    /// <param name="request">The full ADU request bytes to send.</param>
    /// <param name="expectedResponseLength">
    /// Number of bytes to read. Pass 0 for variable-length responses (e.g. FC17 over RTU),
    /// which will read until the inter-frame timeout. TCP transports always ignore this
    /// parameter and determine the length from the MBAP header.
    /// </param>
    Task<byte[]> SendAsync(byte[] request, int expectedResponseLength, CancellationToken cancellationToken = default);
}
