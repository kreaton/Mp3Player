namespace Mp3Player.Core;

public enum PlayerState { Stopped, Playing, Paused }

/// <summary>What a "delete the current folder" would remove; show this to the user before calling it.</summary>
/// <param name="FolderPath">The folder that would be deleted.</param>
/// <param name="FileCount">All files in it, including subfolders and non-MP3 files.</param>
/// <param name="TrackCount">How many of those are MP3 tracks in the playlist.</param>
/// <param name="TotalBytes">Total size of all files.</param>
public sealed record DirectoryDeletionPreview(string FolderPath, int FileCount, int TrackCount, long TotalBytes);

/// <summary>
/// The whole player behind one UI-free class: load a folder tree, play in order or shuffled (no repeats),
/// optional crossfade, volume, seeking, tag info, and deleting the current track or its folder.
///
/// Threading: create it and call it on the UI thread. Every event is raised on that thread
/// (the audio thread's "track ended" signal is marshalled through the SynchronizationContext captured at
/// construction), so UI code never needs Invoke/BeginInvoke.
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    /// <summary>Crossfade length used for manual Next/Previous, capped so skipping stays snappy.</summary>
    private const double ManualFadeSeconds = 2.0;

    private readonly AudioEngine _engine = new();
    private readonly Playlist _playlist = new();
    private readonly SynchronizationContext _context;

    private PlayerState _state = PlayerState.Stopped;
    private string? _libraryRoot;
    private string? _infoPath;
    private TrackInfo? _info;
    private double _crossfadeSeconds;
    private int _volume = 80;
    private bool _advanceWhenResumed;   // the track ended while paused
    private bool _disposed;

    public MusicPlayer()
    {
        _context = SynchronizationContext.Current ?? new SynchronizationContext();
        _engine.TrackEnding += v => _context.Post(_ => OnTrackEnding(v), null);
        ApplyVolume();
    }

    // ------------------------------------------------------------------ events

    /// <summary>State, play mode, track position or track count changed: refresh status displays.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The current track (and so <see cref="CurrentPath"/>/<see cref="CurrentInfo"/>) changed.</summary>
    public event EventHandler? TrackChanged;

    /// <summary>
    /// A human-readable note about what just happened (a file was skipped, something was deleted).
    /// Always raised after the <see cref="TrackChanged"/> and <see cref="StateChanged"/> of the same operation.
    /// </summary>
    public event EventHandler<string>? Notice;

    // ------------------------------------------------------------------ state

    public PlayerState State => _state;

    /// <summary>The folder that was opened with <see cref="LoadFolderAsync"/>.</summary>
    public string? LibraryFolder => _libraryRoot;

    public string? CurrentPath => _playlist.Current;
    public string? CurrentDirectory => CurrentPath is { } p ? Path.GetDirectoryName(p) : null;

    /// <summary>File and tag info of the current track (null when there is none).</summary>
    public TrackInfo? CurrentInfo => _info;

    public int TrackCount => _playlist.Count;

    /// <summary>1-based number of the current track in the play order (0 when there is none).</summary>
    public int TrackNumber => _playlist.Current == null ? 0 : _playlist.Position + 1;

    public TimeSpan Position => _engine.Position;

    public TimeSpan Duration => _engine.Current != null ? _engine.Duration : _info?.Duration ?? TimeSpan.Zero;

    /// <summary>Random order without repeats until every track has been played.</summary>
    public bool Shuffle
    {
        get => _playlist.Shuffle;
        set
        {
            if (_playlist.Shuffle == value) return;
            _playlist.SetShuffle(value);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Crossfade length in seconds; 0 turns crossfading off.</summary>
    public double CrossfadeSeconds
    {
        get => _crossfadeSeconds;
        set => _crossfadeSeconds = Math.Clamp(value, 0, 30);
    }

    /// <summary>0..100. Applied on a squared curve, which feels more even to the ear than a linear one.</summary>
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            ApplyVolume();
        }
    }

    private void ApplyVolume()
    {
        double v = _volume / 100.0;
        _engine.SetVolume(v * v);
    }

    // ------------------------------------------------------------------ loading and transport

    /// <summary>
    /// Scans the folder and all its subfolders for MP3 files (on a worker thread) and replaces the playlist.
    /// Stops playback; starts the first track if <paramref name="play"/> is true.
    /// </summary>
    public async Task LoadFolderAsync(string folder, bool play)
    {
        ThrowIfDisposed();
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        List<string> files = await Task.Run(() => Playlist.Scan(root));
        ThrowIfDisposed();

        _engine.Stop();
        _advanceWhenResumed = false;
        _state = PlayerState.Stopped;
        _libraryRoot = root;
        _playlist.SetTracks(files);
        RefreshInfo();

        if (play && _playlist.Current is { } first)
        {
            StartTrack(first, automatic: false);
        }
        else
        {
            TrackChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Starts the current track, or resumes if paused.</summary>
    public void Play()
    {
        ThrowIfDisposed();
        if (_state == PlayerState.Paused) Resume();
        else if (_state == PlayerState.Stopped && _playlist.Current is { } current) StartTrack(current, automatic: false);
    }

    public void Pause()
    {
        ThrowIfDisposed();
        if (_state != PlayerState.Playing) return;
        _engine.Pause();
        _state = PlayerState.Paused;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Next()
    {
        ThrowIfDisposed();
        if (_playlist.Count == 0) return;
        StartTrack(_playlist.MoveNext(), automatic: false);
    }

    /// <summary>Goes to the previous track; after the first 3 seconds it restarts the current track instead.</summary>
    public void Previous()
    {
        ThrowIfDisposed();
        if (_playlist.Count == 0) return;
        if (_state != PlayerState.Stopped && _engine.Position.TotalSeconds > 3)
        {
            _engine.Seek(TimeSpan.Zero);
            return;
        }
        StartTrack(_playlist.MovePrevious(), automatic: false);
    }

    public void Seek(TimeSpan position)
    {
        ThrowIfDisposed();
        if (_state != PlayerState.Stopped) _engine.Seek(position);
    }

    // ------------------------------------------------------------------ deleting

    /// <summary>
    /// Deletes the current track's file and plays the next track. Ask the user first.
    /// Throws (and leaves the player stopped, playlist unchanged) if the file cannot be deleted.
    /// </summary>
    public void DeleteCurrentTrack(bool useRecycleBin = true)
    {
        ThrowIfDisposed();
        string path = _playlist.Current ?? throw new InvalidOperationException("There is no current track.");

        ReleaseFiles();                       // Windows will not delete a file that is still open
        try
        {
            DeleteFile(path, useRecycleBin);
        }
        catch
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            throw;
        }

        string? next = _playlist.RemoveCurrentAndAdvance();
        FinishRemoval(next, "Deleted: " + Path.GetFileName(path));
    }

    /// <summary>
    /// True if the current track's folder may be deleted. False when there is no track, or when the track is
    /// directly in the opened library folder (deleting that would delete the whole library).
    /// </summary>
    public bool CanDeleteCurrentDirectory
    {
        get
        {
            string? dir = CurrentDirectory;
            if (dir == null || _libraryRoot == null) return false;
            string d = Normalize(dir), root = Normalize(_libraryRoot);
            string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            return !string.Equals(d, root, StringComparison.OrdinalIgnoreCase)
                   && d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Counts what <see cref="DeleteCurrentDirectory"/> would remove, for the confirmation prompt.</summary>
    public DirectoryDeletionPreview GetCurrentDirectoryDeletionPreview()
    {
        ThrowIfDisposed();
        string dir = CurrentDirectory ?? throw new InvalidOperationException("There is no current track.");

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,             // hidden/system files are deleted too, so count them
        };
        int files = 0;
        long bytes = 0;
        foreach (string f in Directory.EnumerateFiles(dir, "*", options))
        {
            files++;
            try { bytes += new FileInfo(f).Length; } catch { /* vanished or unreadable */ }
        }
        return new DirectoryDeletionPreview(dir, files, _playlist.CountUnder(dir), bytes);
    }

    /// <summary>
    /// Deletes the folder containing the current track (normally a whole album), including subfolders, removes
    /// its tracks from the playlist and plays the next track outside it. Ask the user first.
    /// Refuses (InvalidOperationException) if <see cref="CanDeleteCurrentDirectory"/> is false.
    /// </summary>
    public void DeleteCurrentDirectory(bool useRecycleBin = true)
    {
        ThrowIfDisposed();
        string dir = CurrentDirectory ?? throw new InvalidOperationException("There is no current track.");
        if (!CanDeleteCurrentDirectory)
            throw new InvalidOperationException(
                "The current track is directly in the opened folder; deleting its folder would delete the whole library.");

        ReleaseFiles();
        try
        {
            DeleteDirectory(dir, useRecycleBin);
        }
        catch
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            throw;
        }

        string? next = _playlist.RemoveDirectoryAndAdvance(dir);
        FinishRemoval(next, "Deleted folder: " + Path.GetFileName(dir));
    }

    // ------------------------------------------------------------------ internals

    private void Resume()
    {
        if (_advanceWhenResumed)
        {
            // The track reached its end while paused: carry on with the next one.
            StartTrack(_playlist.MoveNext(), automatic: true);
            return;
        }
        _engine.Resume();
        _state = PlayerState.Playing;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrackEnding(Voice v)
    {
        if (_disposed || !ReferenceEquals(v, _engine.Current)) return;   // stale: the user already skipped
        if (_state == PlayerState.Paused) { _advanceWhenResumed = true; return; }
        if (_state != PlayerState.Playing) return;
        StartTrack(_playlist.MoveNext(), automatic: true);
    }

    /// <summary>Plays the given track (skipping unreadable files) and raises the change events.</summary>
    private void StartTrack(string? path, bool automatic, string? notice = null)
    {
        var notes = new List<string>();
        if (notice != null) notes.Add(notice);

        bool started = false;
        int attempts = 0;
        int maxAttempts = Math.Max(1, _playlist.Count);
        while (path != null && attempts++ < maxAttempts)
        {
            try
            {
                double fade = _crossfadeSeconds;
                double transition = automatic ? fade : Math.Min(fade, ManualFadeSeconds);
                _engine.Play(path, transition, fade);
                _engine.Resume();
                started = true;
            }
            catch (Exception)
            {
                notes.Add("Skipped unreadable file: " + Path.GetFileName(path));
                path = _playlist.MoveNext();
            }
            if (started) break;
        }

        _advanceWhenResumed = false;
        if (started)
        {
            _state = PlayerState.Playing;
        }
        else
        {
            _engine.Stop();
            _state = PlayerState.Stopped;
        }

        RefreshInfo();
        TrackChanged?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (notes.Count > 0) Notice?.Invoke(this, string.Join("; ", notes));
    }

    private void FinishRemoval(string? next, string notice)
    {
        if (next == null)
        {
            RefreshInfo();
            TrackChanged?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
            Notice?.Invoke(this, notice + " - no tracks left");
        }
        else
        {
            StartTrack(next, automatic: false, notice);
        }
    }

    private void ReleaseFiles()
    {
        _engine.Stop();
        _advanceWhenResumed = false;
        _state = PlayerState.Stopped;
    }

    private void RefreshInfo()
    {
        string? path = _playlist.Current;
        if (path == null)
        {
            _infoPath = null;
            _info = null;
        }
        else if (path != _infoPath)
        {
            _info = TrackInfo.Read(path);
            _infoPath = path;
        }
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static void DeleteFile(string path, bool recycle)
    {
        if (recycle)
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        else
            File.Delete(path);
    }

    private static void DeleteDirectory(string path, bool recycle)
    {
        if (recycle)
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        else
            Directory.Delete(path, recursive: true);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
    }
}
