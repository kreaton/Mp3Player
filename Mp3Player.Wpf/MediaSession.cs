using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using Windows.Media;
using Windows.Storage.Streams;

namespace Mp3Player.Wpf;

/// <summary>
/// Connects the player to Windows' System Media Transport Controls (SMTC): the media overlay shown by the
/// volume/media keys and Quick Settings, the lock screen, and global hardware media keys.
/// Windows talks to it on a background thread; everything is marshalled back to the UI thread here.
/// </summary>
internal sealed class MediaSession : IDisposable
{
    private readonly SystemMediaTransportControls _smtc;
    private readonly PlayerViewModel _vm;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timelineTimer;
    private bool _disposed;

    private MediaSession(SystemMediaTransportControls smtc, PlayerViewModel vm, Dispatcher dispatcher)
    {
        _smtc = smtc;
        _vm = vm;
        _dispatcher = dispatcher;

        _smtc.IsEnabled = true;
        _smtc.ButtonPressed += OnButtonPressed;
        _smtc.PlaybackPositionChangeRequested += OnPositionChangeRequested;

        _vm.TrackChanged += OnTrackChanged;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        // The overlay shows a progress bar; keep it roughly in step while playing (and after seeks).
        _timelineTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _timelineTimer.Tick += (_, _) => UpdateTimeline();

        UpdateDisplay();
        UpdateStatus();
    }

    /// <summary>Returns null when SMTC is unavailable (very old Windows, or the shell refuses), so the app still runs.</summary>
    public static MediaSession? TryCreate(IntPtr hwnd, PlayerViewModel vm, Dispatcher dispatcher)
    {
        try
        {
            var smtc = SystemMediaTransportControlsInterop.GetForWindow(hwnd);
            return new MediaSession(smtc, vm, dispatcher);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ Windows -> player

    private void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        SystemMediaTransportControlsButton button = args.Button;
        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            switch (button)
            {
                case SystemMediaTransportControlsButton.Play when !_vm.IsPlaying:
                case SystemMediaTransportControlsButton.Pause when _vm.IsPlaying:
                    Run(_vm.PlayPauseCommand);
                    break;
                case SystemMediaTransportControlsButton.Next:
                    Run(_vm.NextCommand);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    Run(_vm.PreviousCommand);
                    break;
            }
            UpdateTimeline();
        });

        static void Run(System.Windows.Input.ICommand command)
        {
            if (command.CanExecute(null)) command.Execute(null);
        }
    }

    private void OnPositionChangeRequested(SystemMediaTransportControls sender, PlaybackPositionChangeRequestedEventArgs args)
    {
        TimeSpan position = args.RequestedPlaybackPosition;
        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed || !_vm.IsSeekable) return;
            _vm.SeekTo(position.TotalSeconds);
            UpdateTimeline();
        });
    }

    // ------------------------------------------------------------------ player -> Windows

    private void OnTrackChanged(object? sender, EventArgs e)
    {
        UpdateDisplay();
        UpdateTimeline();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.IsPlaying) or nameof(PlayerViewModel.HasTracks)
            or nameof(PlayerViewModel.IsSeekable))
            UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_disposed) return;
        bool has = _vm.HasTracks;

        _smtc.IsPlayEnabled = has;
        _smtc.IsPauseEnabled = has;
        _smtc.IsNextEnabled = has;
        _smtc.IsPreviousEnabled = has;
        _smtc.PlaybackStatus = !has ? MediaPlaybackStatus.Closed
            : _vm.IsPlaying ? MediaPlaybackStatus.Playing
            : _vm.IsSeekable ? MediaPlaybackStatus.Paused
            : MediaPlaybackStatus.Stopped;

        if (_vm.IsPlaying) _timelineTimer.Start();
        else _timelineTimer.Stop();
        UpdateTimeline();
    }

    private void UpdateDisplay()
    {
        if (_disposed) return;
        SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
        updater.ClearAll();

        if (_vm.CurrentPath is { } path && _vm.CurrentInfo is { } info)
        {
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = string.IsNullOrWhiteSpace(info.Title) ? Path.GetFileNameWithoutExtension(path) : info.Title;
            updater.MusicProperties.Artist = info.Artist;
            updater.MusicProperties.AlbumArtist = info.Artist;
            updater.MusicProperties.AlbumTitle = info.Album;
            if (uint.TryParse(info.TrackNo.Split('/')[0].Trim(), out uint trackNo))
                updater.MusicProperties.TrackNumber = trackNo;

            if (info.CoverArt is { Length: > 0 } cover)
                updater.Thumbnail = RandomAccessStreamReference.CreateFromStream(new MemoryStream(cover).AsRandomAccessStream());
        }

        updater.Update();
    }

    private void UpdateTimeline()
    {
        if (_disposed) return;
        TimeSpan duration = _vm.HasTracks ? _vm.Duration : TimeSpan.Zero;
        TimeSpan position = _vm.IsSeekable ? _vm.Position : TimeSpan.Zero;
        if (position > duration) position = duration;

        _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
        {
            StartTime = TimeSpan.Zero,
            MinSeekTime = TimeSpan.Zero,
            Position = position,
            MaxSeekTime = duration,
            EndTime = duration,
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timelineTimer.Stop();
        _vm.TrackChanged -= OnTrackChanged;
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _smtc.ButtonPressed -= OnButtonPressed;
        _smtc.PlaybackPositionChangeRequested -= OnPositionChangeRequested;
        try
        {
            _smtc.DisplayUpdater.ClearAll();
            _smtc.DisplayUpdater.Update();
            _smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
            _smtc.IsEnabled = false;
        }
        catch
        {
            // Shutting down: nothing to do if the shell has already let go.
        }
    }
}
