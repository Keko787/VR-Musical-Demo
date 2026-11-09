using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace VRShootingGallery.Pipes
{
    /// <summary>The parts of a track the machine listens for, each played by one of its instruments.</summary>
    public enum RigLane
    {
        Kick,
        Snare,
        Tom,
        HiHat,
        Crash,
        Bass,
        Mid,
        High,
    }

    /// <summary>A note found in the track: when, in which part, its pitch class for the tuned parts (−1 for drums), and how hard.</summary>
    public struct RigNote
    {
        public float Time;
        public RigLane Lane;
        public int PitchClass;
        public float Strength;

        /// <summary>Where it came from, for the record: the onset's rise in its band, dB; for a crash, how much louder the music got.</summary>
        public float Rise;

        /// <summary>How sure its pitch is, for the record: the strongest pitch class over the average one. 0 for a drum.</summary>
        public float Clarity;

        public bool Pitched => Lane >= RigLane.Bass;
    }

    /// <summary>A note <see cref="RigScore"/> found and then dropped, and why. Kept only when asked for, to see what it hears.</summary>
    public struct RigDrop
    {
        public RigNote Note;
        public string Why;
    }

    /// <summary>
    /// A track turned into notes before they are heard, so every ball can be thrown a second or two
    /// ahead and land exactly as its note plays. The audio is the clock and this is the score read
    /// off it: onsets in a handful of frequency bands — kick, snare, hi-hat, bass, mid, high — and
    /// for the tuned bands the pitch class that sounds at each onset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clip's samples are copied out a few seconds a frame on the main thread (Unity's audio
    /// API is not thread-safe) into one mono buffer at half the clip's rate, and a worker thread
    /// analyses them as they arrive and publishes notes in time order. It runs tens of times faster
    /// than the track plays, so after the first moments it is far ahead of the music; a note found
    /// too late to throw on time just enters part way along its arc. The buffer is dropped when the
    /// analysis is done; the notes, a few thousand small structs, are kept.
    /// </para>
    /// <para>
    /// Onsets: each band's energy every five milliseconds in decibels, its rise over the last
    /// fifteen, and peaks of that rise that stand clear of their neighbourhood and of the band's
    /// recent average — a standard energy-flux onset detector, one per band. Pitch: a Goertzel
    /// filter at every semitone in the band's range just after the onset, summed into a chroma, the
    /// strongest of which is the pitch class; an onset with no clear pitch (a snare's noise in the
    /// melody band) is dropped rather than given a random note.
    /// </para>
    /// <para>
    /// Only clips that hand back their samples work: Decompress On Load, the default for a dropped-
    /// in MP3. A streamed or compressed-in-memory clip sets <see cref="Failed"/>.
    /// </para>
    /// <para>
    /// Every note records the rise it came from and, if tuned, how sure its pitch is. Made to keep
    /// them, a score also keeps the notes it finds and then drops, with why — no clear pitch, too weak,
    /// a hi-hat that is a kick's or a tom's spill, a high note alone, wires with no drum body — so the
    /// pipe rig's notes export can show what it hears.
    /// </para>
    /// </remarks>
    public class RigScore
    {
        /// <summary>Seconds per analysis hop.</summary>
        public const float HopSeconds = 0.005f;

        /// <summary>
        /// Seconds the worker holds notes back before publishing them, so the notes every band finds —
        /// a crash is only decided a second after its hit — can be published in order.
        /// </summary>
        const float k_Hold = 1.3f;

        readonly AudioClip m_Clip;
        readonly float m_Length;
        float[] m_Buffer = new float[0];
        float[] m_Pcm;
        int m_Channels;
        int m_Factor;
        int m_TotalFrames;
        int m_ReadFrames;
        int m_Written;                               // samples of m_Pcm filled in; the worker reads up to here
        volatile bool m_AllRead;

        Thread m_Thread;
        volatile bool m_Cancel;
        volatile bool m_Done;
        volatile bool m_Failed;
        volatile float m_Analysed;
        readonly AutoResetEvent m_Signal = new(false);
        readonly object m_Lock = new();
        readonly List<RigNote> m_Notes = new();
        readonly bool m_KeepDropped;
        readonly List<RigDrop> m_Dropped = new();

        public AudioClip Clip => m_Clip;
        public bool Done => m_Done;
        public bool Failed => m_Failed;

        /// <summary>Why it failed, for the status line.</summary>
        public string Error { get; private set; }

        /// <summary>Seconds of the track analysed so far.</summary>
        public float Analysed => m_Analysed;

        public float Progress => m_Length > 0f ? Mathf.Clamp01(m_Analysed / m_Length) : 0f;

        public int NoteCount
        {
            get
            {
                lock (m_Lock)
                    return m_Notes.Count;
            }
        }

        /// <summary>
        /// Starts on <paramref name="clip"/>: the reading is done by <see cref="Step"/>, the analysis by
        /// a worker. One score per clip; a new clip gets a new score. With <paramref name="keepDropped"/>
        /// the notes it finds and then drops are kept too, with why, for <see cref="CopyDropped"/>.
        /// </summary>
        public RigScore(AudioClip clip, bool keepDropped = false)
        {
            m_Clip = clip;
            m_KeepDropped = keepDropped;
            m_Length = clip != null ? clip.length : 0f;

            if (clip == null || clip.loadType == AudioClipLoadType.Streaming)
            {
                Fail(clip == null ? "no clip" : "the clip streams; set its Load Type to Decompress On Load");
                return;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            m_Channels = Mathf.Max(1, clip.channels);
            m_TotalFrames = clip.samples;

            // Half rate keeps everything up to the hi-hats' band, at half the memory and half the work.
            m_Factor = clip.frequency >= 32000 ? 2 : 1;
            m_Pcm = new float[m_TotalFrames / m_Factor + 1];

            var analysis = new Analysis(this, clip.frequency / (float)m_Factor);
            m_Thread = new Thread(analysis.Run) { IsBackground = true, Name = "Pipe Rig score", Priority = System.Threading.ThreadPriority.BelowNormal };
            m_Thread.Start();
        }

        /// <summary>
        /// Copies up to <paramref name="seconds"/> of the clip for the worker. Call once a frame until
        /// it has all been read; a call before the clip has finished loading does nothing.
        /// </summary>
        public void Step(float seconds)
        {
            if (m_Failed || m_AllRead || m_Clip == null || m_Pcm == null)
                return;

            if (m_Clip.loadState == AudioDataLoadState.Failed)
            {
                Fail("the clip failed to load");
                return;
            }

            if (m_Clip.loadState != AudioDataLoadState.Loaded)
                return;

            int frames = Mathf.Min(Mathf.Max(m_Factor, Mathf.RoundToInt(m_Clip.frequency * seconds)), m_TotalFrames - m_ReadFrames);
            if (frames < m_Factor)
            {
                FinishReading();
                return;
            }

            frames -= frames % m_Factor;
            int length = frames * m_Channels;
            if (m_Buffer.Length != length)
                m_Buffer = new float[length];

            if (!m_Clip.GetData(m_Buffer, m_ReadFrames))
            {
                Fail("its samples cannot be read; set its Load Type to Decompress On Load");
                return;
            }

            // A mono mix, with each pair of frames averaged into one sample: the simplest low-pass
            // that will do ahead of halving the rate, since the top octave only matters as energy.
            int written = m_Written;
            float scale = 1f / (m_Channels * m_Factor);
            for (int f = 0; f < frames; f += m_Factor)
            {
                float sum = 0f;
                int end = (f + m_Factor) * m_Channels;
                for (int i = f * m_Channels; i < end; i++)
                    sum += m_Buffer[i];
                if (written < m_Pcm.Length)
                    m_Pcm[written++] = sum * scale;
            }

            Volatile.Write(ref m_Written, written);
            m_ReadFrames += frames;
            if (m_ReadFrames >= m_TotalFrames - (m_Factor - 1))
                FinishReading();
            else
                m_Signal.Set();
        }

        void FinishReading()
        {
            m_AllRead = true;
            m_Signal.Set();
        }

        /// <summary>
        /// Appends the notes published since <paramref name="from"/> to <paramref name="into"/> and
        /// returns how many there are now, the <paramref name="from"/> for next time.
        /// </summary>
        public int CopyNotes(List<RigNote> into, int from)
        {
            lock (m_Lock)
            {
                for (int i = Mathf.Max(0, from); i < m_Notes.Count; i++)
                    into.Add(m_Notes[i]);
                return m_Notes.Count;
            }
        }

        /// <summary>Appends the notes dropped so far, with why, to <paramref name="into"/>: none unless the score was made to keep them.</summary>
        public void CopyDropped(List<RigDrop> into)
        {
            lock (m_Lock)
                into.AddRange(m_Dropped);
        }

        /// <summary>Stops the worker, for a track abandoned before it was read.</summary>
        public void Cancel()
        {
            m_Cancel = true;
            m_Signal.Set();
            m_Thread = null;
            m_Pcm = null;
        }

        void Fail(string why)
        {
            Error = why;
            m_Failed = true;
            m_Pcm = null;
        }

        void Publish(List<RigNote> notes)
        {
            lock (m_Lock)
                m_Notes.AddRange(notes);
        }

        void Record(RigNote note, string why)
        {
            lock (m_Lock)
                m_Dropped.Add(new RigDrop { Note = note, Why = why });
        }

        // ================================================================== the worker

        /// <summary>One frequency band's detector settings.</summary>
        readonly struct BandSpec
        {
            public readonly float Low, High;
            public readonly bool Picks;

            /// <summary>Least seconds between onsets.</summary>
            public readonly float MinGap;

            /// <summary>dB a rise must stand above its neighbourhood's average, and the least rise at all.</summary>
            public readonly float Delta, Least;

            /// <summary>Hops the rise is measured over: longer for a band whose instruments start softly.</summary>
            public readonly int Lag;

            /// <summary>
            /// dB under the band's recent loudest that an onset must reach. A rise is measured in dB, so a
            /// little spill from a neighbouring band into a quiet one is a big rise; it is not a loud one.
            /// </summary>
            public readonly float Reach;

            /// <summary>Fourth-order edges instead of second: the tuned bands sit next to each other and must not hear each other's notes.</summary>
            public readonly bool Sharp;

            public BandSpec(float low, float high, bool picks, float minGap = 0.15f, float delta = 2.5f, float least = 4f, int lag = 3,
                float reach = 16f, bool sharp = false)
            {
                Low = low;
                High = high;
                Picks = picks;
                MinGap = minGap;
                Delta = delta;
                Least = least;
                Lag = lag;
                Reach = reach;
                Sharp = sharp;
            }
        }

        // Band order: the lanes below index into it.
        const int k_Kick = 0, k_Body = 1, k_Crack = 2, k_Hat = 3, k_Bass = 4, k_Mid = 5, k_High = 6, k_Broad = 7;

        static readonly BandSpec[] k_Bands =
        {
            new(35f, 110f, true, 0.2f, 3f, 4.5f, 3, 15f),            // kick
            new(150f, 400f, false),                                   // snare and tom body: tells a tom from a snare
            new(1800f, 6000f, true, 0.2f, 3f, 4.5f, 3, 15f),          // snare wires
            new(7000f, 10500f, true, 0.15f, 2.5f, 4f, 2, 18f),        // hi-hat
            new(80f, 220f, true, 0.14f, 2.5f, 4f, 6, 15f, true),      // bass: under the mallets' range
            new(220f, 700f, true, 0.12f, 2.5f, 4f, 4, 16f, true),     // mid: the mallets' range
            new(700f, 2600f, true, 0.13f, 2f, 3.5f, 3, 20f, true),    // high: the strings' range
            new(30f, 0f, false),                                      // everything: the drops that bring a crash
        };

        /// <summary>MIDI range searched for each tuned lane's pitch, and the Goertzel window length.</summary>
        static readonly (int Low, int High, int Window)[] k_PitchRange =
        {
            (36, 64, 4096),   // bass
            (48, 77, 2048),   // mid
            (72, 98, 1024),   // high
        };

        struct Biquad
        {
            float m_B0, m_B1, m_B2, m_A1, m_A2, m_Z1, m_Z2;

            public static Biquad Pass(bool high, float frequency, float rate)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(frequency, 10f, rate * 0.45f) / rate;
                float cos = Mathf.Cos(w);
                float alpha = Mathf.Sin(w) / (2f * 0.7071f);
                float a0 = 1f + alpha;
                float b1 = high ? -(1f + cos) : 1f - cos;
                float b0 = high ? (1f + cos) * 0.5f : (1f - cos) * 0.5f;
                return new Biquad
                {
                    m_B0 = b0 / a0,
                    m_B1 = b1 / a0,
                    m_B2 = b0 / a0,
                    m_A1 = -2f * cos / a0,
                    m_A2 = (1f - alpha) / a0,
                };
            }

            public float Process(float x)
            {
                float y = m_B0 * x + m_Z1;
                m_Z1 = m_B1 * x - m_A1 * y + m_Z2;
                m_Z2 = m_B2 * x - m_A2 * y;
                return y;
            }
        }

        sealed class Band
        {
            public BandSpec Spec;
            public Biquad HighPass, LowPass, HighPass2, LowPass2;
            public bool HasLowPass;
            public double Sum;
            public readonly List<float> Level = new();       // dB per hop
            public readonly List<float> Smooth = new();
            public readonly List<float> Rise = new();        // the onset function
            public float Peak = -120f;                        // running loudest, which gates out the band's silences
            public int LastOnset = -100000;
        }

        struct Pending
        {
            public RigNote Note;
            public int PitchStart;                            // first sample of the pitch window; −1 once resolved or not needed
            public int PitchLane;
        }

        /// <summary>The worker's state: the filters, the bands' histories and the notes not yet published.</summary>
        sealed class Analysis
        {
            const int k_Before = 6;                  // hops a peak must top behind it
            const int k_After = 8;                   // and ahead of it, which is how far behind the stream the picking runs
            const int k_Average = 60;                // hops of history the threshold averages
            const float k_Gate = 40f;                // dB under the band's loudest below which it is silence
            const float k_Tonal = 2.2f;              // a pitch's chroma over the mean chroma for it to count as a note
            const float k_Together = 0.025f;         // seconds within which hits in two parts are one moment
            const int k_KickHistory = 64;            // a kick has to be in the stronger half of the track's last this many
            const int k_KickWarmUp = 16;             // kicks heard before their floor is used; until then, k_KickStrength
            const float k_KickStrength = 0.9f;       // a kick weaker than this is more often another part's thump
            const float k_TunedStrength = 0.5f;      // a tuned note weaker than this, more often a stray than a note

            readonly RigScore m_Score;
            readonly float m_Rate;
            readonly int m_Hop;
            readonly Band[] m_Bands;
            readonly List<Pending> m_Pending = new();
            readonly List<RigNote> m_Ready = new();
            readonly Dictionary<int, float[]> m_Windows = new();
            readonly float[] m_Chroma = new float[12];

            int m_Read;
            int m_InHop;
            int m_Hops;
            double m_BroadSum;
            readonly List<float> m_LongTerm = new();
            readonly Queue<int> m_CrashCandidates = new();
            int m_LastCandidate = -100000;
            int m_LastCrash = -100000;
            readonly float[] m_Kicks = new float[k_KickHistory];      // the last kicks' strengths, a ring
            readonly float[] m_KicksSorted = new float[k_KickHistory];
            int m_KicksHeard;

            public Analysis(RigScore score, float rate)
            {
                m_Score = score;
                m_Rate = rate;
                m_Hop = Mathf.Max(1, Mathf.RoundToInt(rate * HopSeconds));
                m_Bands = new Band[k_Bands.Length];
                for (int b = 0; b < k_Bands.Length; b++)
                {
                    var spec = k_Bands[b];
                    m_Bands[b] = new Band
                    {
                        Spec = spec,
                        HighPass = Biquad.Pass(true, spec.Low, rate),
                        LowPass = Biquad.Pass(false, spec.High > 0f ? spec.High : rate * 0.45f, rate),
                        HighPass2 = Biquad.Pass(true, spec.Low, rate),
                        LowPass2 = Biquad.Pass(false, spec.High > 0f ? spec.High : rate * 0.45f, rate),
                        HasLowPass = spec.High > 0f && spec.High < rate * 0.45f,
                    };
                }
            }

            float HopTime(int hop) => hop * m_Hop / m_Rate;

            public void Run()
            {
                try
                {
                    while (!m_Score.m_Cancel)
                    {
                        int available = Volatile.Read(ref m_Score.m_Written);
                        var pcm = m_Score.m_Pcm;
                        if (pcm == null)
                            return;

                        if (available - m_Read >= m_Hop)
                        {
                            Consume(pcm, available);
                            Resolve(pcm, available, false);
                            Release(HopTime(m_Hops) - k_Hold);
                            m_Score.m_Analysed = HopTime(m_Hops);
                            continue;
                        }

                        if (m_Score.m_AllRead)
                        {
                            Resolve(pcm, available, true);
                            Release(float.PositiveInfinity);
                            m_Score.m_Analysed = Mathf.Max(m_Score.m_Length, HopTime(m_Hops));
                            m_Score.m_Pcm = null;
                            m_Score.m_Done = true;
                            return;
                        }

                        m_Score.m_Signal.WaitOne(30);
                    }
                }
                catch (Exception e)
                {
                    m_Score.Error = e.Message;
                    m_Score.m_Failed = true;
                }
            }

            /// <summary>Filters every new sample into the bands, closing a hop every few milliseconds.</summary>
            void Consume(float[] pcm, int available)
            {
                for (; m_Read < available; m_Read++)
                {
                    float x = pcm[m_Read];
                    for (int b = 0; b < m_Bands.Length; b++)
                    {
                        var band = m_Bands[b];
                        float y = band.HighPass.Process(x);
                        if (band.HasLowPass)
                            y = band.LowPass.Process(y);
                        if (band.Spec.Sharp)
                            y = band.LowPass2.Process(band.HighPass2.Process(y));
                        band.Sum += y * y;
                    }

                    if (++m_InHop >= m_Hop)
                    {
                        m_InHop = 0;
                        CloseHop();
                    }
                }
            }

            void CloseHop()
            {
                int n = m_Hops++;
                for (int b = 0; b < m_Bands.Length; b++)
                {
                    var band = m_Bands[b];
                    float level = 10f * Mathf.Log10((float)(band.Sum / m_Hop) + 1e-12f);
                    band.Sum = 0;
                    band.Level.Add(level);
                    band.Peak = Mathf.Max(level, band.Peak - 6f * HopSeconds / 4f);  // loses 6 dB in four seconds

                    int count = band.Level.Count;
                    float smooth = count >= 3 ? (level + band.Level[count - 2] + band.Level[count - 3]) / 3f : level;
                    band.Smooth.Add(smooth);

                    int lag = band.Spec.Lag;
                    float rise = 0f;
                    if (count > lag && level > band.Peak - k_Gate)
                        rise = Mathf.Max(0f, smooth - band.Smooth[count - 1 - lag]);
                    band.Rise.Add(rise);
                }

                // A second's running loudness, for spotting a drop: the dB of its mean power. The mean of its
                // hops' dB would let a few near-silent hops drag a second far down while the music is no quieter.
                var broad = m_Bands[k_Broad].Level;
                m_BroadSum += Power(broad[n]);
                int window = Mathf.RoundToInt(1f / HopSeconds);
                if (n >= window)
                    m_BroadSum -= Power(broad[n - window]);
                m_LongTerm.Add(10f * Mathf.Log10((float)(Math.Max(0.0, m_BroadSum) / Mathf.Min(n + 1, window)) + 1e-12f));

                int decide = n - k_After;
                if (decide >= k_Average)
                    Pick(decide);

                // A crash candidate is judged once the second after it is in.
                while (m_CrashCandidates.Count > 0 && m_CrashCandidates.Peek() + window <= n)
                    JudgeCrash(m_CrashCandidates.Dequeue(), n);
            }

            /// <summary>A hop's mean power from its level in dB.</summary>
            static double Power(float level) => Math.Pow(10.0, level / 10.0);

            /// <summary>Decides whether hop <paramref name="m"/> is an onset in each band, now that the hops after it are in.</summary>
            void Pick(int m)
            {
                float kick = Onset(k_Kick, m);
                if (kick > 0f)
                    Add(m, RigLane.Kick, kick, -1, k_Bands[k_Kick].Lag);

                float crack = Onset(k_Crack, m);
                if (crack > 0f)
                {
                    // A drum has a body as well as wires; a hi-hat's sizzle in the same band does not.
                    // Then a tom is mostly body and a snare mostly wires.
                    float body = 0f;
                    var bodyRise = m_Bands[k_Body].Rise;
                    for (int i = Mathf.Max(0, m - 2); i <= m + 2 && i < bodyRise.Count; i++)
                        body = Mathf.Max(body, bodyRise[i]);
                    if (body >= 2.5f)
                    {
                        Add(m, body > crack + 4f ? RigLane.Tom : RigLane.Snare, crack, -1, k_Bands[k_Crack].Lag);
                    }
                    else
                    {
                        if (m_Score.m_KeepDropped)
                            m_Score.Record(Note(m, RigLane.Snare, crack, k_Bands[k_Crack].Lag, float.NaN), "wires but no drum body");
                        crack = 0f;
                    }
                }

                if ((kick > 0f || crack > 0f) && m - m_LastCandidate >= Mathf.RoundToInt(1f / HopSeconds))
                {
                    m_LastCandidate = m;
                    m_CrashCandidates.Enqueue(m);
                }

                float hat = Onset(k_Hat, m);
                if (hat > 0f)
                    Add(m, RigLane.HiHat, hat, -1, k_Bands[k_Hat].Lag);

                float bass = Onset(k_Bass, m);
                if (bass > 0f)
                    Add(m, RigLane.Bass, bass, 0, k_Bands[k_Bass].Lag);

                float mid = Onset(k_Mid, m);
                if (mid > 0f)
                    Add(m, RigLane.Mid, mid, 1, k_Bands[k_Mid].Lag);

                float high = Onset(k_High, m);
                if (high > 0f)
                    Add(m, RigLane.High, high, 2, k_Bands[k_High].Lag);
            }

            /// <summary>The rise at hop <paramref name="m"/> if it is an onset in band <paramref name="b"/>, else 0.</summary>
            float Onset(int b, int m)
            {
                var band = m_Bands[b];
                var spec = band.Spec;
                var rise = band.Rise;
                float r = rise[m];
                if (r < spec.Least || m - band.LastOnset < Mathf.RoundToInt(spec.MinGap / HopSeconds))
                    return 0f;

                for (int i = m - k_Before; i <= m + k_After; i++)
                    if (i != m && rise[i] > r || (i > m && rise[i] == r))
                        return 0f;

                float sum = 0f;
                for (int i = m - k_Average; i <= m + k_After; i++)
                    sum += rise[i];
                float mean = sum / (k_Average + k_After + 1);
                if (r < mean * 1.5f + spec.Delta)
                    return 0f;

                float loudest = float.NegativeInfinity;
                for (int i = m; i <= m + 3; i++)
                    loudest = Mathf.Max(loudest, band.Level[i]);
                if (loudest < band.Peak - spec.Reach)
                    return 0f;

                band.LastOnset = m;
                return r;
            }

            /// <summary>
            /// A crash on a kick or snare that lands as the music jumps up — a drop, or a chorus coming
            /// in: the second after the hit is well louder than the quietest second of the three before it,
            /// each by its mean power. Its strength is how big that jump is, so the biggest drops can be told
            /// from the rest. A jump up from silence is the music starting, not a drop: it measured 70–105 dB
            /// at the top of nearly every track, rang the gong there and made every real drop look small
            /// beside it.
            /// </summary>
            void JudgeCrash(int m, int now)
            {
                if (m - m_LastCrash < Mathf.RoundToInt(4f / HopSeconds))
                    return;

                int back = Mathf.RoundToInt(3f / HopSeconds);
                float quietest = float.PositiveInfinity;
                for (int i = Mathf.Max(0, m - back); i < m; i++)
                    quietest = Mathf.Min(quietest, m_LongTerm[i]);
                if (quietest < m_Bands[k_Broad].Peak - k_Gate)
                    return;

                // m_LongTerm[i] is the second up to i, so the second after the hit is read at m + 1 s.
                int after = Mathf.Min(now, m + Mathf.RoundToInt(1f / HopSeconds));
                float jump = m_LongTerm[after] - quietest;
                if (float.IsInfinity(quietest) || jump < 6f)
                    return;

                // Given as a rise of jump − 2 dB: the least drop, 6 dB, is a strength of 0.35, and 12.5 dB or more is 1.
                m_LastCrash = m;
                Add(m, RigLane.Crash, jump - 2f, -1, k_Bands[k_Kick].Lag, jump);
            }

            /// <summary>
            /// A note found at hop <paramref name="m"/>, its strength from <paramref name="rise"/>; recorded as
            /// coming from that rise, or from <paramref name="measured"/> when it is something else (a crash's jump).
            /// </summary>
            void Add(int m, RigLane lane, float rise, int pitchLane, int lag, float measured = float.NaN)
            {
                // The pitch is read from a window starting just after the attack's thump.
                int start = pitchLane >= 0 ? (m + 1) * m_Hop : -1;
                m_Pending.Add(new Pending { Note = Note(m, lane, rise, lag, measured), PitchStart = start, PitchLane = pitchLane });
            }

            RigNote Note(int m, RigLane lane, float rise, int lag, float measured)
            {
                // The rise is measured over the lag before m and smoothed over three hops, so the attack
                // itself is about half the lag earlier.
                return new RigNote
                {
                    Time = Mathf.Max(0f, HopTime(m - 1 - lag / 2)),
                    Lane = lane,
                    PitchClass = -1,
                    Strength = Mathf.Clamp01(0.35f + (rise - 4f) / 10f),
                    Rise = float.IsNaN(measured) ? rise : measured,
                };
            }

            /// <summary>Reads the pitch of every pending tuned note whose window has come in.</summary>
            void Resolve(float[] pcm, int available, bool final)
            {
                for (int i = 0; i < m_Pending.Count; i++)
                {
                    var pending = m_Pending[i];
                    if (pending.PitchStart < 0)
                        continue;

                    var range = k_PitchRange[pending.PitchLane];
                    int start = pending.PitchStart;
                    int length = range.Window;
                    if (start + length > available)
                    {
                        if (!final)
                            continue;
                        length = Mathf.Max(0, available - start);
                    }

                    pending.Note.PitchClass = length >= 256 ? PitchClass(pcm, start, length, range.Low, range.High, out pending.Note.Clarity) : -2;
                    pending.PitchStart = -1;
                    m_Pending[i] = pending;
                }
            }

            /// <summary>The strongest pitch class in the window, by a chroma of Goertzel powers; −2 if nothing in it is clearly pitched.</summary>
            int PitchClass(float[] pcm, int start, int length, int lowMidi, int highMidi, out float clarity)
            {
                if (!m_Windows.TryGetValue(length, out var window))
                {
                    window = new float[length];
                    for (int i = 0; i < length; i++)
                        window[i] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (length - 1));
                    m_Windows[length] = window;
                }

                Array.Clear(m_Chroma, 0, 12);
                for (int midi = lowMidi; midi <= highMidi; midi++)
                {
                    float frequency = RigTarget.Frequency(midi);
                    if (frequency >= m_Rate * 0.45f)
                        break;

                    float coeff = 2f * Mathf.Cos(2f * Mathf.PI * frequency / m_Rate);
                    float s1 = 0f, s2 = 0f;
                    for (int i = 0; i < length; i++)
                    {
                        float s0 = pcm[start + i] * window[i] + coeff * s1 - s2;
                        s2 = s1;
                        s1 = s0;
                    }

                    m_Chroma[midi % 12] += s1 * s1 + s2 * s2 - coeff * s1 * s2;
                }

                int best = 0;
                float total = 0f;
                for (int pc = 0; pc < 12; pc++)
                {
                    total += m_Chroma[pc];
                    if (m_Chroma[pc] > m_Chroma[best])
                        best = pc;
                }

                clarity = total > 0f ? m_Chroma[best] / (total / 12f) : 0f;
                return clarity >= k_Tonal ? best : -2;
            }

            /// <summary>
            /// Publishes, in time order, every resolved note older than <paramref name="before"/> that
            /// <see cref="Dropped"/> keeps.
            /// </summary>
            void Release(float before)
            {
                if (m_Pending.Count == 0)
                    return;

                m_Pending.Sort((a, b) => a.Note.Time.CompareTo(b.Note.Time));
                m_Ready.Clear();
                int taken = 0;
                for (; taken < m_Pending.Count; taken++)
                {
                    var pending = m_Pending[taken];
                    if (pending.Note.Time >= before || pending.PitchStart >= 0)
                        break;

                    string why = Dropped(taken);
                    if (why != null)
                    {
                        if (m_Score.m_KeepDropped)
                            m_Score.Record(pending.Note, why);
                        continue;
                    }

                    m_Ready.Add(pending.Note);
                }

                if (taken == 0)
                    return;

                m_Pending.RemoveRange(0, taken);
                if (m_Ready.Count > 0)
                    m_Score.Publish(m_Ready);
            }

            /// <summary>
            /// Why pending note <paramref name="i"/> is dropped, or null if it is played. The rules were set
            /// by reading the notes against the music on a beat grid (see the pipe rig doc), and they turn
            /// on what lands with a note:
            /// <list type="bullet">
            /// <item>A kick has to be among the track's stronger ones (<see cref="StrongKick"/>): the weaker
            /// half are more often another part's thump, off the beat.</item>
            /// <item>A bass or mid note has to be at least fairly strong, and is played whatever lands with
            /// it. Dropping the ones that land with a drum, against spill, dropped most of the notes on the
            /// beat, since that is where the bass and the chords play with the drums.</item>
            /// <item>A hi-hat with a kick or a tom is their spill and is dropped; one with a snare is played.</item>
            /// <item>A high note is played only if it lands with a drum or a mid note, and is fairly strong:
            /// on its own, the top band follows nothing in time.</item>
            /// </list>
            /// </summary>
            string Dropped(int i)
            {
                var note = m_Pending[i].Note;
                if (note.Pitched && note.PitchClass < 0)
                    return "no clear pitch";

                RigLane other;
                switch (note.Lane)
                {
                    case RigLane.Kick:
                        return StrongKick(note.Strength) ? null : "weak kick";
                    case RigLane.HiHat:
                        if (!Together(i, out other) || other == RigLane.Snare)
                            return null;
                        return other == RigLane.Kick ? "spill from Kick" : "spill from Tom";
                    case RigLane.Bass:
                    case RigLane.Mid:
                        return note.Strength >= k_TunedStrength ? null : "weak";
                    case RigLane.High:
                        if (!Together(i, out other) || other is not (RigLane.Kick or RigLane.Snare or RigLane.Mid))
                            return "alone";
                        return note.Strength >= k_TunedStrength ? null : "weak";
                    default:
                        return null;
                }
            }

            /// <summary>
            /// Whether a kick of <paramref name="strength"/> is in the stronger half of the track's last
            /// <see cref="k_KickHistory"/> kicks, and remembers it among them; called once a kick, in time
            /// order. A fixed floor starved the tracks whose kick sits soft in the mix, down to a kick every
            /// few seconds; one from the track's own kicks keeps as many on the beat without that.
            /// </summary>
            bool StrongKick(float strength)
            {
                int heard = Mathf.Min(m_KicksHeard, k_KickHistory);
                float floor = k_KickStrength;
                if (heard >= k_KickWarmUp)
                {
                    Array.Copy(m_Kicks, m_KicksSorted, heard);
                    Array.Sort(m_KicksSorted, 0, heard);
                    float middle = (heard - 1) * 0.5f;
                    int below = (int)middle;
                    floor = Mathf.Lerp(m_KicksSorted[below], m_KicksSorted[Mathf.Min(below + 1, heard - 1)], middle - below);
                }

                m_Kicks[m_KicksHeard % k_KickHistory] = strength;
                m_KicksHeard++;
                return strength >= floor;
            }

            /// <summary>
            /// Whether a hit in another part, of a kind <paramref name="i"/>'s band also hears, lands within
            /// <see cref="k_Together"/> of pending note <paramref name="i"/>, and which part's.
            /// </summary>
            bool Together(int i, out RigLane with)
            {
                var note = m_Pending[i].Note;
                for (int step = -1; step <= 1; step += 2)
                {
                    for (int j = i + step; j >= 0 && j < m_Pending.Count; j += step)
                    {
                        var other = m_Pending[j].Note;
                        if (Mathf.Abs(other.Time - note.Time) > k_Together)
                            break;
                        if (Spills(other.Lane, note.Lane))
                        {
                            with = other.Lane;
                            return true;
                        }
                    }
                }

                with = note.Lane;
                return false;
            }

            /// <summary>
            /// Whether a hit in <paramref name="from"/> also sounds in <paramref name="lane"/>'s band: a
            /// kick's thump in the bass, its click and a snare's wires in the hats and the top, a mallet's
            /// overtones in the top.
            /// </summary>
            static bool Spills(RigLane from, RigLane lane) => lane switch
            {
                RigLane.HiHat => from is RigLane.Kick or RigLane.Snare or RigLane.Tom,
                RigLane.Bass => from is RigLane.Kick or RigLane.Snare or RigLane.Tom,
                RigLane.Mid => from is RigLane.Snare or RigLane.Tom,
                RigLane.High => from is RigLane.Kick or RigLane.Snare or RigLane.Tom or RigLane.HiHat or RigLane.Mid,
                _ => false,
            };
        }
    }
}
