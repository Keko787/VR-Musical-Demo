using System;
using System.Threading;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// Synthesises a <see cref="DemoSongs"/> score into audio: one stem per part, each rendered note by
    /// note from the same notes the animals read, measured (<see cref="StemAnalysis"/>), then panned
    /// toward its animal's wall and mixed with a little reverb into one stereo clip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plain C#, no Unity: it runs on worker threads while the previous song plays, and outside Unity
    /// entirely for checking a render. Parts render in parallel, each into its own scratch buffer, and
    /// are added into the mix one at a time — so memory is the mix plus one stem per worker (about
    /// 100 MB at peak), not eleven stems.
    /// </para>
    /// <para>
    /// The voices are deliberately simple — a pitch-swept sine kick, filtered noise, detuned saws, a
    /// two-operator FM electric piano, saws through vowel formants for the voices — chosen to be clearly
    /// distinct from one another, so a listener can pick each part out and find its animal. Oscillators
    /// use a polynomial sine and slow-moving parameters are updated every 32 samples, which is what
    /// keeps a 90-second song to a few seconds of work.
    /// </para>
    /// </remarks>
    public static class SongSynth
    {
        public const int DefaultSampleRate = 44100;

        /// <summary>Per part: level, pan (−1 left wall … +1 right wall) and reverb send.</summary>
        static readonly (float gain, float pan, float send)[] k_Mix =
        {
            (0.95f, 0.00f, 0.03f),   // Kick — elephant, front
            (0.38f, 0.00f, 0.00f),   // SubBass — whale, floor
            (1.10f, -0.50f, 0.20f),  // Snare — gorilla, left wall
            (0.30f, 0.50f, 0.45f),   // Choir — wolves, right wall
            (0.36f, 0.00f, 0.35f),   // Arp — starlings, front
            (0.35f, -0.20f, 0.30f),  // Texture — fireflies, everywhere
            (0.65f, 0.00f, 0.28f),   // Vocal — parrot, front centre
            (0.95f, -0.25f, 0.05f),  // Bass — bullfrog, left of the viewer
            (0.45f, 0.30f, 0.08f),   // HiHat — woodpecker, right of the viewer
            (0.38f, 0.00f, 0.50f),   // Pads — jellyfish, overhead
            (0.59f, 0.05f, 0.30f),   // Keys — octopus, low centre
        };

        /// <summary>
        /// Renders <paramref name="song"/>'s notes into <see cref="SongData.Pcm"/> (interleaved stereo) and
        /// fills in <see cref="SongData.Analysis"/> for every part that has notes.
        /// </summary>
        public static void Render(SongData song, int sampleRate = DefaultSampleRate)
        {
            int frames = (int)Math.Ceiling(song.Length * sampleRate);
            var mix = new float[frames * 2];
            var send = new float[frames];
            var mixLock = new object();
            int next = -1;

            void Worker()
            {
                var stem = new float[frames];
                for (int p = Interlocked.Increment(ref next); p < Parts.Count; p = Interlocked.Increment(ref next))
                {
                    var part = (Part)p;
                    var track = song.Track(part);
                    if (track.Count == 0)
                        continue;

                    Array.Clear(stem, 0, stem.Length);
                    RenderPart(part, track, stem, sampleRate);
                    song.Analysis[p] = StemAnalysis.Measure(stem, 1, sampleRate);

                    var (gain, pan, sendLevel) = k_Mix[p];
                    float angle = (pan + 1f) * (float)Math.PI * 0.25f;
                    float left = gain * (float)Math.Cos(angle) * 1.41421356f;
                    float right = gain * (float)Math.Sin(angle) * 1.41421356f;
                    float wet = gain * sendLevel;

                    lock (mixLock)
                    {
                        for (int i = 0; i < frames; i++)
                        {
                            float x = stem[i];
                            mix[2 * i] += x * left;
                            mix[2 * i + 1] += x * right;
                            send[i] += x * wet;
                        }
                    }
                }
            }

            int workers = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
            var threads = new Thread[workers - 1];
            for (int w = 0; w < threads.Length; w++)
            {
                threads[w] = new Thread(Worker) { IsBackground = true, Name = "SongSynth" };
                threads[w].Start();
            }

            Worker();
            foreach (var thread in threads)
                thread.Join();

            Reverb(send, mix, sampleRate);
            Master(mix);

            song.Pcm = mix;
            song.Channels = 2;
            song.SampleRate = sampleRate;
        }

        static void RenderPart(Part part, NoteTrack track, float[] output, int sampleRate)
        {
            var noise = new Noise((uint)(part + 1) * 2654435761u);
            for (int i = 0; i < track.Count; i++)
            {
                var note = track[i];
                int start = (int)Math.Round(note.Time * sampleRate);
                if (start >= output.Length)
                    continue;

                var voice = new Voice(output, start, sampleRate, note);
                switch (part)
                {
                    case Part.Kick: Kick(ref voice, ref noise); break;
                    case Part.Snare:
                        if (GmDrum.IsTom(note.Pitch)) Tom(ref voice, ref noise);
                        else Snare(ref voice, ref noise);
                        break;
                    case Part.HiHat: Hat(ref voice, ref noise); break;
                    case Part.Texture:
                        if (note.Pitch >= 84) Glint(ref voice);
                        else Shaker(ref voice, ref noise);
                        break;
                    case Part.SubBass: Sub(ref voice); break;
                    case Part.Bass: Bass(ref voice); break;
                    case Part.Pads: Pad(ref voice); break;
                    case Part.Keys: ElectricPiano(ref voice); break;
                    case Part.Arp: Pluck(ref voice); break;
                    case Part.Vocal:
                        int previous = i > 0 && note.Time - track[i - 1].End < 0.15 ? track[i - 1].Pitch : note.Pitch;
                        Sing(ref voice, ref noise, previous, Vowel.Ah, Vowel.Oh, 1, 0.3f, 5.4f, 0.03f, 0.10f);
                        break;
                    case Part.Choir:
                        Sing(ref voice, ref noise, note.Pitch, Vowel.Oo, Vowel.Oo, 2, 0.15f, 4.6f, 0.15f, 0.45f);
                        break;
                }
            }
        }

        // ================================================================== voices

        /// <summary>Where a note writes: its first sample, the buffer, and the note itself.</summary>
        struct Voice
        {
            public readonly float[] Output;
            public readonly int Start;
            public readonly float Rate;
            public readonly float Velocity;
            public readonly float Frequency;
            public readonly float Duration;
            public readonly int Pitch;

            public Voice(float[] output, int start, int sampleRate, Note note)
            {
                Output = output;
                Start = start;
                Rate = sampleRate;
                Velocity = note.Velocity;
                Frequency = Hz(note.Pitch);
                Duration = (float)note.Duration;
                Pitch = note.Pitch;
            }

            /// <summary>Samples to render for <paramref name="seconds"/>, clipped to the buffer.</summary>
            public int Length(float seconds) => Math.Max(0, Math.Min(Output.Length - Start, (int)(seconds * Rate)));
        }

        static float Hz(double pitch) => 440f * (float)Math.Pow(2.0, (pitch - 69.0) / 12.0);

        /// <summary>A sine dropping from about 170 Hz to 46 Hz with a click on top: a round, punchy kick.</summary>
        static void Kick(ref Voice v, ref Noise noise)
        {
            int n = v.Length(0.5f);
            float phase = 0f, sweep = 1f, body = 1f, click = 1f;
            float sweepK = Decay(30f, v.Rate), bodyK = Decay(7f, v.Rate), clickK = Decay(350f, v.Rate);
            for (int i = 0; i < n; i++)
            {
                phase = Wrap(phase + (46f + 125f * sweep) / v.Rate);
                float s = Sine(phase) * body + noise.Next() * click * 0.25f;
                v.Output[v.Start + i] += SoftClip(s * 1.5f) * v.Velocity;
                sweep *= sweepK;
                body *= bodyK;
                click *= clickK;
            }
        }

        static void Snare(ref Voice v, ref Noise noise)
        {
            int n = v.Length(0.32f);
            float phase = 0f, tone = 1f, rattle = 1f, low = 0f;
            float toneK = Decay(22f, v.Rate), rattleK = Decay(15f, v.Rate);
            float a = OnePole(1200f, v.Rate);
            for (int i = 0; i < n; i++)
            {
                phase = Wrap(phase + 190f / v.Rate);
                float x = noise.Next();
                low += a * (x - low);
                float s = Sine(phase) * tone * 0.5f + (x - low) * rattle * 0.85f;
                v.Output[v.Start + i] += s * v.Velocity;
                tone *= toneK;
                rattle *= rattleK;
            }
        }

        static void Tom(ref Voice v, ref Noise noise)
        {
            float baseHz = v.Frequency * 0.5f;   // GM tom notes sit an octave above the drum's pitch
            int n = v.Length(0.55f);
            float phase = 0f, sweep = 1f, body = 1f, attack = 1f;
            float sweepK = Decay(18f, v.Rate), bodyK = Decay(8f, v.Rate), attackK = Decay(120f, v.Rate);
            for (int i = 0; i < n; i++)
            {
                phase = Wrap(phase + baseHz * (1f + 0.5f * sweep) / v.Rate);
                float s = Sine(phase) * body + noise.Next() * attack * 0.3f;
                v.Output[v.Start + i] += SoftClip(s * 1.2f) * v.Velocity * 0.9f;
                sweep *= sweepK;
                body *= bodyK;
                attack *= attackK;
            }
        }

        static readonly float[] k_HatRatios = { 205.3f, 304.4f, 369.6f, 522.7f, 540.0f, 800.0f };

        /// <summary>Six detuned square waves and noise, high-passed hard: a metallic hat. The open hat rings until its note ends.</summary>
        static void Hat(ref Voice v, ref Noise noise)
        {
            bool open = GmDrum.IsOpenHat(v.Pitch);
            float ring = open ? Math.Max(0.12f, v.Duration) : 0.09f;
            int n = v.Length(ring + 0.05f);
            Span<float> phases = stackalloc float[6];
            float env = 1f, envK = Decay(open ? 6f : 55f, v.Rate), choke = Decay(60f, v.Rate);
            float low1 = 0f, low2 = 0f, a = OnePole(6500f, v.Rate);
            int chokeAt = (int)(ring * v.Rate);
            for (int i = 0; i < n; i++)
            {
                float metal = 0f;
                for (int k = 0; k < 6; k++)
                {
                    phases[k] = Wrap(phases[k] + k_HatRatios[k] * 1.7f / v.Rate);
                    metal += phases[k] < 0.5f ? 1f : -1f;
                }

                float x = metal * 0.12f + noise.Next() * 0.6f;
                low1 += a * (x - low1);
                float high = x - low1;
                low2 += a * (high - low2);

                v.Output[v.Start + i] += (high - low2) * env * v.Velocity * 3.2f;
                env *= i >= chokeAt ? choke : envK;
            }
        }

        static void Shaker(ref Voice v, ref Noise noise)
        {
            int n = v.Length(0.12f);
            float env = 0f, decay = Decay(38f, v.Rate), attack = 1f / (0.004f * v.Rate);
            float low = 0f, a = OnePole(5000f, v.Rate);
            for (int i = 0; i < n; i++)
            {
                env = i * attack < 1f ? i * attack : env * decay;
                float x = noise.Next();
                low += a * (x - low);
                v.Output[v.Start + i] += (x - low) * env * v.Velocity * 1.5f;
            }
        }

        /// <summary>A high bell: two near-octave sines ringing out — a firefly lighting up.</summary>
        static void Glint(ref Voice v)
        {
            int n = v.Length(1.0f);
            float phase1 = 0f, phase2 = 0f, env = 1f, decay = Decay(5f, v.Rate);
            float step1 = v.Frequency / v.Rate, step2 = v.Frequency * 2.01f / v.Rate, fadeIn = 1f / (0.002f * v.Rate);
            for (int i = 0; i < n; i++)
            {
                phase1 = Wrap(phase1 + step1);
                phase2 = Wrap(phase2 + step2);
                float s = Sine(phase1) + 0.4f * Sine(phase2);
                v.Output[v.Start + i] += s * env * Math.Min(1f, i * fadeIn) * v.Velocity * 0.35f;
                env *= decay;
            }
        }

        static void Sub(ref Voice v)
        {
            const float release = 0.07f;
            int n = v.Length(v.Duration + release);
            float phase = 0f, step = v.Frequency / v.Rate;
            for (int i = 0; i < n; i++)
            {
                float t = i / v.Rate;
                phase = Wrap(phase + step);
                float s = Sine(phase) + 0.12f * Sine(Wrap(2f * phase));
                v.Output[v.Start + i] += SoftClip(s * 1.3f) * Envelope(t, 0.006f, v.Duration, release) * v.Velocity;
            }
        }

        /// <summary>A saw through a two-pole low-pass whose cutoff snaps open on the note and closes — a plucked bass.</summary>
        static void Bass(ref Voice v)
        {
            const float release = 0.05f;
            int n = v.Length(v.Duration + release);
            float phase = 0f, low1 = 0f, low2 = 0f, a = 0f;
            float sweep = 1f, sweepK = Decay(14f, v.Rate), step = v.Frequency / v.Rate;
            for (int i = 0; i < n; i++)
            {
                if ((i & 31) == 0)
                    a = OnePole(160f + 2400f * v.Velocity * sweep, v.Rate);

                float t = i / v.Rate;
                phase = Wrap(phase + step);
                float saw = 2f * phase - 1f;
                low1 += a * (saw - low1);
                low2 += a * (low1 - low2);

                float level = Envelope(t, 0.004f, v.Duration, release) * (0.65f + 0.35f * sweep);
                v.Output[v.Start + i] += (low2 + 0.35f * Sine(phase)) * level * v.Velocity;
                sweep *= sweepK;
            }
        }

        /// <summary>Three saws a fraction of a semitone apart, softly filtered, slow in and slow out.</summary>
        static void Pad(ref Voice v)
        {
            const float attack = 0.45f, release = 0.9f;
            int n = v.Length(v.Duration + release);
            float p1 = 0f, p2 = 0.33f, p3 = 0.66f, low1 = 0f, low2 = 0f, a = 0f;
            float s1 = v.Frequency * 0.9942f / v.Rate, s2 = v.Frequency / v.Rate, s3 = v.Frequency * 1.0058f / v.Rate;
            for (int i = 0; i < n; i++)
            {
                float t = i / v.Rate;
                if ((i & 31) == 0)
                    a = OnePole(1100f + 500f * Sine(Wrap(t * 0.2f)), v.Rate);

                p1 = Wrap(p1 + s1);
                p2 = Wrap(p2 + s2);
                p3 = Wrap(p3 + s3);
                float saw = 2f * (p1 + p2 + p3) - 3f;

                low1 += a * (saw - low1);
                low2 += a * (low1 - low2);
                v.Output[v.Start + i] += low2 * Envelope(t, attack, v.Duration, release) * v.Velocity * 0.22f;
            }
        }

        /// <summary>Two-operator FM: a bright attack that mellows as the modulation index decays — an electric piano.</summary>
        static void ElectricPiano(ref Voice v)
        {
            const float release = 0.15f;
            int n = v.Length(v.Duration + release);
            float phase = 0f, step = v.Frequency / v.Rate;

            // Index in cycles of phase deviation (radians / 2π).
            float index = (1.5f + 1.5f * v.Velocity) / (2f * (float)Math.PI), indexK = Decay(4.5f, v.Rate);
            float tine = 0.15f, tineK = Decay(28f, v.Rate), body = 1f, bodyK = Decay(1.4f, v.Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / v.Rate;
                phase = Wrap(phase + step);
                float s = Sine(Wrap(phase + index * Sine(phase))) * body + Sine(Wrap(phase * 4f)) * tine;
                v.Output[v.Start + i] += s * Envelope(t, 0.002f, v.Duration, release) * v.Velocity * 0.45f;
                index *= indexK;
                tine *= tineK;
                body *= bodyK;
            }
        }

        /// <summary>A narrow pulse with a snapping filter: a short, glassy pluck for the arpeggio.</summary>
        static void Pluck(ref Voice v)
        {
            float length = Math.Min(v.Duration, 0.22f) + 0.06f;
            int n = v.Length(length);
            float phase = 0f, low = 0f, a = 0f, env = 1f, envK = Decay(11f, v.Rate), sweep = 1f, sweepK = Decay(22f, v.Rate);
            float step = v.Frequency / v.Rate, fadeIn = 1f / (0.002f * v.Rate);
            for (int i = 0; i < n; i++)
            {
                if ((i & 31) == 0)
                    a = OnePole(500f + 5200f * sweep, v.Rate);

                phase = Wrap(phase + step);
                float pulse = phase < 0.32f ? 1f : -0.47f;
                low += a * (pulse - low);
                v.Output[v.Start + i] += low * env * Math.Min(1f, i * fadeIn) * v.Velocity;
                env *= envK;
                sweep *= sweepK;
            }
        }

        // ---------------------------------------------------------------- voices that sing

        enum Vowel { Ah, Oh, Oo }

        static (float f1, float f2, float f3) Formants(Vowel vowel) => vowel switch
        {
            Vowel.Ah => (760f, 1180f, 2600f),
            Vowel.Oh => (520f, 880f, 2500f),
            _ => (330f, 790f, 2350f),
        };

        /// <summary>
        /// Saws with vibrato through three resonant band-passes at a vowel's formants, gliding from the
        /// previous note and morphing from one vowel to another over the note: a wordless voice. The
        /// parrot sings "ah–oh", the wolves "oo".
        /// </summary>
        static void Sing(ref Voice v, ref Noise noise, int fromPitch, Vowel from, Vowel to, int saws, float vibratoDepth,
            float vibratoHz, float attack, float release)
        {
            int n = v.Length(v.Duration + release);
            var (a1, a2, a3) = Formants(from);
            var (b1, b2, b3) = Formants(to);
            float startHz = Hz(fromPitch);
            Span<float> phases = stackalloc float[saws];
            for (int k = 0; k < saws; k++)
                phases[k] = k / (float)saws;

            Svf band1 = default, band2 = default, band3 = default;
            float c1 = 0f, c2 = 0f, c3 = 0f, step = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / v.Rate;

                // Formants, glide and vibrato all move slowly; work them out every 32 samples.
                if ((i & 31) == 0)
                {
                    float morph = Math.Min(1f, t / Math.Max(0.1f, v.Duration));
                    c1 = Svf.Coefficient(Lerp(a1, b1, morph), v.Rate);
                    c2 = Svf.Coefficient(Lerp(a2, b2, morph), v.Rate);
                    c3 = Svf.Coefficient(Lerp(a3, b3, morph), v.Rate);

                    float glide = Math.Min(1f, t / 0.06f);
                    float vibrato = vibratoDepth * Math.Min(1f, Math.Max(0f, (t - 0.18f) / 0.3f)) * Sine(Wrap(vibratoHz * t));
                    step = Lerp(startHz, v.Frequency, glide) * (float)Math.Pow(2.0, vibrato / 12.0) / v.Rate;
                }

                float source = 0f;
                for (int k = 0; k < saws; k++)
                {
                    float detune = saws > 1 ? 1f + (k - (saws - 1) * 0.5f) * 0.006f : 1f;
                    phases[k] = Wrap(phases[k] + step * detune);
                    source += 2f * phases[k] - 1f;
                }

                source = source / saws + noise.Next() * 0.04f;
                float s = band1.Band(source, c1, 0.12f) + band2.Band(source, c2, 0.10f) * 0.7f +
                          band3.Band(source, c3, 0.08f) * 0.25f;
                v.Output[v.Start + i] += s * Envelope(t, attack, v.Duration, release) * v.Velocity * 0.9f;
            }
        }

        /// <summary>Chamberlin state-variable filter, band-pass output. Stable for cutoffs well under a sixth of the rate.</summary>
        struct Svf
        {
            float m_Low, m_Band;

            public static float Coefficient(float cutoff, float rate) => 2f * (float)Math.Sin(Math.PI * Math.Min(cutoff, rate / 7f) / rate);

            public float Band(float input, float f, float damping)
            {
                m_Low += f * m_Band;
                float high = input - m_Low - damping * m_Band;
                m_Band += f * high;
                return m_Band * damping * 2f;
            }
        }

        // ================================================================== helpers

        /// <summary>sin(2π·<paramref name="cycles"/>) for cycles in [0, 1), to about 0.1%: a parabola with one correction term.</summary>
        static float Sine(float cycles)
        {
            float x = cycles >= 0.5f ? cycles - 1f : cycles;   // −0.5 … 0.5
            float y = 8f * x - 16f * x * Math.Abs(x);
            return 0.225f * (y * Math.Abs(y) - y) + y;
        }

        /// <summary>Keeps a phase in [0, 1). Phases only ever advance by less than a cycle per call here.</summary>
        static float Wrap(float phase) => phase >= 1f ? phase - (int)phase : phase;

        /// <summary>Attack ramp, hold to the note's end, then a linear release.</summary>
        static float Envelope(float t, float attack, float hold, float release)
        {
            if (t < attack)
                return t / attack;
            if (t < hold)
                return 1f;
            return Math.Max(0f, 1f - (t - hold) / release);
        }

        /// <summary>Per-sample multiplier for an exponential decay at <paramref name="rate"/> per second.</summary>
        static float Decay(float rate, float sampleRate) => (float)Math.Exp(-rate / sampleRate);

        /// <summary>One-pole low-pass coefficient for a cutoff in Hz; a cheap approximation that stays stable.</summary>
        static float OnePole(float cutoff, float sampleRate)
        {
            float x = 2f * (float)Math.PI * cutoff / sampleRate;
            return x / (1f + x);
        }

        static float SoftClip(float x) => x / (1f + Math.Abs(x)) * 1.5f;

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>A small xorshift: deterministic noise, so a song renders the same every time.</summary>
        struct Noise
        {
            uint m_State;

            public Noise(uint seed) => m_State = seed == 0 ? 1u : seed;

            public float Next()
            {
                m_State ^= m_State << 13;
                m_State ^= m_State >> 17;
                m_State ^= m_State << 5;
                return (m_State & 0xFFFFFF) / (float)0x800000 - 1f;
            }
        }

        // ================================================================== mix

        static readonly int[] k_Combs = { 1116, 1188, 1277, 1356 };
        static readonly int[] k_AllPasses = { 556, 441 };

        /// <summary>
        /// A small Freeverb-style room on the send: four damped combs and two all-passes per side, the
        /// right side's delays a little longer so the tail spreads across the speakers.
        /// </summary>
        static void Reverb(float[] send, float[] mix, int sampleRate)
        {
            float scale = sampleRate / 44100f;
            for (int side = 0; side < 2; side++)
            {
                var combs = new float[k_Combs.Length][];
                var combIndex = new int[k_Combs.Length];
                var combLow = new float[k_Combs.Length];
                for (int c = 0; c < combs.Length; c++)
                    combs[c] = new float[(int)((k_Combs[c] + side * 23) * scale)];

                var passes = new float[k_AllPasses.Length][];
                var passIndex = new int[k_AllPasses.Length];
                for (int p = 0; p < passes.Length; p++)
                    passes[p] = new float[(int)((k_AllPasses[p] + side * 23) * scale)];

                const float feedback = 0.80f, damping = 0.3f, wet = 0.08f;
                for (int i = 0; i < send.Length; i++)
                {
                    float input = send[i];
                    float output = 0f;
                    for (int c = 0; c < combs.Length; c++)
                    {
                        var buffer = combs[c];
                        float delayed = buffer[combIndex[c]];
                        combLow[c] = delayed * (1f - damping) + combLow[c] * damping;
                        buffer[combIndex[c]] = input + combLow[c] * feedback;
                        if (++combIndex[c] >= buffer.Length) combIndex[c] = 0;
                        output += delayed;
                    }

                    for (int p = 0; p < passes.Length; p++)
                    {
                        var buffer = passes[p];
                        float delayed = buffer[passIndex[p]];
                        buffer[passIndex[p]] = output + delayed * 0.5f;
                        output = delayed - output;
                        if (++passIndex[p] >= buffer.Length) passIndex[p] = 0;
                    }

                    mix[2 * i + side] += output * wet;
                }
            }
        }

        /// <summary>
        /// Normalises the mix so its peak would reach 1.25, then rounds everything above 0.7 off smoothly —
        /// a gentle limiter on the kick's peaks, so the mix is loud enough and the loudest moments land near 0.95.
        /// </summary>
        static void Master(float[] mix)
        {
            float peak = 1e-6f;
            for (int i = 0; i < mix.Length; i++)
                peak = Math.Max(peak, Math.Abs(mix[i]));

            float gain = 1.25f / peak;
            for (int i = 0; i < mix.Length; i++)
            {
                float x = mix[i] * gain;
                mix[i] = Math.Abs(x) < 0.7f ? x : Math.Sign(x) * (0.7f + 0.25f * (float)Math.Tanh((Math.Abs(x) - 0.7f) / 0.25f));
            }
        }
    }
}
