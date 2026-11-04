# MP3 Player

A small Windows MP3 player that plays every `.mp3` in a folder and all its subfolders. It comes with two front ends, Windows Forms and WPF, built on one shared player core.

## Features

- **Folder playback**: pick a folder and every MP3 under it (subfolders included) goes into the playlist.
- **Shuffle**: every track plays once in random order before any track repeats.
- **Crossfade**: fades the end of one track into the start of the next (1–18 seconds).
- **Previous/next**: "Previous" restarts the current track if it has played for more than 3 seconds.
- **Seek and volume**.
- **Search**: type part of an artist, album or song title to find a track and play it (Ctrl+F or the Search button).
- **Keyboard shortcuts**: see below.
- **Track info**: title, artist, album and cover art, read from the ID3 tags. The WPF version takes its accent color from the cover.
- **Clean-up while listening**: delete the current track, or the whole folder (usually an album) it lives in. Both ask for confirmation first and send the files to the Recycle Bin. Folder delete is disabled for tracks that sit directly in the opened folder.
- **About page**: icon, version and credits (the ⓘ button in WPF, "About..." in Windows Forms). The version also shows at the bottom right of the main window.
- **Remembers your settings**: last folder, volume, shuffle and crossfade are saved to `%AppData%\Mp3Player\settings.json`. Both front ends share this file.

## Keyboard shortcuts

| Key | Action |
|---|---|
| Space | Play / pause |
| Ctrl+Right | Next track |
| Ctrl+Left | Previous track (restarts the track after 3 seconds) |
| Right / Left | Seek forward / back 5 seconds |
| Up / Down | Volume up / down 5% |
| S | Shuffle on / off |
| Ctrl+F | Search for a track |
| Play/pause, next and previous media keys | Same as the buttons, when the player window is active |

In the search window, type to narrow the list. Up and Down select a track, Enter plays it and Esc closes the window. You can also double-click a track. Words can be in any order and accents are ignored, so `tears bonnie` finds "Save Up All Your Tears" by Bonnie Tyler.

Space always means play/pause, even right after you've clicked a button. To press a focused button from the keyboard, use Enter in WPF. In the Windows Forms version, click it with the mouse.

## Projects

| Project | Description |
|---|---|
| `Mp3Player.Core` | The UI-independent player: audio engine with crossfading (NAudio), playlist and shuffle, tag reading (TagLibSharp), settings. See its [API reference](Mp3Player.Core/README.md). |
| `Mp3Player.App` | Windows Forms front end (`Mp3Player.exe`). |
| `Mp3Player.Wpf` | WPF front end with the Fluent theme (`Mp3PlayerWpf.exe`). |

## Requirements

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build and run

```bash
dotnet build Mp3Player.slnx
```

```bash
dotnet run --project Mp3Player.Wpf
```

```bash
dotnet run --project Mp3Player.App
```

You can also open `Mp3Player.slnx` in a Visual Studio version that supports .NET 10 and set the project you want as the startup project.

### Starting with a folder

Either executable takes an optional folder path. When it's given, that folder is opened and playback starts right away:

```bash
dotnet run --project Mp3Player.Wpf -- "D:\Music\Some Album"
```

## Dependencies

- [NAudio](https://github.com/naudio/NAudio) 2.2.1: audio decoding and playback
- [TagLibSharp](https://github.com/mono/taglib-sharp) 2.3.0: ID3 tags and cover art
