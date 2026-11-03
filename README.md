# MP3 Player

A small Windows MP3 player that plays every `.mp3` in a folder and all its subfolders. It comes with two front ends, Windows Forms and WPF, built on one shared player core.

## Features

- **Folder playback**: pick a folder and every MP3 under it (subfolders included) goes into the playlist.
- **Shuffle**: every track plays once in random order before any track repeats.
- **Crossfade**: fades the end of one track into the start of the next (1–12 seconds).
- **Previous/next**: "Previous" restarts the current track if it has played for more than 3 seconds.
- **Seek and volume**.
- **Track info**: title, artist, album and cover art, read from the ID3 tags. The WPF version takes its accent color from the cover.
- **Clean-up while listening**: delete the current track, or the whole folder (usually an album) it lives in. Both ask for confirmation first and send the files to the Recycle Bin. Folder delete is disabled for tracks that sit directly in the opened folder.
- **Remembers your settings**: last folder, volume, shuffle and crossfade are saved to `%AppData%\Mp3Player\settings.json`. Both front ends share this file.

## Projects

| Project | Description |
|---|---|
| `Mp3Player.Core` | The UI-independent player: audio engine with crossfading (NAudio), playlist and shuffle, tag reading (TagLibSharp), settings. |
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
