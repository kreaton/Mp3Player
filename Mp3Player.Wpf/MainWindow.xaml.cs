using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Win32;
using Mp3Player.Core;
using Velopack;
using Velopack.Sources;

namespace Mp3Player.Wpf;

/// <summary>
/// View code only: window plumbing, the in-window cards (confirmation, about, search), keyboard shortcuts, and the accent/animation effects.
/// All player logic lives in <see cref="PlayerViewModel"/> and Mp3Player.Core.
/// </summary>
internal partial class MainWindow : Window, IDialogService
{
    // Public GitHub repo whose releases BuildRelease.ps1 uploads to
    private const string RepoUrl = "https://github.com/kreaton/Mp3Player";

    private readonly UpdateManager _updateManager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    private readonly PlayerViewModel _vm;
    private readonly TrackNotifier _notifier;
    private MediaSession? _mediaSession;
    private TaskCompletionSource<bool>? _confirmation;

    public MainWindow(string? startFolder)
    {
        InitializeComponent();

        _vm = new PlayerViewModel(this, startFolder);
        DataContext = _vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        ApplyAccent(_vm.Accent, animate: false);

        // Position slider: drag = preview, release = seek; keyboard = seek immediately.
        SeekSlider.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(SeekSlider_MouseDown), true);
        SeekSlider.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(SeekSlider_MouseUp), true);
        SeekSlider.ValueChanged += SeekSlider_ValueChanged;

        VersionText.Text = "v" + AppInfo.Version;
        AboutName.Text = AppInfo.Name;
        AboutVersion.Text = "Version " + AppInfo.Version;

        PreviewKeyDown += OnPreviewKeyDown;
        _vm.SearchIndexReady += (_, _) =>
        {
            if (SearchOverlay.Visibility == Visibility.Visible) RunSearch();
        };
        Loaded += async (_, _) =>
        {
            Focus();      // so the keyboard shortcuts work right away
            await _vm.InitializeAsync();
        };
        Closing += OnClosing;

        // Windows integration: taskbar thumbnail buttons, the media overlay (SMTC), and "now playing" notifications.
        _ = new TaskbarButtons(this, _vm, BringToFront);
        SourceInitialized += (_, _) =>
            _mediaSession = MediaSession.TryCreate(new WindowInteropHelper(this).Handle, _vm, Dispatcher);
        _notifier = new TrackNotifier(() => Dispatcher.BeginInvoke(BringToFront));
        _vm.TrackChanged += (_, _) =>
            // After the StateChanged that follows a track change, so IsPlaying is current.
            Dispatcher.BeginInvoke(NotifyTrack, DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------ "now playing" notification

    private void NotifyTrack()
    {
        // Only when it is useful: switched on, actually playing, and the window is not already in front of you.
        if (!_vm.TrackNotifications || !_vm.IsPlaying || IsActive || _vm.CurrentInfo == null) return;
        _notifier.Show(_vm.Heading, _vm.SubHeading, _vm.CurrentInfo.CoverArt);
    }

    private void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    // ------------------------------------------------------------------ view-model -> view effects

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlayerViewModel.Accent):
                ApplyAccent(_vm.Accent, animate: true);
                break;
            case nameof(PlayerViewModel.CoverArt):
                FadeIn(CoverCard, 1.0, 350);
                FadeIn(BackdropImage, 0.42, 700);
                break;
        }
    }

    private static void FadeIn(UIElement element, double to, int milliseconds) =>
        element.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new QuadraticEase() });

    /// <summary>Retints the UI. The brush instances stay the same, so everything bound to them follows.</summary>
    private void ApplyAccent(Color color, bool animate)
    {
        if (Resources["AccentBrush"] is SolidColorBrush brush && !brush.IsFrozen)
        {
            if (animate)
                brush.BeginAnimation(SolidColorBrush.ColorProperty,
                    new ColorAnimation(color, TimeSpan.FromMilliseconds(500)) { EasingFunction = new QuadraticEase() });
            else
                brush.Color = color;
        }
        else
        {
            Resources["AccentBrush"] = new SolidColorBrush(color);
        }
    }

    // ------------------------------------------------------------------ position slider

    private void SeekSlider_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (SeekSlider.IsEnabled) _vm.BeginSeek();
    }

    private void SeekSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_vm.IsSeeking) _vm.EndSeek(SeekSlider.Value);
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_vm.IsUpdatingPosition) return;                 // the timer moved it, not the user
        if (_vm.IsSeeking) _vm.PreviewSeek(e.NewValue);
        else if (SeekSlider.IsKeyboardFocusWithin) _vm.SeekTo(e.NewValue);
    }

    // ------------------------------------------------------------------ IDialogService

    public Task<bool> ConfirmAsync(string title, string message, string detail, string confirmText)
    {
        _confirmation?.TrySetResult(false);

        DialogTitle.Text = title;
        DialogMessage.Text = message;
        DialogDetail.Text = detail;
        DialogDetailBox.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
        ConfirmButton.Content = confirmText;

        _confirmation = new TaskCompletionSource<bool>();
        MainLayer.IsEnabled = false;
        Overlay.Visibility = Visibility.Visible;
        FadeIn(Overlay, 1.0, 160);

        // Focus "Cancel" so a stray Enter or Space never deletes anything.
        Dispatcher.BeginInvoke(new Action(() => CancelButton.Focus()), DispatcherPriority.Input);
        return _confirmation.Task;
    }

    private void CloseConfirmation(bool result)
    {
        Overlay.Visibility = Visibility.Collapsed;
        MainLayer.IsEnabled = true;
        Focus();
        var pending = _confirmation;
        _confirmation = null;
        pending?.TrySetResult(result);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => CloseConfirmation(false);

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => CloseConfirmation(true);

    public void ShowMessage(string title, string message, bool isError) =>
        MessageBox.Show(this, message, title, MessageBoxButton.OK,
            isError ? MessageBoxImage.Error : MessageBoxImage.Information);

    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the folder to play (its subfolders are included)",
            Multiselect = false,
        };
        if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
            dialog.InitialDirectory = initialFolder;
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    // ------------------------------------------------------------------ about card

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        AboutIcon.Source ??= LoadLargestIconFrame();
        MainLayer.IsEnabled = false;
        AboutOverlay.Visibility = Visibility.Visible;
        FadeIn(AboutOverlay, 1.0, 160);
        Dispatcher.BeginInvoke(new Action(() => AboutCloseButton.Focus()), DispatcherPriority.Input);
    }

    private void CloseAbout()
    {
        AboutOverlay.Visibility = Visibility.Collapsed;
        MainLayer.IsEnabled = true;
        Focus();
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (!_updateManager.IsInstalled)
        {
            MessageBox.Show(this, "Updates are only available in the installed app.", "Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var newVersion = await _updateManager.CheckForUpdatesAsync();
            if (newVersion == null)
            {
                MessageBox.Show(this, "No updates available.", "Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"A new version ({newVersion.TargetFullRelease.Version}) is available. Do you want to download and install it now?",
                "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            await _updateManager.DownloadUpdatesAsync(newVersion);

            // Install and restart once the window has closed the normal way, saving the settings
            _updateManager.WaitExitThenApplyUpdates(newVersion);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Update failed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AboutClose_Click(object sender, RoutedEventArgs e) => CloseAbout();

    private void AboutOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => CloseAbout();

    // Clicks on the card itself must not reach the overlay behind it.
    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // No browser available: nothing useful to do.
        }
        e.Handled = true;
    }

    /// <summary>The 256 px frame of app.ico; a plain Image would pick the first (16 px) frame and blur it.</summary>
    private static ImageSource? LoadLargestIconFrame()
    {
        try
        {
            var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/app.ico"),
                BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames.MaxBy(f => f.PixelWidth);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ search card

    private const int MaxSearchResults = 100;

    private void SearchButton_Click(object sender, RoutedEventArgs e) => OpenSearch();

    private void OpenSearch()
    {
        SearchBox.Text = "";
        RunSearch();
        MainLayer.IsEnabled = false;
        SearchOverlay.Visibility = Visibility.Visible;
        FadeIn(SearchOverlay, 1.0, 160);
        Dispatcher.BeginInvoke(new Action(() => SearchBox.Focus()), DispatcherPriority.Input);
    }

    private void CloseSearch()
    {
        SearchOverlay.Visibility = Visibility.Collapsed;
        MainLayer.IsEnabled = true;
        Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RunSearch();

    private void RunSearch()
    {
        string? selected = (SearchList.SelectedItem as TrackSummary)?.FilePath;
        var results = _vm.SearchTracks(SearchBox.Text, MaxSearchResults);

        SearchList.ItemsSource = results;
        SearchList.SelectedItem = results.FirstOrDefault(t => t.FilePath == selected) ?? results.FirstOrDefault();
        if (SearchList.SelectedItem != null) SearchList.ScrollIntoView(SearchList.SelectedItem);

        string count = !_vm.HasTracks ? "No tracks loaded"
            : results.Count == 0 ? "No matches"
            : results.Count == MaxSearchResults ? $"First {MaxSearchResults} tracks - type more to narrow it down"
            : results.Count == 1 ? "1 track"
            : $"{results.Count} tracks";
        SearchHint.Text = _vm.IsSearchIndexReady ? count : count + " (still reading tags, some may be missing)";
    }

    private void MoveSearchSelection(int delta)
    {
        if (SearchList.Items.Count == 0) return;
        SearchList.SelectedIndex = Math.Clamp(SearchList.SelectedIndex + delta, 0, SearchList.Items.Count - 1);
        SearchList.ScrollIntoView(SearchList.SelectedItem);
    }

    private void PlaySearchSelection()
    {
        if (SearchList.SelectedItem is not TrackSummary track) return;
        CloseSearch();
        _vm.PlayTrack(track.FilePath);
    }

    private void SearchPlay_Click(object sender, RoutedEventArgs e) => PlaySearchSelection();

    private void SearchClose_Click(object sender, RoutedEventArgs e) => CloseSearch();

    private void SearchOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => CloseSearch();

    // Double-clicking a row plays it; the scrollbar and empty space must not.
    private void SearchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(SearchList, (DependencyObject)e.OriginalSource) is ListBoxItem)
            PlaySearchSelection();
    }

    /// <summary>These keys drive the list from anywhere in the card; anything else is typed into the search box.</summary>
    private void HandleSearchKey(KeyEventArgs e)
    {
        if (e.Key != Key.Tab && SearchList.IsKeyboardFocusWithin) SearchBox.Focus();     // a row was clicked

        switch (e.Key)
        {
            case Key.Escape: CloseSearch(); break;
            case Key.Enter: PlaySearchSelection(); break;
            case Key.Down: MoveSearchSelection(1); break;
            case Key.Up: MoveSearchSelection(-1); break;
            case Key.PageDown: MoveSearchSelection(8); break;
            case Key.PageUp: MoveSearchSelection(-8); break;
            default: return;
        }
        e.Handled = true;
    }

    // ------------------------------------------------------------------ window events

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_confirmation != null || AboutOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key != Key.Escape) return;      // no shortcuts underneath a card
            if (_confirmation != null) CloseConfirmation(false);
            else CloseAbout();
            e.Handled = true;
        }
        else if (SearchOverlay.Visibility == Visibility.Visible)
        {
            HandleSearchKey(e);
        }
        else
        {
            HandleShortcut(e);
        }
    }

    /// <summary>
    /// The player's shortcuts. They are handled here, before the focused control sees them, so Space always means
    /// play/pause (not "click the button you just used") and the arrows work wherever the focus is.
    /// With the media overlay (SMTC) active, Windows delivers the hardware media keys through it instead,
    /// so they are left alone here to avoid acting on one key press twice.
    /// </summary>
    private void HandleShortcut(KeyEventArgs e)
    {
        const double SeekStep = 5, VolumeStep = 5;

        switch (e.Key, Keyboard.Modifiers)
        {
            case (Key.Space, ModifierKeys.None) when !e.IsRepeat:
            case (Key.MediaPlayPause, ModifierKeys.None) when !e.IsRepeat && _mediaSession == null:
                Run(_vm.PlayPauseCommand);
                break;
            case (Key.Right, ModifierKeys.Control) when !e.IsRepeat:
            case (Key.MediaNextTrack, ModifierKeys.None) when !e.IsRepeat && _mediaSession == null:
                Run(_vm.NextCommand);
                break;
            case (Key.Left, ModifierKeys.Control) when !e.IsRepeat:
            case (Key.MediaPreviousTrack, ModifierKeys.None) when !e.IsRepeat && _mediaSession == null:
                Run(_vm.PreviousCommand);
                break;
            case (Key.Right, ModifierKeys.None):
                _vm.SeekBy(SeekStep);
                break;
            case (Key.Left, ModifierKeys.None):
                _vm.SeekBy(-SeekStep);
                break;
            case (Key.Up, ModifierKeys.None):
                _vm.Volume = Math.Min(100, _vm.Volume + VolumeStep);
                break;
            case (Key.Down, ModifierKeys.None):
                _vm.Volume = Math.Max(0, _vm.Volume - VolumeStep);
                break;
            case (Key.S, ModifierKeys.None) when !e.IsRepeat:
                _vm.Shuffle = !_vm.Shuffle;
                break;
            case (Key.F, ModifierKeys.Control) when !e.IsRepeat:
                OpenSearch();
                break;
            default:
                return;
        }
        e.Handled = true;

        static void Run(ICommand command)
        {
            if (command.CanExecute(null)) command.Execute(null);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _confirmation?.TrySetResult(false);
        _mediaSession?.Dispose();
        _notifier.Clear();
        _vm.Dispose();
    }
}
