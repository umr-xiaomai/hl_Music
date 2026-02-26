using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuGou.Net.Adapters.Lyrics;
using KuGou.Net.Clients;
using SimpleAudio;
using SukiUI.Toasts;

namespace TestMusic.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    private readonly LyricClient _lyricClient;
    private readonly MusicClient _musicClient;
    private readonly DispatcherTimer _playbackTimer;
    private readonly SimpleAudioPlayer _player;
    private readonly Random _random = new();
    private readonly ISukiToastManager _toastManager;
    
    private readonly UserClient _userClient;
    private readonly PlaylistClient _playlistClient;

    [ObservableProperty] private LyricLineViewModel? _currentLyricLine;

    // 歌词解析缓存
    private List<KrcLine> _currentLyrics = new();

    // 歌词相关
    [ObservableProperty] private string _currentLyricText = "---";
    [ObservableProperty] private string _currentLyricTrans = "";

    [ObservableProperty] private SongItem? _currentPlayingSong;
    [ObservableProperty] private double _currentPositionSeconds;
    [ObservableProperty] private bool _isDraggingProgress;
    [ObservableProperty] private bool _isPlayingAudio;
    [ObservableProperty] private string _musicQuality = "high";

    // [新增] 随机模式状态
    [ObservableProperty] private bool _isShuffleMode;

    [ObservableProperty] private float _musicVolume = 1.0f;

    // [新增] 影子队列：永远保存原始顺序
    private List<SongItem> _originalQueue = new();

    // 状态消息 (用于通知 MainWindow 显示 Toast 或底部文字)
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private double _totalDurationSeconds;
    
    private Dictionary<string, int> _favoriteCache = new(StringComparer.OrdinalIgnoreCase);
    [ObservableProperty] private bool _isCurrentSongLiked;
    
    [ObservableProperty] private bool _isTogglingLike;

    public PlayerViewModel(MusicClient musicClient, LyricClient lyricClient, ISukiToastManager toastManager, UserClient userClient, PlaylistClient playlistClient)
    {
        _musicClient = musicClient;
        _lyricClient = lyricClient;
        _toastManager = toastManager;
        _userClient = userClient;
        _playlistClient = playlistClient;

        _player = new SimpleAudioPlayer();

        _player.PlaybackEnded += () => Dispatcher.UIThread.Post(() => PlayNextCommand.Execute(null));

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _playbackTimer.Tick += OnPlaybackTimerTick;
    }

    public ObservableCollection<LyricLineViewModel> LyricLines { get; } = new();

    // 播放队列 (独立于页面)
    public ObservableCollection<SongItem> PlaybackQueue { get; } = new();

    /// <summary>
    ///     核心播放逻辑
    /// </summary>
    /// <param name="song">要播放的歌曲</param>
    /// <param name="contextList">
    ///     歌曲所在的上下文列表。
    ///     如果不为空，说明用户在列表中点击了播放，需要替换队列。
    ///     如果为空，说明用户在点击"上一首/下一首"，保持队列不变。
    /// </param>
    public async Task PlaySongAsync(SongItem? song, IList<SongItem>? contextList = null)
    {
        if (song == null) return;
        if (contextList != null && contextList.Any())
        {
            _originalQueue = contextList.ToList();
            PlaybackQueue.Clear();

            if (IsShuffleMode)
            {
                PlaybackQueue.Add(song);

                var otherSongs = contextList.Where(x => x != song).ToList();
                var n = otherSongs.Count;
                while (n > 1)
                {
                    n--;
                    var k = _random.Next(n + 1);
                    (otherSongs[k], otherSongs[n]) = (otherSongs[n], otherSongs[k]);
                }

                foreach (var item in otherSongs) PlaybackQueue.Add(item);
            }
            else
            {
                foreach (var item in contextList) PlaybackQueue.Add(item);
            }
        }
        else if (PlaybackQueue.Count == 0)
        {
            _originalQueue.Add(song);
            PlaybackQueue.Add(song);
        }
        
        if (CurrentPlayingSong != null) CurrentPlayingSong.IsPlaying = false;
        CurrentPlayingSong = song;
        CurrentPlayingSong.IsPlaying = true;
        CheckCurrentSongLikeStatus();
        StatusMessage = $"正在缓冲: {song.Name}";

        StopAndReset();
        try
        {
            string? url ;
            if (System.IO.File.Exists(song.LocalFilePath))
            {
                url = song.LocalFilePath;
                StatusMessage = $"正在播放本地文件: {song.Name}";
            }
            else
            {
                var playData = await _musicClient.GetPlayInfoAsync(song.Hash, MusicQuality);
                if (playData != null && playData.Status != 1)
                {
                    _toastManager.CreateToast()
                        .OfType(NotificationType.Warning)
                        .WithTitle("无法获取播放链接")
                        .WithContent($"错误代码: {playData.ErrCode}, 正在尝试下一首...")
                        .Dismiss().After(TimeSpan.FromSeconds(3))
                        .Queue();
                    
                    await Task.Delay(1000);
                    await PlayNext();
                    return;
                }

                url = playData?.Urls.FirstOrDefault(x => !string.IsNullOrEmpty(x));
                _ = LoadLyrics(song.Hash, song.Name);
            }
            
            if (_player.Load(url))
            {
                _player.SetVolume(MusicVolume);
                _player.Play();
                IsPlayingAudio = true;
                TotalDurationSeconds =
                    song.DurationSeconds > 0 ? song.DurationSeconds : _player.GetDuration().TotalSeconds;
                _playbackTimer.Start();
                StatusMessage = $"正在播放: {song.Name}";
            }
            else
            {
                StatusMessage = "音频流加载失败";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"播放出错: {ex.Message}";
            StopAndReset();
        }
    }
    
    [RelayCommand]
    private async Task PlaySong(SongItem? song)
    {
        await PlaySongAsync(song);
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_player.IsPlaying)
        {
            _player.Pause();
            IsPlayingAudio = false;
            _playbackTimer.Stop();
        }
        else
        {
            _player.Play();
            IsPlayingAudio = true;
            _playbackTimer.Start();
        }
    }

    [RelayCommand]
    private async Task PlayNext()
    {
        if (PlaybackQueue.Count == 0) return;
        var idx = CurrentPlayingSong != null ? PlaybackQueue.IndexOf(CurrentPlayingSong) : -1;
        var nextIdx = (idx + 1) % PlaybackQueue.Count;
        await PlaySongAsync(PlaybackQueue[nextIdx]);
    }

    [RelayCommand]
    private async Task PlayPrevious()
    {
        if (PlaybackQueue.Count == 0) return;
        var idx = CurrentPlayingSong != null ? PlaybackQueue.IndexOf(CurrentPlayingSong) : -1;
        var prevIdx = idx - 1;
        if (prevIdx < 0) prevIdx = PlaybackQueue.Count - 1;
        await PlaySongAsync(PlaybackQueue[prevIdx]);
    }

    [RelayCommand]
    private void ClearQueue()
    {
        PlaybackQueue.Clear();
        _originalQueue.Clear();
        StopAndReset();
    }

    [RelayCommand]
    private void RemoveFromQueue(SongItem song)
    {
        PlaybackQueue.Remove(song);
        _originalQueue.Remove(song);
        if (PlaybackQueue.Count == 0) StopAndReset();
    }

    [RelayCommand]
    private void AddToNext(SongItem? song)
    {
        if (song == null) return;
        var originalIdx = CurrentPlayingSong != null ? _originalQueue.IndexOf(CurrentPlayingSong) : -1;
        if (originalIdx >= 0 && originalIdx < _originalQueue.Count - 1)
            _originalQueue.Insert(originalIdx + 1, song);
        else
            _originalQueue.Add(song);

        var playIdx = CurrentPlayingSong != null ? PlaybackQueue.IndexOf(CurrentPlayingSong) : -1;
        if (playIdx >= 0 && playIdx < PlaybackQueue.Count - 1)
            PlaybackQueue.Insert(playIdx + 1, song);
        else
            PlaybackQueue.Add(song);

        StatusMessage = "已添加到播放队列";
    }

    [RelayCommand]
    private void ToggleShuffleMode()
    {
        IsShuffleMode = !IsShuffleMode;

        if (PlaybackQueue.Count == 0) return;

        var currentSong = CurrentPlayingSong;

        if (IsShuffleMode)
        {
            var songsToShuffle = PlaybackQueue.Where(x => x != currentSong).ToList();
            var n = songsToShuffle.Count;
            while (n > 1)
            {
                n--;
                var k = _random.Next(n + 1);
                (songsToShuffle[k], songsToShuffle[n]) = (songsToShuffle[n], songsToShuffle[k]);
            }

            PlaybackQueue.Clear();
            if (currentSong != null) PlaybackQueue.Add(currentSong);
            foreach (var song in songsToShuffle) PlaybackQueue.Add(song);
        }
        else
        {
            if (_originalQueue.Count != PlaybackQueue.Count) _originalQueue = PlaybackQueue.ToList();

            PlaybackQueue.Clear();
            foreach (var song in _originalQueue) PlaybackQueue.Add(song);
        }
    }

    private void StopAndReset()
    {
        _playbackTimer.Stop();
        _player.Stop();
        IsPlayingAudio = false;
        CurrentLyricText = "---";
        CurrentLyricTrans = "";
        CurrentPositionSeconds = 0;
        LyricLines.Clear();
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs e)
    {
        if (_player.IsStopped || IsDraggingProgress) return;
        var pos = _player.GetPosition();
        CurrentPositionSeconds = pos.TotalSeconds;
        UpdateLyrics(pos.TotalMilliseconds);
    }

    // 拖动进度条逻辑
    partial void OnCurrentPositionSecondsChanged(double value)
    {
        if (Math.Abs(value - _player.GetPosition().TotalSeconds) < 0.5) return;
        _player.SetPosition(TimeSpan.FromSeconds(value));
        UpdateLyrics(value * 1000);
    }

    partial void OnMusicVolumeChanged(float value)
    {
        _player.SetVolume(value);
    }

    private async Task LoadLyrics(string hash, string name)
    {
        CurrentLyricTrans = "";
        _currentLyrics.Clear();
        
        LyricLines.Clear();
        CurrentLyricLine = null;

        try
        {
            var searchJson = await _lyricClient.SearchLyricAsync(hash, null, name, "no");

            if (!searchJson.TryGetProperty("candidates", out var candidatesElem) ||
                candidatesElem.ValueKind != JsonValueKind.Array) return;

            var candidates = candidatesElem.EnumerateArray().ToList();
            if (candidates.Count == 0)
            {
                CurrentLyricText = "未找到歌词";
                return;
            }

            var bestMatch = candidates.First();
            var id = bestMatch.GetProperty("id").GetString();
            var key = bestMatch.GetProperty("accesskey").GetString();
            var fmt = bestMatch.TryGetProperty("fmt", out var f) ? f.GetString() ?? "krc" : "krc";

            if (id != null && key != null)
            {
                var lyricResult = await _lyricClient.GetLyricAsync(id, key, fmt);
                if (!string.IsNullOrEmpty(lyricResult.DecodedContent))
                {
                    var krc = KrcParser.Parse(lyricResult.DecodedContent);
                    _currentLyrics = krc.Lines;

                    foreach (var line in _currentLyrics)
                        LyricLines.Add(new LyricLineViewModel
                        {
                            Content = line.Content,
                            Translation = line.Translation,
                            StartTime = line.StartTime,
                            Duration = line.Duration,
                            IsActive = false
                        });

                    CurrentLyricText = _currentLyrics.Count > 0 ? "" : "暂无歌词";
                }
            }
        }
        catch
        {
            CurrentLyricText = "歌词获取失败";
        }
    }

    private void UpdateLyrics(double currentMs)
    {
        if (LyricLines.Count == 0) return;


        var currentVm =
            LyricLines.FirstOrDefault(x => currentMs >= x.StartTime && currentMs < x.StartTime + x.Duration);

        if (currentVm == null)
            currentVm = LyricLines.LastOrDefault(x => x.StartTime <= currentMs);

        if (currentVm != null && currentVm != CurrentLyricLine)
        {
            if (CurrentLyricLine != null)
                CurrentLyricLine.IsActive = false;

            CurrentLyricLine = currentVm;
            CurrentLyricLine.IsActive = true;

            CurrentLyricText = currentVm.Content;
            CurrentLyricTrans = currentVm.Translation;
        }
    }
    
    
    private readonly HashSet<string> _likedHashes = new();
    // Dictionary 用于删除时，通过Hash找到对应的 FileId (API要求)
    private readonly Dictionary<string, int> _hashToFileId = new();

    // [新增] 固定歌单ID (根据你的描述)
    private const string LikeListIdForAction = "2"; 

    // [新增] UI状态：是否我喜欢
    [ObservableProperty] private bool _isLiked;
    
    public async Task LoadLikeListAsync()
    {
        try
        {
            // 获取用户歌单列表
            var playlists = await _userClient.GetPlaylistsAsync();
            // 根据描述：固定第二个为我喜欢的歌单
            if (playlists.Count < 2) return;
            
            var likePlaylist = playlists[1]; // 索引1是第二个
            if (string.IsNullOrEmpty(likePlaylist.GlobalId)) return;

            // 获取歌单详情（获取所有歌曲）
            var songs = await _playlistClient.GetSongsAsync(likePlaylist.GlobalId, pageSize: 1000);

            lock (_likedHashes)
            {
                _likedHashes.Clear();
                _hashToFileId.Clear();
                foreach (var song in songs)
                {
                    if (!string.IsNullOrEmpty(song.Hash))
                    {
                        _likedHashes.Add(song.Hash.ToLowerInvariant());
                        // 缓存 FileId，删除时需要
                        // 注意：SDK返回的FileId是int，API可能需要转换
                        if (song.FileId != 0) 
                        {
                            _hashToFileId[song.Hash.ToLowerInvariant()] = song.FileId;
                        }
                    }
                }
            }
            
            // 刷新当前播放歌曲的状态
            CheckCurrentSongLikeStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载收藏列表失败: {ex.Message}";
        }
    }

    // 2. 检查当前歌曲是否在缓存中
    private void CheckCurrentSongLikeStatus()
    {
        if (CurrentPlayingSong != null && !string.IsNullOrEmpty(CurrentPlayingSong.Hash))
        {
            var hash = CurrentPlayingSong.Hash.ToLowerInvariant();
            IsLiked = _likedHashes.Contains(hash);
        }
        else
        {
            IsLiked = false;
        }
    }

    // 3. 切换收藏状态
    [RelayCommand]
    private async Task ToggleLike()
    {
        if (CurrentPlayingSong == null) return;
        var song = CurrentPlayingSong;
        var hash = song.Hash.ToLowerInvariant();

        try
        {
            if (IsLiked)
            {
                // --- 执行删除 ---
                if (_hashToFileId.TryGetValue(hash, out var fileId))
                {
                    // 将 int 转为 long 列表
                    var idList = new List<long> { fileId };
                    var result = await _playlistClient.RemoveSongsAsync(LikeListIdForAction, idList);
                    
                    if (result.Status == 1)
                    {
                        IsLiked = false;
                        _likedHashes.Remove(hash);
                        _hashToFileId.Remove(hash);
                        StatusMessage = "已取消收藏";
                    }
                    else
                    {
                        StatusMessage = $"取消失败: {result.ErrorCode}";
                    }
                }
                else
                {
                    // 特殊情况：刚添加还没刷新列表，没有FileId
                    StatusMessage = "正在同步列表，请稍后再试...";
                    await LoadLikeListAsync(); // 重新拉取以获取FileId
                }
            }
            else
            {
                // --- 执行添加 ---
                // 构造 Tuple List (Name, Hash, AlbumId, MixSongId)
                var songList = new List<(string Name, string Hash, string AlbumId, string MixSongId)>
                {
                    (song.Name, song.Hash, song.AlbumId ?? "0", "0")
                };

                var result = await _playlistClient.AddSongsAsync(LikeListIdForAction, songList);

                if (result.Status == 1)
                {
                    IsLiked = true;
                    _likedHashes.Add(hash);
                    StatusMessage = "已添加到我喜欢";
                    
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(1000); // 稍等API生效
                        await LoadLikeListAsync();
                    });
                }
                else
                {
                    StatusMessage = $"收藏失败: {result.ErrorCode}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"操作失败: {ex.Message}";
        }
    }
}