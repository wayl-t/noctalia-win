# NoctaliaWin 🌙

**Нативная панель для Windows в стиле [Noctalia](https://github.com/noctalia-dev/noctalia) —
вёрстка, цвета и виджеты один в один с оригиналом.**

Noctalia — оболочка десктопа для Linux/Wayland; на Windows её запустить нельзя (нет layer-shell,
эксклюзивных зон и композиторского блюра). NoctaliaWin — написанная с нуля панель, которая
воспроизводит **реальные значения из исходников и документации Noctalia v5**: палитру, геометрию,
форму, состав виджетов и поведение.

## Соответствие оригиналу

| Параметр Noctalia | Значение в оригинале | В NoctaliaWin |
| --- | --- | --- |
| `thickness` | `34` | по умолчанию `34` |
| `margin_ends` | `180` | по умолчанию `180` |
| `margin_edge` | `10` | по умолчанию `10` |
| `radius` | `12` | по умолчанию `12` |
| `padding` | `14` | по умолчанию `14` |
| `widget_spacing` | `6` | по умолчанию `6` |
| `font_family` | `sans-serif` | Segoe UI Variable Text |
| `font_weight` | `500` | по умолчанию `500` |
| `border` / `border_width` | `outline` / `0.0` | по умолчанию так же |
| `compositor_blur` | `true` | Acrylic |
| `background_opacity` | `1.0` | по умолчанию `1.0` |
| `capsule` | выключено | выключено |
| `corner_radius_scale` | `1.0` | `scale` |
| `time_format` / `date_format` | `{:%H:%M}` / `%A, %x` | `HH:mm` / `ddd, d MMM` |
| `auto_hide_delay_ms` | `0` | по умолчанию `0` |
| `smart_auto_hide` | — | есть |
| порядок виджетов | `launcher, workspaces` / `clock` / `media, tray, notifications, network, volume, battery, control-center, session` | так же |
| палитра | Noctalia `surface #070722`, `primary #fff59b`, `secondary #a9aefe`, `outline #21215F` | один в один |

## Возможности

- 📌 **Работает как панель задач** — панель закреплена у края и резервирует рабочую область,
  максимизированные окна под неё **не залезают**. Позиция: `top` или `bottom`
- 🎨 **Палитры один в один** — 10 встроенных пресетов оригинала (Noctalia, Ayu, Catppuccin, Dracula,
  Eldritch, Gruvbox, Kanagawa, Nord, Rosé Pine, Tokyo-Night), тёмный и светлый варианты каждого;
  режим `dark` / `light` / `auto` (как в Windows); `pure_black_dark`
- 🫧 **Капсулы и вогнутые углы** — `capsule` с заливкой/радиусом/обводкой из ролей палитры,
  `concave_edge_corners` (панель «вытекает» из края, как carve в оригинале)
- 🧩 **Виджеты как в оригинале** — лаунчер, рабочие столы, часы, медиа, трей, уведомления,
  сеть (скорость), громкость, батарея, control-center (быстрые настройки), session (питание),
  заголовок окна, статистика. Зоны `start` / `center` / `end` собираются из конфига
- 🕐 **Часы** — форматы `time_format` и `date_format`
- 🔔 **Control center** — панель вкладок как в оригинале: 700×520, компактный сайдбар с 12 вкладками
  (главная, медиа, звук, экран, система, питание, сеть, bluetooth, погода, календарь, уведомления,
  экранное время), шапка с заголовком и действиями, карточки пользователя/медиа/даты-времени и
  сетка быстрых ярлыков (wifi, bluetooth, caffeine, nightlight, notification, power_profile)
- 🪟 **Лаунчер** — поиск по приложениям из меню «Пуск»
- 🫥 **Авто-скрытие** — `auto_hide`, `smart_auto_hide` (видна, когда стол пуст),
  `show_on_workspace_switch`, задержка `auto_hide_delay_ms`
- ✨ **Подсветка под курсором, подсказки и анимации** — всё отключается
- 🔊 **Громкость** — колёсико на панели регулирует, клик открывает слайдер с mute
- ⌨️ **Ctrl+Alt+Space** — показать/скрыть, **Ctrl+Alt+S** — настройки
- 🚫 Панель **не отбирает фокус** и скрыта из Alt-Tab

## Установка

### Вариант 1 — готовый exe

Скачайте `NoctaliaWin.exe` из [Releases](../../releases) и запустите.
Одно приложение, ничего дополнительно ставить не нужно.

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
    "Thickness": 34,
    "MarginEnds": 180,
    "MarginEdge": 10,
    "CornerRadius": 12,
    "ConcaveEdgeCorners": true,
    "BackgroundOpacity": 1.0,
    "Blur": true,
    "Border": "outline",
    "BorderWidth": 0.0,
    "Padding": 14,
    "WidgetSpacing": 6,
    "Scale": 1.0,
    "FontScale": 1.0,
    "FontWeight": 500,
    "Capsule": false,
    "CapsuleFill": "surface_variant",
    "CapsuleThickness": 0.76,
    "CapsulePadding": 6,
    "Shadow": true,
    "ReserveSpace": true,
    "AutoHide": false,
    "AutoHideDelayMs": 0,
    "SmartAutoHide": false,
    "ShowOnWorkspaceSwitch": true,
    "HoverHighlight": true,
    "ShowTooltips": true,
    "Animations": true,
    "DeadZoneLeft": "none",
    "DeadZoneRight": "control-center",
    "Start": ["launcher", "workspaces"],
    "Center": ["clock"],
    "End": ["media", "tray", "notifications", "network", "volume", "battery", "control-center", "session"]
  },
  "Theme": {
    "Mode": "dark",
    "Palette": "Noctalia",
    "PureBlackDark": false,
    "Primary": null,
    "Surface": null,
    "OnSurface": null,
    "Dim": null,
    "Outline": null
  },
  "Clock": {
    "TimeFormat": "HH:mm",
    "DateFormat": "ddd, d MMM"
  },
  "ControlCenter": {
    "Width": 700,
    "Height": 520,
    "ShowSidebar": true,
    "CompactSidebar": true,
    "ShowShortcutLabels": true,
    "ShowSessionButton": true,
    "Shortcuts": [ "wifi", "bluetooth", "caffeine", "nightlight", "notification", "power_profile" ],
    "HiddenTabs": []
  },
  "AutoStart": false
}
```

### Секция `Bar`

| Ключ | Смысл |
| --- | --- |
| `Position` | `top` или `bottom` |
| `Thickness` | высота полосы (`34` — как в оригинале) |
| `MarginEnds` | отступ от краёв по длине полосы (`180` — как в оригинале) |
| `MarginEdge` | отступ от экрана по нормали (`10` — как в оригинале) |
| `CornerRadius` | скругление углов (`12`), можно задать по углам: `RadiusTopLeft` и т. д. |
| `ConcaveEdgeCorners` | вогнутые углы у края экрана (работает при `MarginEdge = 0`, как в оригинале) |
| `BackgroundOpacity` | прозрачность подложки, `0.0`–`1.0` |
| `Blur` | Acrylic-размытие под панелью (`compositor_blur`) |
| `Border` / `BorderWidth` | обводка полосы: роль палитры или `#hex`; `0` выключает |
| `Padding` / `WidgetSpacing` | внутренний отступ и зазор между виджетами |
| `Scale` / `FontScale` | общий масштаб и масштаб только текста |
| `FontWeight` | насыщенность шрифта, CSS 100–1000 (по умолчанию `500`) |
| `Capsule` + `CapsuleFill` / `CapsuleThickness` / `CapsuleRadius` / `CapsuleOpacity` / `CapsulePadding` / `CapsuleBorder` | капсулы под виджетами |
| `Shadow` | тень под панелью |
| `ReserveSpace` | держать свою полосу рабочей области (окна не залезают) |
| `AutoHide` / `AutoHideDelayMs` / `SmartAutoHide` / `ShowOnWorkspaceSwitch` | авто-скрытие и его варианты |
| `HoverHighlight` / `ShowTooltips` / `Animations` / `AnimationSpeed` | подсветка, подсказки, анимации |
| `DeadZoneLeft` / `DeadZoneRight` | действие по клику на пустой отступ: `none`, `control-center`, `launcher`, `session`, `settings` |
| `Start` / `Center` / `End` | списки виджетов по зонам |
| `ShowWindowTitle` | добавить заголовок активного окна в зону `start` |

Виджеты: `launcher`, `workspaces`, `title`, `clock`, `media`, `tray`, `notifications`, `network`,
`volume`, `battery`, `control-center`, `session`, `stats`.

### Секция `Theme`

`Palette` — имя пресета (`Noctalia`, `Ayu`, `Catppuccin`, `Dracula`, `Eldritch`, `Gruvbox`,
`Kanagawa`, `Nord`, `Rosé Pine`, `Tokyo-Night`). `Mode` — `dark`, `light` или `auto`.

Роли палитры (`Primary`, `Surface`, `OnSurface`, `Outline`, `Dim`, `Background` и др.) можно
переопределить — пустое значение (`null`) означает «взять из пресета». В `Border`, `CapsuleFill`
и других цветовых полях можно указывать как `#RRGGBB`/`#RRGGBBAA`, так и **имя роли** —
например `"CapsuleFill": "primary"`. Хочется свою палитру:

```json
"Theme": {
  "Mode": "dark",
  "Palette": "Noctalia",
  "Primary": "#ff89b4fa",
  "Surface": "#cc11111b"
}
```

### Секция `ControlCenter`

| Ключ | Смысл |
| --- | --- |
| `Width` / `Height` | размеры панели (`700` × `520` — как в оригинале) |
| `ShowSidebar` / `CompactSidebar` | боковая колонка вкладок; `compact` — только иконки (`sidebar_mode = compact`) |
| `ShowShortcutLabels` | подписи под иконками ярлыков дашборда |
| `ShowSessionButton` | кнопка «Питание» в шапке вкладки |
| `Shortcuts` | до 6 ярлыков дашборда: `wifi`, `bluetooth`, `caffeine`, `nightlight`, `notification`, `power_profile`, `wallpaper`, `screen_recorder`, `session` |
| `HiddenTabs` | скрытые вкладки: `home`, `media`, `audio`, `monitor`, `system`, `power`, `network`, `bluetooth`, `weather`, `calendar`, `notifications`, `screen-time` |

## Управление

- Клик по рабочим столам — переключение, «+» — создать новый
- Клик по часам — календарь, по громкости — слайдер, по control-center — быстрые настройки
- Колёсико на громкости — регулировка без открытия слайдера
- ПКМ по пустому отступу справа — быстрые настройки (`dead_zone`)
- Ctrl+Alt+Space — показать/скрыть, Ctrl+Alt+S — настройки
- ПКМ по иконке в трее — меню, двойной клик — папка конфига

## Совместимость

- **Windows 10 (1809–22H2)** — основная поддержка, проверено на 22H2 (build 19045)
- Windows 11 — виртуальные рабочие столы используют API Windows 10, на 11 могут не работать;
  остальные функции должны работать

## Ограничения

Полного совпадения с оригиналом быть не может — в Windows нет layer-shell, эксклюзивных зон и
композиторского блюра, а вместо MPRIS/DBus работают другие API. Что заменено:

- **Размытие** — композиторский блюр → Acrylic Windows (`SetWindowCompositionAttribute`)
- **Медиа** — MPRIS → `GlobalSystemMediaTransportControls` (трек, артист, альбом, статус, обложка;
  клик — play/pause, ПКМ/колесо — переключение трека)
- **Трей** — StatusNotifier → иконка в системном трее Windows
- **Уведомления** — daemon уведомлений → центр уведомлений Windows
- **Сеть** — NetworkManager → счётчики интерфейсов Windows

## Иконки

Иконки — **один в один с оригиналом**: в exe встроен тот же шрифт Tabler
(`assets/fonts/noctalia-tabler.ttf` из репозитория Noctalia), а имена иконок берутся из
`tabler.json` и таблицы алиасов `src/render/text/glyph_registry.cpp`. Поэтому в коде панели
иконки задаются **по имени**, как в оригинале:

```csharp
MakeGlyph("battery-4");      // батарея
MakeGlyph("volume-high");    // громкость (volume, через алиас)
MakeGlyph("media-play");     // play (player-play-filled, через алиас)
```

Алиасы (`volume-high` → `volume`, `shutdown` → `power`, `caffeine-on` → `mug-filled`,
`brightness-high` → `brightness-up-filled` и т. д.) перенесены из оригинала дословно.
Динамические иконки повторяют логику оригинала: батарея по порогам 85/55/30/10,
громкость по порогам 40 % (`audioVolumeGlyph`), Wi-Fi по уровню сигнала, статус
воспроизведения в медиа-виджете.

## Идеи для развития

- [x] Медиа-виджет через `GlobalSystemMediaTransportControls` (трек, play/pause, обложка)
- [x] Иконки Tabler как в оригинале (встроенный шрифт `noctalia-tabler.ttf`)
- [ ] Виджет погоды
- [ ] Иконки Tabler как в оригинале (встроить шрифт)
- [ ] Мультимониторность (сейчас панель на основном экране)
- [ ] Поддержка Windows 11 API виртуальных рабочих столов

## Технологии

- C# / WPF (.NET 8), один самодостаточный exe (~180 МБ, содержит рантайм)
- Win32: `SetWindowCompositionAttribute` (Acrylic), `Shell_NotifyIcon` (трей),
  `SetWindowRgn` + `CombineRgn` (скругления и вогнутые углы), `SystemParametersInfo`
  (резерв рабочей области), `GetCursorPos` (авто-скрытие)
- COM: виртуальные рабочие столы, Core Audio API (громкость)
- WinRT: `GlobalSystemMediaTransportControls` (медиа), `Fonts.GetFontFamilies` (шрифт иконок)
- WMI: яркость монитора
- Шрифт иконок Tabler (`noctalia-tabler.ttf`, лицензия MIT) — из репозитория Noctalia

## Лицензия

MIT. Шрифт иконок Tabler — MIT (см. `Assets/`)
