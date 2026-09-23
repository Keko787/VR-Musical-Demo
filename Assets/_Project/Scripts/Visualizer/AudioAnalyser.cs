using System;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// Turns whatever is playing into numbers the room can react to: a row of frequency bands that
    /// stay in 0–1 whatever the track's level, three energy bands, the raw waveform, and a beat.
    /// Every visual layer reads from this one component, so the walls, lasers and ribbons agree on
    /// what the music is doing frame by frame.
    /// </summary>
    /// <remarks>
    /// The bands are log-spaced and each carries its own slow auto-gain, so a quiet acoustic track
    /// fills the tubes as well as a mastered club mix, and a band with nothing in it (highs on a
    /// bass-heavy track) sits near zero instead of dancing on hiss. Beats come from spectral flux in
    /// the kick range against an adaptive threshold, and are rate-limited: the room is enclosed and
    /// dark, and fast flashing is a photosensitivity risk, so nothing downstream can flash faster
    /// than <see cref="MaxBeatsPerSecond"/> however busy the track is.
    /// </remarks>
    [DefaultExecutionOrder(-100)] // before every layer that reads it
    public class AudioAnalyser : MonoBehaviour
    {
        public const int MaxBands = 64;

        [Header("Source")]
        [SerializeField, Tooltip("What to listen to. Empty listens to the AudioListener, i.e. everything the room is playing, " +
            "which is the right choice once a microphone source is added.")]
        AudioSource m_Source;

        [SerializeField, Tooltip("Spectrum bins, a power of two from 64 to 8192; the FFT window is twice this. 2048 at 48 kHz " +
            "is 11.7 Hz per bin and an 85 ms window, which tells a kick from a bass note without lagging the beat. 4096 " +
            "doubles the resolution and the lag.")]
        int m_FftSize = 2048;

        [SerializeField] FFTWindow m_Window = FFTWindow.BlackmanHarris;

        [Header("Bands")]
        [SerializeField, Range(4, MaxBands), Tooltip("Log-spaced bands between the lowest and highest frequency.")]
        int m_BandCount = 24;

        [SerializeField] float m_LowestHz = 40f;
        [SerializeField] float m_HighestHz = 14000f;

        [SerializeField, Tooltip("Decibels below a band's running peak that map to zero. 40 dB is a VU meter's range.")]
        float m_RangeDb = 40f;

        [SerializeField, Tooltip("Spectral tilt, as a power of frequency. Music falls off toward the highs; 0.5 flattens it enough " +
            "that the auto-gain does not have to.")]
        float m_Tilt = 0.5f;

        [SerializeField, Tooltip("Linear magnitude below which a band's peak will not follow, so silence and hiss do not get " +
            "amplified into a full-scale show.")]
        float m_NoiseFloor = 0.0015f;

        [SerializeField, Tooltip("Seconds for a band's auto-gain peak to fall by half when the music gets quieter.")]
        float m_GainHalfLife = 3f;

        [SerializeField, Tooltip("Seconds for a band to rise to a louder value. Short, so transients land.")]
        float m_Attack = 0.015f;

        [SerializeField, Tooltip("Seconds for a band to fall back. Longer than the attack, so the show does not flicker.")]
        float m_Release = 0.2f;

        [Header("Beats")]
        [SerializeField, Tooltip("The kick drum's range. Onsets are looked for here, not across the whole spectrum, " +
            "so hi-hats do not count as beats.")]
        float m_KickLowHz = 35f;

        [SerializeField] float m_KickHighHz = 160f;

        [SerializeField, Tooltip("How far above the recent average an onset has to be, in standard deviations.")]
        float m_BeatSensitivity = 1.4f;

        [SerializeField, Tooltip("Seconds of history the adaptive threshold looks back over.")]
        float m_BeatHistorySeconds = 1.2f;

        [SerializeField, Tooltip("Hard cap on how often a beat can fire. 3 Hz is the usual photosensitivity guidance; " +
            "the visualizer lowers it in its low-intensity mode.")]
        float m_MaxBeatsPerSecond = 3f;

        [SerializeField, Tooltip("Seconds for the beat pulse to fall to a third.")]
        float m_BeatDecay = 0.18f;

        [Header("History")]
        [SerializeField, Tooltip("Times per second the bands are copied into the spectrogram history the point clouds read.")]
        float m_HistoryRate = 24f;

        [SerializeField, Range(8, 256), Tooltip("Slices of history kept. 72 at 24 Hz is three seconds.")]
        int m_HistorySlices = 72;

        [Header("Idle")]
        [SerializeField, Tooltip("Seconds of silence before the analyser reports the room as idle.")]
        float m_SilenceSeconds = 1.5f;

        [SerializeField, Tooltip("Seconds the idle value takes to ramp between 0 and 1, so the layers can ease into a screensaver.")]
        float m_IdleFade = 2f;

        // ---------------------------------------------------------------- outputs

        /// <summary>Smoothed, auto-gained band levels in 0–1, low to high. Length is <see cref="BandCount"/>.</summary>
        public float[] Bands { get; private set; } = new float[0];

        /// <summary>The most recent output samples, normalised to roughly −1…1 by a slow peak follower.</summary>
        public float[] Waveform { get; private set; } = new float[0];

        /// <summary>Energy in 40–150 Hz, 150–2000 Hz and 2–14 kHz, each 0–1 with its own auto-gain.</summary>
        public float Bass { get; private set; }
        public float Mid { get; private set; }
        public float High { get; private set; }

        /// <summary>Overall level, 0–1, from the RMS of the output against a slow peak.</summary>
        public float Loudness { get; private set; }

        /// <summary>True on the frame a beat fires.</summary>
        public bool Beat { get; private set; }

        /// <summary>1 on a beat, decaying toward 0. What a flash should read.</summary>
        public float BeatPulse { get; private set; }

        /// <summary>How far past the threshold the last beat was, 0–1. A soft hit and a drop are not the same beat.</summary>
        public float BeatStrength { get; private set; }

        public int BeatCount { get; private set; }

        /// <summary>0 while music plays, ramping to 1 after <see cref="m_SilenceSeconds"/> of nothing.</summary>
        public float Idle { get; private set; }

        /// <summary>True when there is nothing to hear at all (no source, no clip, or silence).</summary>
        public bool Silent { get; private set; } = true;

        /// <summary>Slices of band history kept; read them newest-first through <see cref="HistoryAt(int, int)"/>.</summary>
        public int HistorySlices => m_HistorySlices;

        public float HistoryRate => m_HistoryRate;

        /// <summary>How far the clock is toward the next history sample, 0–1, so a layer can slide between slices instead of stepping.</summary>
        public float HistoryPhase { get; private set; }

        /// <summary>Slices filled so far, up to <see cref="HistorySlices"/>.</summary>
        public int HistoryCount { get; private set; }

        public event Action<float> OnBeat;

        public int BandCount => m_BandCount;
        public AudioSource Source { get => m_Source; set => m_Source = value; }

        public float MaxBeatsPerSecond
        {
            get => m_MaxBeatsPerSecond;
            set => m_MaxBeatsPerSecond = Mathf.Max(0.1f, value);
        }

        // ---------------------------------------------------------------- state

        float[] m_Spectrum = new float[0];
        float[] m_PrevSpectrum = new float[0];
        float[] m_Samples = new float[0];

        int[] m_BandLo = new int[0];
        int[] m_BandHi = new int[0];
        float[] m_BandTilt = new float[0];
        float[] m_BandPeak = new float[0];

        int m_KickLo, m_KickHi;
        int m_BassLo, m_BassHi, m_MidLo, m_MidHi, m_HighLo, m_HighHi;
        float m_BassPeak, m_MidPeak, m_HighPeak, m_LoudPeak, m_WavePeak;
        float m_BassRaw;

        const int k_History = 128;
        readonly float[] m_FluxHistory = new float[k_History];
        readonly float[] m_FluxTime = new float[k_History];
        int m_FluxHead;
        float m_LastBeatTime = float.NegativeInfinity;
        float m_SilentSince;
        float[] m_History = new float[0];   // slices × bands, a ring
        int m_HistoryHead;
        float m_HistoryClock;

        int m_ConfiguredFft = -1;
        int m_ConfiguredBands = -1;
        int m_ConfiguredRate = -1;
        int m_ConfiguredSlices = -1;

        void OnEnable()
        {
            Configure();
        }

        void OnValidate()
        {
            m_FftSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(m_FftSize, 64, 8192));
            m_BandCount = Mathf.Clamp(m_BandCount, 4, MaxBands);
            m_LowestHz = Mathf.Max(10f, m_LowestHz);
            m_HighestHz = Mathf.Max(m_LowestHz * 2f, m_HighestHz);
            m_MaxBeatsPerSecond = Mathf.Max(0.1f, m_MaxBeatsPerSecond);
            m_HistoryRate = Mathf.Max(1f, m_HistoryRate);
            m_HistorySlices = Mathf.Clamp(m_HistorySlices, 8, 256);
            if (isActiveAndEnabled)
                Configure();
        }

        /// <summary>
        /// Lays out the band edges over the FFT bins. Each band gets at least one bin, so with a
        /// small FFT the lowest bands share bins rather than read zero.
        /// </summary>
        void Configure()
        {
            int rate = AudioSettings.outputSampleRate;
            if (m_ConfiguredFft == m_FftSize && m_ConfiguredBands == m_BandCount && m_ConfiguredRate == rate &&
                m_ConfiguredSlices == m_HistorySlices && m_Spectrum.Length == m_FftSize)
                return;

            m_ConfiguredFft = m_FftSize;
            m_ConfiguredBands = m_BandCount;
            m_ConfiguredRate = rate;
            m_ConfiguredSlices = m_HistorySlices;

            m_History = new float[m_HistorySlices * m_BandCount];
            m_HistoryHead = 0;
            HistoryCount = 0;

            m_Spectrum = new float[m_FftSize];
            m_PrevSpectrum = new float[m_FftSize];
            m_Samples = new float[Mathf.Min(m_FftSize, 2048)];
            Waveform = new float[m_Samples.Length];

            Bands = new float[m_BandCount];
            m_BandLo = new int[m_BandCount];
            m_BandHi = new int[m_BandCount];
            m_BandTilt = new float[m_BandCount];
            m_BandPeak = new float[m_BandCount];

            // GetSpectrumData hands back fftSize bins covering 0 … rate/2, so each bin is half a
            // sample-rate-over-fftSize wide.
            float binHz = rate / (2f * m_FftSize);
            int maxBin = m_FftSize - 1;

            float ratio = m_HighestHz / m_LowestHz;
            for (int i = 0; i < m_BandCount; i++)
            {
                float lowHz = m_LowestHz * Mathf.Pow(ratio, i / (float)m_BandCount);
                float highHz = m_LowestHz * Mathf.Pow(ratio, (i + 1) / (float)m_BandCount);

                int lo = Mathf.Clamp(Mathf.FloorToInt(lowHz / binHz), 1, maxBin);
                int hi = Mathf.Clamp(Mathf.FloorToInt(highHz / binHz), lo + 1, maxBin + 1);

                m_BandLo[i] = lo;
                m_BandHi[i] = hi;
                m_BandTilt[i] = Mathf.Pow(Mathf.Sqrt(lowHz * highHz) / 1000f, m_Tilt);
                m_BandPeak[i] = m_NoiseFloor;
            }

            Range(m_KickLowHz, m_KickHighHz, binHz, maxBin, out m_KickLo, out m_KickHi);
            Range(40f, 150f, binHz, maxBin, out m_BassLo, out m_BassHi);
            Range(150f, 2000f, binHz, maxBin, out m_MidLo, out m_MidHi);
            Range(2000f, 14000f, binHz, maxBin, out m_HighLo, out m_HighHi);

            m_BassPeak = m_MidPeak = m_HighPeak = m_NoiseFloor;
            m_LoudPeak = 0.02f;
            m_WavePeak = 0.05f;
        }

        static void Range(float lowHz, float highHz, float binHz, int maxBin, out int lo, out int hi)
        {
            lo = Mathf.Clamp(Mathf.FloorToInt(lowHz / binHz), 1, maxBin);
            hi = Mathf.Clamp(Mathf.CeilToInt(highHz / binHz), lo + 1, maxBin + 1);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Configure();
            Sample();

            float halfLifeDecay = m_GainHalfLife > 0f ? Mathf.Pow(0.5f, dt / m_GainHalfLife) : 0f;
            float attack = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, m_Attack));
            float release = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, m_Release));

            for (int i = 0; i < m_BandCount; i++)
            {
                float raw = Mean(m_Spectrum, m_BandLo[i], m_BandHi[i]) * m_BandTilt[i];
                Bands[i] = Follow(Bands[i], Normalise(raw, ref m_BandPeak[i], halfLifeDecay), attack, release);
            }

            m_BassRaw = Mean(m_Spectrum, m_BassLo, m_BassHi);
            Bass = Follow(Bass, Normalise(m_BassRaw, ref m_BassPeak, halfLifeDecay), attack, release);
            Mid = Follow(Mid, Normalise(Mean(m_Spectrum, m_MidLo, m_MidHi) * 2f, ref m_MidPeak, halfLifeDecay), attack, release);
            High = Follow(High, Normalise(Mean(m_Spectrum, m_HighLo, m_HighHi) * 6f, ref m_HighPeak, halfLifeDecay), attack, release);

            float rms = Rms(m_Samples);
            Loudness = Follow(Loudness, Normalise(rms, ref m_LoudPeak, halfLifeDecay, 0.01f), attack, release);

            UpdateHistory(dt);
            UpdateWaveform(halfLifeDecay);
            UpdateIdle(rms, dt);
            UpdateBeat(dt);

            Array.Copy(m_Spectrum, m_PrevSpectrum, m_Spectrum.Length);
        }

        void Sample()
        {
            if (m_Source != null)
            {
                m_Source.GetSpectrumData(m_Spectrum, 0, m_Window);
                m_Source.GetOutputData(m_Samples, 0);
            }
            else
            {
                AudioListener.GetSpectrumData(m_Spectrum, 0, m_Window);
                AudioListener.GetOutputData(m_Samples, 0);
            }
        }

        static float Mean(float[] data, int lo, int hi)
        {
            hi = Mathf.Min(hi, data.Length);
            if (hi <= lo)
                return 0f;

            float sum = 0f;
            for (int b = lo; b < hi; b++)
                sum += data[b];

            return sum / (hi - lo);
        }

        static float Rms(float[] samples)
        {
            if (samples.Length == 0)
                return 0f;

            double sum = 0;
            for (int i = 0; i < samples.Length; i++)
                sum += samples[i] * (double)samples[i];

            return (float)Math.Sqrt(sum / samples.Length);
        }

        /// <summary>
        /// A value against its own running peak, in decibels, mapped onto 0–1. The peak rises at once
        /// and decays slowly, and never drops under the floor, so a passage that is merely quieter
        /// reads as quieter rather than being re-scaled back to full.
        /// </summary>
        float Normalise(float raw, ref float peak, float decay, float floor = -1f)
        {
            if (floor < 0f)
                floor = m_NoiseFloor;

            peak = Mathf.Max(raw, peak * decay, floor);
            if (raw <= floor * 0.5f)
                return 0f;

            float db = 20f * Mathf.Log10(Mathf.Max(raw, 1e-9f) / peak);
            return Mathf.Clamp01(1f + db / Mathf.Max(1f, m_RangeDb));
        }

        static float Follow(float current, float target, float attack, float release)
        {
            return current + (target - current) * (target > current ? attack : release);
        }

        /// <summary>
        /// Copies the bands into the history ring on a fixed clock, so the spectrogram's time axis
        /// is regular whatever the frame rate. A hitch is bounded to one lap of the ring.
        /// </summary>
        void UpdateHistory(float dt)
        {
            float interval = 1f / Mathf.Max(1f, m_HistoryRate);
            m_HistoryClock = Mathf.Min(m_HistoryClock + dt, interval * m_HistorySlices);

            while (m_HistoryClock >= interval)
            {
                m_HistoryClock -= interval;
                Array.Copy(Bands, 0, m_History, m_HistoryHead * m_BandCount, m_BandCount);
                m_HistoryHead = (m_HistoryHead + 1) % m_HistorySlices;
                HistoryCount = Mathf.Min(HistoryCount + 1, m_HistorySlices);
            }

            HistoryPhase = m_HistoryClock / interval;
        }

        /// <summary>A band's level <paramref name="slice"/> samples ago; 0 is the most recent sample.</summary>
        public float HistoryAt(int slice, int band)
        {
            if (m_History.Length == 0)
                return 0f;

            slice = Mathf.Clamp(slice, 0, m_HistorySlices - 1);
            band = Mathf.Clamp(band, 0, m_BandCount - 1);
            int index = ((m_HistoryHead - 1 - slice) % m_HistorySlices + m_HistorySlices) % m_HistorySlices;
            return m_History[index * m_BandCount + band];
        }

        /// <summary>The same, interpolated between two slices, for a layout that maps history onto something continuous.</summary>
        public float HistoryAt(float slice, int band)
        {
            int lo = Mathf.FloorToInt(slice);
            return Mathf.Lerp(HistoryAt(lo, band), HistoryAt(lo + 1, band), slice - lo);
        }

        void UpdateWaveform(float decay)
        {
            float peak = 0f;
            for (int i = 0; i < m_Samples.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(m_Samples[i]));

            m_WavePeak = Mathf.Max(peak, m_WavePeak * decay, 0.02f);
            float gain = 1f / m_WavePeak;

            for (int i = 0; i < m_Samples.Length; i++)
                Waveform[i] = Mathf.Clamp(m_Samples[i] * gain, -1f, 1f);
        }

        void UpdateIdle(float rms, float dt)
        {
            bool silent = rms < 1e-4f;
            if (!silent)
                m_SilentSince = Time.time;

            Silent = silent;
            float target = silent && Time.time - m_SilentSince > m_SilenceSeconds ? 1f : 0f;
            float step = m_IdleFade > 0f ? dt / m_IdleFade : 1f;
            Idle = Mathf.MoveTowards(Idle, target, step);
        }

        /// <summary>
        /// Spectral flux in the kick range — how much louder each bin got since the last frame —
        /// against the mean and spread of the last second or so of the same measure. A beat is a
        /// frame that stands well clear of that history, and not too soon after the last one.
        /// </summary>
        void UpdateBeat(float dt)
        {
            Beat = false;
            BeatPulse *= Mathf.Exp(-dt / Mathf.Max(0.01f, m_BeatDecay));

            float flux = 0f;
            int hi = Mathf.Min(m_KickHi, m_Spectrum.Length);
            for (int b = m_KickLo; b < hi; b++)
                flux += Mathf.Max(0f, m_Spectrum[b] - m_PrevSpectrum[b]);

            flux /= Mathf.Max(dt, 1e-3f);

            float now = Time.time;
            m_FluxHistory[m_FluxHead] = flux;
            m_FluxTime[m_FluxHead] = now;
            m_FluxHead = (m_FluxHead + 1) % k_History;

            int n = 0;
            double sum = 0, sumSq = 0;
            for (int i = 0; i < k_History; i++)
            {
                if (now - m_FluxTime[i] > m_BeatHistorySeconds || m_FluxTime[i] <= 0f)
                    continue;

                n++;
                sum += m_FluxHistory[i];
                sumSq += m_FluxHistory[i] * (double)m_FluxHistory[i];
            }

            if (n < 8)
                return;

            float mean = (float)(sum / n);
            float std = Mathf.Sqrt(Mathf.Max(0f, (float)(sumSq / n) - mean * mean));
            float threshold = mean + m_BeatSensitivity * std;

            bool loudEnough = m_BassRaw > m_NoiseFloor * 2f;
            bool spaced = now - m_LastBeatTime >= 1f / m_MaxBeatsPerSecond;

            if (flux > threshold && flux > mean * 1.2f && loudEnough && spaced && Idle < 0.5f)
            {
                Beat = true;
                BeatCount++;
                BeatPulse = 1f;
                BeatStrength = Mathf.Clamp01((flux - threshold) / Mathf.Max(threshold, 1e-6f));
                m_LastBeatTime = now;
                OnBeat?.Invoke(BeatStrength);
            }
        }

        /// <summary>A band level by fractional index, interpolated, so a layer with more or fewer tubes than bands stays smooth.</summary>
        public float BandAt(float index)
        {
            if (Bands.Length == 0)
                return 0f;

            index = Mathf.Clamp(index, 0f, Bands.Length - 1);
            int lo = Mathf.FloorToInt(index);
            int hi = Mathf.Min(lo + 1, Bands.Length - 1);
            return Mathf.Lerp(Bands[lo], Bands[hi], index - lo);
        }
    }
}
