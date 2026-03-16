using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;

namespace SimpleAudio;

public class SimpleAudioPlayer : IDisposable
{
    private SyncProcedure _endSyncProc;
    private int _stream;

    
    private int _eqHandle = 0;      
    private int _reverbHandle = 0;  
    private int _chorusHandle = 0;  
    
    private float[] _currentEQ = new float[10];
    private bool _surroundEnabled = false;
    private float _userVolume = 1.0f; 

    // ISO 标准的 10 段均衡器中心频率
    private static readonly float[] EQFreqs = { 31.5f, 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };

    public SimpleAudioPlayer()
    {
    }

    public event Action PlaybackEnded;

    public static void Initialize()
    {
        if (Bass.CurrentDevice == -1)
            if (!Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero))
                Console.WriteLine($"[BASS Init Error] {Bass.LastError}");
        
        Bass.PluginLoad(GetBassPluginName("bassflac"));
        if (!OperatingSystem.IsMacOS())
            Bass.PluginLoad(GetBassPluginName("bass_aac"));
        
        try
        {
            var fxVersion = BassFx.Version; 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BASS_FX Load Error] {ex.Message}");
        }
        
        Bass.Configure(Configuration.NetBufferLength, 5000); 
        Bass.Configure(Configuration.NetPreBuffer, 20); 
        Bass.Configure(Configuration.NetReadTimeOut, 10000);
    }

    #region 状态属性

    public bool IsPlaying => _stream != 0 && Bass.ChannelIsActive(_stream) == PlaybackState.Playing;


    public bool IsPaused => _stream != 0 && Bass.ChannelIsActive(_stream) == PlaybackState.Paused;

    public bool IsStopped => _stream == 0 || Bass.ChannelIsActive(_stream) == PlaybackState.Stopped;
    
    public bool IsStalled => _stream != 0 && Bass.ChannelIsActive(_stream) == PlaybackState.Stalled;

    #endregion

    #region 核心控制

    /// <summary>
    ///     加载音频
    /// </summary>
    public bool Load(string url)
    {
        Stop();

        // 【超级重点】：必须加上 BassFlags.Float！
        // 浮点解码意味着内部音频管道的上限无限大。
        // 即便你 EQ 拉到顶了，内部波形也不会削顶（不炸音），只有在最后输出给声卡时才会结算！
        var flags = BassFlags.Default | BassFlags.Float;

        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            _stream = Bass.CreateStream(url, 0, flags, null, IntPtr.Zero);
        else if (File.Exists(url))
            _stream = Bass.CreateStream(url, 0, 0, flags);

        if (_stream != 0)
        {
            _endSyncProc = EndSync;
            Bass.ChannelSetSync(_stream, SyncFlags.End, 0, _endSyncProc, IntPtr.Zero);
            
            // 每次流刷新，必须重新挂载音效
            _eqHandle = 0;
            _reverbHandle = 0;
            _chorusHandle = 0;
            ApplyEQ();
            ApplySurround();
            UpdateActualVolume();

            return true;
        }

        return false;
    }
    
    private void UpdateActualVolume()
    {
        if (_stream != 0)
        {
            // 防炸音黑科技2 (Headroom)：
            // 如果开了空间音效，或者 EQ 里有高增益(>3dB)，音频总能量必定会膨胀。
            // 此时我们主动把整体输出音量压低 20% (乘以 0.8f)，给音效留出动态空间，配合 Float 直接杜绝炸音。
            float headroom = (_surroundEnabled || _currentEQ.Any(g => g > 3f)) ? 0.8f : 1.0f;
            
            // 对数听觉曲线
            float actualVolume = (float)Math.Pow(_userVolume, 2) * headroom;
            Bass.ChannelSetAttribute(_stream, ChannelAttribute.Volume, Math.Clamp(actualVolume, 0f, 1f));
        }
    }

    /// <summary>
    ///     播放或继续播放
    /// </summary>
    public void Play()
    {
        if (_stream != 0)
            if (!Bass.ChannelPlay(_stream))
                Console.WriteLine($"[Play Error] {Bass.LastError}");
    }

    /// <summary>
    ///     暂停播放
    /// </summary>
    public void Pause()
    {
        if (_stream != 0 && IsPlaying) Bass.ChannelPause(_stream);
    }

    /// <summary>
    ///     停止播放并释放当前流资源
    /// </summary>
    public void Stop()
    {
        if (_stream != 0)
        {
            Bass.ChannelStop(_stream);
            Bass.StreamFree(_stream);
            _stream = 0;
        }
    }
    
    public static void Free()
    {
        Bass.Free();
    }

    #endregion

    #region 进度与音量

    /// <summary>
    ///     设置音量 (0.0 - 1.0)
    /// </summary>
    public void SetVolume(float volume)
    {
        if (_stream != 0) _userVolume = volume;;
        UpdateActualVolume();
    }

    /// <summary>
    ///     获取当前音量
    /// </summary>
    public float GetVolume()
    {
        if (_stream != 0 && Bass.ChannelGetAttribute(_stream, ChannelAttribute.Volume, out var vol)) return vol;
        return 0f;
    }

    /// <summary>
    ///     跳转进度 (Seek)
    /// </summary>
    /// <param name="time">目标时间点</param>
    public void SetPosition(TimeSpan time)
    {
        if (_stream == 0) return;

        // 将时间转换为字节位置
        var positionBytes = Bass.ChannelSeconds2Bytes(_stream, time.TotalSeconds);

        // 执行跳转
        Bass.ChannelSetPosition(_stream, positionBytes);
    }

    /// <summary>
    ///     跳转进度 (按百分比 0.0 - 1.0)
    /// </summary>
    public void SetPosition(double percentage)
    {
        if (_stream == 0) return;

        var len = Bass.ChannelGetLength(_stream);
        if (len > 0)
        {
            var pos = (long)(len * Math.Clamp(percentage, 0.0, 1.0));
            Bass.ChannelSetPosition(_stream, pos);
        }
    }

    public TimeSpan GetDuration()
    {
        if (_stream == 0) return TimeSpan.Zero;
        var len = Bass.ChannelGetLength(_stream);
        return len < 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_stream, len));
    }

    public TimeSpan GetPosition()
    {
        if (_stream == 0) return TimeSpan.Zero;
        var pos = Bass.ChannelGetPosition(_stream);
        return pos < 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_stream, pos));
    }

    #endregion

    #region 内部逻辑

    private void EndSync(int handle, int channel, int data, IntPtr user)
    {
        PlaybackEnded?.Invoke();
    }

    public void Dispose()
    {
        Stop();
    }

    private static string GetBassPluginName(string baseName)
    {
        if (OperatingSystem.IsWindows()) return $"{baseName}.dll";
        if (OperatingSystem.IsMacOS()) return $"lib{baseName}.dylib";
        return $"lib{baseName}.so";
    }

    #endregion
    
    #region 音效引擎控制

    /// <summary>
    /// 设置10段EQ (长度必须为10, 取值范围一般在 -15 到 15 之间，代表增益 dB)
    /// </summary>
    public void SetEQ(float[] gains)
    {
        if (gains == null || gains.Length != 10) return;
        _currentEQ = gains;
        ApplyEQ();
        UpdateActualVolume(); 
    }

    public void SetSurround(bool enable)
    {
        _surroundEnabled = enable;
        ApplySurround();
        UpdateActualVolume();
    }

    private void ApplyEQ()
    {
        if (_stream == 0) return;

        if (_eqHandle == 0)
        {
            _eqHandle = Bass.ChannelSetFX(_stream, EffectType.PeakEQ, 0);

            if (_eqHandle != 0)
            {
                for (int i = 0; i < 10; i++)
                {
                    var eq = new PeakEQParameters
                    {
                        lBand = -1, 
                        fCenter = EQFreqs[i],
                        fGain = _currentEQ[i],
                        fBandwidth = 1.0f,     
                        fQ = 0f,
                        lChannel = FXChannelFlags.All
                    };
                    Bass.FXSetParameters(_eqHandle, eq);
                }
            }
        }
        else
        {
            for (int i = 0; i < 10; i++)
            {
                var eq = new PeakEQParameters
                {
                    lBand = i, 
                    fCenter = EQFreqs[i],
                    fGain = _currentEQ[i],
                    fBandwidth = 1.0f,     
                    fQ = 0f,
                    lChannel = FXChannelFlags.All
                };
                Bass.FXSetParameters(_eqHandle, eq);
            }
        }
    }

    private void ApplySurround()
    {
        if (_stream == 0) return;

        if (_reverbHandle != 0) { Bass.ChannelRemoveFX(_stream, _reverbHandle); _reverbHandle = 0; }
        if (_chorusHandle != 0) { Bass.ChannelRemoveFX(_stream, _chorusHandle); _chorusHandle = 0; }

        if (_surroundEnabled)
        {
            _reverbHandle = Bass.ChannelSetFX(_stream, EffectType.Freeverb, 1);
            if (_reverbHandle != 0)
            {
                var reverb = new ReverbParameters
                {
                    fDryMix = 1.0f,
                    fWetMix = 0.05f,
                    fRoomSize = 0.55f,
                    fDamp = 0.6f,
                    fWidth = 1.0f
                };
                Bass.FXSetParameters(_reverbHandle, reverb);
            }
            
            _chorusHandle = Bass.ChannelSetFX(_stream, EffectType.Chorus, 2);
            if (_chorusHandle != 0)
            {
                var chorus = new ChorusParameters
                {
                    fDryMix = 1.0f,
                    fWetMix = 0.12f,
                    fFeedback = 0.0f,
                    fMinSweep = 8f,
                    fMaxSweep = 8f,
                    fRate = 0f,
                    lChannel = FXChannelFlags.All
                };
                Bass.FXSetParameters(_chorusHandle, chorus);
            }
        }
    }
    #endregion

}