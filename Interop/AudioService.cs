using System.Runtime.InteropServices;

namespace NoctaliaWin.Interop;

// Core Audio API — управление громкостью через COM (IMMDeviceEnumerator / IAudioEndpointVolume).
internal static class AudioGuids
{
    public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
}

// Порядок методов строго по endpointvolume.h (Windows SDK).
// ВАЖНО: в интерфейсе НЕТ SetAllVolumes/GetAllVolumes, а SetMute идёт ПЕРЕД GetMute —
// любое расхождение сдвигает vtable-слоты и вызовы попадают в чужие методы (AV в памяти).
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);            // 3
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);          // 4
    [PreserveSig] int GetChannelCount(out uint channelCount);                // 5
    [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr eventContext);          // 6
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr eventContext);     // 7
    [PreserveSig] int GetMasterVolumeLevel(out float level);                 // 8
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);           // 9
    [PreserveSig] int SetChannelVolumeLevel(uint channelNum, float level, IntPtr eventContext);   // 10
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channelNum, float level, IntPtr eventContext); // 11
    [PreserveSig] int GetChannelVolumeLevel(uint channelNum, out float level);           // 12
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channelNum, out float level);     // 13
    [PreserveSig] int SetMute(int mute, IntPtr eventContext);                // 14
    [PreserveSig] int GetMute(out int mute);                                 // 15
    [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);  // 16
    [PreserveSig] int VolumeStepUp(IntPtr eventContext);                     // 17
    [PreserveSig] int VolumeStepDown(IntPtr eventContext);                   // 18
    [PreserveSig] int QueryHardwareSupport(out uint mask);                   // 19
    [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb); // 20
}

/// <summary>Управление громкостью системного аудиовыхода.</summary>
public sealed class AudioService : IDisposable
{
    private const int ERender = 0;
    private const int EConsole = 0;

    private IMMDeviceEnumerator _enumerator;
    private IAudioEndpointVolume _volume;
    private bool _unavailable;

    public bool IsAvailable => !_unavailable && Ensure() != null;

    private IAudioEndpointVolume Ensure()
    {
        if (_unavailable) return null;
        if (_volume != null) return _volume;
        try
        {
            _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(AudioGuids.CLSID_MMDeviceEnumerator));
            if (_enumerator.GetDefaultAudioEndpoint(ERender, EConsole, out IMMDevice device) != 0
                || device == null)
            {
                _unavailable = true;
                return null;
            }

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            int hr = device.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out object obj);
            Marshal.ReleaseComObject(device);
            if (hr != 0 || obj == null)
            {
                _unavailable = true;
                return null;
            }
            _volume = (IAudioEndpointVolume)obj;
        }
        catch
        {
            _unavailable = true;
            _volume = null;
        }
        return _volume;
    }

    /// <summary>Громкость 0.0–1.0; при недоступности — 0.</summary>
    public float GetVolume()
    {
        var v = Ensure();
        if (v == null) return 0f;
        try { v.GetMasterVolumeLevelScalar(out float level); return level; }
        catch { return 0f; }
    }

    public void SetVolume(float level01)
    {
        var v = Ensure();
        if (v == null) return;
        level01 = Math.Clamp(level01, 0f, 1f);
        try { v.SetMasterVolumeLevelScalar(level01, IntPtr.Zero); }
        catch { /* noop */ }
    }

    public bool GetMute()
    {
        var v = Ensure();
        if (v == null) return false;
        try { v.GetMute(out int mute); return mute != 0; }
        catch { return false; }
    }

    public void SetMute(bool mute)
    {
        var v = Ensure();
        if (v == null) return;
        try { v.SetMute(mute ? 1 : 0, IntPtr.Zero); }
        catch { /* noop */ }
    }

    public void ToggleMute() => SetMute(!GetMute());

    public void Dispose()
    {
        if (_volume != null) Marshal.ReleaseComObject(_volume);
        if (_enumerator != null) Marshal.ReleaseComObject(_enumerator);
    }
}
