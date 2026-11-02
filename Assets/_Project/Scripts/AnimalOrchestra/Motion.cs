using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The shapes every performer's motion is made of. All of them are functions of time relative to a
    /// note, so a pose is computed from the song clock each frame rather than accumulated — a dropped
    /// frame skips ahead instead of putting an animal behind the music.
    /// </summary>
    public static class Motion
    {
        /// <summary>
        /// How far into a triggered move an animal is, given <paramref name="tau"/>, the time from the hit
        /// (negative before it). Blend <c>rest → windup</c> by <paramref name="windup"/>, then that toward
        /// <c>strike</c> by <paramref name="strike"/>:
        /// <list type="bullet">
        /// <item>from <paramref name="lead"/> before the hit, it eases into the windup;</item>
        /// <item>in the last third of the lead it accelerates from the windup into the strike, reaching it
        /// exactly on the note — the hit frame lands on the beat;</item>
        /// <item>after the hit it eases from the strike back to rest over <paramref name="recover"/>.</item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// The plan's rule is under 200 ms from the start of a move to its hit frame; keep
        /// <paramref name="lead"/> there. <see cref="Performer.TryHit"/> shortens it for fast repeats.
        /// </remarks>
        public static void Hit(double tau, float lead, float recover, out float windup, out float strike)
        {
            windup = 0f;
            strike = 0f;

            if (tau < 0.0)
            {
                float before = (float)-tau;
                if (lead <= 0f || before >= lead)
                    return;

                float split = lead * 0.35f;
                if (before > split)
                {
                    windup = Smooth01((lead - before) / (lead - split));
                }
                else
                {
                    windup = 1f;
                    float s = 1f - before / split;
                    strike = s * s;   // accelerating into the hit, like something thrown
                }
            }
            else if (tau < recover)
            {
                strike = 1f - Smooth01((float)tau / recover);
            }
        }

        /// <summary>
        /// 0 before the hit, up to 1 over <paramref name="attack"/>, then halving every
        /// <paramref name="halfLife"/> seconds. A flash, a glow, a puff.
        /// </summary>
        public static float Pulse(double since, float attack, float halfLife)
        {
            if (since < 0.0)
                return 0f;

            if (since < attack)
                return (float)(since / attack);

            return Mathf.Pow(0.5f, (float)(since - attack) / Mathf.Max(1e-4f, halfLife));
        }

        public static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Frame-rate independent smoothing toward a target: halfway there every <paramref name="halfLife"/> seconds.</summary>
        public static float Damp(float current, float target, float halfLife, float deltaTime) =>
            Mathf.Lerp(current, target, DampFactor(halfLife, deltaTime));

        public static Vector3 Damp(Vector3 current, Vector3 target, float halfLife, float deltaTime) =>
            Vector3.Lerp(current, target, DampFactor(halfLife, deltaTime));

        public static Quaternion Damp(Quaternion current, Quaternion target, float halfLife, float deltaTime) =>
            Quaternion.Slerp(current, target, DampFactor(halfLife, deltaTime));

        public static Color Damp(Color current, Color target, float halfLife, float deltaTime) =>
            Color.Lerp(current, target, DampFactor(halfLife, deltaTime));

        static float DampFactor(float halfLife, float deltaTime) =>
            1f - Mathf.Pow(0.5f, deltaTime / Mathf.Max(1e-4f, halfLife));

        /// <summary>A sine of <paramref name="hz"/> cycles per second, phase in cycles.</summary>
        public static float Wave(float time, float hz, float phase = 0f) =>
            Mathf.Sin((time * hz + phase) * 2f * Mathf.PI);

        /// <summary><paramref name="pitch"/> placed 0–1 between <paramref name="low"/> and <paramref name="high"/>.</summary>
        public static float PitchNorm(int pitch, int low, int high) =>
            high <= low ? 0.5f : Mathf.Clamp01((pitch - low) / (float)(high - low));

        /// <summary>
        /// A colour for a pitch class, walked round the circle of fifths rather than the chromatic scale,
        /// so chords a fifth apart — the common progressions — get neighbouring hues and a song's colours
        /// drift rather than jump.
        /// </summary>
        public static Color PitchColor(int pitch, float saturation, float value)
        {
            int pitchClass = ((pitch % 12) + 12) % 12;
            int fifths = pitchClass * 7 % 12;
            return Color.HSVToRGB(fifths / 12f, saturation, value);
        }

        /// <summary>A deterministic 0–1 value per integer, for spreading animals' idle loops apart.</summary>
        public static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
