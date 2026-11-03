using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mp3Player.Wpf;

/// <summary>Picks a vivid accent colour from album art, used to tint the whole UI.</summary>
internal static class CoverPalette
{
    public static readonly Color Default = Color.FromRgb(0x8B, 0x5C, 0xF6);

    public static Color FromImage(BitmapSource source)
    {
        try
        {
            // Shrink to a tiny image; averaging ~600 pixels is plenty for a mood colour.
            double sx = 24.0 / source.PixelWidth, sy = 24.0 / source.PixelHeight;
            var scaled = new TransformedBitmap(source, new ScaleTransform(sx, sy));
            var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            int w = converted.PixelWidth, h = converted.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            converted.CopyPixels(px, stride, 0);

            double r = 0, g = 0, b = 0, total = 0;
            for (int i = 0; i + 3 < px.Length; i += 4)
            {
                double bl = px[i], gr = px[i + 1], rd = px[i + 2];
                double max = Math.Max(rd, Math.Max(gr, bl)), min = Math.Min(rd, Math.Min(gr, bl));
                double sat = max <= 0 ? 0 : (max - min) / max;
                double weight = 0.05 + sat * sat * (max / 255.0);   // vivid, bright pixels dominate
                r += rd * weight; g += gr * weight; b += bl * weight; total += weight;
            }
            if (total <= 0) return Default;

            RgbToHsv(r / total / 255, g / total / 255, b / total / 255, out double hue, out double sat0, out _);
            if (sat0 < 0.12) return Default;                         // grey cover: keep the house colour

            double s = Math.Clamp(sat0, 0.45, 0.85);
            double v = 0.85;                                         // light enough for white text on top
            HsvToRgb(hue, s, v, out double rr, out double gg, out double bb);
            return Color.FromRgb((byte)Math.Round(rr * 255), (byte)Math.Round(gg * 255), (byte)Math.Round(bb * 255));
        }
        catch
        {
            return Default;
        }
    }

    private static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        v = max;
        s = max <= 0 ? 0 : d / max;
        if (d <= 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / d) % 6);
        else if (max == g) h = 60 * (((b - r) / d) + 2);
        else h = 60 * (((r - g) / d) + 4);
        if (h < 0) h += 360;
    }

    private static void HsvToRgb(double h, double s, double v, out double r, out double g, out double b)
    {
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
        (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        r += m; g += m; b += m;
    }
}
