using UnityEngine;
using VRShootingGallery.Strings;

namespace VRShootingGallery.Pipes
{
    /// <summary>
    /// The machine's own sounds, synthesised at load so they are tuned to exactly the notes the
    /// builder lays out and nothing has to be recorded or bought: plucked strings by Karplus–Strong
    /// (<see cref="PluckedStringSynth"/>, the harp's), mallet bars by a few decaying partials, and
    /// the drums from swept sines, filtered noise and clangorous square waves.
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
                case RigGroup.Mallets:
                    return Mallet(name, frequency, sampleRate, seed);
                default:
                    return target.Drum switch
                    {
                        RigDrum.Kick => Kick(name, sampleRate, seed),
                        RigDrum.Tom => Tom(name, sampleRate, seed),
                        RigDrum.Snare => Snare(name, sampleRate, seed),
                        RigDrum.HiHat => Metal(name, sampleRate, seed, 0.09f, 0.05f),
                        RigDrum.Crash => Metal(name, sampleRate, seed, 1.9f, 0.9f),
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

        /// <summary>A tom: a shorter sweep, higher, with a little skin noise.</summary>
        public static AudioClip Tom(string name, int sampleRate, int seed)
        {
            int total = Mathf.CeilToInt(0.75f * sampleRate);
            var samples = new float[total];
            var rng = new System.Random(seed);
            float phase = 0f;
            float low = 0f;
            for (int n = 0; n < total; n++)
            {
                float t = n / (float)sampleRate;
                float f = 98f + 70f * Mathf.Exp(-t / 0.07f);
                phase += 2f * Mathf.PI * f / sampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.3f * (white - low);
                samples[n] = Mathf.Sin(phase) * Mathf.Exp(-t / 0.3f) + low * 0.35f * Mathf.Exp(-t / 0.05f);
            }

            return Finish(name, samples, sampleRate, 0.0005f);
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
