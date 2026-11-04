using Mp3Player.Core;

namespace Mp3Player;

/// <summary>Find a track by typing part of its artist, album or title, and play it.</summary>
internal sealed class SearchForm : Form
{
    private const int MaxResults = 100;

    private readonly MusicPlayer _player;
    private readonly TextBox _txtQuery = new() { Dock = DockStyle.Fill };
    private readonly Label _lblHint = new()
    {
        AutoSize = true, ForeColor = SystemColors.GrayText, UseMnemonic = false, Margin = new Padding(3, 6, 3, 6),
    };
    private readonly ListView _list = new()
    {
        View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
    };
    private readonly Button _btnPlay = new() { Text = "Play", AutoSize = true, Margin = new Padding(3, 8, 3, 0) };
    private readonly Button _btnClose = new() { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(3, 8, 3, 0) };

    public SearchForm(MusicPlayer player)
    {
        _player = player;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Find a track";
        ClientSize = new Size(640, 460);
        MinimumSize = new Size(420, 320);
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        _list.Columns.Add("Title");
        _list.Columns.Add("Artist");
        _list.Columns.Add("Album");

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_txtQuery, 0, 0);
        layout.Controls.Add(_lblHint, 0, 1);
        layout.Controls.Add(_list, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, WrapContents = false,
        };
        buttons.Controls.AddRange(new Control[] { _btnClose, _btnPlay });
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);

        AcceptButton = _btnPlay;
        CancelButton = _btnClose;

        _txtQuery.TextChanged += (_, _) => RunSearch();
        _txtQuery.KeyDown += OnQueryKeyDown;
        _list.DoubleClick += (_, _) => PlaySelection();
        _list.Resize += (_, _) => SizeColumns();
        _btnPlay.Click += (_, _) => PlaySelection();
        _player.SearchIndexReady += OnIndexReady;

        SizeColumns();
        RunSearch();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _player.SearchIndexReady -= OnIndexReady;
        base.OnFormClosed(e);
    }

    private void OnIndexReady(object? sender, EventArgs e) => RunSearch();

    private void SizeColumns()
    {
        int width = _list.ClientSize.Width;
        _list.Columns[0].Width = width * 45 / 100;
        _list.Columns[1].Width = width * 28 / 100;
        _list.Columns[2].Width = width * 27 / 100;
    }

    private void RunSearch()
    {
        string? selected = (_list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag : null) as string;
        var results = _player.SearchTracks(_txtQuery.Text, MaxResults);

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var track in results)
        {
            _list.Items.Add(new ListViewItem(new[] { track.DisplayTitle, track.Artist, track.Album }) { Tag = track.FilePath });
        }
        ListViewItem? select = _list.Items.Cast<ListViewItem>().FirstOrDefault(i => (string)i.Tag! == selected)
                               ?? (_list.Items.Count > 0 ? _list.Items[0] : null);
        if (select != null)
        {
            select.Selected = true;
            select.EnsureVisible();
        }
        _list.EndUpdate();

        string count = _player.TrackCount == 0 ? "No tracks loaded"
            : results.Count == 0 ? "No matches"
            : results.Count == MaxResults ? $"First {MaxResults} tracks - type more to narrow it down"
            : results.Count == 1 ? "1 track"
            : $"{results.Count} tracks";
        _lblHint.Text = _player.IsSearchIndexReady ? count : count + " (still reading tags, some may be missing)";
    }

    /// <summary>The arrow keys move through the list while the focus stays in the text box.</summary>
    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        int delta = e.KeyCode switch
        {
            Keys.Down => 1,
            Keys.Up => -1,
            Keys.PageDown => 8,
            Keys.PageUp => -8,
            _ => 0,
        };
        if (delta == 0 || _list.Items.Count == 0) return;

        int current = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : 0;
        ListViewItem item = _list.Items[Math.Clamp(current + delta, 0, _list.Items.Count - 1)];
        item.Selected = true;
        item.EnsureVisible();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void PlaySelection()
    {
        if (_list.SelectedItems.Count == 0) return;
        _player.PlayTrack((string)_list.SelectedItems[0].Tag!);
        DialogResult = DialogResult.OK;
    }
}
