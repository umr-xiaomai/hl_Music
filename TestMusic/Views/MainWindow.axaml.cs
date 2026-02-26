using Avalonia.Controls;
using Avalonia.Interactivity;
using SukiUI.Controls;
using TestMusic.Services;
using TestMusic.ViewModels;

namespace TestMusic.Views;

public partial class MainWindow : SukiWindow
{
    public bool CanClose { get; set; } = false; 
    
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.MainWindow = this;
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var behavior = SettingsManager.Settings.CloseBehavior;
        if (behavior == CloseBehavior.MinimizeToTray && !CanClose)
        {
            e.Cancel = true; 
            this.Hide();    
            return;        
        }
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ForceCloseDesktopLyric();
        }

        base.OnClosing(e);
    }
}