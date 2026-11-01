using UnityEngine;
using VRShootingGallery.Strings;

namespace VRShootingGallery.Pipes
{
    /// <summary>
    /// The machine's own sounds, synthesised at load so they are tuned to exactly the notes the
    /// builder lays out and nothing has to be recorded or bought: plucked strings and the guitar by
    /// Karplus–Strong (<see cref="PluckedStringSynth"/>, the harp's), mallet bars, glockenspiel bars,
    /// chimes and the gong by decaying partials at each one's own ratios, and the drums from swept
    /// sines, filtered noise and clangorous square waves.
    /// </summary>
    /// <remarks>Rendering every voice takes a few tens of milliseconds, once, as the scene starts.</remarks>
    public static class RigVoices
    {
        const float k_Peak = 0.8f;

        /// <summary>The sound for <paramref name="target"/>, by what it is.</summary>
        public static AudioClip For(RigTarget target, int sampleRate)
        {
            if (target == null)
                return null;

            float frequency = RigTarget.Frequency(target.Midi);
            int seed = target.Midi * 31 + (int)target.Drum * 7 + (int)target.Group;
            string name = $"Rig_{target.Group}_{target.Title}";

            switch (target.Group)
            {
                case RigGroup.Bass:
                    // Plucked near the bridge with a soft thumb: round, with a long ring.
                    return PluckedStringSynth.Render(name, frequency, 2.6f, sampleRate, seed, 0.18f, 0.42f);
                case RigGroup.Strings:
                    return PluckedStringSynth.Render(name, frequency, 2.2f, sampleRate, seed, 0.3f, 0.7f);
                case RigGroup.Guitar:
                    // Steel strings picked near the bridge: brighter than the harp's, the low ones ringing longest.
                    return PluckedStringSynth.Render(name, frequency, Mathf.Lerp(3.2f, 2.2f, Mathf.InverseLerp(40f, 64f, target.Midi)),
                        sampleRate, seed, 0.14f, 0.75f);
                case RigGroup.Mallets:
                    return Mallet(name, frequency, sampleRate, seed);
                case RigGroup.Glockenspiel:
                    return Glockenspiel(name, frequency, sampleRate, seed);
                case RigGroup.Chimes:
                    return Chime(name, frequency, sampleRate, seed);
                default:
                    return target.Drum switch
                    {
                        RigDrum.Kick => Kick(name, sampleRate, seed),
                        RigDrum.RackTom => Tom(name, sampleRate, seed, 138f),
                        RigDrum.Tom => Tom(name, sampleRate, seed),
                        RigDrum.FloorTom => Tom(name, sampleRate, seed, 70f),
                        RigDrum.Snare => Snare(name, sampleRate, seed),
                        RigDrum.HiHat => Metal(name, sampleRate, seed, 0.09f, 0.05f),
                        RigDrum.Crash => Metal(name, sampleRate, seed, 1.9f, 0.9f),
                        RigDrum.Gong => Gong(name, sampleRate, seed),
                        _ => Tom(name, sampleRate, seed),
                    };
            }
        }

        /// <summary>
        /// A wooden bar over a resonator: the fundamental, its fourth and tenth partials (a marimba's
        /// bars are cut so the overtones fall there), each dying faster than the one below, and a
        /// short soft knock where the ball lands.
        /// </summary>
        public static AudioClip Mallet(string name, float frequency, int sampleRate, int seed)
        {
            // Low bars ring for longer; the overtones are gone in a fraction of that.
            float ring = Mathf.Clamp(1.1f * Mathf.Sqrt(220f / frequency), 0.35f, 1.4f);
            int total = Mathf.CeilToInt((ring * 2.2f + 0.05f) * sampleRate);
            var samples = new float[total];

            float[] ratio = { 1f, 3.98f, 9.9f };
            float[] level = { 1f, 0.32f, 0.1f };
            float[] decay = { ring, ring * 0.25f, ring * 0.08f };

            var rng = new System.Random(seed);
            float knock = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++)
                {
                    float f = frequency * ratio[k];
                    if (f >= sampleRate * 0.45f)
                        continue;
                    s += level[k] * Mathf.Exp(-t / decay[k]) * Mathf.Sin(2f * Mathf.PI * f * t);
                }

                // The knock: a few milliseconds of softened noise.
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                knock += 0.25f * (white - knock);
                s += 0.25f * knock * Mathf.Exp(-t / 0.004f);

                samples[n] = s;
            }

            return Finish(name, samples, sampleRate, 0.002f);
        }

        /// <summary>A kick: a sine swept down from a thump to a boom, and a click on top.</summary>
        public static AudioClip Kick(string name, int sampleRate, int seed)
        {
            int total = Mathf.CeilToInt(0.6f * sampleRate);
            var samples = new float[total];
            var rng = new System.Random(seed);
            float phase = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float f = 46f + 120f * Mathf.Exp(-t / 0.04f);
                phase += 2f * Mathf.PI * f / sampleRate;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t / 0.28f);
                float click = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t / 0.0025f) * 0.4f;
                samples[n] = body + click;
            }

            return Finish(name, samples, sampleRate, 0.0005f);
        }

        /// <summary>
        /// A tom: a shorter sweep than the kick's, higher, settling on <paramref name="pitch"/> Hz, with
        /// a little skin noise. A bigger, lower tom rings longer.
        /// </summary>
        public static AudioClip Tom(string name, int sampleRate, int seed, float pitch = 98f)
        {
            float ring = 0.3f * Mathf.Sqrt(98f / pitch);
            int total = Mathf.CeilToInt(2.5f * ring * sampleRate);
            var samples = new float[total];
            var rng = new System.Random(seed);
            float phase = 0f;
            float low = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float f = pitch * (1f + 0.71f * Mathf.Exp(-t / 0.07f));
                phase += 2f * Mathf.PI * f / sampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.3f * (white - low);
                samples[n] = Mathf.Sin(phase) * Mathf.Exp(-t / ring) + low * 0.35f * Mathf.Exp(-t / 0.05f);
            }

            return Finish(name, samples, sampleRate, 0.0005f);
        }

        /// <summary>
        /// A glockenspiel bar: steel, so it rings long and bright, with the overtones of a bar free at
        /// both ends (2.76 and 5.40 times the note), which a glockenspiel's bars are not tuned away
        /// from as a marimba's are, and a hard tick where the ball lands.
        /// </summary>
        public static AudioClip Glockenspiel(string name, float frequency, int sampleRate, int seed)
        {
            const float ring = 1.6f;
            int total = Mathf.CeilToInt((ring * 2.2f + 0.05f) * sampleRate);
            var samples = new float[total];

            float[] ratio = { 1f, 2.76f, 5.4f };
            float[] level = { 1f, 0.25f, 0.08f };
            float[] decay = { ring, ring * 0.2f, ring * 0.06f };
            var gain = Partials(frequency, ratio, level, decay, sampleRate, out var fall);

            var rng = new System.Random(seed);
            float tick = 0f;
            float previous = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++)
                {
                    s += gain[k] * Mathf.Sin(2f * Mathf.PI * frequency * ratio[k] * t);
                    gain[k] *= fall[k];
                }

                // The tick: a millisecond or two of noise with the low end taken out.
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                tick = 0.7f * (tick + white - previous);
                previous = white;
                s += 0.2f * tick * Mathf.Exp(-t / 0.0015f);

                samples[n] = s;
            }

            return Finish(name, samples, sampleRate, 0.0005f);
        }

        /// <summary>
        /// A tubular chime. A tube's own modes are far from harmonic, but the ones that carry, high up,
        /// sit close to 2 : 3 : 4, and the ear hears the note they imply — the strike note, an octave
        /// under the lowest of them. So the note is built from those, each a hair off its neighbour so
        /// the tube shimmers, ringing for seconds, over a quickly gone metallic clang.
        /// </summary>
        public static AudioClip Chime(string name, float frequency, int sampleRate, int seed)
        {
            const float ring = 3.2f;
            int total = Mathf.CeilToInt(ring * 1.6f * sampleRate);
            var samples = new float[total];

            float[] ratio = { 1f, 2f, 3.01f, 4.17f, 5.43f, 6.8f };
            float[] level = { 0.35f, 1f, 0.7f, 0.45f, 0.25f, 0.15f };
            float[] decay = { ring, ring * 0.9f, ring * 0.7f, ring * 0.45f, ring * 0.2f, ring * 0.1f };
            var gain = Partials(frequency, ratio, level, decay, sampleRate, out var fall);

            var rng = new System.Random(seed);
            float clang = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++)
                {
                    float f = frequency * ratio[k];
                    s += gain[k] * Mathf.Sin(2f * Mathf.PI * f * t);
                    gain[k] *= fall[k];
                }

                // The shimmer: the strike-note partials beat against a twin a fraction of a hertz away.
                s += 0.4f * gain[1] * Mathf.Sin(2f * Mathf.PI * (frequency * 2f + 0.7f) * t);
                s += 0.3f * gain[2] * Mathf.Sin(2f * Mathf.PI * (frequency * 3.01f + 1.1f) * t);

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                clang += 0.6f * (white - clang);
                s += 0.3f * clang * Mathf.Exp(-t / 0.006f);

                samples[n] = s;
            }

            return Finish(name, samples, sampleRate, 0.001f);
        }

        /// <summary>
        /// A gong: a low hum and a cluster of inharmonic partials above it, each a little detuned at
        /// random. The struck boss gives a soft thump; the higher partials take a moment to bloom and
        /// then shimmer away over several seconds, which is the swell a gong is known for.
        /// </summary>
        public static AudioClip Gong(string name, int sampleRate, int seed)
        {
            const float ring = 4.5f;
            const float root = 82f;
            int total = Mathf.CeilToInt(ring * 1.5f * sampleRate);
            var samples = new float[total];

            float[] ratio = { 1f, 1.48f, 1.97f, 2.53f, 3.12f, 3.75f, 4.41f, 5.2f, 6.1f, 7.3f };
            var rng = new System.Random(seed);
            var level = new float[ratio.Length];
            var decay = new float[ratio.Length];
            var bloom = new float[ratio.Length];
            for (int k = 0; k < ratio.Length; k++)
            {
                ratio[k] *= 1f + 0.012f * (float)(rng.NextDouble() * 2.0 - 1.0);
                level[k] = 1f / (1f + 0.35f * k);
                decay[k] = ring / (1f + 0.25f * k);

                // The low partials sound at once; the higher, the later they bloom, and the sooner they go.
                bloom[k] = Mathf.Exp(-1f / ((0.05f + 0.06f * k) * sampleRate));
            }

            var gain = Partials(root, ratio, level, decay, sampleRate, out var fall);
            var unbloomed = new float[ratio.Length];
            for (int k = 0; k < ratio.Length; k++)
                unbloomed[k] = 1f;

            float low = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++)
                {
                    s += gain[k] * (1f - unbloomed[k]) * Mathf.Sin(2f * Mathf.PI * root * ratio[k] * t);
                    gain[k] *= fall[k];
                    unbloomed[k] *= bloom[k];
                }

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.08f * (white - low);
                s += 1.2f * low * Mathf.Exp(-t / 0.04f);

                samples[n] = s;
            }

            return Finish(name, samples, sampleRate, 0.002f);
        }

        /// <summary>A snare: a short drum tone under a burst of bright noise, the wires.</summary>
        public static AudioClip Snare(string name, int sampleRate, int seed)
        {
            int total = Mathf.CeilToInt(0.45f * sampleRate);
            var samples = new float[total];
            var rng = new System.Random(seed);
            float low = 0f;
            float lower = 0f;
            float phase = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                phase += 2f * Mathf.PI * (185f + 40f * Mathf.Exp(-t / 0.02f)) / sampleRate;
                float tone = Mathf.Sin(phase) * Mathf.Exp(-t / 0.08f) * 0.55f;

                // Noise between two one-pole filters: the hiss of the wires, without the fizz or the rumble.
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.55f * (white - low);
                lower += 0.04f * (low - lower);
                float wires = (low - lower) * Mathf.Exp(-t / 0.13f);

                samples[n] = tone + wires;
            }

            return Finish(name, samples, sampleRate, 0.0005f);
        }

        /// <summary>
        /// A cymbal: six square waves at clangorous, unrelated pitches and some noise, high-passed,
        /// dying over <paramref name="ring"/> seconds with the noise gone in <paramref name="noiseRing"/>.
        /// Short, it is a closed hi-hat; long, a crash.
        /// </summary>
        public static AudioClip Metal(string name, int sampleRate, int seed, float ring, float noiseRing)
        {
            int total = Mathf.CeilToInt((ring * 2.5f + 0.05f) * sampleRate);
            var samples = new float[total];
            var rng = new System.Random(seed);
            float[] partials = { 2.0f, 3.0f, 4.16f, 5.43f, 6.79f, 8.21f };
            var phases = new float[partials.Length];
            const float root = 205f;

            float previous = 0f;
            float high = 0f;
            float previousHigh = 0f;
            float higher = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float clang = 0f;
                for (int k = 0; k < partials.Length; k++)
                {
                    phases[k] += root * partials[k] / sampleRate;
                    phases[k] -= Mathf.Floor(phases[k]);
                    clang += phases[k] < 0.5f ? 1f : -1f;
                }

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float raw = clang / partials.Length * Mathf.Exp(-t / ring) + white * 0.6f * Mathf.Exp(-t / noiseRing);

                // Two one-pole high-passes take the body out, leaving the sizzle.
                high = 0.9f * (high + raw - previous);
                previous = raw;
                higher = 0.9f * (higher + high - previousHigh);
                previousHigh = high;
                samples[n] = higher;
            }

            return Finish(name, samples, sampleRate, 0.0005f);
        }

        /// <summary>
        /// Each partial's starting level and what it is multiplied by every sample to die away over its
        /// decay: cheaper than an exponential per sample, for voices with many partials ringing for
        /// seconds. A partial over the clip's highest frequency starts silent.
        /// </summary>
        static float[] Partials(float frequency, float[] ratio, float[] level, float[] decay, int sampleRate, out float[] fall)
        {
            var gain = new float[ratio.Length];
            fall = new float[ratio.Length];
            for (int k = 0; k < ratio.Length; k++)
            {
                gain[k] = frequency * ratio[k] < sampleRate * 0.45f ? level[k] : 0f;
                fall[k] = Mathf.Exp(-1f / (Mathf.Max(0.001f, decay[k]) * sampleRate));
            }

            return gain;
        }

        /// <summary>A ramp in, a fade over the last tenth so the clip ends silent, a peak normalise, and the clip.</summary>
        static AudioClip Finish(string name, float[] samples, int sampleRate, float attackSeconds)
        {
            int attack = Mathf.Max(1, Mathf.RoundToInt(attackSeconds * sampleRate));
            for (int n = 0; n < attack && n < samples.Length; n++)
                samples[n] *= (float)n / attack;

            int tail = Mathf.Max(1, samples.Length / 10);
            for (int n = 0; n < tail; n++)
                samples[samples.Length - 1 - n] *= (float)n / tail;

            float peak = 0f;
            foreach (float s in samples)
                peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 1e-6f)
            {
                float gain = k_Peak / peak;
                for (int n = 0; n < samples.Length; n++)
                    samples[n] *= gain;
            }

            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
