using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VRShootingGallery.Pipes;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// A track from the music folder read into the orchestra's parts, for the visualize mode. These are
    /// finished mixes with no MIDI and no stems, so everything the animals read is heard off the mix:
    /// the notes, the beat, the chords and each part's loudness.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The notes</b> are the pipe rig's (<see cref="RigScore"/>), which already turns these tracks into
    /// onsets per band — kick, snare, tom, hi-hat, a crash where the music jumps up, and bass, mid and high
    /// notes with their pitch class. Each goes to the animal whose part it is nearest: kicks to the
    /// elephant, snares and toms to the gorilla, hats to the woodpecker, bass notes to the bullfrog (the
    /// strongest of them to the whale as well), mid notes to the octopus, high notes to the starlings and
    /// the parrot. A crash is the elephant's trumpet, the woodpecker's open hat and a burst of fireflies.
    /// Pitches are pitch classes only, so each part sings in one octave of its own.
    /// </para>
    /// <para>
    /// <b>The beat</b> is the length those onsets repeat at, at one, two, three and four beats, weighed toward
    /// the tempos songs are usually written at, then the grid they line up on exactly. One tempo for the whole
    /// track: right for produced music, and only slowly wrong for a live one, since it moves the animals'
    /// bobbing and not their hits. On eleven of the folder's tracks it found the published tempo on the
    /// nine that have one known (97, 90, 100, 100, 102, 120, 100 BPM and so on).
    /// </para>
    /// <para>
    /// <b>The chords</b> are a chroma per beat matched against the major and minor triads, smoothed so a
    /// chord holds until the harmony really moves. The jellyfish's pads play every chord; the wolves'
    /// choir sings them only in the loud sections, so they howl in the choruses.
    /// </para>
    /// <para>
    /// <b>The loudness</b> of each part is <see cref="StemAnalysis"/> run on the band of the mix that part
    /// lives in — under 150 Hz for the kick, 300 Hz to 3.4 kHz for the voice, above 6 kHz for the hats —
    /// so a part's signal rises when its band does, not with the whole song. Note velocities come from how
    /// hard a note is against the rest of its part, and how loud the song is there.
    /// </para>
    /// <para>
    /// The work is split like the score's: the samples are copied out a slice a frame on the main thread
    /// (<see cref="Step"/>; Unity's audio API is not thread-safe), the score's worker finds the notes as they
    /// come in, and the rest runs on a task once both are done. A four-minute track takes a few seconds.
    /// Only clips that hand back their samples work: Decompress On Load, a dropped-in MP3's default.
    /// </para>
    /// </remarks>
    public sealed class MixedSong
    {
        /// <summary>Seconds of the clip copied out per <see cref="Step"/>: a few milliseconds of the main thread.</summary>
        const float k_SliceSeconds = 15f;

        readonly AudioClip m_Clip;
        readonly string m_Title;
        readonly RigScore m_Score;
        readonly float m_StartedAt;

        float[] m_Mono;                               // the mix, mono, at half the clip's rate
        float[] m_Slice = Array.Empty<float>();
        int m_Channels;
        int m_Factor;
        float m_Rate;
        int m_TotalFrames;
        int m_ReadFrames;
        int m_Written;
        bool m_AllRead;
        Task<SongData> m_Arrange;

        public MixedSong(AudioClip clip)
        {
            m_Clip = clip;
            m_Title = TrackIndicator.TitleOf(clip);
            m_StartedAt = Time.realtimeSinceStartup;

            if (clip == null || clip.loadType == AudioClipLoadType.Streaming)
            {
                Fail(clip == null ? "no clip" : "the clip streams; set its Load Type to Decompress On Load");
                return;
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            m_Channels = Mathf.Max(1, clip.channels);
            m_TotalFrames = clip.samples;
            m_Factor = clip.frequency >= 32000 ? 2 : 1;
            m_Rate = clip.frequency / (float)m_Factor;
            m_Mono = new float[m_TotalFrames / m_Factor + 1];

            m_Score = new RigScore(clip);
            if (m_Score.Failed)
                Fail(m_Score.Error);
        }

        public AudioClip Clip => m_Clip;

        /// <summary>The song as the orchestra reads it, once it is ready; null until then.</summary>
        public SongData Result { get; private set; }

        public bool Done => Result != null;

        public bool Failed { get; private set; }

        public string Error { get; private set; }

        /// <summary>Seconds since reading began: how long a track took, for the log.</summary>
        public float Elapsed => Time.realtimeSinceStartup - m_StartedAt;

        /// <summary>
        /// Moves the reading along: copies a slice of the clip, feeds the score, and once both are through
        /// starts the arranging, then picks up its result. Call once a frame until <see cref="Done"/> or
        /// <see cref="Failed"/>.
        /// </summary>
        public void Step()
        {
            if (Failed || Done)
                return;

            if (!m_AllRead)
                Read();
            if (Failed)
                return;

            if (!m_Score.Done && !m_Score.Failed)
                m_Score.Step(k_SliceSeconds);
            if (m_Score.Failed)
            {
                Fail(m_Score.Error);
                return;
            }

            if (m_Arrange == null)
            {
                if (!m_AllRead || !m_Score.Done)
                    return;

                var notes = new List<RigNote>();
                m_Score.CopyNotes(notes, 0);
                var mono = m_Mono;
                int count = m_Written;
                float rate = m_Rate;
                string title = m_Title;
                double length = m_Clip.length;
                m_Arrange = Task.Run(() => Arrange(title, length, notes, mono, count, rate));
                return;
            }

            if (!m_Arrange.IsCompleted)
                return;

            if (m_Arrange.IsFaulted || m_Arrange.Result == null)
                Fail(m_Arrange.Exception?.InnerException?.Message ?? "nothing came of it");
            else
                Result = m_Arrange.Result;

            m_Arrange = null;
            m_Mono = null;
        }

        /// <summary>Stops reading, for a track given up before it was ready. A task already arranging finishes and is dropped.</summary>
        public void Cancel()
        {
            m_Score?.Cancel();
            m_Mono = null;
            Failed = true;
            Error ??= "cancelled";
        }

        void Fail(string why)
        {
            Error = why;
            Failed = true;
            m_Mono = null;
            m_Score?.Cancel();
        }

        /// <summary>Copies the next slice of the clip into the mono mix, each pair of frames averaged into one sample.</summary>
        void Read()
        {
            if (m_Clip.loadState == AudioDataLoadState.Failed)
            {
                Fail("the clip failed to load");
                return;
            }

            if (m_Clip.loadState != AudioDataLoadState.Loaded)
                return;

            int frames = Mathf.Min(Mathf.RoundToInt(m_Clip.frequency * k_SliceSeconds), m_TotalFrames - m_ReadFrames);
            frames -= frames % m_Factor;
            if (frames <= 0)
            {
                m_AllRead = true;
                return;
            }

            int length = frames * m_Channels;
            if (m_Slice.Length != length)
                m_Slice = new float[length];

            if (!m_Clip.GetData(m_Slice, m_ReadFrames))
            {
                Fail("its samples cannot be read; set its Load Type to Decompress On Load");
                return;
            }

            float scale = 1f / (m_Channels * m_Factor);
            for (int f = 0; f < frames; f += m_Factor)
            {
                float sum = 0f;
                int end = (f + m_Factor) * m_Channels;
                for (int i = f * m_Channels; i < end; i++)
                    sum += m_Slice[i];
                if (m_Written < m_Mono.Length)
                    m_Mono[m_Written++] = sum * scale;
            }

            m_ReadFrames += frames;
            if (m_ReadFrames >= m_TotalFrames - (m_Factor - 1))
                m_AllRead = true;
        }

        // ================================================================== arranging, on a worker

        /// <summary>How long a drum note lasts: only long enough to count as sounding.</summary>
        const double k_Hit = 0.1;

        /// <summary>Seconds within which a kick and a crash are one hit.</summary>
        const double k_Together = 0.03;

        /// <summary>The loudest a velocity gets from strength and loudness; 1 is kept for a crash, which is what makes the elephant trumpet.</summary>
        const float k_MostVelocity = 0.94f;

        static SongData Arrange(string title, double length, List<RigNote> found, float[] mono, int count, float rate)
        {
            var song = new SongData { Title = title, Length = length };

            Measure(song, mono, count, rate, out var loudness);

            var (bpm, firstBeat) = Beat(found, length);
            song.Bpm = bpm;
            song.FirstBeat = firstBeat;

            var chords = Chords(mono, count, rate, length, bpm, firstBeat, loudness);
            Score(song, found, chords, loudness);
            return song;
        }

        // ------------------------------------------------------------------ loudness

        /// <summary>
        /// Each part's continuous signals, from its own band of the mix; and the song's loudness over a
        /// couple of seconds as a rank from 0 (its quietest) to 1 (its loudest), which sets velocities and
        /// where the choir sings.
        /// </summary>
        static void Measure(SongData song, float[] mono, int count, float rate, out Func<double, float> loudness)
        {
            var mix = mono;
            if (mono.Length != count)
            {
                mix = new float[count];
                Array.Copy(mono, mix, count);
            }

            var band = new float[count];
            int sampleRate = Mathf.RoundToInt(rate);

            PartAnalysis InBand(float low, float high)
            {
                Filter(mix, band, low, high, rate);
                return StemAnalysis.Measure(band, 1, sampleRate);
            }

            var full = StemAnalysis.Measure(mix, 1, sampleRate);
            var middle = InBand(250f, 2000f);

            song.Analysis[(int)Part.Kick] = InBand(0f, 150f);
            song.Analysis[(int)Part.SubBass] = InBand(0f, 90f);
            song.Analysis[(int)Part.Bass] = InBand(50f, 250f);
            song.Analysis[(int)Part.Vocal] = InBand(300f, 3400f);
            song.Analysis[(int)Part.HiHat] = InBand(6000f, 0f);
            song.Analysis[(int)Part.Pads] = middle;
            song.Analysis[(int)Part.Keys] = middle;
            song.Analysis[(int)Part.Choir] = middle;
            song.Analysis[(int)Part.Texture] = full;

            // Two seconds of the whole mix's level about each moment, ranked against the rest of the song.
            int frames = full.Frames;
            int half = Mathf.RoundToInt(full.FrameRate);
            var sums = new double[frames + 1];
            for (int i = 0; i < frames; i++)
                sums[i + 1] = sums[i] + full.Sample(Band.Level, i / full.FrameRate);

            var smooth = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                int from = Math.Max(0, i - half), to = Math.Min(frames, i + half + 1);
                smooth[i] = (float)((sums[to] - sums[from]) / Math.Max(1, to - from));
            }

            var sorted = (float[])smooth.Clone();
            Array.Sort(sorted);
            float frameRate = full.FrameRate;
            loudness = time =>
            {
                int i = Mathf.Clamp((int)(time * frameRate), 0, smooth.Length - 1);
                if (sorted.Length == 0)
                    return 0.5f;

                int at = Array.BinarySearch(sorted, smooth[i]);
                if (at < 0)
                    at = ~at;
                return at / (float)Math.Max(1, sorted.Length - 1);
            };
        }

        /// <summary>
        /// <paramref name="from"/> through a second-order high-pass at <paramref name="low"/> and low-pass at
        /// <paramref name="high"/> into <paramref name="into"/>; 0 leaves that edge open.
        /// </summary>
        static void Filter(float[] from, float[] into, float low, float high, float rate)
        {
            var highPass = low > 0f ? Biquad.Pass(true, low, rate) : default;
            var lowPass = high > 0f ? Biquad.Pass(false, high, rate) : default;
            for (int i = 0; i < from.Length; i++)
            {
                float x = from[i];
                if (low > 0f)
                    x = highPass.Process(x);
                if (high > 0f)
                    x = lowPass.Process(x);
                into[i] = x;
            }
        }

        /// <summary>A second-order Butterworth section (the cookbook's), the pipe rig's own filter.</summary>
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

        // ------------------------------------------------------------------ the beat

        /// <summary>Frames a second of the onset train the beat is found in.</summary>
        const int k_BeatRate = 100;

        /// <summary>
        /// The tempo and where its grid falls, in two steps. First the beat's length: the onsets as a train
        /// of pulses, and the lag whose first four multiples the train repeats at best (its autocorrelation
        /// summed over them), weighed toward the tempos songs are mostly written at. A beat repeats at one,
        /// two, three and four beats, where a wrong length that still fits some onsets — two thirds of the
        /// beat, which matched a song's kicks and made 97 BPM read as 145 — repeats at only some of those.
        /// Twice or half the tempo can still win, which only makes the animals bob twice a beat or every
        /// other, still on it. Then the exact tempo and the phase, near that: on a candidate grid each onset is
        /// an arrow at its phase in the beat, and on the right grid they point the same way and add up.
        /// </summary>
        static (float bpm, double firstBeat) Beat(List<RigNote> notes, double length)
        {
            var times = new List<double>(notes.Count);
            var weights = new List<double>(notes.Count);
            foreach (var note in notes)
            {
                double weight = note.Lane switch
                {
                    RigLane.Kick => 1.0,
                    RigLane.Snare => 0.9,
                    RigLane.Tom => 0.5,
                    RigLane.Crash => 0.8,
                    RigLane.HiHat => 0.3,
                    RigLane.Bass => 0.4,
                    _ => 0.2,
                };
                times.Add(note.Time);
                weights.Add(weight * (0.5 + 0.5 * note.Strength));
            }

            if (times.Count < 16)
                return (120f, 0.0);

            // The train, each onset spread over its neighbouring frames for the detector's few milliseconds of jitter.
            int frames = (int)(length * k_BeatRate) + 2;
            var train = new double[frames];
            for (int i = 0; i < times.Count; i++)
            {
                int at = (int)Math.Round(times[i] * k_BeatRate);
                if (at < 1 || at >= frames - 1)
                    continue;
                train[at - 1] += 0.25 * weights[i];
                train[at] += 0.5 * weights[i];
                train[at + 1] += 0.25 * weights[i];
            }

            double mean = 0.0;
            foreach (double x in train)
                mean += x;
            mean /= frames;
            for (int i = 0; i < frames; i++)
                train[i] -= mean;

            // Beats from 200 down to 60 BPM, so lags out to four of the slowest.
            double shortest = 60.0 * k_BeatRate / 200.0, longest = 60.0 * k_BeatRate / 60.0;
            int most = Math.Min(frames - 1, (int)Math.Ceiling(longest * 4) + 1);
            var correlation = new double[most + 1];
            for (int lag = 0; lag <= most; lag++)
            {
                double sum = 0.0;
                for (int i = 0; i + lag < frames; i++)
                    sum += train[i] * train[i + lag];
                correlation[lag] = sum;
            }

            double At(double lag)
            {
                int i = (int)lag;
                if (i + 1 > most)
                    return 0.0;
                double f = lag - i;
                return correlation[i] * (1.0 - f) + correlation[i + 1] * f;
            }

            double bestBeat = 60.0 * k_BeatRate / 120.0, best = double.NegativeInfinity;
            for (double beat = shortest; beat <= longest; beat += 0.05)
            {
                double repeats = At(beat) + At(2 * beat) + At(3 * beat) + At(4 * beat);
                double score = repeats * Prior(60.0 * k_BeatRate / beat);
                if (score > best)
                {
                    best = score;
                    bestBeat = beat;
                }
            }

            // Within a couple of percent of that, the tempo the onsets line up on exactly, and its phase.
            double near = 60.0 * k_BeatRate / bestBeat, bestBpm = near, sharpest = -1.0;
            for (double bpm = near * 0.98; bpm <= near * 1.02; bpm += 0.005)
            {
                double coherence = Coherence(times, weights, bpm, out _);
                if (coherence > sharpest)
                {
                    sharpest = coherence;
                    bestBpm = bpm;
                }
            }

            Coherence(times, weights, bestBpm, out double phase);
            return ((float)bestBpm, phase * 60.0 / bestBpm);
        }

        /// <summary>How well the onsets line up on a grid at <paramref name="bpm"/>, 0 to 1, and the grid's phase, 0 to 1 of a beat.</summary>
        static double Coherence(List<double> times, List<double> weights, double bpm, out double phase)
        {
            double re = 0.0, im = 0.0, total = 0.0;
            double radiansPerSecond = 2.0 * Math.PI * bpm / 60.0;
            for (int i = 0; i < times.Count; i++)
            {
                double angle = radiansPerSecond * times[i];
                re += weights[i] * Math.Cos(angle);
                im += weights[i] * Math.Sin(angle);
                total += weights[i];
            }

            phase = Math.Atan2(im, re) / (2.0 * Math.PI);
            if (phase < 0.0)
                phase += 1.0;
            return total > 0.0 ? Math.Sqrt(re * re + im * im) / total : 0.0;
        }

        /// <summary>How likely a tempo is before listening: most songs sit near 115 BPM, within an octave or so.</summary>
        static double Prior(double bpm)
        {
            double octaves = Math.Log(bpm / 115.0, 2.0) / 0.7;
            return Math.Exp(-0.5 * octaves * octaves);
        }

        // ------------------------------------------------------------------ the chords

        /// <summary>A stretch of one chord: its root's pitch class and whether it is minor.</summary>
        readonly struct Chord
        {
            public readonly double Start, End;
            public readonly int Root;
            public readonly bool Minor;
            public readonly float Loudness;

            public Chord(double start, double end, int root, bool minor, float loudness)
            {
                Start = start;
                End = end;
                Root = root;
                Minor = minor;
                Loudness = loudness;
            }
        }

        /// <summary>Lowest and highest MIDI note the chroma listens to: E2 to B5, where chords are played.</summary>
        const int k_ChromaLow = 40, k_ChromaHigh = 83;

        /// <summary>The chroma's window, samples at about 11 kHz: a third of a second, enough to part two semitones at E2.</summary>
        const int k_ChromaWindow = 4096;

        const double k_ChromaHop = 0.125;

        /// <summary>What a beat with no clear chord scores as 'no chord', against a triad's match of 0 to 1.</summary>
        const float k_NoChord = 0.55f;

        /// <summary>What changing chord costs, so a chord holds through a passing note or a beat of drums.</summary>
        const float k_ChangeCost = 0.12f;

        /// <summary>
        /// The chords, one per beat and then joined: a chroma every eighth of a second (a Goertzel filter on
        /// every semitone from E2 to B5, folded into twelve pitch classes), averaged over each beat, matched
        /// against the twelve major and twelve minor triads, and then the best path through them that
        /// changes chord only when the next one fits clearly better (Viterbi, with a cost per change).
        /// </summary>
        static List<Chord> Chords(float[] mono, int count, float rate, double length, float bpm, double firstBeat,
            Func<double, float> loudness)
        {
            // About 11 kHz: chords sit under a kilohertz.
            int factor = Math.Max(1, (int)Math.Round(rate / 11025.0));
            float r = rate / factor;
            int n = count / factor;
            var x = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int k = 0; k < factor; k++)
                    sum += mono[i * factor + k];
                x[i] = sum / factor;
            }

            int hop = Math.Max(1, (int)(r * k_ChromaHop));
            int frames = n >= k_ChromaWindow ? (n - k_ChromaWindow) / hop + 1 : 0;
            var chroma = new float[frames][];
            var energy = new float[frames];

            var window = new float[k_ChromaWindow];
            for (int i = 0; i < window.Length; i++)
                window[i] = 0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * i / (window.Length - 1));

            int semitones = k_ChromaHigh - k_ChromaLow + 1;
            var coefficients = new float[semitones];
            for (int s = 0; s < semitones; s++)
                coefficients[s] = 2f * (float)Math.Cos(2.0 * Math.PI * 440.0 * Math.Pow(2.0, (k_ChromaLow + s - 69) / 12.0) / r);

            var windowed = new float[k_ChromaWindow];
            for (int f = 0; f < frames; f++)
            {
                int start = f * hop;
                for (int i = 0; i < k_ChromaWindow; i++)
                    windowed[i] = x[start + i] * window[i];

                var c = new float[12];
                for (int s = 0; s < semitones; s++)
                {
                    float coefficient = coefficients[s], s1 = 0f, s2 = 0f;
                    for (int i = 0; i < k_ChromaWindow; i++)
                    {
                        float s0 = windowed[i] + coefficient * s1 - s2;
                        s2 = s1;
                        s1 = s0;
                    }

                    float power = Math.Max(0f, s1 * s1 + s2 * s2 - coefficient * s1 * s2);
                    c[(k_ChromaLow + s) % 12] += (float)Math.Sqrt(power);
                    energy[f] += power;
                }

                chroma[f] = c;
            }

            // Beats to average over: the beat grid, from the first whole beat in the song.
            double period = 60.0 / Math.Max(30f, bpm);
            var edges = new List<double>();
            double edge = firstBeat - Math.Floor(firstBeat / period) * period;
            if (edge > 0.0)
                edges.Add(0.0);
            for (; edge < length; edge += period)
                edges.Add(edge);
            edges.Add(length);

            int beats = edges.Count - 1;
            var beatChroma = new float[beats][];
            var beatEnergy = new float[beats];
            double frameOffset = k_ChromaWindow * 0.5 / r;
            for (int b = 0; b < beats; b++)
            {
                var c = new float[12];
                int first = Math.Max(0, (int)Math.Ceiling((edges[b] - frameOffset) / k_ChromaHop));
                int last = Math.Min(frames - 1, (int)Math.Floor((edges[b + 1] - frameOffset) / k_ChromaHop));
                for (int f = first; f <= last; f++)
                {
                    for (int p = 0; p < 12; p++)
                        c[p] += chroma[f][p];
                    beatEnergy[b] += energy[f];
                }

                beatChroma[b] = c;
            }

            // A beat far quieter than the song's loud ones has no chord to find: silence, or drums alone.
            var sortedEnergy = (float[])beatEnergy.Clone();
            Array.Sort(sortedEnergy);
            float gate = sortedEnergy.Length > 0 ? sortedEnergy[(int)(0.9f * (sortedEnergy.Length - 1))] * 0.02f : 0f;

            // Scores: 0–11 major on that root, 12–23 minor, 24 none.
            const int states = 25;
            var match = new float[beats, states];
            for (int b = 0; b < beats; b++)
            {
                var c = beatChroma[b];
                bool silent = beatEnergy[b] <= gate;
                Normalise(c);
                for (int root = 0; root < 12; root++)
                {
                    match[b, root] = silent ? 0f : Triad(c, root, false);
                    match[b, 12 + root] = silent ? 0f : Triad(c, root, true);
                }

                match[b, 24] = silent ? 1f : k_NoChord;
            }

            // Viterbi: the path that scores most, less a cost for each change of chord.
            var total = new float[states];
            var next = new float[states];
            var from = new int[beats, states];
            for (int s = 0; s < states; s++)
                total[s] = beats > 0 ? match[0, s] : 0f;

            for (int b = 1; b < beats; b++)
            {
                int bestState = 0;
                for (int s = 1; s < states; s++)
                    if (total[s] > total[bestState])
                        bestState = s;

                for (int s = 0; s < states; s++)
                {
                    float stay = total[s], change = total[bestState] - k_ChangeCost;
                    from[b, s] = stay >= change ? s : bestState;
                    next[s] = Math.Max(stay, change) + match[b, s];
                }

                (total, next) = (next, total);
            }

            var path = new int[beats];
            if (beats > 0)
            {
                int end = 0;
                for (int s = 1; s < states; s++)
                    if (total[s] > total[end])
                        end = s;

                path[beats - 1] = end;
                for (int b = beats - 1; b > 0; b--)
                    path[b - 1] = from[b, path[b]];
            }

            // Joined into stretches of one chord.
            var chords = new List<Chord>();
            for (int b = 0; b < beats;)
            {
                int state = path[b], run = b;
                while (run + 1 < beats && path[run + 1] == state)
                    run++;

                double start = edges[b], end = edges[run + 1];
                if (state < 24)
                    chords.Add(new Chord(start, end, state % 12, state >= 12, loudness((start + end) * 0.5)));
                b = run + 1;
            }

            return chords;
        }

        static void Normalise(float[] c)
        {
            // Compressed, then the floor taken off so a broad wash of noise counts for nothing, then unit length.
            float floor = float.MaxValue;
            for (int p = 0; p < 12; p++)
            {
                c[p] = (float)Math.Sqrt(c[p]);
                floor = Math.Min(floor, c[p]);
            }

            double length = 0.0;
            for (int p = 0; p < 12; p++)
            {
                c[p] -= floor;
                length += c[p] * c[p];
            }

            float scale = length > 1e-12 ? (float)(1.0 / Math.Sqrt(length)) : 0f;
            for (int p = 0; p < 12; p++)
                c[p] *= scale;
        }

        /// <summary>How well a normalised chroma matches the triad on <paramref name="root"/>: its cosine against the triad's three notes.</summary>
        static float Triad(float[] c, int root, bool minor)
        {
            int third = (root + (minor ? 3 : 4)) % 12, fifth = (root + 7) % 12;
            return (c[root] + c[third] + c[fifth]) / (float)Math.Sqrt(3.0);
        }

        // ------------------------------------------------------------------ the parts

        /// <summary>The notes, part by part: the score's onsets dealt out to the animals, and the chords voiced for the pads and the choir.</summary>
        static void Score(SongData song, List<RigNote> found, List<Chord> chords, Func<double, float> loudness)
        {
            var rank = StrengthRanks(found);
            var crashes = new List<double>();
            foreach (var note in found)
                if (note.Lane == RigLane.Crash)
                    crashes.Add(note.Time);

            var kick = new List<Note>();
            var snare = new List<Note>();
            var hat = new List<Note>();
            var texture = new List<Note>();
            var bass = new List<Note>();
            var sub = new List<Note>();
            var keys = new List<Note>();
            var arp = new List<Note>();
            var vocal = new List<Note>();

            bool highTom = true;
            double lastSub = double.NegativeInfinity;
            for (int i = 0; i < found.Count; i++)
            {
                var note = found[i];
                double t = note.Time;
                float velocity = Mathf.Clamp(0.3f + 0.45f * rank[i] + 0.25f * loudness(t), 0.05f, k_MostVelocity);
                int pc = note.PitchClass;

                switch (note.Lane)
                {
                    case RigLane.Kick:
                        kick.Add(new Note(t, k_Hit, GmDrum.Kick, Near(crashes, t) ? 1f : velocity));
                        break;
                    case RigLane.Snare:
                        snare.Add(new Note(t, k_Hit, GmDrum.Snare, velocity));
                        break;
                    case RigLane.Tom:
                        // The score cannot tell one tom from another, so the gorilla's fists alternate.
                        snare.Add(new Note(t, k_Hit, highTom ? GmDrum.TomHighMid : GmDrum.TomLow, velocity));
                        highTom = !highTom;
                        break;
                    case RigLane.HiHat:
                        hat.Add(new Note(t, k_Hit * 0.6, GmDrum.ClosedHat, velocity));
                        texture.Add(new Note(t, k_Hit * 0.6, 70, velocity * 0.8f));
                        break;
                    case RigLane.Crash:
                        // The kick it lands with is full strength, the elephant's trumpet (above); a crash
                        // that came off a snare gets a kick of its own (below).
                        hat.Add(new Note(t, 0.5, GmDrum.OpenHat, 1f));
                        texture.Add(new Note(t, 0.3, 96, 1f));
                        break;
                    case RigLane.Bass when pc >= 0:
                        bass.Add(new Note(t, 0.0, 36 + pc, velocity));
                        if (rank[i] >= 0.6f && t - lastSub >= 0.45)
                        {
                            sub.Add(new Note(t, 0.0, 24 + pc, velocity));
                            lastSub = t;
                        }
                        break;
                    case RigLane.Mid when pc >= 0:
                        keys.Add(new Note(t, 0.0, 48 + pc, velocity));
                        break;
                    case RigLane.High when pc >= 0:
                        arp.Add(new Note(t, 0.0, 72 + pc, velocity));
                        vocal.Add(new Note(t, 0.0, 72 + pc, velocity));
                        if (rank[i] >= 0.7f)
                            texture.Add(new Note(t, 0.2, 84 + pc, velocity));
                        break;
                }
            }

            // A crash that came off a snare: the elephant still stomps on it.
            foreach (double t in crashes)
                if (!Near(kick, t))
                    kick.Add(new Note(t, k_Hit, GmDrum.Kick, 1f));

            // A pitched note lasts until the next in its part, within what that part's animal can show.
            Hold(bass, 0.12, 0.8);
            Hold(sub, 0.3, 1.5);
            Hold(keys, 0.1, 0.6);
            Hold(arp, 0.06, 0.3);
            Hold(vocal, 0.15, 1.5);

            var pads = new List<Note>();
            var choir = new List<Note>();
            float choirFrom = ChoirThreshold(chords);
            foreach (var chord in chords)
            {
                int third = chord.Root + (chord.Minor ? 3 : 4);
                int fifth = chord.Root + 7;
                double duration = chord.End - chord.Start;
                float velocity = Mathf.Lerp(0.55f, 0.9f, chord.Loudness);
                foreach (int pitch in new[] { chord.Root, third, fifth })
                    pads.Add(new Note(chord.Start, duration, 48 + pitch, velocity));

                if (chord.Loudness >= choirFrom && duration >= 0.4)
                    foreach (int pitch in new[] { chord.Root, third, fifth })
                        choir.Add(new Note(chord.Start, duration, 60 + pitch, velocity));
            }

            Set(song, Part.Kick, kick);
            Set(song, Part.Snare, snare);
            Set(song, Part.HiHat, hat);
            Set(song, Part.Texture, texture);
            Set(song, Part.Bass, bass);
            Set(song, Part.SubBass, sub);
            Set(song, Part.Keys, keys);
            Set(song, Part.Arp, arp);
            Set(song, Part.Vocal, vocal);
            Set(song, Part.Pads, pads);
            Set(song, Part.Choir, choir);
        }

        static void Set(SongData song, Part part, List<Note> notes) => song.Tracks[(int)part] = new NoteTrack(notes);

        /// <summary>
        /// Each note's strength as a rank among its own part's, 0 to 1 — ties sharing the middle of their
        /// run — so every part has soft and hard notes whatever its band's levels.
        /// </summary>
        static float[] StrengthRanks(List<RigNote> notes)
        {
            var rank = new float[notes.Count];
            var byLane = new Dictionary<RigLane, List<int>>();
            for (int i = 0; i < notes.Count; i++)
            {
                if (!byLane.TryGetValue(notes[i].Lane, out var list))
                    byLane[notes[i].Lane] = list = new List<int>();
                list.Add(i);
            }

            foreach (var list in byLane.Values)
            {
                list.Sort((a, b) => notes[a].Strength.CompareTo(notes[b].Strength));
                for (int start = 0; start < list.Count;)
                {
                    int end = start;
                    while (end + 1 < list.Count && notes[list[end + 1]].Strength == notes[list[start]].Strength)
                        end++;

                    float value = list.Count > 1 ? (start + end) * 0.5f / (list.Count - 1) : 0.5f;
                    for (int k = start; k <= end; k++)
                        rank[list[k]] = value;
                    start = end + 1;
                }
            }

            return rank;
        }

        /// <summary>Sets each note's length to the gap to the next one, held between <paramref name="least"/> and <paramref name="most"/> seconds.</summary>
        static void Hold(List<Note> notes, double least, double most)
        {
            notes.Sort();
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                double gap = i + 1 < notes.Count ? notes[i + 1].Time - note.Time : most;
                notes[i] = new Note(note.Time, Math.Clamp(gap, least, most), note.Pitch, note.Velocity);
            }
        }

        /// <summary>How loud a chord's stretch has to be for the choir to sing it: the loudest two fifths of the song's chords.</summary>
        static float ChoirThreshold(List<Chord> chords)
        {
            if (chords.Count == 0)
                return 1f;

            var loudness = new float[chords.Count];
            for (int i = 0; i < chords.Count; i++)
                loudness[i] = chords[i].Loudness;
            Array.Sort(loudness);
            return Math.Max(0.5f, loudness[(int)(0.6f * (loudness.Length - 1))]);
        }

        static bool Near(List<double> times, double t)
        {
            foreach (double time in times)
                if (Math.Abs(time - t) <= k_Together)
                    return true;
            return false;
        }

        static bool Near(List<Note> notes, double t)
        {
            foreach (var note in notes)
                if (Math.Abs(note.Time - t) <= k_Together)
                    return true;
            return false;
        }
    }
}
