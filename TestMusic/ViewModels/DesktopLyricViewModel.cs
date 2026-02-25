using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;

namespace TestMusic.ViewModels;

public partial class DesktopLyricViewModel(PlayerViewModel player) : ViewModelBase
{
    public PlayerViewModel Player { get; } = player;

    [ObservableProperty] 
    private double _fontSize = 30;

    
    [ObservableProperty]
    private bool _isLocked;
    
    [RelayCommand]
    private void ToggleLock()
    {
        IsLocked = !IsLocked;
    }
}