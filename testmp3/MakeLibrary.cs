#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false
#:property PublishTrimmed=false
#:package TagLibSharp@2.3.0

// Builds the fictional "Party Night" demo library used for the promo video's screenshots:
// 6 albums x 5 silent MP3s (128 kbps, 44.1 kHz stereo) with full tags and generated cover art.
// Usage (from this folder):  dotnet run MakeLibrary.cs -- <path to ffmpeg.exe>
// The ffmpeg bundled with Remotion works: ..\promo-video\node_modules\@remotion\compositor-win32-x64-msvc\ffmpeg.exe

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

string ffmpeg = args.Length > 0 ? args[0] : "ffmpeg";
string root = Path.Combine(Directory.GetCurrentDirectory(), "Party Night");

var albums = new Album[]
{
    new("Neon Harbour", "Midnight Confetti", 2024, "Dance", "neon",
        [("Midnight Confetti", 182), ("Glitter on the Stairs", 214), ("Taxi at Four", 197), ("Strobe Heart", 229), ("Last Song, Lights On", 241)]),
    new("Disco Lighthouse", "Sunset Service", 2023, "Disco", "disco",
        [("Sunset Service", 246), ("Mirrorball Tide", 223), ("Pier Party", 205), ("Saltwater Strut", 238), ("Harbour Lights", 252)]),
    new("Paper Comets", "Paint the Ceiling", 2025, "Pop", "comets",
        [("Paint the Ceiling", 201), ("Kitchen Karaoke", 246), ("Cardboard Rocket", 188), ("Sofa Fort", 213), ("Wish You Were Louder", 226)]),
    new("The Velvet Static", "Afterglow FM", 2021, "Indie", "velvet",
        [("Afterglow FM", 246), ("Dial Tone Summer", 219), ("Radio Silence", 232), ("Late Night Request", 207), ("Signal Lost", 261)]),
    new("Lena Sol", "Slow Bloom", 2022, "Acoustic", "lena",
        [("Slow Bloom", 182), ("Morning Tea", 196), ("Window Seat", 221), ("Rain on the Greenhouse", 243), ("Hummingbird", 174)]),
    new("Midnight Mopeds", "Bad Demos Vol. 2", 2019, "Rock", "mopeds",
        [("Feedback Song", 199), ("Untitled Demo 7 (Copy)", 246), ("Garage Take 41", 211), ("Rehearsal Noise", 187), ("Why Did I Keep This", 234)]),
};

// WPF drawing needs an STA thread.
var thread = new Thread(() =>
{
    foreach (var album in albums)
    {
        string dir = Path.Combine(root, $"{album.Artist} - {album.Title}");
        Directory.CreateDirectory(dir);
        byte[] cover = Covers.Render(album.Cover);

        for (int i = 0; i < album.Tracks.Length; i++)
        {
            var (title, seconds) = album.Tracks[i];
            string file = Path.Combine(dir, $"{i + 1:00} {album.Artist} - {title}.mp3");
            Silence(file, seconds);

            using var f = TagLib.File.Create(file);
            f.Tag.Title = title;
            f.Tag.Performers = [album.Artist];
            f.Tag.AlbumArtists = [album.Artist];
            f.Tag.Album = album.Title;
            f.Tag.Year = (uint)album.Year;
            f.Tag.Track = (uint)(i + 1);
            f.Tag.TrackCount = (uint)album.Tracks.Length;
            f.Tag.Genres = [album.Genre];
            f.Tag.Pictures = [new TagLib.Picture(new TagLib.ByteVector(cover)) { Type = TagLib.PictureType.FrontCover, MimeType = "image/png", Description = "Cover" }];
            f.Save();
            Console.WriteLine(file);
        }
    }
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

void Silence(string file, int seconds)
{
    var p = Process.Start(new ProcessStartInfo(ffmpeg,
        ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "anullsrc=r=44100:cl=stereo", "-t", seconds.ToString(),
         "-c:a", "libmp3lame", "-b:a", "128k", file]) { UseShellExecute = false })!;
    p.WaitForExit();
    if (p.ExitCode != 0) throw new Exception("ffmpeg failed for " + file);
}

record Album(string Artist, string Title, int Year, string Genre, string Cover, (string Title, int Seconds)[] Tracks);

static class Covers
{
    private const int Size = 600;

    public static byte[] Render(string name)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            switch (name)
            {
                case "neon": Neon(dc); break;
                case "disco": Disco(dc); break;
                case "comets": Comets(dc); break;
                case "velvet": Velvet(dc); break;
                case "lena": Lena(dc); break;
                case "mopeds": Mopeds(dc); break;
            }
        }
        var bmp = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Brush Solid(string hex, double opacity = 1) => new SolidColorBrush(C(hex)) { Opacity = opacity };

    private static void Background(DrawingContext dc, string from, string to, Point start, Point end) =>
        dc.DrawRectangle(new LinearGradientBrush(C(from), C(to), start, end), null, new Rect(0, 0, Size, Size));

    /// <summary>Magenta to violet, with falling confetti.</summary>
    private static void Neon(DrawingContext dc)
    {
        Background(dc, "#F0389A", "#5B21B6", new Point(0, 0), new Point(1, 1));
        var rnd = new Random(7);
        for (int i = 0; i < 90; i++)
        {
            double x = rnd.NextDouble() * Size, y = rnd.NextDouble() * Size, w = 10 + rnd.NextDouble() * 22;
            dc.PushTransform(new RotateTransform(rnd.NextDouble() * 180, x, y));
            dc.DrawRectangle(i % 3 == 0 ? Solid("#FFFFFF", 0.95) : Solid("#F9A8D4", 0.55), null, new Rect(x, y, w, w * 0.42));
            dc.Pop();
        }
    }

    /// <summary>Orange to red, with a white sun sinking behind stripes.</summary>
    private static void Disco(DrawingContext dc)
    {
        Background(dc, "#FF9A1F", "#E0245E", new Point(0, 0), new Point(0.4, 1));
        dc.DrawEllipse(Solid("#FFF5EE"), null, new Point(300, 290), 175, 175);
        for (int i = 0; i < 6; i++)
            dc.DrawRectangle(Solid("#E0245E"), null, new Rect(0, 340 + i * 32, Size, 8 + i * 1.5));
    }

    /// <summary>Teal to blue, with a rising line of five bright dots.</summary>
    private static void Comets(DrawingContext dc)
    {
        Background(dc, "#1ED9C3", "#1D4ED8", new Point(0, 0), new Point(1, 1));
        dc.DrawLine(new Pen(Solid("#FFFFFF", 0.3), 14) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(0, 520), new Point(480, 150));
        for (int i = 0; i < 5; i++)
        {
            var p = new Point(90 + i * 90, 420 - i * 68);
            dc.DrawLine(new Pen(Solid("#FFFFFF", 0.35), 22) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(p.X - 30, p.Y + 30), p);
            dc.DrawEllipse(Solid("#FFFFFF"), null, p, 22, 22);
        }
    }

    /// <summary>Violet to pink, with concentric rings like a record.</summary>
    private static void Velvet(DrawingContext dc)
    {
        Background(dc, "#A855F7", "#EC4899", new Point(0, 0), new Point(1, 1));
        var c = new Point(300, 300);
        for (int i = 0; i < 6; i++)
            dc.DrawEllipse(null, new Pen(Solid("#FFFFFF", i % 2 == 0 ? 0.95 : 0.55), i % 2 == 0 ? 12 : 8), c, 270 - i * 46, 270 - i * 46);
        dc.DrawEllipse(Solid("#FFFFFF"), null, c, 22, 22);
    }

    /// <summary>Spring green, with a soft eight-petal flower.</summary>
    private static void Lena(DrawingContext dc)
    {
        Background(dc, "#84CC50", "#0E7A5A", new Point(0, 0), new Point(1, 1));
        var c = new Point(300, 300);
        for (int i = 0; i < 8; i++)
        {
            dc.PushTransform(new RotateTransform(i * 45, c.X, c.Y));
            dc.DrawEllipse(i % 2 == 0 ? Solid("#FFFFFF", 0.35) : Solid("#0B5D45", 0.45), null, new Point(300, 165), 52, 135);
            dc.Pop();
        }
        dc.DrawEllipse(Solid("#FFFFFF"), null, c, 46, 46);
    }

    /// <summary>Yellow to orange, with wonky stripes like scribbled-out song titles.</summary>
    private static void Mopeds(DrawingContext dc)
    {
        Background(dc, "#F5D623", "#D9441C", new Point(0, 0), new Point(1, 1));
        var rnd = new Random(41);
        for (int i = 0; i < 9; i++)
        {
            double y = 80 + i * 52, tilt = (rnd.NextDouble() - 0.5) * 6;
            dc.PushTransform(new RotateTransform(tilt, 300, y));
            dc.DrawRectangle(i % 3 == 0 ? Solid("#FFF8EC") : Solid("#7A3A10", 0.45), null, new Rect(70 + rnd.NextDouble() * 20, y, 420 + rnd.NextDouble() * 40, 26));
            dc.Pop();
        }
    }
}
