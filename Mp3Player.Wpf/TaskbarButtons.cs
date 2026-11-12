using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Win32;

namespace Mp3Player.Wpf;

/// <summary>Delete / previous / play-pause / next buttons in the taskbar thumbnail preview (hover over the taskbar icon).</summary>
internal sealed class TaskbarButtons
{
    // 16x16 path data. Trash can: handle, lid, and a body with two slots (path markup fills even-odd, so the inner bars are cut out).
    private const string DeletePath = "M6,1 H10 V2.5 H6 Z M2,2.5 H14 V4 H2 Z M3.5,5 H12.5 L11.7,15 H4.3 Z M6,7 H7 V13 H6 Z M9,7 H10 V13 H9 Z";
    private const string PreviousPath = "M14,2 L6,8 L14,14 Z M2,2 H5 V14 H2 Z";
    private const string NextPath = "M2,2 L10,8 L2,14 Z M11,2 H14 V14 H11 Z";
    private const string PlayPath = "M4,2 L14,8 L4,14 Z";
    private const string PausePath = "M3,2 H7 V14 H3 Z M9,2 H13 V14 H9 Z";

    private readonly PlayerViewModel _vm;
    private readonly ThumbButtonInfo _delete, _previous, _playPause, _next;
    private Brush _iconBrush = Brushes.White;

    /// <param name="showWindow">Brings the window to the front, where deleting asks for confirmation.</param>
    public TaskbarButtons(Window window, PlayerViewModel vm, Action showWindow)
    {
        _vm = vm;
        _playPause = new ThumbButtonInfo { Command = vm.PlayPauseCommand };
        _previous = new ThumbButtonInfo { Command = vm.PreviousCommand, Description = "Previous" };
        _next = new ThumbButtonInfo { Command = vm.NextCommand, Description = "Next" };

        var delete = new RelayCommand(
            () =>
            {
                if (!vm.DeleteWithoutConfirmation) showWindow();
                if (vm.DeleteTrackCommand.CanExecute(null)) vm.DeleteTrackCommand.Execute(null);
            },
            () => vm.DeleteTrackCommand.CanExecute(null));
        _delete = new ThumbButtonInfo { Command = delete, Description = "Delete track" };

        window.TaskbarItemInfo ??= new TaskbarItemInfo();
        window.TaskbarItemInfo.ThumbButtonInfos = new ThumbButtonInfoCollection
        {
            _delete, _previous, _playPause, _next,
        };

        vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdateIcons();

        // The preview follows the Windows light/dark setting; recolour the icons when it changes.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        window.Closed += (_, _) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General) Dispatch(UpdateIcons);
    }

    private void Dispatch(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);

    /// <summary>Black icons on the light taskbar preview, white ones on the dark one.</summary>
    private void UpdateIcons()
    {
        _iconBrush = IsLightTaskbar() ? Brushes.Black : Brushes.White;
        _delete.ImageSource = Icon(DeletePath);
        _previous.ImageSource = Icon(PreviousPath);
        _next.ImageSource = Icon(NextPath);
        UpdatePlayPause();
    }

    private static bool IsLightTaskbar()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.IsPlaying)) UpdatePlayPause();
    }

    private void UpdatePlayPause()
    {
        _playPause.ImageSource = Icon(_vm.IsPlaying ? PausePath : PlayPath);
        _playPause.Description = _vm.IsPlaying ? "Pause" : "Play";
    }

    /// <summary>A 16x16 icon from path data (the transparent square fixes the bounds so every icon lines up).</summary>
    private ImageSource Icon(string pathData)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(_iconBrush, null, Geometry.Parse(pathData)));
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
