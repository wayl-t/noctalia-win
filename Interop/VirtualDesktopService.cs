using System.Runtime.InteropServices;

namespace NoctaliaWin.Interop;

/// <summary>
/// COM-интероп для виртуальных рабочих столов Windows 10 (1809–22H2).
/// GUID'ы и порядок методов — по публичной реализации MScholtes/VirtualDesktop (MIT).
/// </summary>
internal static class VdGuids
{
    public static readonly Guid CLSID_ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    public static readonly Guid CLSID_VirtualDesktopManagerInternal = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
}

[ComImport, Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
internal interface IApplicationView { }

[ComImport, Guid("FF72FFDD-BE7E-43FC-9C03-AD81681E88E4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktop
{
    bool IsViewVisible(IApplicationView view);
    Guid GetId();
}

[ComImport, Guid("F31574D6-B682-4CDC-BD56-1827860ABEC6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopManagerInternal
{
    int GetCount();
    void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
    bool CanViewMoveDesktops(IApplicationView view);
    IVirtualDesktop GetCurrentDesktop();
    void GetDesktops(out IObjectArray desktops);
    [PreserveSig]
    int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
    void SwitchDesktop(IVirtualDesktop desktop);
    IVirtualDesktop CreateDesktop();
    void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
    IVirtualDesktop FindDesktop(ref Guid desktopid);
}

[ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectArray
{
    void GetCount(out int count);
    void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
}

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IServiceProvider10
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(ref Guid service, ref Guid riid);
}

/// <summary>Безопасная обёртка над COM API виртуальных рабочих столов.</summary>
public sealed class VirtualDesktopService
{
    private IVirtualDesktopManagerInternal _manager;
    private bool _unavailable;

    public bool IsAvailable => !_unavailable && EnsureManager() != null;

    private IVirtualDesktopManagerInternal EnsureManager()
    {
        if (_unavailable) return null;
        if (_manager != null) return _manager;
        try
        {
            var shell = (IServiceProvider10)Activator.CreateInstance(
                Type.GetTypeFromCLSID(VdGuids.CLSID_ImmersiveShell));
            Guid service = VdGuids.CLSID_VirtualDesktopManagerInternal;
            Guid iid = typeof(IVirtualDesktopManagerInternal).GUID;
            _manager = (IVirtualDesktopManagerInternal)shell.QueryService(ref service, ref iid);
            Marshal.ReleaseComObject(shell);
        }
        catch
        {
            _unavailable = true;
            _manager = null;
        }
        return _manager;
    }

    public int GetCount()
    {
        var m = EnsureManager();
        if (m == null) return 0;
        try { return m.GetCount(); }
        catch { _unavailable = true; return 0; }
    }

    public int GetCurrentIndex()
    {
        var m = EnsureManager();
        if (m == null) return -1;
        try
        {
            var current = m.GetCurrentDesktop();
            if (current == null) return -1;
            var currentId = current.GetId();

            m.GetDesktops(out IObjectArray desktops);
            var iid = typeof(IVirtualDesktop).GUID;
            for (int i = 0; i < m.GetCount(); i++)
            {
                desktops.GetAt(i, ref iid, out object obj);
                var desktop = (IVirtualDesktop)obj;
                if (desktop.GetId() == currentId)
                {
                    Marshal.ReleaseComObject(desktops);
                    return i;
                }
            }
            Marshal.ReleaseComObject(desktops);
            return -1;
        }
        catch { _unavailable = true; return -1; }
    }

    public void SwitchTo(int index)
    {
        var m = EnsureManager();
        if (m == null) return;
        try
        {
            m.GetDesktops(out IObjectArray desktops);
            var iid = typeof(IVirtualDesktop).GUID;
            desktops.GetAt(index, ref iid, out object obj);
            Marshal.ReleaseComObject(desktops);
            m.SwitchDesktop((IVirtualDesktop)obj);
        }
        catch { /* noop */ }
    }

    public void Create()
    {
        var m = EnsureManager();
        try { m?.CreateDesktop(); }
        catch { /* noop */ }
    }
}
