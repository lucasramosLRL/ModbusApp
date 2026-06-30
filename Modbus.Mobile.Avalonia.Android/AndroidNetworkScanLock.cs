using System;
using Android.Content;
using Android.Net.Wifi;
using Modbus.Mobile.Avalonia.Services;
using Application = Android.App.Application;

namespace Modbus.Mobile.Avalonia.Android;

/// <summary>
/// Holds a Wi-Fi <see cref="WifiManager.MulticastLock"/> (and a WifiLock) for the
/// duration of a UDP broadcast device scan. Without the multicast lock the Android
/// Wi-Fi driver filters out the broadcast replies and the scan finds nothing.
/// Requires CHANGE_WIFI_MULTICAST_STATE + ACCESS_WIFI_STATE in the manifest.
/// </summary>
public sealed class AndroidNetworkScanLock : INetworkScanLock
{
    public IDisposable Acquire()
    {
        var wifi = (WifiManager?)Application.Context.GetSystemService(Context.WifiService);
        return new Token(wifi);
    }

    private sealed class Token : IDisposable
    {
        private WifiManager.MulticastLock? _multicast;

        public Token(WifiManager? wifi)
        {
            var multicast = wifi?.CreateMulticastLock("modbus-scan");
            if (multicast is null) return;

            multicast.SetReferenceCounted(false);
            multicast.Acquire();
            _multicast = multicast;
        }

        public void Dispose()
        {
            try { if (_multicast is { IsHeld: true }) _multicast.Release(); } catch { }
            _multicast?.Dispose();
            _multicast = null;
        }
    }
}
