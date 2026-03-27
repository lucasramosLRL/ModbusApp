using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Modbus.Core.Domain.Entities;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Services.Scanning;

public class DeviceScanService : IDeviceScanService
{
    private readonly IModbusServiceFactory _factory;

    public DeviceScanService(IModbusServiceFactory factory)
    {
        _factory = factory;
    }

    public async IAsyncEnumerable<DeviceScanResult> ScanRtuAsync(
        RtuConfig rtuConfig,
        byte startAddress,
        byte endAddress,
        IProgress<ScanProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int total = endAddress - startAddress + 1;
        int found = 0;

        var tempDevice = new ModbusDevice
        {
            Name = "scan-probe",
            SlaveId = startAddress,
            TransportType = TransportType.Rtu,
            Rtu = rtuConfig
        };

        using var service = _factory.Create(tempDevice);

        await service.ConnectAsync(cancellationToken);

        for (byte addr = startAddress; addr <= endAddress; addr++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int current = addr - startAddress + 1;
            progress?.Report(new ScanProgress
            {
                Current = current,
                Total = total,
                Found = found,
                CurrentLabel = $"Address {addr}"
            });

            DeviceScanResult? result = null;
            try
            {
                using var perAddrCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                perAddrCts.CancelAfter(TimeSpan.FromSeconds(2));

                var slaveIdData = await service.ReportSlaveIdAsync(addr, perAddrCts.Token);
                var serialNumber = await TryReadSerialNumberAsync(service, addr, cancellationToken);
                result = BuildResult(addr, slaveIdData.RawData, serialNumber, null, rtuConfig);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // per-address timeout — device not present, continue
            }
            catch (Exception)
            {
                // any other error (Modbus exception, framing error) — skip address
            }

            if (result != null)
            {
                found++;
                yield return result;
            }
        }
    }

    // Discovery frame sent as UDP broadcast: 00 00 00 F6
    private static readonly byte[] UdpDiscoveryFrame = [0x00, 0x00, 0x00, 0xF6];

    public async IAsyncEnumerable<DeviceScanResult> ScanTcpAsync(
        int port,
        IProgress<ScanProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        progress?.Report(new ScanProgress
        {
            Current = 0,
            Total = 1,
            Found = 0,
            CurrentLabel = "Broadcasting..."
        });

        // Step 1 — UDP broadcast and collect responding IPs
        var respondingIps = new List<string>();

        using (var udp = new UdpClient())
        {
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            var broadcastEp = new IPEndPoint(IPAddress.Broadcast, port);
            await udp.SendAsync(UdpDiscoveryFrame, UdpDiscoveryFrame.Length, broadcastEp);

            using var listenCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            listenCts.CancelAfter(TimeSpan.FromSeconds(3));

            try
            {
                while (!listenCts.Token.IsCancellationRequested)
                {
                    var udpResult = await udp.ReceiveAsync(listenCts.Token);
                    var ip = udpResult.RemoteEndPoint.Address.ToString();
                    if (!respondingIps.Contains(ip))
                    {
                        respondingIps.Add(ip);
                        progress?.Report(new ScanProgress
                        {
                            Current = 0,
                            Total = 1,
                            Found = respondingIps.Count,
                            CurrentLabel = $"Heard from {ip}"
                        });
                    }
                }
            }
            catch (OperationCanceledException) { /* listen window closed */ }
        }

        // Step 2 — connect via Modbus TCP to each discovered IP to read device info
        int total = respondingIps.Count;
        int found = 0;

        for (int i = 0; i < respondingIps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ip = respondingIps[i];
            progress?.Report(new ScanProgress
            {
                Current = i + 1,
                Total = total,
                Found = found,
                CurrentLabel = ip
            });

            var tcpConfig = new TcpConfig { IpAddress = ip, Port = port };
            var tempDevice = new ModbusDevice
            {
                Name = "scan-probe",
                SlaveId = 1,
                TransportType = TransportType.Tcp,
                Tcp = tcpConfig
            };

            DeviceScanResult result;
            using var service = _factory.Create(tempDevice);
            try
            {
                using var perIpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                perIpCts.CancelAfter(TimeSpan.FromMilliseconds(1500));

                await service.ConnectAsync(perIpCts.Token);
                var slaveIdData = await service.ReportSlaveIdAsync(1, perIpCts.Token);
                var serialNumber = await TryReadSerialNumberAsync(service, 1, cancellationToken);
                result = BuildResult(1, slaveIdData.RawData, serialNumber, tcpConfig, null);
            }
            catch (Exception)
            {
                // Device responded to broadcast but didn't answer Modbus — return with basic info
                result = new DeviceScanResult
                {
                    SlaveId = 1,
                    SuggestedName = $"Device @ {ip}",
                    Tcp = tcpConfig
                };
            }

            found++;
            yield return result;
        }
    }

    private static async Task<uint?> TryReadSerialNumberAsync(
        IModbusService service, byte slaveId, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var words = await service.ReadInputRegistersAsync(slaveId, 0, 2, cts.Token);
            return (uint)(words[0] << 16) | words[1];
        }
        catch
        {
            return null;
        }
    }

    private static DeviceScanResult BuildResult(
        byte slaveId,
        byte[] rawData,
        uint? serialNumber,
        TcpConfig? tcp,
        RtuConfig? rtu)
    {
        byte? deviceCode = rawData.Length > 0 ? rawData[0] : null;
        byte? firmwareVersion = rawData.Length > 2 ? rawData[2] : null;
        string? modelName = deviceCode.HasValue ? DeviceCodeRegistry.GetModelName(deviceCode.Value) : null;

        string suggestedName = (modelName, serialNumber) switch
        {
            (not null, not null) => $"{modelName} #{serialNumber.Value:D8}",
            (not null, null)     => $"{modelName} (Slave {slaveId})",
            _                    => $"Device 0x{slaveId:X2}"
        };

        return new DeviceScanResult
        {
            SlaveId = slaveId,
            DeviceCode = deviceCode,
            ModelName = modelName,
            FirmwareVersion = firmwareVersion,
            SerialNumber = serialNumber,
            SuggestedName = suggestedName,
            Tcp = tcp,
            Rtu = rtu
        };
    }

}
