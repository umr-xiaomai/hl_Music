using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using KugouAvaloniaPlayer.Services;
using KugouAvaloniaPlayer.ViewModels;
using SukiUI.Controls;

namespace KugouAvaloniaPlayer.Views;

public partial class MainWindow : SukiWindow
{
    private bool _isSyncingToFullScreen;

    public MainWindow()
    {
        InitializeComponent();
    }

    public bool CanClose { get; set; }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var behavior = SettingsManager.Settings.CloseBehavior;
        if (behavior == CloseBehavior.MinimizeToTray && !CanClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (DataContext is MainWindowViewModel vm) vm.ForceCloseDesktopLyric();

        base.OnClosing(e);
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.IsQueuePaneOpen = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WindowStateProperty || _isSyncingToFullScreen) return;
        if (change.OldValue is not WindowState oldState || change.NewValue is not WindowState newState) return;

        // Match SukiUI.Demo behavior expectation: maximizing the window enters fullscreen.
        if (oldState != WindowState.FullScreen && newState == WindowState.Maximized && CanFullScreen)
            EnterFullScreenFromMaximized();
    }

    private void EnterFullScreenFromMaximized()
    {
        _isSyncingToFullScreen = true;
        try
        {
            ToggleFullScreen();
        }
        finally
        {
            _isSyncingToFullScreen = false;
        }
    }
}
