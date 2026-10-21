using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using NoctaliaWin.Interop;

namespace NoctaliaWin.Services;

/// <summary>Лёгкие системные метрики: CPU, RAM, батарея, яркость.</summary>
public sealed class SystemStatsService : IDisposable
{
    private PerformanceCounter _cpuCounter;
    private bool _cpuFailed;
    private float _cpuSmoothed;

    public float CpuPercent { get; private set; }
    public float RamPercent { get; private set; }
    public long RamUsedGb { get; private set; }
    public long RamTotalGb { get; private set; }

    // Батарея
    public bool HasBattery { get; private set; }
    public int BatteryPercent { get; private set; }
    public bool OnAc { get; private set; }
    public bool Charging { get; private set; }

    // Сеть: скорость приёма/передачи, как network_rx/network_tx в Noctalia
    private long _lastRxBytes = -1, _lastTxBytes = -1;
    private DateTime _lastNetSampleUtc = DateTime.MinValue;
    public double NetRxBytesPerSec { get; private set; }
    public double NetTxBytesPerSec { get; private set; }

    /// <summary>Готовая подпись скорости сети (компактная, как network_speed_compact).</summary>
    public string NetText { get; private set; } = "";

    public SystemStatsService()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ = _cpuCounter.NextValue(); // первый замер всегда 0 — прогрев
        }
        catch
        {
            _cpuFailed = true;
        }
        Refresh();
    }

    public void Refresh()
    {
        // CPU
        if (!_cpuFailed)
        {
            try
            {
                float raw = _cpuCounter.NextValue();
                _cpuSmoothed = _cpuSmoothed <= 0 ? raw : _cpuSmoothed * 0.7f + raw * 0.3f;
                CpuPercent = Math.Clamp(_cpuSmoothed, 0, 100);
            }
            catch
            {
                _cpuFailed = true;
            }
        }

        // RAM
        try
        {
            var mem = new MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(ref mem))
            {
                RamPercent = mem.dwMemoryLoad;
                RamTotalGb = (long)(mem.ullTotalPhys / 1024 / 1024 / 1024);
                RamUsedGb = (long)((mem.ullTotalPhys - mem.ullAvailPhys) / 1024 / 1024 / 1024);
            }
        }
        catch { /* noop */ }

        // Батарея
        if (NativeMethods.GetSystemPowerStatus(out var sps))
        {
            HasBattery = sps.BatteryFlag != 128; // 128 = батареи нет
            BatteryPercent = sps.BatteryLifePercent == 255 ? 0 : sps.BatteryLifePercent;
            OnAc = sps.ACLineStatus == 1;
            Charging = (sps.BatteryFlag & 8) != 0;
        }

        RefreshNetwork();
    }

    /// <summary>Скорость сети по счётчикам интерфейсов (как network монитор в Noctalia).</summary>
    private void RefreshNetwork()
    {
        try
        {
            long rx = 0, tx = 0;
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                var stats = ni.GetIPStatistics();
                rx += stats.BytesReceived;
                tx += stats.BytesSent;
            }

            var now = DateTime.UtcNow;
            if (_lastRxBytes >= 0 && _lastNetSampleUtc != DateTime.MinValue)
            {
                double dt = (now - _lastNetSampleUtc).TotalSeconds;
                if (dt > 0.2)
                {
                    double rxRate = Math.Max(0, (rx - _lastRxBytes) / dt);
                    double txRate = Math.Max(0, (tx - _lastTxBytes) / dt);
                    // сглаживание, чтобы подпись не дёргалась
                    NetRxBytesPerSec = NetRxBytesPerSec <= 0 ? rxRate : NetRxBytesPerSec * 0.6 + rxRate * 0.4;
                    NetTxBytesPerSec = NetTxBytesPerSec <= 0 ? txRate : NetTxBytesPerSec * 0.6 + txRate * 0.4;
                    NetText = BuildNetText();
                }
            }

            _lastRxBytes = rx;
            _lastTxBytes = tx;
            _lastNetSampleUtc = now;
        }
        catch { /* noop */ }
    }

    /// <summary>Компактная подпись: "↓1.2M ↑240K" (как network_speed_compact в оригинале).</summary>
    private string BuildNetText()
    {
        // очень маленький поток считаем «тишиной», чтобы виджет не мигал
        if (NetRxBytesPerSec < 1024 && NetTxBytesPerSec < 1024) return "";
        return $"\u2193{Fmt(NetRxBytesPerSec)} \u2191{Fmt(NetTxBytesPerSec)}";
    }

    private static string Fmt(double bytesPerSec)
    {
        if (bytesPerSec >= 1024 * 1024) return $"{bytesPerSec / 1024 / 1024:0.#}M";
        if (bytesPerSec >= 1024) return $"{bytesPerSec / 1024:0}K";
        return $"{bytesPerSec:0}B";
    }

    /// <summary>Яркость 0–100; -1 если недоступно (десктоп без WMI-монитора).</summary>
    public int GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (var o in searcher.Get())
                return Convert.ToInt32(o["CurrentBrightness"]);
        }
        catch { /* noop */ }
        return -1;
    }

    public void SetBrightness(int value)
    {
        value = Math.Clamp(value, 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (var o in searcher.Get())
            {
                if (o is ManagementObject mo)
                    mo.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)value });
                break;
            }
        }
        catch { /* noop */ }
    }

    public void Dispose() => _cpuCounter?.Dispose();
}

[StructLayout(LayoutKind.Sequential)]
internal struct MEMORYSTATUSEX
{
    public uint dwLength;
    public uint dwMemoryLoad;
    public ulong ullTotalPhys;
    public ulong ullAvailPhys;
    public ulong ullTotalPageFile;
    public ulong ullAvailPageFile;
    public ulong ullTotalVirtual;
    public ulong ullAvailVirtual;
    public ulong ullAvailExtendedVirtual;

    public MEMORYSTATUSEX()
    {
        dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
    }
}
