using CommunityToolkit.Mvvm.ComponentModel;
using TestMusic.Models;

namespace TestMusic.ViewModels;

public partial class SongItem : ObservableObject
{
    [ObservableProperty] private string _albumId = "";
    [ObservableProperty] private string? _cover = "avares://TestMusic/Assets/Default.png";
    [ObservableProperty] private double _durationSeconds;

    [ObservableProperty] private string _hash = "";
    [ObservableProperty] private string _fileId = "";

    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _singer = "";
    [ObservableProperty] private string? _localFilePath;
}

public partial class PlaylistItem : ObservableObject
{
    [ObservableProperty] private int _count;
    [ObservableProperty] private string _cover = "avares://TestMusic/Assets/Default.png";
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
    
    
    [ObservableProperty] private PlaylistType _type;
    [ObservableProperty] private string? _localPath;
}