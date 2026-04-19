using System.Runtime.InteropServices;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;

namespace SimpleAudio;

public partial class SimpleAudioPlayer
{
    public void SetEQ(float[]? gains)
    {
        if (gains == null || gains.Length != 10)
        {
            return;
        }

        CurrentEq = gains;
        ApplyEQ();
        UpdateActualVolume();
    }

    public void SetSurround(bool enable)
    {
        SurroundEnabled = enable;
        if (enable)
        {
            StereoWidth = 0.20f;
            ReverbAmount = 0.15f;
            ReverbTimeMs = 1500f;
            ChorusMix = 0.20f;
            EchoMix = 0.15f;
        }
        else
        {
            StereoWidth = 0f;
            ReverbAmount = 0f;
            ChorusMix = 0f;
            EchoMix = 0f;
        }

        ApplySpatialEffects();
        UpdateActualVolume();
    }

    public void SetStereoWidth(float width)
    {
        StereoWidth = Math.Clamp(width, 0f, 1f);
        SurroundEnabled = HasAnySpatialEffectEnabled();
        ApplySpatialEffects();
        UpdateActualVolume();
    }

    public void SetReverbAmount(float amount)
    {
        ReverbAmount = Math.Clamp(amount, 0f, 1f);
        SurroundEnabled = HasAnySpatialEffectEnabled();
        ApplySpatialEffects();
        UpdateActualVolume();
    }

    public void SetReverbTime(float milliseconds)
    {
        ReverbTimeMs = Math.Clamp(milliseconds, 100f, 4000f);
        SurroundEnabled = HasAnySpatialEffectEnabled();
        ApplySpatialEffects();
    }

    public void SetChorusMix(float mix)
    {
        ChorusMix = Math.Clamp(mix, 0f, 1f);
        SurroundEnabled = HasAnySpatialEffectEnabled();
        ApplySpatialEffects();
        UpdateActualVolume();
    }

    public void SetEchoMix(float mix)
    {
        EchoMix = Math.Clamp(mix, 0f, 1f);
        SurroundEnabled = HasAnySpatialEffectEnabled();
        ApplySpatialEffects();
        UpdateActualVolume();
    }

    public void SetTransitionGain(float gain)
    {
        TransitionGain = Math.Clamp(gain, 0f, 1.25f);
        UpdateActualVolume();
    }

    public void SetTransitionTone(float depth)
    {
        TransitionToneDepth = Math.Clamp(depth, 0f, 1f);
        ApplyTransitionTone();
        UpdateActualVolume();
    }

    public AudioAnalysisSnapshot GetRealtimeAnalysisSnapshot()
    {
        if (Stream == 0)
        {
            return AudioAnalysisSnapshot.Empty;
        }

        var fft = new float[1024];
        var fftRead = Bass.ChannelGetData(Stream, fft, (int)(DataFlags.FFT2048 | DataFlags.FFTNoWindow | DataFlags.FFTRemoveDC));
        if (fftRead < 0)
        {
            return AudioAnalysisSnapshot.Empty with
            {
                PositionSeconds = GetPosition().TotalSeconds,
                DurationSeconds = GetDuration().TotalSeconds
            };
        }

        var channelInfo = Bass.ChannelGetInfo(Stream);
        var sampleRate = channelInfo.Frequency > 0 ? channelInfo.Frequency : 44100;
        var totalEnergy = 0d;
        var highEnergy = 0d;
        var weightedFrequency = 0d;
        var dcOffset = fft.Length > 0 ? 1 : 0;
        for (var i = dcOffset; i < fft.Length; i++)
        {
            var value = Math.Max(0f, fft[i]);
            var energy = (double)value * value;
            totalEnergy += energy;
            var freq = i * sampleRate / 2048.0;
            weightedFrequency += freq * energy;
            if (freq >= 3200)
            {
                highEnergy += energy;
            }
        }

        var rms = totalEnergy > 0 ? Math.Sqrt(totalEnergy / Math.Max(1, fft.Length - dcOffset)) : 0d;
        var brightness = totalEnergy > 0 ? highEnergy / totalEnergy : 0d;
        var spectralCentroid = totalEnergy > 0 ? weightedFrequency / totalEnergy : 0d;
        return new AudioAnalysisSnapshot
        {
            PositionSeconds = GetPosition().TotalSeconds,
            DurationSeconds = GetDuration().TotalSeconds,
            Rms = rms,
            Brightness = brightness,
            SpectralCentroid = spectralCentroid
        };
    }

    private void ApplyEQ()
    {
        if (Stream == 0)
        {
            return;
        }

        if (PeakEqHandle == 0)
        {
            PeakEqHandle = Bass.ChannelSetFX(Stream, EffectType.PeakEQ, 0);
        }

        if (PeakEqHandle != 0)
        {
            for (var i = 0; i < 10; i++)
            {
                var eq = new PeakEQParameters
                {
                    lBand = i,
                    fCenter = EQFreqs[i],
                    fGain = CurrentEq[i],
                    fBandwidth = 1f,
                    fQ = 0f
                };
                Bass.FXSetParameters(PeakEqHandle, eq);
            }
        }
    }

    private void ApplySpatialEffects()
    {
        if (Stream == 0)
        {
            return;
        }

        Bass.ChannelGetInfo(Stream, out var info);
        var hasStereoWidth = StereoWidth > 0.001f && info.Channels == 2;
        var hasReverb = ReverbAmount > 0.001f;
        var hasChorus = ChorusMix > 0.001f && info.Channels == 2;
        var hasEcho = EchoMix > 0.001f;

        if (hasStereoWidth)
        {
            if (StereoDspHandle == 0)
            {
                StereoDspHandle = Bass.ChannelSetDSP(Stream, _stereoDspProc, IntPtr.Zero);
            }
        }
        else if (StereoDspHandle != 0)
        {
            Bass.ChannelRemoveDSP(Stream, StereoDspHandle);
            StereoDspHandle = 0;
        }

        if (hasReverb)
        {
            if (ReverbHandle == 0)
            {
                ReverbHandle = Bass.ChannelSetFX(Stream, EffectType.DXReverb, 1);
            }
        }
        else if (ReverbHandle != 0)
        {
            Bass.ChannelRemoveFX(Stream, ReverbHandle);
            ReverbHandle = 0;
        }

        if (ReverbHandle != 0)
        {
            var reverb = new DXReverbParameters
            {
                fInGain = 0f,
                fReverbMix = -18f + ReverbAmount * 12f,
                fReverbTime = ReverbTimeMs,
                fHighFreqRTRatio = 0.2f + ReverbAmount * 0.35f
            };
            Bass.FXSetParameters(ReverbHandle, reverb);
        }

        if (hasChorus)
        {
            if (ChorusHandle == 0)
            {
                ChorusHandle = Bass.ChannelSetFX(Stream, EffectType.DXChorus, 2);
            }
        }
        else if (ChorusHandle != 0)
        {
            Bass.ChannelRemoveFX(Stream, ChorusHandle);
            ChorusHandle = 0;
        }

        if (ChorusHandle != 0)
        {
            var chorus = new DXChorusParameters
            {
                fDelay = 8f + ChorusMix * 8f,
                fDepth = 3f + ChorusMix * 10f,
                fFeedback = 4f + ChorusMix * 12f,
                fFrequency = 0.18f + ChorusMix * 0.3f,
                lWaveform = DXWaveform.Sine,
                fWetDryMix = ChorusMix * 35f,
                lPhase = DXPhase.Positive180
            };
            Bass.FXSetParameters(ChorusHandle, chorus);
        }

        if (hasEcho)
        {
            if (EchoHandle == 0)
            {
                EchoHandle = Bass.ChannelSetFX(Stream, EffectType.Echo, 3);
            }
        }
        else if (EchoHandle != 0)
        {
            Bass.ChannelRemoveFX(Stream, EchoHandle);
            EchoHandle = 0;
        }

        if (EchoHandle != 0)
        {
            Bass.FXSetParameters(EchoHandle, new EchoParameters
            {
                fDryMix = Math.Clamp(1f - EchoMix * 0.18f, 0.75f, 1f),
                fWetMix = EchoMix * 0.28f,
                fFeedback = 0.08f + EchoMix * 0.24f,
                fDelay = 0.16f + EchoMix * 0.18f,
                bStereo = 0
            });
        }
    }

    private void ApplyTransitionTone()
    {
        if (Stream == 0)
        {
            return;
        }

        if (TransitionToneDepth <= 0.001f)
        {
            if (HighShelfHandle != 0)
            {
                Bass.ChannelRemoveFX(Stream, HighShelfHandle);
                HighShelfHandle = 0;
            }

            if (LowPassHandle != 0)
            {
                Bass.ChannelRemoveFX(Stream, LowPassHandle);
                LowPassHandle = 0;
            }

            return;
        }

        if (HighShelfHandle == 0)
        {
            HighShelfHandle = Bass.ChannelSetFX(Stream, EffectType.BQF, 10);
        }

        if (HighShelfHandle != 0)
        {
            Bass.FXSetParameters(HighShelfHandle, new BQFParameters
            {
                lFilter = BQFType.HighShelf,
                fCenter = DefaultHighShelfCenterHz,
                fGain = -24f * TransitionToneDepth,
                fS = 0.82f
            });
        }

        if (LowPassHandle == 0)
        {
            LowPassHandle = Bass.ChannelSetFX(Stream, EffectType.BQF, 11);
        }

        if (LowPassHandle != 0)
        {
            Bass.FXSetParameters(LowPassHandle, new BQFParameters
            {
                lFilter = BQFType.LowPass,
                fCenter = Math.Clamp(DefaultLowPassCutoffHz - TransitionToneDepth * 17200f, 800f, DefaultLowPassCutoffHz),
                fQ = 0.65f + TransitionToneDepth * 1.35f
            });
        }
    }

    private bool HasAnySpatialEffectEnabled()
    {
        return StereoWidth > 0.001f || ReverbAmount > 0.001f || ChorusMix > 0.001f || EchoMix > 0.001f;
    }

    private void StereoEnhancerDSP(int handle, int channel, IntPtr buffer, int length, IntPtr user)
    {
        if (length == 0 || buffer == IntPtr.Zero)
        {
            return;
        }

        var floatCount = length / 4;
        if (DspBuffer.Length < floatCount)
        {
            DspBuffer = new float[floatCount];
        }

        Marshal.Copy(buffer, DspBuffer, 0, floatCount);

        var width = StereoWidth;
        for (var i = 0; i < floatCount - 1; i += 2)
        {
            var l = DspBuffer[i];
            var r = DspBuffer[i + 1];
            DspBuffer[i] = l + (l - r) * width;
            DspBuffer[i + 1] = r + (r - l) * width;
        }

        Marshal.Copy(DspBuffer, 0, buffer, floatCount);
    }
}
