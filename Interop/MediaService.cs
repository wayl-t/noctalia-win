using System;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace NoctaliaWin.Interop;

/// <summary>
/// Медиа активной сессии Windows через <see cref="GlobalSystemMediaTransportControlsSessionManager"/>
/// (аналог MPRIS в оригинальном Noctalia).
///
/// Даёт те же данные, что mpris_service в оригинале: трек, артист, альбом, статус воспроизведения
/// и превью обложки. Для управления есть play/pause, next, previous.
///
/// Все вызовы WinRT асинхронные; сервис держит «снимок» состояния, который обновляет вызывающая
/// сторона (таймер панели) через <see cref="RefreshAsync"/>. Ошибки глушатся: если WinRT недоступен
/// или нет активной сессии, сервис просто сообщает «нет плеера».
/// </summary>
public sealed class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager _manager;

    public bool Available { get; private set; }
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string Album { get; private set; } = "";
    public bool IsPlaying { get; private set; }
    /// <summary>PNG/JPEG-байты обложки либо null.</summary>
    public byte[] Artwork { get; private set; }
    /// <summary>Источник (имя приложения-плеера).</summary>
    public string Source { get; private set; } = "";

    private bool _disposed;

    /// <summary>Ленивая инициализация менеджера (WinRT требует STA/окна).</summary>
    private async Task<bool> EnsureAsync()
    {
        if (_manager != null) return true;
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
            _manager = null;
        }
        return _manager != null;
    }

    /// <summary>Прочитать текущее состояние активной медиа-сессии.</summary>
    public async Task RefreshAsync()
    {
        if (_disposed) return;
        if (!await EnsureAsync()) { Clear(); return; }

        try
        {
            var session = _manager.GetCurrentSession();
            if (session == null) { Clear(); return; }

            Source = session.SourceAppUserModelId ?? "";

            var props = await session.TryGetMediaPropertiesAsync();
            Title = props?.Title ?? "";
            Artist = props?.Artist ?? "";
            Album = props?.AlbumTitle ?? "";

            var info = session.GetPlaybackInfo();
            IsPlaying = info?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            // Обложка читается только когда трек сменился — дорогая операция.
            if (!string.IsNullOrEmpty(Title) && Title != _lastArtTitle)
            {
                _lastArtTitle = Title;
                Artwork = await ReadThumbnailAsync(props);
            }

            Available = !string.IsNullOrWhiteSpace(Title);
        }
        catch
        {
            Clear();
        }
    }

    private string _lastArtTitle;

    private static async Task<byte[]> ReadThumbnailAsync(GlobalSystemMediaTransportControlsSessionMediaProperties props)
    {
        try
        {
            var thumb = props?.Thumbnail;
            if (thumb == null) return null;

            using var stream = await thumb.OpenReadAsync();
            if (stream == null || stream.Size == 0) return null;

            var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);
            await stream.ReadAsync(buffer, (uint)stream.Size, Windows.Storage.Streams.InputStreamOptions.None);

            var bytes = new byte[buffer.Length];
            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                reader.ReadBytes(bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private void Clear()
    {
        Available = false;
        Title = Artist = Album = Source = "";
        IsPlaying = false;
        Artwork = null;
        _lastArtTitle = null;
    }

    // ── Управление ───────────────────────────────────────────────────────────

    public async Task TogglePlayPauseAsync()
    {
        if (!await EnsureAsync()) return;
        try
        {
            var s = _manager.GetCurrentSession();
            if (s != null) await s.TryTogglePlayPauseAsync();
        }
        catch { /* noop */ }
    }

    public async Task NextAsync()
    {
        if (!await EnsureAsync()) return;
        try
        {
            var s = _manager.GetCurrentSession();
            if (s != null) await s.TrySkipNextAsync();
        }
        catch { /* noop */ }
    }

    public async Task PreviousAsync()
    {
        if (!await EnsureAsync()) return;
        try
        {
            var s = _manager.GetCurrentSession();
            if (s != null) await s.TrySkipPreviousAsync();
        }
        catch { /* noop */ }
    }

    public void Dispose()
    {
        _disposed = true;
        _manager = null;
    }
}
