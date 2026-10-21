# NoctaliaWin 🌙

**Нативная панель для Windows в стиле [Noctalia](https://github.com/noctalia-dev/noctalia).**

Noctalia — популярная оболочка десктопа для Linux/Wayland, но на Windows запустить её нельзя.
NoctaliaWin — это написанная с нуля панель для Windows, которая повторяет её дух:
плавающая сверху панель со скруглением и размытым фоном (Acrylic), рабочие столы, часы,
статистика системы и выдвижные панели быстрых настроек.

## Возможности

- 🖥 **Рабочие столы** — индикатор виртуальных рабочих столов (COM API Windows 10), клик для переключения, кнопка «+» для создания нового
- 🕐 **Часы и календарь** — клик по времени открывает календарь
- 📊 **Статистика** — загрузка CPU, использованная оперативная память
- 🔋 **Батарея** — процент заряда и индикация зарядки (если ноутбук)
- 🔊 **Громкость** — колёсико мыши прямо на панели регулирует громкость, клик — выдвижной слайдер с mute
- ⚙️ **Быстрые настройки** — яркость (если поддерживается), автозагрузка, блокировка, сон, перезагрузка, выключение
- 🪟 **Заголовок активного окна** слева на панели
- 🎨 **Конфиг в JSON** — цвета, размеры, формат времени, набор виджетов; перезагрузка на лету при сохранении файла
- 📌 **Трей** — иконка в трее с меню (настройки, перезагрузка конфига, выход)
- ⌨️ **Ctrl+Alt+Space** — показать/скрыть панель
- 🚫 Панель **не отвлекает фокус** от активного окна и скрыта из Alt-Tab
- 🛡 **Резервирование рабочей области** — максимизированные окна не залезают под панель

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

Конфиг лежит в `%APPDATA%\NoctaliaWin\config.json` и перечитывается автоматически при сохранении.
Пример:

```json
{
  "BackgroundColor": "#CC1A1B26",
  "AccentColor": "#FFB4BEFE",
  "TextColor": "#FFF2F2F7",
  "DimTextColor": "#FF9A9AA8",
  "Height": 40,
  "MarginX": 12,
  "MarginTop": 10,
  "CornerRadius": 14,
  "ReserveWorkArea": true,
  "ShowWorkspaces": true,
  "ShowWindowTitle": true,
  "ShowStats": true,
  "ShowBattery": true,
  "ShowVolume": true,
  "ClockFormat": "HH:mm",
  "DateFormat": "ddd, d MMM",
  "AutoStart": false
}
```

Цвета — в формате `#AARRGGBB` или `#RRGGBB`. Хочется другой палитры — меняйте `BackgroundColor` и `AccentColor`,
панель подхватит их сразу, без перезапуска.

## Совместимость

- **Windows 10 (1809–22H2)** — основная поддержка, проверено на 22H2 (build 19045)
- Windows 11 — виртуальные рабочие столы используют API Windows 10, на 11 могут не работать; остальные функции должны работать

## Идеи для развития

- [ ] Медиа-виджет (текущий трек, play/pause) через GlobalSystemMediaTransportControls
- [ ] Виджеты погоды и сети
- [ ] Панель уведомлений
- [ ] Лаунчер приложений (аналог rofi) по хоткею
- [ ] Поддержка Windows 11 API виртуальных рабочих столов
- [ ] Свои темы-палитры (как community-palettes у Noctalia)

## Технологии

- C# / WPF (.NET 8), один самодостаточный exe (~180 МБ, содержит рантайм)
- Win32: `SetWindowCompositionAttribute` (Acrylic), `Shell_NotifyIcon` (трей), `SetWindowRgn` (скругление)
- COM: виртуальные рабочие столы, Core Audio API (громкость)
- WMI: яркость монитора

## Лицензия

MIT
