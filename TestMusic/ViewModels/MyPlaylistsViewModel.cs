using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TestMusic.Models;
using TestMusic.Services;

namespace TestMusic.ViewModels;

public partial class MyPlaylistsViewModel : PageViewModelBase
{
    private readonly KuGou.Net.Clients.UserClient _userClient;
    private readonly KuGou.Net.Clients.PlaylistClient _playlistClient;
    
    private int _currentPage = 1;
    [ObservableProperty] private bool _isLoadingMore; // 防止重复触发
    private bool _hasMoreSongs = true; 
    
    // 4. 控制 UI 显示：True 显示歌曲列表，False 显示歌单大格子
    [ObservableProperty] private bool _isShowingSongs;

    // 2. 当前选中的歌单对象
    [ObservableProperty] private PlaylistItem? _selectedPlaylist;

    public override string DisplayName => "我的歌单";
    public override string Icon => "/Assets/music-player-svgrepo-com.svg";
    private const string FolderCover = "avares://TestMusic/Assets/Default.png";
    private const string DefaultCover = "avares://TestMusic/Assets/Default.png";
    private const string LikeCover = "avares://TestMusic/Assets/LikeList.jpg";

    // 1. 所有的歌单列表
    public ObservableCollection<PlaylistItem> Playlists { get; } = new();

    // 3. 选中歌单内的歌曲列表
    public ObservableCollection<SongItem> SelectedPlaylistSongs { get; } = new();
    
    public ObservableCollection<PlaylistItem> Items { get; } = new();

    // 返回歌单列表的命令
    [RelayCommand]
    private void GoBack()
    {
        IsShowingSongs = false;
        SelectedPlaylist = null;
        SelectedPlaylistSongs.Clear();
    }
    
    public MyPlaylistsViewModel(
        KuGou.Net.Clients.UserClient userClient, 
        KuGou.Net.Clients.PlaylistClient playlistClient)
    {
        _userClient = userClient;
        _playlistClient = playlistClient;
        
        _ = LoadAllPlaylists();
    }
    
     [RelayCommand]
    private async Task LoadAllPlaylists()
    {
        Items.Clear();
        
        Items.Add(new PlaylistItem 
        { 
            Name = "新建/添加", 
            Type = PlaylistType.AddButton 
        });

        // 2. 加载【本地歌单】 (从 Settings 读取)
        foreach (var path in SettingsManager.Settings.LocalMusicFolders)
        {
            if (Directory.Exists(path))
            {
                var dirName = new DirectoryInfo(path).Name;
                Items.Add(new PlaylistItem
                {
                    Name = dirName,
                    LocalPath = path,
                    Type = PlaylistType.Local,
                    Cover = FolderCover, // 使用本地默认封面
                    Count = 0 // 暂时为0，点击进去扫描后再更新，或者后台预扫
                });
            }
        }

        // 3. 加载【在线歌单】
        try
        {
            var onlinePlaylists = await _userClient.GetPlaylistsAsync();
            foreach (var item in onlinePlaylists)
            {
                if (!string.IsNullOrEmpty(item.GlobalId))
                {
                    Items.Add(new PlaylistItem
                    {
                        Name = item.Name,
                        Id = item.GlobalId,
                        Count = item.Count,
                        Type = PlaylistType.Online,
                        Cover = string.IsNullOrWhiteSpace(item.Pic) ? item.ListId!=2? DefaultCover:LikeCover : item.Pic
                    });
                }
            }
        }
        catch
        {
            //StatusMessage = "在线歌单加载失败";
        }
    }

    // [核心] 打开歌单详情
    [RelayCommand]
    private async Task OpenPlaylist(PlaylistItem item)
    {
        if (item.Type == PlaylistType.AddButton) return; // 点击加号不应该进这里

        SelectedPlaylist = item;
        IsShowingSongs = true;
        SelectedPlaylistSongs.Clear();

        _currentPage = 1;
        _hasMoreSongs = true;
        IsLoadingMore = false;

        if (item.Type == PlaylistType.Online)
        {
            // 加载第一页
            await LoadMoreSongsInternal(); 
        }
        else if (item.Type == PlaylistType.Local)
        {
            await ScanLocalFolder(item.LocalPath!);
        }
    }
    
    [RelayCommand]
    private async Task LoadMore()
    {
        // 只有在线歌单、没有正在加载、且还有更多数据时才执行
        if (SelectedPlaylist?.Type != PlaylistType.Online || IsLoadingMore || !_hasMoreSongs) 
            return;

        _currentPage++;
        await LoadMoreSongsInternal();
    }
    
    [RelayCommand]
    private void DeleteLocalPlaylist(PlaylistItem item)
    {
        if (item == null || item.Type != PlaylistType.Local) return;

        // 1. 从界面集合移除
        Items.Remove(item);

        // 2. 从配置中移除
        if (!string.IsNullOrEmpty(item.LocalPath))
        {
            if (SettingsManager.Settings.LocalMusicFolders.Contains(item.LocalPath))
            {
                SettingsManager.Settings.LocalMusicFolders.Remove(item.LocalPath);
                SettingsManager.Save();
            }
        }
    }

    // [新增] 核心加载逻辑
    private async Task LoadMoreSongsInternal()
    {
        if (SelectedPlaylist == null) return;
        
        IsLoadingMore = true;
        try
        {
            var songs = await _playlistClient.GetSongsAsync(SelectedPlaylist.Id, _currentPage, 100);
            
            if (songs.Count < 100)
            {
                _hasMoreSongs = false; 
            }

            foreach (var s in songs)
            {
                var singerName = s.Singers.Count > 0 ? string.Join("、", s.Singers.Select(x => x.Name)) : "未知";
                
                // 注意：这里是 Add 而不是 Clear，追加到列表末尾
                SelectedPlaylistSongs.Add(new SongItem
                {
                    Name = s.Name,
                    Singer = singerName,
                    Hash = s.Hash,
                    AlbumId = s.AlbumId,
                    Cover = string.IsNullOrWhiteSpace(s.Cover) ? DefaultCover : s.Cover,
                    DurationSeconds = s.DurationMs / 1000.0,
                    
                });
            }
        }
        catch (Exception)
        {
            // 加载失败时回滚页码，允许用户重试
            _currentPage--; 
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private async Task ScanLocalFolder(string path)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(path)) return;
            var supportedExtensions = new[] { ".mp3", ".flac", ".wav", ".ogg", ".m4a" };
            
            try 
            {
                var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                foreach (var file in files)
                {
                    try
                    {
                        // 使用 TagLib 读取信息
                        using var tfile = TagLib.File.Create(file);
                        var title = tfile.Tag.Title ?? Path.GetFileNameWithoutExtension(file);
                        var artists = tfile.Tag.Performers;
                        var singer = artists.Length > 0 ? string.Join(", ", artists) : "未知艺术家";

                        var songItem = new SongItem
                        {
                            Name = title,
                            Singer = singer,
                            DurationSeconds = tfile.Properties.Duration.TotalSeconds,
                            LocalFilePath = file, // 标记路径
                            Cover = DefaultCover
                        };

                        // 回到 UI 线程添加
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedPlaylistSongs.Add(songItem));
                    }
                    catch { /* 忽略损坏文件 */ }
                }
            }
            catch { /* 忽略权限错误 */ }
        });
    }

    // [新增] 处理“添加”按钮点击
    // 供 View 调用，因为需要 StorageProvider，或者通过 Messenger
    public void AddLocalPlaylist(string path)
    {
        // 1. 保存到配置
        if (!SettingsManager.Settings.LocalMusicFolders.Contains(path))
        {
            SettingsManager.Settings.LocalMusicFolders.Add(path);
            SettingsManager.Save();
        }

        // 2. 刷新列表
        _ = LoadAllPlaylists();
    }
    
}