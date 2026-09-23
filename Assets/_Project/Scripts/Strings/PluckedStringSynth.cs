using UnityEngine;

namespace VRShootingGallery.Strings
{
    /// <summary>
    /// Renders one plucked note into an <see cref="AudioClip"/> with the Karplus–Strong loop: a burst
    /// of noise is fed into a delay line exactly one period long, and on every pass round the loop
    /// each sample is averaged with its neighbour. The averaging is a low-pass filter, so the upper
    /// harmonics die first and the burst settles into a tone that mellows as it rings — which is
    /// what a real string does.
    /// </summary>
    /// <remarks>
    /// Synthesised rather than sampled for two reasons. The project has no harp recordings, and
    /// audio files go through LFS. And a clip made here is tuned to exactly the note the builder
    /// asks for, so retuning the harp is a constant change rather than a recording session.
    /// Rendering all of the strings takes a few tens of milliseconds at scene load.
    /// </remarks>
    public static class PluckedStringSynth
    {
        /// <summary>
        /// Where along the string the pluck lands, as a fraction of its length, when nobody says. The
        /// comb filter this puts on the excitation hollows out every harmonic with a node there, which
        /// is most of what separates a harp's pluck from a guitar's — a harpist plays nearer the middle
        /// of the string, a guitarist nearer the bridge.
        /// </summary>
        public const float k_HarpPluckPosition = 0.28f;

        /// <summary>How much of the noise burst's brightness survives, when nobody says. Lower is warmer.</summary>
        public const float k_HarpTone = 0.55f;

        const float k_Peak = 0.8f;
        const float k_TailSeconds = 0.25f;
        const float k_AttackSeconds = 0.0005f;

        /// <summary>
        /// Renders a note. <paramref name="ringSeconds"/> is the time to −60 dB; the loop's per-period
        /// loss is derived from it, so a bass string and a treble string can be given the decay a
        /// real harp gives them rather than whatever falls out of the filter.
        /// </summary>
        public static AudioClip Render(string name, float frequency, float ringSeconds, int sampleRate, int seed)
        {
            return Render(name, frequency, ringSeconds, sampleRate, seed, k_HarpPluckPosition, k_HarpTone);
        }

        /// <summary>
        /// Renders a note with a given timbre. <paramref name="pluckPosition"/> is how far along the
        /// string it is plucked, 0..0.5; <paramref name="tone"/> is how bright the attack is, 0..1.
        /// </summary>
        public static AudioClip Render(string name, float frequency, float ringSeconds, int sampleRate, int seed,
            float pluckPosition, float tone)
        {
            frequency = Mathf.Max(20f, frequency);
            ringSeconds = Mathf.Max(0.2f, ringSeconds);
            sampleRate = sampleRate > 0 ? sampleRate : 48000;

            // The two-tap average in the loop costs half a sample of delay, and the fractional part
            // of what is left is read by interpolation — rounding the delay to whole samples instead
            // would put the top strings up to a quarter of a semitone out.
            float period = sampleRate / frequency;
            float loopDelay = period - 0.5f;
            int whole = Mathf.Max(1, Mathf.FloorToInt(loopDelay));
            float fraction = Mathf.Clamp01(loopDelay - whole);
            int ring = whole + 3;

            // Gain per trip round the loop such that the note is 60 dB down after ringSeconds.
            float loss = Mathf.Pow(10f, -3f / (ringSeconds * frequency));

            int total = Mathf.CeilToInt((ringSeconds + k_TailSeconds) * sampleRate);
            var samples = new float[total];
            var history = new float[ring];

            Excite(history, period, seed, Mathf.Clamp(pluckPosition, 0.02f, 0.5f), Mathf.Clamp01(tone));

            for (int n = 0; n < total; n++)
            {
                float a = history[Slot(n - whole, ring)];
                float b = history[Slot(n - whole - 1, ring)];
                float c = history[Slot(n - whole - 2, ring)];

                float now = a + (b - a) * fraction;      // the loop output one period ago
                float before = b + (c - b) * fraction;   // and the sample before that

                float y = loss * 0.5f * (now + before);
                history[Slot(n, ring)] = y;
                samples[n] = y;
            }

            Shape(samples, sampleRate);

            var clip = AudioClip.Create(name, total, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Fills the delay line with the pluck: white noise, softened with a one-pole low-pass so the
        /// attack is a thumb rather than a pick, then comb-filtered for the pluck position.
        /// </summary>
        static void Excite(float[] history, float period, int seed, float pluckPosition, float tone)
        {
            var rng = new System.Random(seed);

            float low = 0f;
            for (int i = 0; i < history.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += tone * (white - low);
                history[i] = low;
            }

            int pick = Mathf.Clamp(Mathf.RoundToInt(pluckPosition * period), 1, history.Length - 1);
            for (int i = history.Length - 1; i >= pick; i--)
                history[i] -= history[i - pick];
        }

        /// <summary>A short ramp in, a fade over the tail so the clip ends silent, and a peak normalise.</summary>
        static void Shape(float[] samples, int sampleRate)
        {
            int attack = Mathf.Max(1, Mathf.RoundToInt(k_AttackSeconds * sampleRate));
            for (int n = 0; n < attack && n < samples.Length; n++)
                samples[n] *= (float)n / attack;

            int tail = Mathf.Min(samples.Length, Mathf.RoundToInt(k_TailSeconds * sampleRate));
            for (int n = 0; n < tail; n++)
                samples[samples.Length - 1 - n] *= (float)n / tail;

            float peak = 0f;
            foreach (float s in samples)
                peak = Mathf.Max(peak, Mathf.Abs(s));

            if (peak <= 1e-6f)
                return;

            float gain = k_Peak / peak;
            for (int n = 0; n < samples.Length; n++)
                samples[n] *= gain;
        }

        static int Slot(int n, int ring)
        {
            int slot = n % ring;
            return slot < 0 ? slot + ring : slot;
        }
    }
}
