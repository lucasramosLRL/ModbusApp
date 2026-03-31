using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
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

        // Probe address 254 first — yield if a device responds
        progress?.Report(new ScanProgress { Current = 0, Total = total, Found = found, CurrentLabel = "Address 254" });
        DeviceScanResult? probeResult = null;
        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeCts.CancelAfter(TimeSpan.FromSeconds(2));
            var probeData = await service.ReportSlaveIdAsync(254, probeCts.Token);
            var probeSerial = await TryReadSerialNumberAsync(service, 254, cancellationToken);
            probeResult = BuildResult(254, probeData.RawData, probeSerial, null, rtuConfig);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        catch (Exception) { }
        if (probeResult != null) { found++; yield return probeResult; }

        for (byte addr = 1; addr <= 247; addr++)
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

    private const int UdpDiscoveryPort = 30718;
    private const int ModbusTcpPort = 502;

    public async IAsyncEnumerable<DeviceScanResult> ScanTcpAsync(
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

        // Step 1 — UDP broadcast from all active adapters and collect responding IPs + response data
        var respondingDevices = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        var localAddresses = GetBroadcastInterfaces();
        var udpClients = localAddresses
            .Select(ip =>
            {
                var c = new UdpClient();
                c.EnableBroadcast = true;
                c.Client.Bind(new IPEndPoint(ip, 0));
                return c;
            })
            .ToList();

        try
        {
            var broadcastEp = new IPEndPoint(IPAddress.Broadcast, UdpDiscoveryPort);

            // Send discovery broadcast 3 times from all adapters, listening 2s after each
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await Task.WhenAll(udpClients.Select(c =>
                    c.SendAsync(UdpDiscoveryFrame, UdpDiscoveryFrame.Length, broadcastEp)));

                using var listenCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                listenCts.CancelAfter(TimeSpan.FromMilliseconds(2000));

                await Task.WhenAll(udpClients.Select(c =>
                    DrainClientAsync(c, respondingDevices, listenCts.Token, progress, cancellationToken)));
            }
        }
        finally
        {
            foreach (var c in udpClients)
                c.Dispose();
        }

        // Step 2 — connect via Modbus TCP to each discovered IP to read device info
        var deviceList = respondingDevices
            .Select(kv => (Ip: kv.Key, UdpResponse: kv.Value))
            .ToList();
        int total = deviceList.Count;
        int found = 0;

        for (int i = 0; i < deviceList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (ip, udpResponse) = deviceList[i];
            var udpInfo = ParseUdpResponse(udpResponse);

            progress?.Report(new ScanProgress
            {
                Current = i + 1,
                Total = total,
                Found = found,
                CurrentLabel = ip
            });

            var tcpConfig = new TcpConfig { IpAddress = ip, Port = ModbusTcpPort };
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
                // Modbus TCP failed — use device info parsed from UDP response
                string? modelName = udpInfo.DeviceCode.HasValue
                    ? DeviceCodeRegistry.GetModelName(udpInfo.DeviceCode.Value)
                    : null;

                string suggestedName = (modelName, udpInfo.SerialNumber) switch
                {
                    (not null, not null) => $"{modelName} #{udpInfo.SerialNumber.Value:D8}",
                    (not null, null)     => $"{modelName} @ {ip}",
                    _                    => $"Device @ {ip}"
                };

                result = new DeviceScanResult
                {
                    SlaveId = 1,
                    DeviceCode = udpInfo.DeviceCode,
                    ModelName = modelName,
                    SerialNumber = udpInfo.SerialNumber,
                    SuggestedName = suggestedName,
                    Tcp = tcpConfig
                };
            }

            found++;
            yield return result;
        }
    }

    private static IReadOnlyList<IPAddress> GetBroadcastInterfaces()
    {
        var results = new List<IPAddress>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address)) continue;
                results.Add(unicast.Address);
            }
        }

        // Fallback: if nothing passed the filter, use original behavior
        if (results.Count == 0)
            results.Add(IPAddress.Any);

        return results;
    }

    private static async Task DrainClientAsync(
        UdpClient udp,
        ConcurrentDictionary<string, byte[]> respondingDevices,
        CancellationToken listenToken,
        IProgress<ScanProgress>? progress,
        CancellationToken userCancellationToken)
    {
        try
        {
            while (!listenToken.IsCancellationRequested)
            {
                var udpResult = await udp.ReceiveAsync(listenToken);
                var ip = udpResult.RemoteEndPoint.Address.ToString();
                if (respondingDevices.TryAdd(ip, udpResult.Buffer))
                {
                    progress?.Report(new ScanProgress
                    {
                        Current = 0,
                        Total = 1,
                        Found = respondingDevices.Count,
                        CurrentLabel = $"Heard from {ip}"
                    });
                }
            }
        }
        catch (OperationCanceledException) when (!userCancellationToken.IsCancellationRequested)
        {
            // 2s listen window closed — normal
        }
    }

    /// <summary>
    /// Parses the UDP discovery response.
    /// Format: [0-3] Header (00 00 00 F7) | [4-7] Serial (uint32 BE) | [8] Device code | [9] Special version
    /// </summary>
    private static (uint? SerialNumber, byte? DeviceCode, byte? SpecialVersion) ParseUdpResponse(byte[] buffer)
    {
        if (buffer is null || buffer.Length < 10)
            return (null, null, null);

        uint serialNumber = (uint)(buffer[4] << 24 | buffer[5] << 16 | buffer[6] << 8 | buffer[7]);
        byte deviceCode = buffer[8];
        byte specialVersion = buffer[9];

        return (serialNumber, deviceCode, specialVersion);
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
