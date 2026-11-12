using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mp3Player.Core;

namespace Mp3Player.Wpf;

/// <summary>Adapts <see cref="MusicPlayer"/> for data binding. No controls are touched here.</summary>
internal sealed class PlayerViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>true = deleted files/folders go to the Recycle Bin (recoverable); false = deleted permanently.</summary>
    private static readonly bool UseRecycleBin = true;

    private const string Dash = "-";
    private const string PlayGlyph = "", PauseGlyph = "";

    private readonly IDialogService _dialogs;
    private readonly MusicPlayer _player;
    private readonly Settings _settings = Settings.Load();
    private readonly string? _startFolder;
    private readonly DispatcherTimer _timer;
    private string _note = "";

    public PlayerViewModel(IDialogService dialogs, string? startFolder)
    {
        _dialogs = dialogs;
        _startFolder = startFolder;
        _player = new MusicPlayer();

        // Initial option values come from the saved settings.
        _shuffle = _settings.Shuffle;
        _crossfadeEnabled = _settings.Crossfade;
        _crossfadeSeconds = _settings.CrossfadeSeconds;
        _volume = _settings.Volume;
        _trackNotifications = _settings.TrackNotifications;
        _deleteWithoutConfirmation = _settings.DeleteTrackWithoutConfirmation;
        _player.Shuffle = _shuffle;
        ApplyCrossfade();
        _player.Volume = (int)Math.Round(_volume);

        OpenFolderCommand = new RelayCommand(async () => await OpenFolderAsync(), () => !IsBusy);
        PlayPauseCommand = new RelayCommand(TogglePlayPause, () => HasTracks);
        PreviousCommand = new RelayCommand(_player.Previous, () => HasTracks);
        NextCommand = new RelayCommand(_player.Next, () => HasTracks);
        DeleteTrackCommand = new RelayCommand(async () => await DeleteTrackAsync(), () => CanDeleteTrack);
        DeleteFolderCommand = new RelayCommand(async () => await DeleteFolderAsync(), () => CanDeleteFolder);

        _player.StateChanged += (_, _) => RefreshState();
        _player.TrackChanged += (_, _) =>
        {
            _note = "";
            RefreshTrack();
            RefreshState();
            TrackChanged?.Invoke(this, EventArgs.Empty);
        };
        _player.Notice += (_, message) =>
        {
            _note = message;
            RefreshState();
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        RefreshTrack();
        RefreshState();
    }

    // ------------------------------------------------------------------ binding plumbing

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    // ------------------------------------------------------------------ for the OS integrations (media overlay, taskbar, notifications)

    /// <summary>The current track changed; <see cref="CurrentInfo"/> and <see cref="CurrentPath"/> are up to date.</summary>
    public event EventHandler? TrackChanged;

    public TrackInfo? CurrentInfo => _player.CurrentInfo;
    public string? CurrentPath => _player.CurrentPath;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.Duration;

    // ------------------------------------------------------------------ commands

    public ICommand OpenFolderCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand DeleteTrackCommand { get; }
    public ICommand DeleteFolderCommand { get; }

    // ------------------------------------------------------------------ track display

    private string _windowTitle = "MP3 Player";
    private string _heading = "Open a folder to start";
    private string _subHeading = "Choose a folder with MP3 files";
    private string _folderText = "No folder selected";
    private string _fileName = Dash, _folderPath = Dash, _details = Dash;
    private string _title = Dash, _artist = Dash, _album = Dash, _year = Dash, _trackNo = Dash, _genre = Dash, _comment = Dash;
    private ImageSource? _cover;
    private bool _hasCover;
    private Color _accent = CoverPalette.Default;

    public string WindowTitle { get => _windowTitle; private set => Set(ref _windowTitle, value); }
    public string Heading { get => _heading; private set => Set(ref _heading, value); }
    public string SubHeading { get => _subHeading; private set => Set(ref _subHeading, value); }
    public string FolderText { get => _folderText; private set => Set(ref _folderText, value); }
    public string FileName { get => _fileName; private set => Set(ref _fileName, value); }
    public string FolderPath { get => _folderPath; private set => Set(ref _folderPath, value); }
    public string Details { get => _details; private set => Set(ref _details, value); }
    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Artist { get => _artist; private set => Set(ref _artist, value); }
    public string Album { get => _album; private set => Set(ref _album, value); }
    public string Year { get => _year; private set => Set(ref _year, value); }
    public string TrackNo { get => _trackNo; private set => Set(ref _trackNo, value); }
    public string Genre { get => _genre; private set => Set(ref _genre, value); }
    public string Comment { get => _comment; private set => Set(ref _comment, value); }
    public ImageSource? CoverArt { get => _cover; private set => Set(ref _cover, value); }
    public bool HasCover { get => _hasCover; private set => Set(ref _hasCover, value); }
    public Color Accent { get => _accent; private set => Set(ref _accent, value); }

    private static string Show(string? text) =>
        string.IsNullOrWhiteSpace(text) ? Dash : text.Replace("\r", " ").Replace("\n", " ");

    private void RefreshTrack()
    {
        string? path = _player.CurrentPath;
        TrackInfo? info = _player.CurrentInfo;

        if (path == null || info == null)
        {
            FileName = FolderPath = Details = Dash;
            Title = Artist = Album = Year = TrackNo = Genre = Comment = Dash;
            Heading = _player.LibraryFolder == null ? "Open a folder to start" : "No tracks";
            SubHeading = _player.LibraryFolder == null ? "Choose a folder with MP3 files" : "No MP3 files left in this folder";
            WindowTitle = "MP3 Player";
            SetCover(null);
            UpdatePosition(TimeSpan.Zero, TimeSpan.Zero);
            return;
        }

        FileName = Show(Path.GetFileName(path));
        FolderPath = Show(Path.GetDirectoryName(path));
        Details = Show(info.Details);
        Title = Show(info.Title);
        Artist = Show(info.Artist);
        Album = Show(info.Album);
        Year = Show(info.Year);
        TrackNo = Show(info.TrackNo);
        Genre = Show(info.Genre);
        Comment = Show(info.Comment);

        bool hasTitle = !string.IsNullOrWhiteSpace(info.Title);
        bool hasArtist = !string.IsNullOrWhiteSpace(info.Artist);
        bool hasAlbum = !string.IsNullOrWhiteSpace(info.Album);

        Heading = hasTitle ? info.Title : Path.GetFileNameWithoutExtension(path);
        SubHeading = hasArtist && hasAlbum ? info.Artist + " • " + info.Album
            : hasArtist ? info.Artist
            : hasAlbum ? info.Album
            : Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
        WindowTitle = (hasArtist && hasTitle ? info.Artist + " - " + info.Title : Heading) + " - MP3 Player";

        SetCover(info.CoverArt);
        UpdatePosition(TimeSpan.Zero, _player.Duration);
    }

    private void SetCover(byte[]? data)
    {
        ImageSource? image = null;
        Color accent = CoverPalette.Default;

        if (data is { Length: > 0 })
        {
            try
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 640;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                }
                bmp.Freeze();
                image = bmp;
                accent = CoverPalette.FromImage(bmp);
            }
            catch
            {
                image = null;     // unsupported or corrupt picture: show the placeholder
            }
        }

        CoverArt = image;
        HasCover = image != null;
        Accent = accent;
    }

    // ------------------------------------------------------------------ state display

    private bool _isPlaying, _hasTracks, _canDeleteTrack, _canDeleteFolder, _isSeekable, _isBusy;
    private string _playPauseGlyph = PlayGlyph, _playPauseTip = "Play";
    private string _statusText = "", _trackText = "No tracks";

    public bool IsPlaying { get => _isPlaying; private set => Set(ref _isPlaying, value); }
    public bool HasTracks { get => _hasTracks; private set => Set(ref _hasTracks, value); }
    public bool CanDeleteTrack { get => _canDeleteTrack; private set => Set(ref _canDeleteTrack, value); }
    public bool CanDeleteFolder { get => _canDeleteFolder; private set => Set(ref _canDeleteFolder, value); }
    public bool IsSeekable { get => _isSeekable; private set => Set(ref _isSeekable, value); }
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public string PlayPauseGlyph { get => _playPauseGlyph; private set => Set(ref _playPauseGlyph, value); }
    public string PlayPauseTip { get => _playPauseTip; private set => Set(ref _playPauseTip, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string TrackText { get => _trackText; private set => Set(ref _trackText, value); }

    private void RefreshState()
    {
        PlayerState state = _player.State;
        bool has = _player.TrackCount > 0;

        IsPlaying = state == PlayerState.Playing;
        HasTracks = has;
        CanDeleteTrack = has && _player.CurrentPath != null;
        CanDeleteFolder = has && _player.CanDeleteCurrentDirectory;
        IsSeekable = state != PlayerState.Stopped;
        PlayPauseGlyph = IsPlaying ? PauseGlyph : PlayGlyph;
        PlayPauseTip = IsPlaying ? "Pause (Space)" : "Play (Space)";

        TrackText = has ? $"Track {_player.TrackNumber} of {_player.TrackCount}" : "No tracks";
        string mode = _player.Shuffle ? "shuffle" : "in order";
        StatusText = $"{state} • {TrackText} ({mode})" + (_note.Length > 0 ? " • " + _note : "");

        CommandManager.InvalidateRequerySuggested();
    }

    // ------------------------------------------------------------------ position / seeking

    private double _position, _duration = 1;
    private string _positionText = "0:00", _durationText = "0:00";

    public double PositionSeconds { get => _position; private set => Set(ref _position, value); }
    public double DurationSeconds { get => _duration; private set => Set(ref _duration, value); }
    public string PositionText { get => _positionText; private set => Set(ref _positionText, value); }
    public string DurationText { get => _durationText; private set => Set(ref _durationText, value); }

    /// <summary>True while the position slider is being updated by the timer (so the view ignores it).</summary>
    public bool IsUpdatingPosition { get; private set; }

    /// <summary>True while the user drags the position slider.</summary>
    public bool IsSeeking { get; private set; }

    private void UpdatePosition(TimeSpan position, TimeSpan duration)
    {
        IsUpdatingPosition = true;
        try
        {
            DurationSeconds = Math.Max(1, duration.TotalSeconds);
            PositionSeconds = Math.Min(position.TotalSeconds, DurationSeconds);
            PositionText = TrackInfo.FormatTime(position);
            DurationText = TrackInfo.FormatTime(duration);
        }
        finally
        {
            IsUpdatingPosition = false;
        }
    }

    private void OnTick()
    {
        if (_player.State == PlayerState.Stopped || IsSeeking) return;
        UpdatePosition(_player.Position, _player.Duration);
    }

    public void BeginSeek() => IsSeeking = true;

    public void PreviewSeek(double seconds) => PositionText = TrackInfo.FormatTime(TimeSpan.FromSeconds(seconds));

    public void EndSeek(double seconds)
    {
        IsSeeking = false;
        SeekTo(seconds);
    }

    public void SeekTo(double seconds) => _player.Seek(TimeSpan.FromSeconds(seconds));

    /// <summary>Keyboard seek: jumps relative to the current position.</summary>
    public void SeekBy(double seconds)
    {
        if (IsSeeking || !IsSeekable) return;
        _player.Seek(_player.Position + TimeSpan.FromSeconds(seconds));
        UpdatePosition(_player.Position, _player.Duration);
    }

    // ------------------------------------------------------------------ options

    private bool _shuffle, _crossfadeEnabled, _trackNotifications, _deleteWithoutConfirmation;
    private double _crossfadeSeconds, _volume;

    public bool Shuffle
    {
        get => _shuffle;
        set { if (Set(ref _shuffle, value)) _player.Shuffle = value; }
    }

    public bool CrossfadeEnabled
    {
        get => _crossfadeEnabled;
        set { if (Set(ref _crossfadeEnabled, value)) ApplyCrossfade(); }
    }

    public double CrossfadeSeconds
    {
        get => _crossfadeSeconds;
        set { if (Set(ref _crossfadeSeconds, Math.Round(value))) ApplyCrossfade(); }
    }

    /// <summary>Show a Windows notification when a new track starts while the window is in the background.</summary>
    public bool TrackNotifications
    {
        get => _trackNotifications;
        set => Set(ref _trackNotifications, value);
    }

    /// <summary>Delete track skips the confirmation card. Deleting a folder is always confirmed.</summary>
    public bool DeleteWithoutConfirmation
    {
        get => _deleteWithoutConfirmation;
        set => Set(ref _deleteWithoutConfirmation, value);
    }

    public double Volume
    {
        get => _volume;
        set { if (Set(ref _volume, value)) _player.Volume = (int)Math.Round(value); }
    }

    private void ApplyCrossfade() => _player.CrossfadeSeconds = _crossfadeEnabled ? _crossfadeSeconds : 0;

    // ------------------------------------------------------------------ search

    /// <summary>All tags have been read: a search that is on screen should be run again.</summary>
    public event EventHandler? SearchIndexReady
    {
        add => _player.SearchIndexReady += value;
        remove => _player.SearchIndexReady -= value;
    }

    public bool IsSearchIndexReady => _player.IsSearchIndexReady;

    public IReadOnlyList<TrackSummary> SearchTracks(string query, int maxResults) => _player.SearchTracks(query, maxResults);

    public void PlayTrack(string path) => _player.PlayTrack(path);

    // ------------------------------------------------------------------ actions

    public async Task InitializeAsync()
    {
        string? dir = _startFolder ?? _settings.Folder;
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            await LoadFolderAsync(dir, play: _startFolder != null);
    }

    private async Task OpenFolderAsync()
    {
        string? dir = _dialogs.PickFolder(_player.LibraryFolder);
        if (dir != null) await LoadFolderAsync(dir, play: true);
    }

    private async Task LoadFolderAsync(string dir, bool play)
    {
        IsBusy = true;
        try
        {
            await _player.LoadFolderAsync(dir, play);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage("MP3 Player", "Could not read the folder:\n\n" + ex.Message, isError: true);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        FolderText = _player.LibraryFolder ?? dir;
        if (_player.TrackCount == 0)
            _dialogs.ShowMessage("MP3 Player", "No MP3 files were found in that folder or its subfolders.", isError: false);
    }

    private void TogglePlayPause()
    {
        if (IsPlaying) _player.Pause();
        else _player.Play();
    }

    /// <summary>Pauses while a confirmation is open so nothing advances underneath it.</summary>
    private bool PauseForPrompt()
    {
        bool wasPlaying = _player.State == PlayerState.Playing;
        if (wasPlaying) _player.Pause();
        return wasPlaying;
    }

    private void ResumeAfterPrompt(bool wasPlaying)
    {
        if (wasPlaying) _player.Play();
    }

    private async Task DeleteTrackAsync()
    {
        string? path = _player.CurrentPath;
        if (path == null) return;

        if (!_deleteWithoutConfirmation)
        {
            bool wasPlaying = PauseForPrompt();
            string how = UseRecycleBin ? "It will be moved to the Recycle Bin" : "It will be deleted permanently";
            bool confirmed = await _dialogs.ConfirmAsync(
                "Delete this track?", $"{how}, and the next track will play.", path, "Delete track");

            if (!confirmed)
            {
                ResumeAfterPrompt(wasPlaying);
                return;
            }
        }

        try
        {
            _player.DeleteCurrentTrack(UseRecycleBin);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage("Delete track", "The file could not be deleted:\n\n" + ex.Message, isError: true);
        }
    }

    private async Task DeleteFolderAsync()
    {
        if (!_player.CanDeleteCurrentDirectory) return;

        DirectoryDeletionPreview preview;
        try
        {
            preview = _player.GetCurrentDirectoryDeletionPreview();
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage("Delete folder", "Could not inspect the folder:\n\n" + ex.Message, isError: true);
            return;
        }

        bool wasPlaying = PauseForPrompt();
        string how = UseRecycleBin ? "It will be moved to the Recycle Bin" : "It will be deleted permanently";
        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete this entire folder?",
            $"It contains {preview.FileCount} file(s) in total ({preview.TrackCount} MP3, " +
            $"{preview.TotalBytes / 1048576.0:0.0} MB), including any subfolders. " +
            $"{how}, and the next track outside this folder will play.",
            preview.FolderPath,
            "Delete folder");

        if (!confirmed)
        {
            ResumeAfterPrompt(wasPlaying);
            return;
        }

        try
        {
            _player.DeleteCurrentDirectory(UseRecycleBin);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage("Delete folder", "The folder could not be deleted:\n\n" + ex.Message, isError: true);
        }
    }

    // ------------------------------------------------------------------ shutdown

    public void Dispose()
    {
        _timer.Stop();
        _settings.Folder = _player.LibraryFolder;
        _settings.Volume = (int)Math.Round(_volume);
        _settings.Shuffle = _shuffle;
        _settings.Crossfade = _crossfadeEnabled;
        _settings.CrossfadeSeconds = (int)_crossfadeSeconds;
        _settings.TrackNotifications = _trackNotifications;
        _settings.DeleteTrackWithoutConfirmation = _deleteWithoutConfirmation;
        _settings.Save();
        _player.Dispose();
    }
}
