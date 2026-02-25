using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using TestMusic.ViewModels;

namespace TestMusic;

partial class App
{
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _playPauseItem;
    private PlayerViewModel? _playerViewModel;

    private void InitializeTrayIcon(PlayerViewModel player, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _playerViewModel = player;
        
        var iconUri = new Uri("avares://TestMusic/Assets/Test.ico"); 
        using var iconStream = AssetLoader.Open(iconUri);
        var icon = new WindowIcon(iconStream);
        
        var showItem = new NativeMenuItem("显示主界面");
        showItem.Click += (s, e) => ShowMainWindow(desktop);

        var sep1 = new NativeMenuItemSeparator();

        var prevItem = new NativeMenuItem("上一首");
        prevItem.Click += (s, e) => player.PlayPreviousCommand.Execute(null);

        _playPauseItem = new NativeMenuItem("播放"); 
        _playPauseItem.Click += (s, e) => player.TogglePlayPauseCommand.Execute(null);

        var nextItem = new NativeMenuItem("下一首");
        nextItem.Click += (s, e) => player.PlayNextCommand.Execute(null);

        var sep2 = new NativeMenuItemSeparator();

        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (s, e) =>
        {
            _trayIcon?.Dispose(); 
            desktop.Shutdown();
        };
        
        var menu = new NativeMenu();
        menu.Add(showItem);
        menu.Add(sep1);
        menu.Add(prevItem);
        menu.Add(_playPauseItem);
        menu.Add(nextItem);
        menu.Add(sep2);
        menu.Add(exitItem);
        
        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "KuGou Avalonia Player",
            Menu = menu,
            IsVisible = true
        };
        
        _trayIcon.Clicked += (s, e) => ShowMainWindow(desktop);
        
        player.PropertyChanged += OnPlayerPropertyChanged;
        
        UpdatePlayPauseText();
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.IsPlayingAudio))
        {
            UpdatePlayPauseText();
        }
    }

    private void UpdatePlayPauseText()
    {
        if (_playPauseItem != null && _playerViewModel != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _playPauseItem.Header = _playerViewModel.IsPlayingAudio ? "暂停" : "播放";
            });
        }
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow == null) return;
        
        if (desktop.MainWindow.WindowState == WindowState.Minimized)
        {
            desktop.MainWindow.WindowState = WindowState.Normal;
        }
        
        if (!desktop.MainWindow.IsVisible)
        {
            desktop.MainWindow.Show();
        }
        
        desktop.MainWindow.Activate();
    }
    
    private void ShutdownTrayIcon()
    {
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        
        if (_playerViewModel != null)
        {
            _playerViewModel.PropertyChanged -= OnPlayerPropertyChanged;
        }
    }
}