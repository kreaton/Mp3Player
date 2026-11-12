using System.Text.Json;

namespace Mp3Player.Core;

/// <summary>
/// Remembers the last folder and options between runs (%AppData%\Mp3Player\settings.json).
/// Shared by every UI, so switching between them keeps your folder, volume and options.
/// </summary>
public sealed class Settings
{
    public string? Folder { get; set; }
    public int Volume { get; set; } = 80;
    public bool Shuffle { get; set; }
    public bool Crossfade { get; set; }
    public int CrossfadeSeconds { get; set; } = 4;

    /// <summary>Show a Windows notification when a new track starts while the player window is in the background.</summary>
    public bool TrackNotifications { get; set; }

    /// <summary>Delete the current track right away, without asking first (folders are always confirmed).</summary>
    public bool DeleteTrackWithoutConfirmation { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mp3Player", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
                s.Volume = Math.Clamp(s.Volume, 0, 100);
                s.CrossfadeSeconds = Math.Clamp(s.CrossfadeSeconds, 1, 18);
                return s;
            }
        }
        catch
        {
            // Corrupt or unreadable settings: fall back to defaults.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Not being able to save settings is not worth bothering the user about.
        }
    }
}
