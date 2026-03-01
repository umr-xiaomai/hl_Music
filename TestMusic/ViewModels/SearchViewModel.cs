using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Net.Abstractions.Models;
using KuGou.Net.Clients;
using Microsoft.Extensions.Logging;

namespace TestMusic.ViewModels;

public enum SearchType
{
    Song,
    Playlist,
    Album
}

public partial class SearchViewModel(
    MusicClient musicClient,
    PlaylistClient playlistClient,
    AlbumClient albumClient,
    ILogger<SearchViewModel> logger) : PageViewModelBase
{
    private const string DefaultCover = "avares://TestMusic/Assets/Default.png";
    [ObservableProperty] private SearchType _currentSearchType = SearchType.Song;
    [ObservableProperty] private string? _detailCover;
    [ObservableProperty] private string? _detailTitle;

    [ObservableProperty] private bool _isSearching;

    // 歌单/专辑详情
    [ObservableProperty] private bool _isShowingDetail;
    [ObservableProperty] private string _searchKeyword = "";

    public override string DisplayName => "搜索";
    public override string Icon => "/Assets/Search.svg";

    // 单曲搜索结果
    public ObservableCollection<SongItem> Songs { get; } = new();

    // 歌单搜索结果
    public ObservableCollection<SearchPlaylistItem> Playlists { get; } = new();

    // 专辑搜索结果
    public ObservableCollection<SearchAlbumItem> Albums { get; } = new();
    public ObservableCollection<SongItem> DetailSongs { get; } = new();

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword)) return;

        IsSearching = true;
        logger.LogInformation("正在搜索: {Keyword}, 类型: {Type}", SearchKeyword, CurrentSearchType);

        ClearResults();

        try
        {
            switch (CurrentSearchType)
            {
                case SearchType.Song:
                    await SearchSongs();
                    break;
                case SearchType.Playlist:
                    await SearchPlaylists();
                    break;
                case SearchType.Album:
                    await SearchAlbums();
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "搜索失败");
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void ClearResults()
    {
        Songs.Clear();
        Playlists.Clear();
        Albums.Clear();
    }

    private async Task SearchSongs()
    {
        var results = await musicClient.SearchAsync(SearchKeyword);
        foreach (var item in results)
            Songs.Add(new SongItem
            {
                Name = item.Name,
                Singer = item.Singer,
                Hash = item.Hash,
                Singers = item.Singers,
                Cover = string.IsNullOrWhiteSpace(item.Cover) ? DefaultCover : item.Cover,
                DurationSeconds = item.Duration
            });
        logger.LogInformation("找到 {Count} 首歌曲", Songs.Count);
    }

    private async Task SearchPlaylists()
    {
        var results = await musicClient.SearchSpecialAsync(SearchKeyword);
        if (results != null)
        {
            foreach (var item in results)
                Playlists.Add(item);
            logger.LogInformation("找到 {Count} 个歌单", Playlists.Count);
        }
    }

    private async Task SearchAlbums()
    {
        var results = await musicClient.SearchAlbumAsync(SearchKeyword);
        if (results != null)
        {
            foreach (var item in results)
                Albums.Add(item);
            logger.LogInformation("找到 {Count} 张专辑", Albums.Count);
        }
    }

    [RelayCommand]
    private void SwitchSearchType(string type)
    {
        if (Enum.TryParse<SearchType>(type, out var searchType))
        {
            CurrentSearchType = searchType;
            ClearResults();
            IsShowingDetail = false;
        }
    }

    [RelayCommand]
    private async Task OpenPlaylist(SearchPlaylistItem item)
    {
        if (item == null) return;

        DetailTitle = item.Name;
        DetailCover = item.Cover ?? DefaultCover;
        IsShowingDetail = true;
        DetailSongs.Clear();

        try
        {
            var songs = await playlistClient.GetSongsAsync(item.GlobalId, 1, 100);
            foreach (var s in songs)
            {
                var singerName = s.Singers.Count > 0 ? string.Join("、", s.Singers.Select(x => x.Name)) : "未知";
                DetailSongs.Add(new SongItem
                {
                    Name = s.Name,
                    Singer = singerName,
                    Hash = s.Hash,
                    AlbumId = s.AlbumId,
                    Singers = s.Singers,
                    Cover = string.IsNullOrWhiteSpace(s.Cover) ? DefaultCover : s.Cover,
                    DurationSeconds = s.DurationMs / 1000.0
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "加载歌单详情失败");
        }
    }

    [RelayCommand]
    private async Task OpenAlbum(SearchAlbumItem item)
    {
        if (item == null)
        {
            logger.LogError("专辑打开失败");
            return;
        }

        DetailTitle = item.Name;
        DetailCover = item.Cover ?? DefaultCover;
        IsShowingDetail = true;
        DetailSongs.Clear();

        try
        {
            var songs = await albumClient.GetSongsAsync(item.AlbumId.ToString());
            if (songs != null)
                foreach (var s in songs)
                {
                    var singerName = s.Singers.Count > 0 ? string.Join("、", s.Singers.Select(x => x.Name)) : "未知";
                    DetailSongs.Add(new SongItem
                    {
                        Name = s.Name,
                        Singer = singerName,
                        Hash = s.Hash,
                        AlbumId = s.AlbumId,
                        Singers = s.Singers,
                        Cover = string.IsNullOrWhiteSpace(s.Cover) ? DefaultCover : s.Cover,
                        DurationSeconds = s.DurationMs / 1000.0
                    });
                }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "加载专辑详情失败");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        IsShowingDetail = false;
        DetailSongs.Clear();
    }

    public async Task SearchAsync(string keyword)
    {
        SearchKeyword = keyword;
        await Search();
    }
}