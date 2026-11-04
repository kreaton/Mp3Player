# Mp3Player.Core

All of the MP3 player's logic in one class library, with no UI code. The WPF front end (`Mp3Player.Wpf`) is a thin layer over it. Use it to build another front end, or to script playback.

It handles:

- scanning a folder tree for `.mp3` files and building a playlist
- playing in order, or shuffled with no repeats
- crossfading between tracks
- volume and seeking
- reading ID3 tags and cover art
- searching the tracks by artist, album or title
- deleting the current track or its folder, to the Recycle Bin
- saving settings between runs

Target: `net10.0-windows`. Dependencies: [NAudio](https://github.com/naudio/NAudio) 2.2.1 (decoding, mixing, output) and [TagLibSharp](https://github.com/mono/taglib-sharp) 2.3.0 (tags).

## Quick start

```csharp
using Mp3Player.Core;

// Create the player on the UI thread (see "Threading" below).
var player = new MusicPlayer { Volume = 70, Shuffle = true, CrossfadeSeconds = 6 };

player.TrackChanged += (_, _) => Console.WriteLine($"Now playing: {player.CurrentInfo?.Title ?? player.CurrentPath}");
player.StateChanged += (_, _) => Console.WriteLine($"{player.State}  {player.TrackNumber}/{player.TrackCount}");
player.Notice       += (_, msg) => Console.WriteLine(msg);

await player.LoadFolderAsync(@"D:\Music", play: true);

// Later:
player.Next();
player.Seek(TimeSpan.FromSeconds(30));
player.Pause();
player.Play();

player.Dispose();   // stops playback and releases the sound card and the open file
```

## Threading

Create `MusicPlayer` on the UI thread and call it only from that thread. The constructor captures `SynchronizationContext.Current`. Every event, including the one that fires when a track reaches its end, is posted through that context, so handlers can update controls without `Invoke`/`Dispatcher` calls.

Make sure the right context is installed before you create the player:

```csharp
// Windows Forms (before Application.Run)
SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

// WPF (in App.OnStartup)
SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));
```

If there's no context (for example a console app), it falls back to a plain `SynchronizationContext`. Events are then raised on thread-pool threads.

## API

### `MusicPlayer`

`public sealed class MusicPlayer : IDisposable`. The whole player. Every method throws `ObjectDisposedException` once the player has been disposed.

#### Events

| Event | Raised when |
|---|---|
| `EventHandler StateChanged` | The state, play mode, position in the playlist or track count changes. Refresh buttons and status text. |
| `EventHandler TrackChanged` | The current track changes, so `CurrentPath`/`CurrentInfo` are new. Refresh title, tags and cover. |
| `EventHandler<string> Notice` | Something worth telling the user happened: an unreadable file was skipped, or a track or folder was deleted. Always raised after the `TrackChanged`/`StateChanged` of the same operation. |
| `EventHandler SearchIndexReady` | All tags of the opened folder have been read. Run a search that's on screen again to get complete results. |

#### State

| Member | Description |
|---|---|
| `PlayerState State` | `Stopped`, `Playing` or `Paused`. |
| `string? LibraryFolder` | The folder last opened with `LoadFolderAsync`, normalized to a full path. |
| `string? CurrentPath` | Full path of the current track, or `null` when the playlist is empty. |
| `string? CurrentDirectory` | Folder that contains the current track. |
| `TrackInfo? CurrentInfo` | Tags and file details of the current track. Read once per track and cached. |
| `int TrackCount` | Number of tracks in the playlist. |
| `int TrackNumber` | 1-based position of the current track in the play order (`0` when there is none). In shuffle mode this is its position in the shuffled order. |
| `TimeSpan Position` | Playback position in the current track. Poll it with a timer to drive a seek bar; no event fires for position changes. |
| `TimeSpan Duration` | Length of the current track. |

#### Settings

| Property | Description |
|---|---|
| `bool Shuffle` | Random order without repeats: every track plays once before a new random round starts, and a new round never begins with the track that just played. Switching it on or off keeps the current track current. Raises `StateChanged`. |
| `double CrossfadeSeconds` | Length of the crossfade between tracks, clamped to 0–30. `0` means no crossfade. When a track ends by itself, the full length is used. When you skip with `Next`/`Previous`, it's capped at 2 seconds so skipping feels responsive. For tracks shorter than twice the crossfade, it shrinks to half the track length. |
| `int Volume` | 0–100 (default 80). Applied on a squared curve, so the steps sound more even than a linear curve would. |
#### Loading and transport

| Method | Description |
|---|---|
| `Task LoadFolderAsync(string folder, bool play)` | Scans `folder` and all its subfolders for `.mp3` files on a worker thread, then replaces the playlist. Tracks are sorted by folder, then file name. Stops current playback first, and starts the first track if `play` is `true`. Await it on the UI thread. |
| `void Play()` | Resumes when paused. When stopped, starts the current track. |
| `void Pause()` | Pauses. If the track ends while paused, playback continues with the next track on `Play()`. |
| `void Next()` | Goes to the next track. In order mode it wraps around at the end; in shuffle mode a new round starts. |
| `void Previous()` | If more than 3 seconds of the track have played, restarts it. Otherwise goes to the previous track. In order mode it wraps to the last track. In shuffle mode it stays on the first track of the round. |
| `void Seek(TimeSpan position)` | Jumps within the current track. Ignored when stopped. |

Unreadable or corrupt files are skipped automatically, and each skip is reported through `Notice`. If no file in the playlist can be played, the player stops.

#### Searching

`LoadFolderAsync` starts reading the tags of every track on a background thread. Searching works right away: until a track's tags have been read, it matches on its file name only.

| Member | Description |
|---|---|
| `IReadOnlyList<TrackSummary> SearchTracks(string query, int maxResults = 100)` | Finds tracks whose title, artist, album or file name contain every word of `query`, in any order, ignoring case and accents. Tracks whose title starts with the query come first, then tracks whose title contains it, then the rest. An empty query returns the tracks in library order. Fast enough to call on every key press. |
| `bool PlayTrack(string path)` | Plays a track from the playlist right away. Returns `false` if it isn't in the playlist. In order mode, playback continues from that track. In shuffle mode the track is inserted after the current one and the rest of the round is kept, so `Previous` returns to where you were and the other tracks still play once each. |
| `bool IsSearchIndexReady` | `false` while tags are still being read. |

```csharp
var hits = player.SearchTracks("bonnie tears");
if (hits.Count > 0) player.PlayTrack(hits[0].FilePath);
```

#### Deleting

These methods delete files on disk, so ask the user first. By default files go to the Recycle Bin. Pass `useRecycleBin: false` to delete them permanently.

| Member | Description |
|---|---|
| `void DeleteCurrentTrack(bool useRecycleBin = true)` | Deletes the current track's file, removes it from the playlist and plays the next track. Throws `InvalidOperationException` when there's no track. If the delete fails, the exception is rethrown and the player is left stopped with the playlist unchanged. |
| `bool CanDeleteCurrentDirectory` | `true` when the current track's folder can be deleted. It's `false` when there's no track, or when the track sits directly in `LibraryFolder`: deleting that folder would delete the whole library. Use it to enable or disable the "delete folder" command. |
| `DirectoryDeletionPreview GetCurrentDirectoryDeletionPreview()` | Counts what `DeleteCurrentDirectory` would remove, for the confirmation dialog. |
| `void DeleteCurrentDirectory(bool useRecycleBin = true)` | Deletes the current track's folder, including subfolders and non-MP3 files. It removes all of that folder's tracks from the playlist and plays the first track after it that's outside the folder. Throws `InvalidOperationException` if `CanDeleteCurrentDirectory` is `false`. |

Example of a confirmed folder delete:

```csharp
if (player.CanDeleteCurrentDirectory)
{
    var p = player.GetCurrentDirectoryDeletionPreview();
    string question = $"Delete \"{p.FolderPath}\"?\n{p.FileCount} files ({p.TrackCount} tracks), {p.TotalBytes / 1048576.0:0.0} MB";
    if (AskUser(question))
    {
        try { player.DeleteCurrentDirectory(); }
        catch (Exception ex) { ShowError(ex.Message); }
    }
}
```

### `PlayerState`

`public enum PlayerState { Stopped, Playing, Paused }`

### `DirectoryDeletionPreview`

`public sealed record DirectoryDeletionPreview(string FolderPath, int FileCount, int TrackCount, long TotalBytes)`

| Field | Description |
|---|---|
| `FolderPath` | The folder that would be deleted. |
| `FileCount` | Every file in it, including subfolders, hidden files and non-MP3 files. |
| `TrackCount` | How many of those are tracks in the playlist. |
| `TotalBytes` | Total size of all the files. |

### `TrackInfo`

`public sealed record TrackInfo(string Title, string Artist, string Album, string Year, string TrackNo, string Genre, string Comment, string Details, TimeSpan Duration, byte[]? CoverArt = null)`

Tag and file information for one track. Missing tags are empty strings, never `null`.

| Member | Description |
|---|---|
| `Title`, `Artist`, `Album`, `Genre`, `Comment` | From the tags. `Artist` and `Genre` join multiple values. |
| `Year` | Release year, or `""`. |
| `TrackNo` | `"3"`, or `"3 / 12"` when the total is known. |
| `Details` | A ready-to-show summary line, such as `"8.4 MB, 320 kbps, 44.1 kHz, stereo, 3:41"`. |
| `Duration` | Length according to the file headers. |
| `CoverArt` | Raw bytes (usually JPEG or PNG) of the embedded front cover, or of the first picture if there's no front cover. `null` when there's none. |
| `static TrackInfo Read(string path)` | Reads a file's tags. Never throws: if the tags can't be read, you get empty fields and the file size in `Details`. |
| `static string FormatTime(TimeSpan t)` | Formats a time as `m:ss`, or `h:mm:ss` from one hour up. Negative values show as `0:00`. |

You usually get a `TrackInfo` from `MusicPlayer.CurrentInfo`, but `TrackInfo.Read` works on any file.

### `TrackSummary`

`public sealed record TrackSummary(string FilePath, string Title, string Artist, string Album)`

One search result. Tags that are missing, or haven't been read yet, are empty strings.

| Member | Description |
|---|---|
| `FilePath` | Full path. Pass it to `MusicPlayer.PlayTrack`. |
| `Title`, `Artist`, `Album` | From the tags. |
| `DisplayTitle` | The title, or the file name without its extension for an untagged track. |
| `DisplaySubtitle` | `"Artist • Album"`, whichever of the two exist, or the folder name for an untagged track. |

### `Settings`

`public sealed class Settings`. Remembers options between runs in `%AppData%\Mp3Player\settings.json`. All front ends use the same file, so switching between them keeps your settings. The player doesn't read it by itself; the front end loads it and applies it.

| Member | Description |
|---|---|
| `string? Folder` | Last opened folder. |
| `int Volume` | 0–100, default 80. |
| `bool Shuffle` | Default `false`. |
| `bool Crossfade` | Whether crossfading is on, default `false`. |
| `int CrossfadeSeconds` | Crossfade length used when it's on: 1–18, default 4. || `static Settings Load()` | Loads the file, clamping values to their valid ranges. Returns defaults if the file is missing or corrupt. Never throws. |
| `void Save()` | Writes the file, creating the folder if needed. Errors are ignored. |

Typical use:

```csharp
var settings = Settings.Load();
player.Volume = settings.Volume;
player.Shuffle = settings.Shuffle;
player.CrossfadeSeconds = settings.Crossfade ? settings.CrossfadeSeconds : 0;if (settings.Folder is { } f && Directory.Exists(f)) await player.LoadFolderAsync(f, play: false);

// on exit
settings.Volume = player.Volume;
settings.Folder = player.LibraryFolder;
settings.Save();
```

## Internals

These types are `internal`. They're described here to help you work on the library, not to be called directly.

- **`Playlist`**: the sorted track list plus the play order. In shuffle mode the order is a random permutation (Fisher–Yates) that's rebuilt after each full round. It also removes deleted tracks or folders and picks the next track after a removal.
- **`AudioEngine`**: the audio pipeline: voices → `MixingSampleProvider` → master `VolumeSampleProvider` → `WaveOutEvent`. The output runs all the time and plays silence when nothing is playing, so pausing and resuming are instant. Starting a track while another is playing fades the old one out while the new one fades in. `TrackEnding` is raised on the audio thread, `CrossfadeSeconds` before the end of the current track. `MusicPlayer` posts it on to the UI thread.
- **`Voice`**: one playing track. It decodes the MP3 with `AudioFileReader` and converts it to 44.1 kHz stereo float. It applies a fade-in/fade-out envelope per frame and removes itself from the mixer once it has faded out. Only mono and stereo files are supported.