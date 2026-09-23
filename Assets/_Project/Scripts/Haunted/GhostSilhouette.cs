using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// The ghost's outline as a 2D shape: a sheet over something that is not there — a round head,
    /// a skirt that widens to a scalloped hem, two eye holes and an open mouth. One definition,
    /// used wherever the ghost is seen flat: its face in the TV static, and its shadow on the walls.
    /// </summary>
    public static class GhostSilhouette
    {
        const float k_HeadY = 0.66f;
        const float k_HeadRadius = 0.2f;
        const float k_HemY = 0.1f;
        const float k_HemHalfWidth = 0.31f;

        /// <summary>
        /// How much of the point (<paramref name="u"/>, <paramref name="v"/>), both 0–1 with
        /// <paramref name="v"/> up, the ghost covers: 1 inside, 0 outside, with an edge
        /// <paramref name="soft"/> wide. <paramref name="time"/> ripples the hem.
        /// </summary>
        public static float Coverage(float u, float v, float time = 0f, float soft = 0.01f, bool holes = true)
        {
            float x = u - 0.5f;

            float head = Mathf.Sqrt(x * x + (v - k_HeadY) * (v - k_HeadY)) - k_HeadRadius;

            float down = Mathf.Clamp01((k_HeadY - v) / (k_HeadY - k_HemY));
            float halfWidth = Mathf.Lerp(k_HeadRadius, k_HemHalfWidth, down);
            float hem = k_HemY + 0.035f * Mathf.Sin(u * Mathf.PI * 6f + time * 2f);
            float body = Mathf.Max(Mathf.Max(Mathf.Abs(x) - halfWidth, hem - v), v - k_HeadY);

            float d = Mathf.Min(head, body);

            if (holes)
            {
                float eyes = Mathf.Min(Ellipse(u, v, 0.43f, 0.68f, 0.04f, 0.06f), Ellipse(u, v, 0.57f, 0.68f, 0.04f, 0.06f));
                float mouth = Ellipse(u, v, 0.5f, 0.56f, 0.035f, 0.05f);
                d = Mathf.Max(d, -Mathf.Min(eyes, mouth));
            }

            return 1f - Step(-soft, soft, d);
        }

        /// <summary>Whether (<paramref name="u"/>, <paramref name="v"/>) is in an eye or the mouth: dark, even where the sheet is bright.</summary>
        public static bool InHole(float u, float v)
        {
            return Ellipse(u, v, 0.43f, 0.68f, 0.04f, 0.06f) < 0f || Ellipse(u, v, 0.57f, 0.68f, 0.04f, 0.06f) < 0f ||
                   Ellipse(u, v, 0.5f, 0.56f, 0.035f, 0.05f) < 0f;
        }

        /// <summary>
        /// Shader-style smoothstep: 0 below <paramref name="edge0"/>, 1 above <paramref name="edge1"/>.
        /// Not <see cref="Mathf.SmoothStep"/>, which interpolates between its first two arguments.
        /// </summary>
        public static float Step(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Approximate signed distance to an axis-aligned ellipse: negative inside.</summary>
        static float Ellipse(float u, float v, float cu, float cv, float ru, float rv)
        {
            float a = (u - cu) / ru;
            float b = (v - cv) / rv;
            return (Mathf.Sqrt(a * a + b * b) - 1f) * Mathf.Min(ru, rv);
        }
    }
}
