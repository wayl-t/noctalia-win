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

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int GetChannelCount(out uint channelCount);
    [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr eventContext);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr eventContext);
    [PreserveSig] int GetMasterVolumeLevel(out float level);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint channelNum, float level, IntPtr eventContext);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channelNum, float level, IntPtr eventContext);
    [PreserveSig] int GetChannelVolumeLevel(uint channelNum, out float level);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channelNum, out float level);
    [PreserveSig] int SetAllVolumes(IntPtr levels, IntPtr eventContext);
    [PreserveSig] int GetAllVolumes(IntPtr levels);
    [PreserveSig] int GetMute(out int mute);
    [PreserveSig] int SetMute(int mute, IntPtr eventContext);
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
            _enumerator.GetDefaultAudioEndpoint(ERender, EConsole, out IMMDevice device);

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out object obj);
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
