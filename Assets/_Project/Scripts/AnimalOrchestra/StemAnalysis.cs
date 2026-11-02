using System;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// Measures a stem into a <see cref="PartAnalysis"/>: its loudness and the loudness of its low and high
    /// ends, a hundred times a second, smoothed by an envelope follower and normalised to the stem's own
    /// loud passages. Run once per stem when a song loads — on the synthesised stems as they render, and
    /// on stems loaded from disk — so the animals read the same kind of signal whatever the song's source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plan's rule: continuous motion uses smoothed signals, never raw per-frame FFT values. The
    /// follower rises in about 15 ms, so a hit still reads as a hit, and falls over about 180 ms, so a
    /// glow fades rather than flickers.
    /// </para>
    /// <para>
    /// The bands are split with one-pole filters rather than an FFT: two cascaded low-passes at 150 Hz for
    /// the low end and the remainder above a 4 kHz low-pass for the high end. Three broad bands are all
    /// any animal reads, and filters cost one multiply-add a sample.
    /// </para>
    /// <para>
    /// All three bands are normalised by the same number — the 95th percentile of the stem's loudness — so
    /// a band reads as the share of the stem's energy that is in it: a hi-hat's low band stays near zero
    /// instead of being stretched up to 1.
    /// </para>
    /// </remarks>
    public static class StemAnalysis
    {
        public const float FrameRate = 100f;

        const float k_AttackSeconds = 0.015f;
        const float k_ReleaseSeconds = 0.18f;
        const float k_LowCutoff = 150f;
        const float k_HighCutoff = 4000f;

        /// <summary>Where the 95th percentile of loudness lands after normalising.</summary>
        const float k_Reference = 0.85f;

        /// <summary>
        /// Measures interleaved <paramref name="samples"/> with <paramref name="channels"/> channels at
        /// <paramref name="sampleRate"/>; the channels are averaged first.
        /// </summary>
        public static PartAnalysis Measure(float[] samples, int channels, int sampleRate)
        {
            channels = Math.Max(1, channels);
            int length = samples.Length / channels;
            int hop = Math.Max(1, (int)Math.Round(sampleRate / FrameRate));
            int frames = Math.Max(1, (length + hop - 1) / hop);

            var level = new float[frames];
            var low = new float[frames];
            var high = new float[frames];

            float aLow = Coefficient(k_LowCutoff, sampleRate);
            float aHigh = Coefficient(k_HighCutoff, sampleRate);
            float low1 = 0f, low2 = 0f, lowForHigh = 0f;

            for (int frame = 0; frame < frames; frame++)
            {
                int start = frame * hop;
                int end = Math.Min(length, start + hop);
                double sumAll = 0.0, sumLow = 0.0, sumHigh = 0.0;

                for (int i = start; i < end; i++)
                {
                    float x = 0f;
                    for (int c = 0; c < channels; c++)
                        x += samples[i * channels + c];
                    x /= channels;

                    low1 += aLow * (x - low1);
                    low2 += aLow * (low1 - low2);
                    lowForHigh += aHigh * (x - lowForHigh);
                    float h = x - lowForHigh;

                    sumAll += x * x;
                    sumLow += low2 * low2;
                    sumHigh += h * h;
                }

                int count = Math.Max(1, end - start);
                level[frame] = (float)Math.Sqrt(sumAll / count);
                low[frame] = (float)Math.Sqrt(sumLow / count);
                high[frame] = (float)Math.Sqrt(sumHigh / count);
            }

            Follow(level);
            Follow(low);
            Follow(high);

            float reference = Percentile(level, 0.95f);
            float scale = reference > 1e-6f ? k_Reference / reference : 0f;
            Scale(level, scale);
            Scale(low, scale);
            Scale(high, scale);

            return new PartAnalysis(FrameRate, level, low, high);
        }

        /// <summary>An envelope follower over the frames: quick to rise, slow to fall.</summary>
        static void Follow(float[] frames)
        {
            float frameSeconds = 1f / FrameRate;
            float attack = 1f - (float)Math.Exp(-frameSeconds / k_AttackSeconds);
            float release = 1f - (float)Math.Exp(-frameSeconds / k_ReleaseSeconds);
            float envelope = 0f;
            for (int i = 0; i < frames.Length; i++)
            {
                float x = frames[i];
                envelope += (x > envelope ? attack : release) * (x - envelope);
                frames[i] = envelope;
            }
        }

        /// <summary>The <paramref name="fraction"/> percentile of the frames that are not silent.</summary>
        static float Percentile(float[] frames, float fraction)
        {
            var sorted = Array.FindAll(frames, x => x > 1e-5f);
            if (sorted.Length == 0)
                return 0f;

            Array.Sort(sorted);
            return sorted[Math.Min(sorted.Length - 1, (int)(fraction * sorted.Length))];
        }

        static void Scale(float[] frames, float scale)
        {
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Math.Min(1.25f, frames[i] * scale);
        }

        static float Coefficient(float cutoff, float sampleRate)
        {
            float x = 2f * (float)Math.PI * cutoff / sampleRate;
            return x / (1f + x);
        }
    }
}
