using System.IO;
using System.Security;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Mp3Player.Wpf;

/// <summary>
/// Shows a silent "now playing" Windows notification. The app is not packaged, so it registers its own
/// notification identity (AppUserModelID) under HKCU; that is what gives the toast its name and icon.
/// Each new toast replaces the previous one, so the notification centre holds at most one.
/// </summary>
internal sealed class TrackNotifier
{
    private const string AppId = "Mp3Player.Wpf.NowPlaying";
    private const string Tag = "now-playing", Group = "player";

    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mp3Player", "notifications");

    private readonly Action _onClicked;
    private ToastNotifier? _notifier;
    private bool _failed;
    private int _imageSlot;

    public TrackNotifier(Action onClicked) => _onClicked = onClicked;

    public void Show(string title, string subtitle, byte[]? cover)
    {
        if (_failed) return;
        try
        {
            _notifier ??= CreateNotifier();

            string? image = cover is { Length: > 0 } ? SaveCover(cover) : null;
            string xml =
                "<toast duration=\"short\">" +
                "<visual><binding template=\"ToastGeneric\">" +
                $"<text hint-maxLines=\"1\">{Escape(title)}</text>" +
                (subtitle.Length > 0 ? $"<text>{Escape(subtitle)}</text>" : "") +
                (image != null ? $"<image placement=\"appLogoOverride\" src=\"{Escape(new Uri(image).AbsoluteUri)}\"/>" : "") +
                "</binding></visual>" +
                "<audio silent=\"true\"/>" +       // never talk over the music
                "</toast>";

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var toast = new ToastNotification(doc)
            {
                Tag = Tag,
                Group = Group,
                ExpirationTime = DateTimeOffset.Now.AddMinutes(30),
            };
            toast.Activated += (_, _) => _onClicked();
            _notifier.Show(toast);
        }
        catch
        {
            _failed = true;     // notifications blocked or unavailable: stop trying for this run
        }
    }

    /// <summary>Removes the toast from the notification centre (when the app closes).</summary>
    public void Clear()
    {
        if (_notifier == null) return;
        try { ToastNotificationManager.History.Remove(Tag, Group, AppId); } catch { /* not important */ }
    }

    private static ToastNotifier CreateNotifier()
    {
        Directory.CreateDirectory(DataDir);
        string iconPath = Path.Combine(DataDir, "app.png");
        if (!File.Exists(iconPath)) SaveAppIcon(iconPath);

        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + AppId))
        {
            key.SetValue("DisplayName", AppInfo.Name);
            key.SetValue("IconUri", iconPath);
        }
        return ToastNotificationManager.CreateToastNotifier(AppId);
    }

    private static void SaveAppIcon(string path)
    {
        var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/app.ico"),
            BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(decoder.Frames.MaxBy(f => f.PixelWidth)!));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>
    /// Writes the cover to one of a few rotating files: Windows reads the image after Show returns,
    /// so the file of the toast on screen must not be overwritten by the next one.
    /// </summary>
    private string SaveCover(byte[] cover)
    {
        bool png = cover.Length > 4 && cover[0] == 0x89 && cover[1] == 0x50 && cover[2] == 0x4E && cover[3] == 0x47;
        _imageSlot = (_imageSlot + 1) % 3;
        string path = Path.Combine(DataDir, $"cover{_imageSlot}{(png ? ".png" : ".jpg")}");
        File.WriteAllBytes(path, cover);
        return path;
    }

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";
}
