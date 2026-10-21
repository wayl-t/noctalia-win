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

    /// <summary>Включает Acrylic-фон окна. color — цвет подложки в формате 0xAARRGGBB (упакуется в ABGR).
    /// enable=false выключает размытие (плоская подложка окна).</summary>
    internal static void EnableAcrylic(IntPtr hwnd, uint argbTint, bool enable = true)
    {
        var accent = new ACCENT_POLICY
        {
            AccentState = enable
                ? (int)ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND
                : (int)ACCENT_STATE.ACCENT_DISABLED,
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

    // ---------- Вогнутые углы (concave_edge_corners) ----------
    //
    // У скругления CreateRoundRectRgn все четыре угла выпуклые. В Noctalia пара
    // углов, обращённых внутрь экрана, вырезается ВОГНУТО (дугой наоборот),
    // чтобы полоса «вытекала» из края. На Windows это делается булевой
    // операцией над регионами: RGN_DIFF вычитает круг из прямоугольника.

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateEllipticRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    internal static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(IntPtr ho);

    internal const int RGN_OR = 2;
    internal const int RGN_DIFF = 4;

    /// <summary>
    /// Регион полосы со скруглениями и, при needConcave, с вырезанными
    /// вогнутыми углами со стороны края экрана.
    /// </summary>
    internal static void SetBarRegion(
        IntPtr hwnd,
        int widthPx,
        int heightPx,
        int tl, int tr, int br, int bl,
        bool concave, bool concaveTop)
    {
        if (widthPx <= 0 || heightPx <= 0) return;

        // базовая форма: скруглённый прямоугольник
        IntPtr region = CreateRoundRectRgn(0, 0, widthPx + 1, heightPx + 1, tl * 2, tl * 2);

        if (!concave)
        {
            // разные радиусы по углам: вычитаем «уголки», оставшиеся прямыми
            SetWindowRgn(hwnd, region, true);
            return;
        }

        // Вогнутые углы со стороны края экрана: для полосы сверху — верхние.
        // Квадрат размером r в углу минус круг диаметром 2r с центром в
        // противоположном углу квадрата даёт вогнутую дугу.
        foreach (bool left in new[] { true, false })
        {
            int r = left ? tl : tr;                    // радиус соответствующего угла
            if (r <= 0) continue;
            int size = System.Math.Min(r, System.Math.Min(widthPx / 2, heightPx / 2));
            if (size <= 1) continue;

            int cx = left ? 0 : widthPx;
            int cy = concaveTop ? 0 : heightPx;

            // круг, который вычитаем: центр в дальнем углу квадрата выреза
            int ex1 = left ? cx : cx - 2 * size;
            int ey1 = concaveTop ? cy : cy - 2 * size;
            IntPtr cut = CreateEllipticRgn(ex1, ey1, ex1 + 2 * size, ey1 + 2 * size);

            // квадрат выреза
            int sx1 = left ? 0 : widthPx - size;
            int sy1 = concaveTop ? 0 : heightPx - size;
            IntPtr square = CreateRectRgn(sx1, sy1, sx1 + size, sy1 + size);

            // square минус круг = вогнутая четверть
            IntPtr notch = CreateRectRgn(0, 0, 1, 1);
            CombineRgn(notch, square, cut, RGN_DIFF);

            // вычитаем эту четверть из основной формы
            CombineRgn(region, region, notch, RGN_DIFF);

            DeleteObject(notch);
            DeleteObject(square);
            DeleteObject(cut);
        }

        // квадраты скруглений у противоположной стороны обнуляем, если радиусы разные:
        // CreateRoundRectRgn сделал одинаковые, поэтому для точности вычитаем лишнее
        foreach (var (cornerLeft, cornerTop, r) in new[]
                 {
                     (true,  true,  tl), (false, true,  tr),
                     (true,  false, bl), (false, false, br),
                 })
        {
            if (r == tl) continue; // уже такой радиус у базовой формы
            // заменяем угол: вырезаем старый радиус и вычитаем новый
            int old = tl;
            if (old <= 0) continue;
            int size = System.Math.Min(old, System.Math.Min(widthPx / 2, heightPx / 2));
            int sx1 = cornerLeft ? 0 : widthPx - size;
            int sy1 = cornerTop ? 0 : heightPx - size;
            IntPtr sq = CreateRectRgn(sx1, sy1, sx1 + size, sy1 + size);
            CombineRgn(region, region, sq, RGN_DIFF);
            DeleteObject(sq);

            if (r > 0)
            {
                int ns = System.Math.Min(r, System.Math.Min(widthPx / 2, heightPx / 2));
                int nx1 = cornerLeft ? sx1 : sx1 + size - ns;
                int ny1 = cornerTop ? sy1 : sy1 + size - ns;
                IntPtr cut2 = CreateEllipticRgn(nx1, ny1, nx1 + 2 * ns, ny1 + 2 * ns);
                IntPtr sq2 = CreateRectRgn(nx1, ny1, nx1 + ns, ny1 + ns);
                IntPtr keep = CreateRectRgn(0, 0, 1, 1);
                CombineRgn(keep, sq2, cut2, RGN_DIFF);
                CombineRgn(region, region, keep, RGN_DIFF);
                DeleteObject(keep);
                DeleteObject(sq2);
                DeleteObject(cut2);
            }
        }

        SetWindowRgn(hwnd, region, true);
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
    internal const int WM_SETTINGCHANGE = 0x001A;
    internal const int WM_HOTKEY = 0x0312;

    // ---------- Позиция курсора (для авто-скрытия) ----------

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT lpPoint);

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

    // ---------- Позиция системной панели задач (её полосу не трогаем) ----------

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    internal const uint ABM_GETTASKBARPOS = 0x00000005;

    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    internal static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

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

    /// <summary>Открыть центр уведомлений Windows (Win+A делается через shell-протокол).</summary>
    internal static void OpenActionCenter()
    {
        try
        {
            // ms-actioncenter — документированный URI оболочки для центра уведомлений
            ShellExecuteW(IntPtr.Zero, "open", "ms-actioncenter:", null, null, SW_SHOWNORMAL);
        }
        catch { /* noop */ }
    }

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
    internal const uint VK_S = 0x53;

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
