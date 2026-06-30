using System;

namespace Modbus.Mobile.Avalonia.Services;

/// <summary>
/// Platform hook for holding any OS-level lock required while doing a UDP broadcast
/// device scan. On Android the Wi-Fi driver drops broadcast/multicast packets unless a
/// <c>WifiManager.MulticastLock</c> is held, so the Android head supplies a real impl;
/// other platforms use the no-op default.
/// </summary>
public interface INetworkScanLock
{
    /// <summary>Acquires the lock; dispose the returned token to release it.</summary>
    IDisposable Acquire();
}

/// <summary>Default no-op lock (desktop / previewer / platforms without the restriction).</summary>
public sealed class NoopNetworkScanLock : INetworkScanLock
{
    private sealed class NoopToken : IDisposable
    {
        public void Dispose() { }
    }

    public IDisposable Acquire() => new NoopToken();
}
