using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Mp3Player.Wpf;

/// <summary>
/// View code only: window plumbing, the in-window confirmation card, and the accent/animation effects.
/// All player logic lives in <see cref="PlayerViewModel"/> and Mp3Player.Core.
/// </summary>
internal partial class MainWindow : Window, IDialogService
{
    private readonly PlayerViewModel _vm;
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

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += async (_, _) => await _vm.InitializeAsync();
        Closing += OnClosing;
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

    // ------------------------------------------------------------------ window events

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_confirmation != null && e.Key == Key.Escape)
        {
            CloseConfirmation(false);
            e.Handled = true;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _confirmation?.TrySetResult(false);
        _vm.Dispose();
    }
}
