using System.Diagnostics;

namespace Mp3Player;

/// <summary>The About dialog: icon, name, version and credits.</summary>
internal sealed class AboutForm : Form
{
    /// <summary>"1.1.0", from the &lt;Version&gt; in the project file.</summary>
    public static string Version { get; } = typeof(AboutForm).Assembly.GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "";

    public AboutForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "About MP3 Player";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel
        {
            AutoSize = true, ColumnCount = 2, Padding = new Padding(16), Dock = DockStyle.Fill,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var icon = new PictureBox
        {
            Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 16, 0),
            Image = LoadIcon(),
        };
        layout.Controls.Add(icon, 0, 0);
        layout.SetRowSpan(icon, 2);

        var text = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        text.Controls.Add(new Label
        {
            Text = "MP3 Player", AutoSize = true, Font = new Font(Font.FontFamily, 14f, FontStyle.Bold),
        });
        text.Controls.Add(new Label { Text = "Version " + Version, AutoSize = true, Margin = new Padding(3, 0, 3, 10) });
        text.Controls.Add(new Label
        {
            Text = "Plays every MP3 in a folder and its subfolders, with shuffle,\ncrossfade and quick clean-up of tracks and albums.",
            AutoSize = true, Margin = new Padding(3, 0, 3, 12),
        });
        text.Controls.Add(new Label { Text = "Credits", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        text.Controls.Add(new Label { Text = "Made by Per Hejndorf, with Claude (Anthropic)", AutoSize = true });
        text.Controls.Add(NewLink("Audio: NAudio by Mark Heath (MIT)", "NAudio", "https://github.com/naudio/NAudio"));
        text.Controls.Add(NewLink("Tags: TagLibSharp (LGPL-2.1)", "TagLibSharp", "https://github.com/mono/taglib-sharp"));
        text.Controls.Add(new Label { Text = "© 2026 Per Hejndorf", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 12, 3, 0) });
        layout.Controls.Add(text, 1, 0);

        var ok = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(3, 14, 0, 0) };
        layout.Controls.Add(ok, 1, 1);
        AcceptButton = ok;
        CancelButton = ok;

        Controls.Add(layout);
    }

    /// <summary>A label whose <paramref name="linkText"/> part opens <paramref name="url"/> in the browser.</summary>
    private static LinkLabel NewLink(string text, string linkText, string url)
    {
        var link = new LinkLabel { Text = text, AutoSize = true };
        link.Links.Clear();
        link.Links.Add(text.IndexOf(linkText, StringComparison.Ordinal), linkText.Length, url);
        link.LinkClicked += (_, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo((string)e.Link!.LinkData!) { UseShellExecute = true });
            }
            catch
            {
                // No browser available: nothing useful to do.
            }
        };
        return link;
    }

    /// <summary>The large frame of the embedded app.ico (the exe's associated icon is only 32 px).</summary>
    private static Image? LoadIcon()
    {
        try
        {
            using var stream = typeof(AboutForm).Assembly.GetManifestResourceStream("Mp3Player.app.ico");
            if (stream == null) return null;
            using var icon = new Icon(stream, new Size(256, 256));
            return icon.ToBitmap();
        }
        catch
        {
            return null;
        }
    }
}
