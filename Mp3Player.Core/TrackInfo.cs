namespace Mp3Player.Core;

/// <summary>File and ID3/tag information for one track (read with TagLibSharp).</summary>
public sealed record TrackInfo(
    string Title,
    string Artist,
    string Album,
    string Year,
    string TrackNo,
    string Genre,
    string Comment,
    string Details,
    TimeSpan Duration,
    byte[]? CoverArt = null)
{
    public static string FormatTime(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
    }

    public static TrackInfo Read(string path)
    {
        long size = 0;
        try { size = new FileInfo(path).Length; } catch { /* ignore */ }
        string sizeText = $"{size / 1048576.0:0.0} MB";

        try
        {
            using var f = TagLib.File.Create(path);
            var t = f.Tag;
            var p = f.Properties;

            string details = $"{sizeText}, {p.AudioBitrate} kbps, {p.AudioSampleRate / 1000.0:0.0##} kHz, " +
                             $"{(p.AudioChannels == 1 ? "mono" : "stereo")}, {FormatTime(p.Duration)}";

            string trackNo = t.Track == 0 ? "" : t.TrackCount > 0 ? $"{t.Track} / {t.TrackCount}" : t.Track.ToString();

            // Embedded cover art (front cover preferred, otherwise the first picture).
            byte[]? cover = null;
            try
            {
                var pic = t.Pictures.FirstOrDefault(x => x.Type == TagLib.PictureType.FrontCover)
                          ?? t.Pictures.FirstOrDefault();
                if (pic != null && pic.Data.Count > 0) cover = pic.Data.Data;
            }
            catch
            {
                // A broken picture frame must not hide the rest of the tags.
            }

            return new TrackInfo(
                t.Title ?? "",
                t.JoinedPerformers ?? "",
                t.Album ?? "",
                t.Year == 0 ? "" : t.Year.ToString(),
                trackNo,
                t.JoinedGenres ?? "",
                t.Comment ?? "",
                details,
                p.Duration,
                cover);
        }
        catch
        {
            // Unreadable or missing tags: still show the file itself.
            return new TrackInfo("", "", "", "", "", "", "", sizeText, TimeSpan.Zero);
        }
    }
}
