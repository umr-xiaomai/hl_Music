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

    // [新增] 随机模式状态
    [ObservableProperty] private bool _isShuffleMode;

    [ObservableProperty] private float _musicVolume = 1.0f;

    // [新增] 影子队列：永远保存原始顺序
    private List<SongItem> _originalQueue = new();

    // 状态消息 (用于通知 MainWindow 显示 Toast 或底部文字)
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private double _totalDurationSeconds;

    public PlayerViewModel(MusicClient musicClient, LyricClient lyricClient, ISukiToastManager toastManager)
    {
        _musicClient = musicClient;
        _lyricClient = lyricClient;
        _toastManager = toastManager;

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

        // 如果传入了新的列表（比如点击了歌单播放）
        if (contextList != null && contextList.Any())
        {
            // 1. 更新影子队列 (这是基准)
            _originalQueue = contextList.ToList();

            // 2. 构建实际播放队列
            PlaybackQueue.Clear();

            if (IsShuffleMode)
            {
                // 如果是随机模式：
                // 将点击的那首歌放在第一个，其余的打乱放入
                PlaybackQueue.Add(song);

                var otherSongs = contextList.Where(x => x != song).ToList();
                // 打乱其他歌曲
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
                // 如果是顺序模式：直接复制
                foreach (var item in contextList) PlaybackQueue.Add(item);
            }
        }
        else if (PlaybackQueue.Count == 0)
        {
            // 队列为空时的保底
            _originalQueue.Add(song);
            PlaybackQueue.Add(song);
        }

        // 2. UI 更新
        if (CurrentPlayingSong != null) CurrentPlayingSong.IsPlaying = false;
        CurrentPlayingSong = song;
        CurrentPlayingSong.IsPlaying = true;
        StatusMessage = $"正在缓冲: {song.Name}";

        StopAndReset();
        try
        {
            // 3. 获取播放地址
            var playData = await _musicClient.GetPlayInfoAsync(song.Hash, "high");
            if (playData != null && playData.Status != 1)
            {
                _toastManager.CreateToast()
                    .OfType(NotificationType.Warning)
                    .WithTitle("因版权原因无法获取播放链接")
                    .WithContent("正在尝试下一首...")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Dismiss().ByClicking()
                    .Queue();
                //StatusMessage = "无法获取播放链接，尝试下一首...";
                await Task.Delay(1000);
                await PlayNext();
                return;
            }

            var url = playData?.Urls.FirstOrDefault(x => !string.IsNullOrEmpty(x));

            // 4. 加载歌词 (异步不等待)
            _ = LoadLyrics(song.Hash, song.Name);

            // 5. 播放
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

    // 提供给 Command 使用的封装
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
        await PlaySongAsync(PlaybackQueue[nextIdx]); // 不传 list，保留队列
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

        // 1. 清空界面绑定的歌词集合
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
}