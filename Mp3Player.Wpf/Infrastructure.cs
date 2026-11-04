using System.Windows.Input;

namespace Mp3Player.Wpf;

/// <summary>What the view-model needs from the window, so it never touches a control directly.</summary>
internal interface IDialogService
{
    /// <summary>Shows an in-window confirmation card; completes with true if the user confirmed.</summary>
    Task<bool> ConfirmAsync(string title, string message, string detail, string confirmText);

    void ShowMessage(string title, string message, bool isError);

    /// <summary>Returns the chosen folder, or null if the user cancelled.</summary>
    string? PickFolder(string? initialFolder);
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>Name, version and credits shown on the About card and in the status bar.</summary>
internal static class AppInfo
{
    public const string Name = "MP3 Player";

    /// <summary>"1.1.0", from the &lt;Version&gt; in the project file.</summary>
    public static string Version { get; } = typeof(AppInfo).Assembly.GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "";
}
