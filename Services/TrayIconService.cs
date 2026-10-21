using System.Runtime.InteropServices;
using System.Text;

namespace NoctaliaWin.Services;

/// <summary>
/// Иконка в трее через чистый Win32 (Shell_NotifyIcon) — без WinForms,
/// чтобы не было конфликтов имён с WPF.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_LEFTALIGN = 0x0000;

    /// <summary>WM_APP-сообщение от трей-иконки.</summary>
    public const int TrayCallbackMessage = 0x8000 + 77;

    public const int CMD_SETTINGS = 1004;
    public const int CMD_OPEN_CONFIG = 1001;
    public const int CMD_RELOAD = 1002;
    public const int CMD_EXIT = 1003;

    /// <summary>Запрос на открытие окна настройки (из меню трей).</summary>
    public Action SettingsRequested;

    private IntPtr _hwnd;
    private bool _added;

    public void Attach(IntPtr hwnd, string tooltip)
    {
        _hwnd = hwnd;
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512), // IDI_APPLICATION
            szTip = tooltip,
        };
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    /// <summary>Вызывается из WndProc главного окна.</summary>
    public IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == TrayCallbackMessage && _added)
        {
            int mouseMsg = lParam.ToInt32();
            if (mouseMsg == WM_LBUTTONDBLCLK)
            {
                ConfigService.Instance.OpenConfigFolder();
                handled = true;
            }
            else if (mouseMsg == WM_RBUTTONUP)
            {
                ShowMenu();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        AppendMenu(menu, MF_STRING, (UIntPtr)CMD_SETTINGS, "Настройки...");
        AppendMenu(menu, MF_STRING, (UIntPtr)CMD_OPEN_CONFIG, "Папка конфига");
        AppendMenu(menu, MF_STRING, (UIntPtr)CMD_RELOAD, "Перезагрузить конфиг");
        AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
        AppendMenu(menu, MF_STRING, (UIntPtr)CMD_EXIT, "Выход");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);
        uint cmd = TrackPopupMenu(menu,
            TPM_LEFTALIGN | TPM_BOTTOMALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
            pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);
        PostMessage(_hwnd, 0x0000, IntPtr.Zero, IntPtr.Zero); // закрыть всплывающее меню

        switch (cmd)
        {
            case CMD_SETTINGS: SettingsRequested?.Invoke(); break;
            case CMD_OPEN_CONFIG: ConfigService.Instance.OpenConfigFolder(); break;
            case CMD_RELOAD: ConfigService.Instance.Reload(); break;
            case CMD_EXIT:
                System.Windows.Application.Current?.Shutdown();
                break;
        }
    }

    public void Dispose()
    {
        if (_added && _hwnd != IntPtr.Zero)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }
}
