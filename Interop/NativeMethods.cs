using System.Runtime.InteropServices;

namespace NoctaliaWin.Interop;

internal static class NativeMethods
{
    // ---------- DWM / композиция (Acrylic на Windows 10) ----------

    internal enum WINDOWCOMPOSITIONATTRIB : uint
    {
        WCA_ACCENT_POLICY = 19,
    }

    internal enum ACCENT_STATE
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ACCENT_POLICY
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor; // ABGR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWCOMPOSITIONATTRIBDATA
    {
        public WINDOWCOMPOSITIONATTRIB Attrib;
        public IntPtr pvData;
        // В нативном API это SIZE_T (8 байт на x64). Объявлено как nuint,
        // иначе старшие 4 байта читаются со стека с мусором и Windows
        // портит память по pbData — отсюда AccessViolation в разных местах.
        public nuint cbData;
    }

    [DllImport("user32.dll")]
    internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

    /// <summary>Включает Acrylic-фон окна. color — цвет подложки в формате 0xAARRGGBB (упакуется в ABGR).</summary>
    internal static void EnableAcrylic(IntPtr hwnd, uint argbTint)
    {
        var accent = new ACCENT_POLICY
        {
            AccentState = (int)ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 2,
            // GradientColor в формате ABGR: A | B | G | R
            GradientColor = (argbTint & 0xFF000000)
                          | ((argbTint & 0x000000FF) << 16)
                          | (argbTint & 0x0000FF00)
                          | ((argbTint & 0x00FF0000) >> 16),
        };

        int size = Marshal.SizeOf<ACCENT_POLICY>();
        IntPtr pData = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(accent, pData, false);
        try
        {
            var data = new WINDOWCOMPOSITIONATTRIBDATA
            {
                Attrib = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                pvData = pData,
                cbData = (nuint)size,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(pData);
        }
    }

    // ---------- Скруглённая область окна (Windows 10 не скругляет через DWM) ----------

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll")]
    internal static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    internal static void SetRoundedRegion(IntPtr hwnd, int widthPx, int heightPx, int radiusPx)
    {
        if (widthPx <= 0 || heightPx <= 0) return;
        IntPtr region = CreateRoundRectRgn(0, 0, widthPx + 1, heightPx + 1, radiusPx * 2, radiusPx * 2);
        SetWindowRgn(hwnd, region, true); // регион освобождается системой
    }

    // ---------- Скрытие из Alt-Tab ----------

    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    internal const int WM_MOUSEACTIVATE = 0x0021;
    internal const int MA_NOACTIVATE = 3;
    internal const int WM_DISPLAYCHANGE = 0x007E;
    internal const int WM_HOTKEY = 0x0312;

    // ---------- Рабочая область (чтобы окна не залезали под панель) ----------

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    internal const uint SPI_GETWORKAREA = 0x0030;
    internal const uint SPI_SETWORKAREA = 0x002F;
    internal const uint SPIF_UPDATEINIFILE = 0x01;
    internal const uint SPIF_SENDCHANGE = 0x02;

    // ---------- Активное окно ----------

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // ---------- Питание ----------

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMPOWERSTATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    internal static extern bool GetSystemPowerStatus(out SYSTEMPOWERSTATUS sps);

    [DllImport("user32.dll")]
    internal static extern bool LockWorkStation();

    [DllImport("powrprof.dll")]
    internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // ---------- Ограничение частоты кадров не требуется; хоткеи ----------

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_WIN = 0x0008;
    internal const uint VK_SPACE = 0x20;

    // ---------- Монитор / DPI ----------

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr hwnd);

    // ---------- Память ----------

    [DllImport("kernel32.dll")]
    internal static extern bool GlobalMemoryStatusEx(ref Services.MEMORYSTATUSEX lpBuffer);

    // ---------- Запуск файлов через оболочку ----------

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr ShellExecuteW(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);

    internal const int SW_SHOWNORMAL = 1;
}
