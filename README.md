# NoctaliaWin 🌙

**Нативная панель для Windows в стиле [Noctalia](https://github.com/noctalia-dev/noctalia).**

Noctalia — популярная оболочка десктопа для Linux/Wayland, но на Windows запустить её нельзя.
NoctaliaWin — это написанная с нуля панель для Windows, которая повторяет её дух и поведение:
док-полоса с размытым фоном (Acrylic), резервирующая рабочую область как панель задач,
рабочие столы, часы, статистика системы, авто-скрытие и настройки — как в оригинале.

## Возможности

- 📌 **Работает как панель задач** — панель закреплена у края экрана и резервирует под себя рабочую область,
  максимизированные окна под неё **не залезают**. Позиция переключается: `top` (по умолчанию, как у Noctalia) или `bottom`
- 📐 **Высота, отступы, скругления** — `thickness`, `margin_ends` (врезка от краёв, как в оригинале),
  `margin_edge`, `cornerRadius`, `padding`, `widgetSpacing`, прозрачность и Acrylic-размытие фона
- 🎨 **Палитра и темы** — тёмная/светлая тема и цветовые роли (`background`, `surface`, `onSurface`,
  `dim`, `primary`, `outline`) в конфиге; цвета можно не задавать — тогда берётся дефолт режима
- 🧩 **Порядок виджетов start/center/end** — три зоны собираются из списков в конфиге,
  ключи: `workspaces`, `title`, `clock`, `stats`, `battery`, `volume`, `settings`
- 🫥 **Авто-скрытие** — панель уезжает за край, когда курсор не рядом, и выезжает при касании края;
  задержка настраивается (`autoHideDelayMs`)
- ✨ **Подсветка под курсором, подсказки и анимации** — мягкая подсветка виджета, tooltips,
  плавное появление popup'ов и выезд панели (всё отключается в конфиге)
- 🖥 **Рабочие столы** — индикатор виртуальных рабочих столов (COM API Windows 10), клик для переключения, «+» для создания
- 🕐 **Часы и календарь** — клик по времени открывает календарь, форматы настраиваются
- 📊 **Статистика** — загрузка CPU, использованная оперативная память
- 🔋 **Батарея** — процент заряда и индикация зарядки (если ноутбук)
- 🔊 **Громкость** — колёсико мыши прямо на панели регулирует громкость, клик — слайдер с mute
- ⚙️ **Окно настроек** — панель, тема, цвета, часы и виджеты мышью;
  само пишет тот же `config.json`
- 🪟 **Заголовок активного окна**, **быстрые настройки** (яркость, автозагрузка, питание)
- 📌 **Трей** — меню с настройками, папкой конфига, перезагрузкой конфига и выходом
- ⌨️ **Ctrl+Alt+Space** — показать/скрыть панель, **Ctrl+Alt+S** — окно настроек
- 🚫 Панель **не отвлекает фокус** от активного окна и скрыта из Alt-Tab

## Установка

### Вариант 1 — готовый exe

Скачайте `NoctaliaWin.exe` из [Releases](../../releases) (или соберите сами, см. ниже) и запустите.
Всё — одно приложение, ничего дополнительно ставить не нужно.

### Вариант 2 — сборка из исходников

Нужен [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/wayl-t/noctalia-win.git
cd noctalia-win
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
.\publish\NoctaliaWin.exe
```

## Конфигурация

Конфиг лежит в `%APPDATA%\NoctaliaWin\config.json` и перечитывается автоматически при сохранении —
панель подхватывает правки без перезапуска. Структура повторяет секции Noctalia:

```json
{
  "Bar": {
    "Position": "top",
    "Thickness": 48,
    "MarginEnds": 100,
    "MarginEdge": 0,
    "CornerRadius": 14,
    "BackgroundOpacity": 1.0,
    "Blur": true,
    "Padding": 14,
    "WidgetSpacing": 6,
    "ReserveSpace": true,
    "AutoHide": false,
    "AutoHideDelayMs": 400,
    "HoverHighlight": true,
    "ShowTooltips": true,
    "Animations": true,
    "Start": ["workspaces", "title"],
    "Center": ["clock"],
    "End": ["stats", "battery", "volume", "settings"]
  },
  "Theme": {
    "Mode": "dark",
    "Background": null,
    "Surface": null,
    "OnSurface": null,
    "Dim": null,
    "Primary": null,
    "Outline": null
  },
  "Clock": {
    "TimeFormat": "HH:mm",
    "DateFormat": "ddd, d MMM"
  },
  "AutoStart": false
}
```

### Секция `Bar`

| Ключ | Смысл |
| --- | --- |
| `Position` | `top` или `bottom` |
| `Thickness` | высота полосы |
| `MarginEnds` | отступ от краёв по длине полосы (`100` = врезка как у Noctalia) |
| `MarginEdge` | отступ от экрана по нормали, `0` = прижата к краю |
| `CornerRadius` | скругление углов |
| `BackgroundOpacity` | прозрачность подложки, `0.2`–`1.0` |
| `Blur` | Acrylic-размытие под панелью |
| `Padding` / `WidgetSpacing` | внутренний отступ и зазор между виджетами |
| `ReserveSpace` | держать свою полосу рабочей области (окна не залезают) |
| `AutoHide` / `AutoHideDelayMs` | авто-скрытие и задержка, мс |
| `HoverHighlight` / `ShowTooltips` / `Animations` | подсветка, подсказки, анимации |
| `Start` / `Center` / `End` | списки виджетов по зонам |

Виджеты: `workspaces`, `title`, `clock`, `stats`, `battery`, `volume`, `settings`.

### Секция `Theme`

Цвета — `#AARRGGBB` или `#RRGGBB`. Пустое значение (`null`) означает «взять дефолт выбранного режима»,
так что для смены на светлую тему достаточно `"Mode": "light"`.

Хочется свою палитру — задайте, например:

```json
"Theme": {
  "Mode": "dark",
  "Background": "#CC11111B",
  "Primary": "#FF89B4FA"
}
```

## Управление

- Клик по рабочим столам — переключение, «+» — создать новый
- Клик по часам — календарь, по громкости — слайдер, по шестерёнке — быстрые настройки
- Колёсико на громкости — регулировка без открытия слайдера
- Ctrl+Alt+Space — показать/скрыть, Ctrl+Alt+S — настройки
- ПКМ по иконке в трее — меню, двойной клик — папка конфига

## Совместимость

- **Windows 10 (1809–22H2)** — основная поддержка, проверено на 22H2 (build 19045)
- Windows 11 — виртуальные рабочие столы используют API Windows 10, на 11 могут не работать; остальные функции должны работать

## Идеи для развития

- [ ] Медиа-виджет (текущий трек, play/pause) через GlobalSystemMediaTransportControls
- [ ] Виджеты погоды и сети
- [ ] Панель уведомлений и control center
- [ ] Лаунчер приложений (аналог rofi) по хоткею
- [ ] Поддержка Windows 11 API виртуальных рабочих столов
- [ ] Мультимониторность (сейчас панель на основном экране)
- [ ] Готовые палитры-пресеты (community palettes у Noctalia)

## Технологии

- C# / WPF (.NET 8), один самодостаточный exe (~180 МБ, содержит рантайм)
- Win32: `SetWindowCompositionAttribute` (Acrylic), `Shell_NotifyIcon` (трей), `SetWindowRgn` (скругление),
  `SystemParametersInfo` (резерв рабочей области), `GetCursorPos` (авто-скрытие)
- COM: виртуальные рабочие столы, Core Audio API (громкость)
- WMI: яркость монитора

## Лицензия

MIT
