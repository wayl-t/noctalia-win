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
