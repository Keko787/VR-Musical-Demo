using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// The shape of a whole track: how loud each section is against the track's own quiet and loud
    /// parts, read from the clip's samples before they are heard. A live analyser only knows what
    /// has already played, so it cannot tell a quiet intro from a quiet song, and it can only react
    /// to a drop after the drop; this can do both, and can see a drop coming.
    /// </summary>
    /// <remarks>
    /// The clip is read a few seconds of audio per call so a four-minute track costs a few dozen
    /// frames of a millisecond or two, not one long hitch. It only works on clips imported as
    /// Decompress On Load — the default for a dropped-in MP3 — because Unity cannot hand back the
    /// samples of a compressed or streamed clip; for anything else <see cref="Failed"/> is set and
    /// callers fall back to live analysis.
    /// </remarks>
    public class TrackEnvelope
    {
        /// <summary>Seconds per loudness window.</summary>
        public const float WindowSeconds = 0.25f;

        /// <summary>Seconds of the moving average that turns window loudness into section loudness.</summary>
        public const float SectionSeconds = 6f;

        /// <summary>Least spread of section loudness, in dB, the normalisation will stretch across 0–1, so a track that barely changes is not blown up into a false arc.</summary>
        const float k_MinRangeDb = 6f;

        AudioClip m_Clip;
        float[] m_Buffer = new float[0];
        readonly List<float> m_WindowDb = new();
        float[] m_Section = new float[0];

        int m_Channels;
        int m_WindowFrames;
        int m_TotalFrames;
        int m_ReadFrames;
        double m_WindowSum;
        int m_WindowCount;

        public AudioClip Clip => m_Clip;

        /// <summary>The whole track has been read and <see cref="SectionAt"/> can be trusted.</summary>
        public bool Ready { get; private set; }

        /// <summary>The clip's samples cannot be read. Fall back to live analysis.</summary>
        public bool Failed { get; private set; }

        /// <summary>How much of the track has been read, 0–1.</summary>
        public float Progress => m_TotalFrames > 0 ? m_ReadFrames / (float)m_TotalFrames : 0f;

        /// <summary>Starts reading <paramref name="clip"/>. The work happens in <see cref="Step"/>.</summary>
        public void Begin(AudioClip clip)
        {
            m_Clip = clip;
            Ready = false;
            Failed = clip == null || clip.loadType != AudioClipLoadType.DecompressOnLoad;
            m_WindowDb.Clear();
            m_Section = new float[0];
            m_ReadFrames = 0;
            m_WindowSum = 0;
            m_WindowCount = 0;

            if (Failed)
                return;

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            m_Channels = Mathf.Max(1, clip.channels);
            m_WindowFrames = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * WindowSeconds));
            m_TotalFrames = clip.samples;
        }

        /// <summary>
        /// Reads up to <paramref name="seconds"/> of audio. Call once a frame until <see cref="Ready"/>
        /// or <see cref="Failed"/>; a call before the clip has finished loading does nothing.
        /// </summary>
        public void Step(float seconds)
        {
            if (Ready || Failed || m_Clip == null)
                return;

            if (m_Clip.loadState == AudioDataLoadState.Failed)
            {
                Failed = true;
                return;
            }

            if (m_Clip.loadState != AudioDataLoadState.Loaded)
                return;

            int frames = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(m_Clip.frequency * seconds)), m_TotalFrames - m_ReadFrames);
            if (frames <= 0)
            {
                Finish();
                return;
            }

            // GetData wraps past the end of the clip, so the buffer is only ever as long as what is left.
            int length = frames * m_Channels;
            if (m_Buffer.Length != length)
                m_Buffer = new float[length];

            if (!m_Clip.GetData(m_Buffer, m_ReadFrames))
            {
                Failed = true;
                return;
            }

            for (int f = 0; f < frames; f++)
            {
                // Power averaged across channels: the mono mix a loudness envelope needs.
                float sum = 0f;
                int i = f * m_Channels;
                for (int c = 0; c < m_Channels; c++)
                    sum += m_Buffer[i + c] * m_Buffer[i + c];

                m_WindowSum += sum / m_Channels;
                if (++m_WindowCount >= m_WindowFrames)
                    CloseWindow();
            }

            m_ReadFrames += frames;
            if (m_ReadFrames >= m_TotalFrames)
                Finish();
        }

        void CloseWindow()
        {
            float rms = Mathf.Sqrt((float)(m_WindowSum / Mathf.Max(1, m_WindowCount)));
            m_WindowDb.Add(20f * Mathf.Log10(Mathf.Max(rms, 1e-5f)));
            m_WindowSum = 0;
            m_WindowCount = 0;
        }

        /// <summary>
        /// Smooths the windows into sections and maps them onto the track's own range: its quietest
        /// tenth reads 0 and its loudest tenth reads 1.
        /// </summary>
        void Finish()
        {
            if (m_WindowCount > m_WindowFrames / 4)
                CloseWindow();

            int n = m_WindowDb.Count;
            if (n == 0)
            {
                Failed = true;
                return;
            }

            int half = Mathf.Max(1, Mathf.RoundToInt(SectionSeconds * 0.5f / WindowSeconds));
            var smoothed = new float[n];
            for (int i = 0; i < n; i++)
            {
                int lo = Mathf.Max(0, i - half);
                int hi = Mathf.Min(n - 1, i + half);
                float sum = 0f;
                for (int j = lo; j <= hi; j++)
                    sum += m_WindowDb[j];

                smoothed[i] = sum / (hi - lo + 1);
            }

            var sorted = (float[])smoothed.Clone();
            System.Array.Sort(sorted);
            float quiet = sorted[Mathf.Clamp(Mathf.RoundToInt(n * 0.1f), 0, n - 1)];
            float loud = sorted[Mathf.Clamp(Mathf.RoundToInt(n * 0.9f), 0, n - 1)];
            float range = Mathf.Max(loud - quiet, k_MinRangeDb);

            // A flat track's range is widened about its middle, so it sits in the middle of the scale
            // rather than pinned to the bottom of it.
            float floor = loud - quiet < k_MinRangeDb ? (quiet + loud) * 0.5f - range * 0.5f : quiet;

            m_Section = new float[n];
            for (int i = 0; i < n; i++)
                m_Section[i] = Mathf.Clamp01((smoothed[i] - floor) / range);

            Ready = true;
        }

        /// <summary>Section loudness at <paramref name="seconds"/> into the track, 0 for its quiet parts and 1 for its loud ones.</summary>
        public float SectionAt(float seconds)
        {
            if (!Ready || m_Section.Length == 0)
                return 0f;

            float index = Mathf.Clamp(seconds / WindowSeconds, 0f, m_Section.Length - 1);
            int lo = Mathf.FloorToInt(index);
            int hi = Mathf.Min(lo + 1, m_Section.Length - 1);
            return Mathf.Lerp(m_Section[lo], m_Section[hi], index - lo);
        }
    }
}
