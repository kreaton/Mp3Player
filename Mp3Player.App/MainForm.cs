using Mp3Player.Core;

namespace Mp3Player;

/// <summary>Pure UI: shows what <see cref="MusicPlayer"/> reports and forwards button clicks to it.</summary>
internal sealed class MainForm : Form
{
    /// <summary>true = deleted files/folders go to the Recycle Bin (recoverable); false = deleted permanently.</summary>
    private static readonly bool UseRecycleBin = true;

    private readonly MusicPlayer _player;
    private readonly Settings _settings = Settings.Load();
    private readonly string? _startFolder;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private readonly ToolTip _tip = new();

    private string _note = "";
    private bool _dragging;

    // ---- controls ----
    private readonly Button _btnOpen = new() { Text = "Open folder...", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button _btnSearch = new() { Text = "Search...", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _txtFolder = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button _btnAbout = new() { Text = "About...", AutoSize = true, Anchor = AnchorStyles.Right };

    private readonly Label _lblName = NewValue(), _lblFolder = NewValue(), _lblDetails = NewValue();
    private readonly Label _lblTitle = NewValue(), _lblArtist = NewValue(), _lblAlbum = NewValue(),
        _lblYear = NewValue(), _lblTrackNo = NewValue(), _lblGenre = NewValue(), _lblComment = NewValue();

    private readonly TrackBar _seek = new()
    {
        Minimum = 0, Maximum = 1, TickStyle = TickStyle.None, Dock = DockStyle.Fill,
        Enabled = false, LargeChange = 10, SmallChange = 5,
    };
    private readonly Label _lblTime = new() { Text = "0:00 / 0:00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };

    private readonly Button _btnPrev = NewButton("Previous", 80), _btnPlay = NewButton("Play", 80),
        _btnPause = NewButton("Pause", 80), _btnNext = NewButton("Next", 80),
        _btnDeleteTrack = NewButton("Delete track", 104), _btnDeleteFolder = NewButton("Delete folder", 104);

    private readonly CheckBox _chkShuffle = new() { Text = "Shuffle (random, no repeats)", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox _chkFade = new() { Text = "Crossfade", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(18, 3, 3, 3) };
    private readonly NumericUpDown _numFade = new() { Minimum = 1, Maximum = 18, Value = 4, Width = 52, Anchor = AnchorStyles.Left };

    private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, LargeChange = 10, SmallChange = 2, Dock = DockStyle.Fill };
    private readonly Label _lblVolume = new() { Text = "80%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };

    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _versionLabel = new() { Text = "v" + AboutForm.Version, ForeColor = SystemColors.GrayText };

    public MainForm(string? startFolder)
    {
        _startFolder = startFolder;
        _player = new MusicPlayer();      // created here (not in a field initializer) so the UI sync context exists
        BuildUi();
        ApplySettings();
        WireEvents();
        ShowCurrentTrack();
        UpdateUi();
        _timer.Start();
    }

    // ------------------------------------------------------------------ UI construction

    private static Label NewValue() => new()
    {
        AutoEllipsis = true,
        UseMnemonic = false,        // "Rock & Roll" must not turn into an underlined R
        Dock = DockStyle.Fill,
        Height = 22,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "-",
    };

    private static Button NewButton(string text, int width) => new()
    {
        Text = text,
        Size = new Size(width, 34),
        Margin = new Padding(4),
    };

    private static TableLayoutPanel NewInfoTable()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    private static void AddInfoRow(TableLayoutPanel t, string caption, Label value)
    {
        int row = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label
        {
            Text = caption + ":",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 3, 14, 3),
        }, 0, row);
        t.Controls.Add(value, 1, row);
    }

    private void BuildUi()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "MP3 Player";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(680, 580);
        MinimumSize = new Size(640, 580);

        try
        {
            var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon != null) Icon = icon;
        }
        catch
        {
            // Title-bar icon is cosmetic.
        }

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // 0 folder
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // 1 file info
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));      // 2 tag info
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // 3 seek
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // 4 buttons
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // 5 options
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // 6 volume
        root.RowCount = 7;

        // 0: folder picker
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(_btnOpen, 0, 0);
        top.Controls.Add(_btnSearch, 1, 0);
        top.Controls.Add(_txtFolder, 2, 0);
        top.Controls.Add(_btnAbout, 3, 0);
        root.Controls.Add(top, 0, 0);

        // 1: file info
        var grpFile = new GroupBox
        {
            Text = "File", Dock = DockStyle.Fill, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 4, 8, 6),
        };
        var tFile = NewInfoTable();
        AddInfoRow(tFile, "Name", _lblName);
        AddInfoRow(tFile, "Folder", _lblFolder);
        AddInfoRow(tFile, "Details", _lblDetails);
        grpFile.Controls.Add(tFile);
        root.Controls.Add(grpFile, 0, 1);

        // 2: tag info
        var grpTags = new GroupBox
        {
            Text = "Tag info", Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 6),
        };
        var tTags = NewInfoTable();
        tTags.AutoSize = false;
        AddInfoRow(tTags, "Title", _lblTitle);
        AddInfoRow(tTags, "Artist", _lblArtist);
        AddInfoRow(tTags, "Album", _lblAlbum);
        AddInfoRow(tTags, "Year", _lblYear);
        AddInfoRow(tTags, "Track", _lblTrackNo);
        AddInfoRow(tTags, "Genre", _lblGenre);
        AddInfoRow(tTags, "Comment", _lblComment);
        grpTags.Controls.Add(tTags);
        root.Controls.Add(grpTags, 0, 2);

        // 3: position
        var seekRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        seekRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        seekRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        seekRow.Controls.Add(_seek, 0, 0);
        seekRow.Controls.Add(_lblTime, 1, 0);
        root.Controls.Add(seekRow, 0, 3);

        // 4: transport buttons + red delete buttons
        _btnDeleteTrack.BackColor = Color.Red;
        _btnDeleteTrack.ForeColor = Color.White;
        _btnDeleteTrack.FlatStyle = FlatStyle.Flat;
        _btnDeleteTrack.FlatAppearance.BorderColor = Color.DarkRed;
        _btnDeleteTrack.Font = new Font(Font, FontStyle.Bold);
        _btnDeleteTrack.Margin = new Padding(24, 4, 4, 4);

        _btnDeleteFolder.BackColor = Color.Firebrick;      // slightly darker: it removes a whole album
        _btnDeleteFolder.ForeColor = Color.White;
        _btnDeleteFolder.FlatStyle = FlatStyle.Flat;
        _btnDeleteFolder.FlatAppearance.BorderColor = Color.DarkRed;
        _btnDeleteFolder.Font = new Font(Font, FontStyle.Bold);

        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, WrapContents = false };
        buttons.Controls.AddRange(new Control[] { _btnPrev, _btnPlay, _btnPause, _btnNext, _btnDeleteTrack, _btnDeleteFolder });
        root.Controls.Add(buttons, 0, 4);

        // 5: options
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        var lblSeconds = new Label { Text = "seconds", AutoSize = true, Anchor = AnchorStyles.Left };
        options.Controls.AddRange(new Control[] { _chkShuffle, _chkFade, _numFade, lblSeconds });
        root.Controls.Add(options, 0, 5);

        // 6: volume
        var volRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        volRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        volRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        volRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        volRow.Controls.Add(new Label { Text = "Volume:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        volRow.Controls.Add(_volume, 1, 0);
        volRow.Controls.Add(_lblVolume, 2, 0);
        root.Controls.Add(volRow, 0, 6);

        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.Items.Add(_versionLabel);

        Controls.Add(root);
        Controls.Add(_statusStrip);   // added after 'root' so it docks to the bottom first

        _tip.SetToolTip(_btnDeleteTrack, "Ask for confirmation, then skip to the next track and delete this file");
        _tip.SetToolTip(_btnDeleteFolder,
            "Ask for confirmation, then delete the whole folder of this track (usually an album) and play the next track.\n" +
            "Disabled for tracks that are directly in the opened folder.");
        _tip.SetToolTip(_chkShuffle, "Play every track once in random order before any track repeats (S)");
        _tip.SetToolTip(_btnSearch, "Find and play a track by artist, album or title (Ctrl+F)");
        _tip.SetToolTip(_btnPlay, "Play (Space)");
        _tip.SetToolTip(_btnPause, "Pause (Space)");
        _tip.SetToolTip(_btnNext, "Next (Ctrl+Right)");
        _tip.SetToolTip(_btnPrev, "Previous (Ctrl+Left; restarts the track after 3 seconds)");
        _tip.SetToolTip(_chkFade, "Fade the end of a track into the start of the next");
    }

    private void ApplySettings()
    {
        _chkShuffle.Checked = _settings.Shuffle;
        _chkFade.Checked = _settings.Crossfade;
        _numFade.Value = Math.Clamp(_settings.CrossfadeSeconds, (int)_numFade.Minimum, (int)_numFade.Maximum);
        _numFade.Enabled = _chkFade.Checked;
        _volume.Value = Math.Clamp(_settings.Volume, 0, 100);
        ApplyShuffle();
        ApplyCrossfade();
        ApplyVolume();
    }

    private void ApplyShuffle() => _player.Shuffle = _chkShuffle.Checked;
    private void ApplyCrossfade() => _player.CrossfadeSeconds = _chkFade.Checked ? (double)_numFade.Value : 0;

    private void ApplyVolume()
    {
        _player.Volume = _volume.Value;
        _lblVolume.Text = _volume.Value + "%";
    }

    private void WireEvents()
    {
        // Player -> UI
        _player.StateChanged += (_, _) => UpdateUi();
        _player.TrackChanged += (_, _) =>
        {
            _note = "";
            ShowCurrentTrack();
        };
        _player.Notice += (_, message) =>
        {
            _note = message;
            UpdateUi();
        };

        // UI -> player
        _btnOpen.Click += async (_, _) => await ChooseFolderAsync();
        _btnPlay.Click += (_, _) => _player.Play();
        _btnPause.Click += (_, _) => _player.Pause();
        _btnNext.Click += (_, _) => _player.Next();
        _btnPrev.Click += (_, _) => _player.Previous();
        _btnDeleteTrack.Click += (_, _) => OnDeleteTrack();
        _btnDeleteFolder.Click += (_, _) => OnDeleteFolder();
        _btnSearch.Click += (_, _) => ShowSearch();
        _btnAbout.Click += (_, _) =>
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        };

        _chkShuffle.CheckedChanged += (_, _) => ApplyShuffle();
        _chkFade.CheckedChanged += (_, _) =>
        {
            _numFade.Enabled = _chkFade.Checked;
            ApplyCrossfade();
        };
        _numFade.ValueChanged += (_, _) => ApplyCrossfade();
        _volume.ValueChanged += (_, _) => ApplyVolume();

        _seek.MouseDown += (_, _) => _dragging = true;
        _seek.MouseUp += (_, _) =>
        {
            _dragging = false;
            _player.Seek(TimeSpan.FromSeconds(_seek.Value));
        };
        _seek.Scroll += (_, _) =>
        {
            if (_dragging) UpdateTimeLabel(TimeSpan.FromSeconds(_seek.Value), _player.Duration);
            else _player.Seek(TimeSpan.FromSeconds(_seek.Value));      // keyboard
        };

        _timer.Tick += (_, _) => OnTick();

        Shown += async (_, _) =>
        {
            string? dir = _startFolder ?? _settings.Folder;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                await LoadFolderAsync(dir, play: _startFolder != null);
        };
    }

    /// <summary>
    /// The player's shortcuts. They are handled here, before the focused control sees them, so Space always means
    /// play/pause (not "click the button you just used") and the arrows work wherever the focus is.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        const int SeekStep = 5, VolumeStep = 5;
        bool isRepeat = (msg.LParam.ToInt64() & 0x40000000) != 0;
        bool arrow = (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down;

        // The crossfade box needs its own arrow keys.
        if (arrow && ActiveControl is NumericUpDown) return base.ProcessCmdKey(ref msg, keyData);

        switch (keyData)
        {
            case Keys.Space or Keys.MediaPlayPause:
                if (isRepeat) break;
                if (_player.State == PlayerState.Playing) _player.Pause();
                else _player.Play();
                break;
            case (Keys.Control | Keys.Right) or Keys.MediaNextTrack:
                if (!isRepeat) _player.Next();
                break;
            case (Keys.Control | Keys.Left) or Keys.MediaPreviousTrack:
                if (!isRepeat) _player.Previous();
                break;
            case Keys.Right:
                _player.Seek(_player.Position + TimeSpan.FromSeconds(SeekStep));
                break;
            case Keys.Left:
                _player.Seek(_player.Position - TimeSpan.FromSeconds(SeekStep));
                break;
            case Keys.Up:
                _volume.Value = Math.Min(_volume.Maximum, _volume.Value + VolumeStep);
                break;
            case Keys.Down:
                _volume.Value = Math.Max(_volume.Minimum, _volume.Value - VolumeStep);
                break;
            case Keys.S:
                if (!isRepeat) _chkShuffle.Checked = !_chkShuffle.Checked;
                break;
            case Keys.Control | Keys.F:
                if (!isRepeat) ShowSearch();
                break;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
        return true;
    }

    private void ShowSearch()
    {
        using var search = new SearchForm(_player);
        search.ShowDialog(this);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        _settings.Folder = _player.LibraryFolder;
        _settings.Volume = _volume.Value;
        _settings.Shuffle = _chkShuffle.Checked;
        _settings.Crossfade = _chkFade.Checked;
        _settings.CrossfadeSeconds = (int)_numFade.Value;
        _settings.Save();
        _player.Dispose();
        base.OnFormClosing(e);
    }

    // ------------------------------------------------------------------ actions

    private async Task ChooseFolderAsync()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Choose the folder to play (its subfolders are included)",
            UseDescriptionForTitle = true,
            SelectedPath = _player.LibraryFolder ?? "",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            await LoadFolderAsync(dlg.SelectedPath, play: true);
    }

    private async Task LoadFolderAsync(string dir, bool play)
    {
        UseWaitCursor = true;
        try
        {
            await _player.LoadFolderAsync(dir, play);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not read the folder:\n\n" + ex.Message, "MP3 Player",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            UseWaitCursor = false;
        }

        _txtFolder.Text = _player.LibraryFolder ?? dir;
        if (_player.TrackCount == 0)
            MessageBox.Show(this, "No MP3 files were found in that folder or its subfolders.", "MP3 Player",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Pauses while a confirmation is open so nothing advances underneath the dialog.</summary>
    private bool PauseForPrompt()
    {
        bool wasPlaying = _player.State == PlayerState.Playing;
        if (wasPlaying) _player.Pause();
        return wasPlaying;
    }

    private void ResumeAfterPrompt(bool wasPlaying)
    {
        if (wasPlaying) _player.Play();
    }

    private void OnDeleteTrack()
    {
        string? path = _player.CurrentPath;
        if (path == null) return;

        bool wasPlaying = PauseForPrompt();
        string how = UseRecycleBin ? "It will be moved to the Recycle Bin" : "It will be deleted permanently";
        var answer = MessageBox.Show(this,
            $"Delete this file?\n\n{path}\n\n{how}, and the next track will play.",
            "Delete track", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            ResumeAfterPrompt(wasPlaying);
            return;
        }

        try
        {
            _player.DeleteCurrentTrack(UseRecycleBin);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The file could not be deleted:\n\n" + ex.Message, "Delete track",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnDeleteFolder()
    {
        if (!_player.CanDeleteCurrentDirectory) return;

        DirectoryDeletionPreview preview;
        try
        {
            preview = _player.GetCurrentDirectoryDeletionPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not inspect the folder:\n\n" + ex.Message, "Delete folder",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        bool wasPlaying = PauseForPrompt();
        string how = UseRecycleBin ? "It will be moved to the Recycle Bin" : "It will be deleted permanently";
        var answer = MessageBox.Show(this,
            $"Delete this ENTIRE folder?\n\n{preview.FolderPath}\n\n" +
            $"It contains {preview.FileCount} file(s) in total ({preview.TrackCount} MP3, " +
            $"{preview.TotalBytes / 1048576.0:0.0} MB), including any subfolders.\n\n" +
            $"{how}, and the next track outside this folder will play.",
            "Delete folder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            ResumeAfterPrompt(wasPlaying);
            return;
        }

        try
        {
            _player.DeleteCurrentDirectory(UseRecycleBin);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The folder could not be deleted:\n\n" + ex.Message, "Delete folder",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------------------------------------------ display

    private void OnTick()
    {
        if (_player.State == PlayerState.Stopped) return;

        TimeSpan pos = _player.Position, dur = _player.Duration;
        if (!_dragging)
        {
            _seek.Value = Math.Clamp((int)pos.TotalSeconds, _seek.Minimum, _seek.Maximum);
            UpdateTimeLabel(pos, dur);
        }
    }

    private void UpdateTimeLabel(TimeSpan pos, TimeSpan dur) =>
        _lblTime.Text = TrackInfo.FormatTime(pos) + " / " + TrackInfo.FormatTime(dur);

    private void SetSeekRange(TimeSpan duration)
    {
        _seek.Value = 0;
        _seek.Maximum = Math.Max(1, (int)duration.TotalSeconds);
        UpdateTimeLabel(TimeSpan.Zero, duration);
    }

    private void SetValue(Label label, string? text)
    {
        string t = string.IsNullOrWhiteSpace(text) ? "-" : text.Replace("\r", " ").Replace("\n", " ");
        label.Text = t;
        _tip.SetToolTip(label, t);
    }

    private void ShowCurrentTrack()
    {
        string? path = _player.CurrentPath;
        TrackInfo? info = _player.CurrentInfo;
        if (path == null || info == null)
        {
            ClearInfo();
            UpdateUi();
            return;
        }

        SetValue(_lblName, Path.GetFileName(path));
        SetValue(_lblFolder, Path.GetDirectoryName(path));
        SetValue(_lblDetails, info.Details);
        SetValue(_lblTitle, info.Title);
        SetValue(_lblArtist, info.Artist);
        SetValue(_lblAlbum, info.Album);
        SetValue(_lblYear, info.Year);
        SetValue(_lblTrackNo, info.TrackNo);
        SetValue(_lblGenre, info.Genre);
        SetValue(_lblComment, info.Comment);

        string heading = !string.IsNullOrWhiteSpace(info.Artist) && !string.IsNullOrWhiteSpace(info.Title)
            ? info.Artist + " - " + info.Title
            : !string.IsNullOrWhiteSpace(info.Title) ? info.Title : Path.GetFileNameWithoutExtension(path);
        Text = heading + " - MP3 Player";

        SetSeekRange(_player.Duration);
        UpdateUi();
    }

    private void ClearInfo()
    {
        foreach (var l in new[] { _lblName, _lblFolder, _lblDetails, _lblTitle, _lblArtist, _lblAlbum,
                                  _lblYear, _lblTrackNo, _lblGenre, _lblComment })
            SetValue(l, null);
        Text = "MP3 Player";
        SetSeekRange(TimeSpan.Zero);
    }

    private void UpdateUi()
    {
        PlayerState state = _player.State;
        bool has = _player.TrackCount > 0;
        _btnPlay.Enabled = has && state != PlayerState.Playing;
        _btnPause.Enabled = state == PlayerState.Playing;
        _btnPrev.Enabled = _btnNext.Enabled = has;
        _btnDeleteTrack.Enabled = has && _player.CurrentPath != null;
        _btnDeleteFolder.Enabled = has && _player.CanDeleteCurrentDirectory;
        _seek.Enabled = state != PlayerState.Stopped;

        string where = has ? $"Track {_player.TrackNumber} of {_player.TrackCount}" : "No tracks";
        string mode = _player.Shuffle ? "shuffle" : "in order";
        _statusLabel.Text = $"{state} - {where} ({mode})" + (_note.Length > 0 ? " - " + _note : "");
    }
}
