using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>A group of palms: where, how many, how tall, and which way the wind has bent them.</summary>
    [System.Serializable]
    public struct PalmGrove
    {
        public Vector3 Centre;

        [Tooltip("Half-extents of the area the palms stand in (x, z).")]
        public Vector2 Spread;

        public int Count;

        [Tooltip("Shortest and tallest, metres.")]
        public Vector2 Height;

        [Tooltip("Which way they lean, degrees about the vertical from straight ahead (+z), clockwise from above.")]
        public float LeanToward;

        [Tooltip("How far they lean at most, degrees from upright.")]
        public float Lean;
    }

    /// <summary>
    /// Palm trees for the beach, all in one mesh: a curved, banded trunk; a crown of fronds, each a
    /// strip folded down along its spine with a ragged edge for the leaflets, rising then drooping; a
    /// few coconuts. The fronds carry sway weights in their third UV channel, so the land shader
    /// waves them in the sea wind — harder with the mids — while the trunks barely move.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimPalms : MonoBehaviour
    {
        [SerializeField] PalmGrove[] m_Groves;

        [SerializeField, Tooltip("The ground the palms stand on.")]
        SimSurface m_Ground;

        [SerializeField] int m_Seed = 3;
        [SerializeField, Range(5, 14)] int m_Fronds = 9;
        [SerializeField] Color m_Trunk = new(0.5f, 0.41f, 0.31f);
        [SerializeField] Color m_TrunkRing = new(0.36f, 0.29f, 0.22f);
        [SerializeField] Color m_Frond = new(0.2f, 0.46f, 0.2f);
        [SerializeField] Color m_FrondTip = new(0.42f, 0.62f, 0.25f);
        [SerializeField] Color m_Coconut = new(0.35f, 0.25f, 0.12f);

        /// <summary>How much the top of a trunk sways, as a fraction of a frond tip's.</summary>
        const float k_TrunkTop = 0.12f;

        void Awake() => Generate();

        void OnDestroy()
        {
            if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
                (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                SimMeshes.Destroy(filter.sharedMesh);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            if (m_Groves == null)
                return;

            var random = new System.Random(m_Seed);
            float Range(float a, float b) => a + (b - a) * (float)random.NextDouble();

            var builder = new SimMeshes.Builder();
            foreach (var grove in m_Groves)
            {
                for (int i = 0; i < grove.Count; i++)
                {
                    float angle = Range(0f, Mathf.PI * 2f);
                    float reach = Mathf.Sqrt(Range(0f, 1f));
                    var at = grove.Centre + new Vector3(Mathf.Cos(angle) * grove.Spread.x * reach, 0f,
                        Mathf.Sin(angle) * grove.Spread.y * reach);
                    at.y = GroundAt(at);

                    float toward = (grove.LeanToward + Range(-40f, 40f)) * Mathf.Deg2Rad;
                    var leanDirection = new Vector3(Mathf.Sin(toward), 0f, Mathf.Cos(toward));
                    float lean = Range(0.25f, 1f) * grove.Lean * Mathf.Deg2Rad;
                    float height = Range(grove.Height.x, grove.Height.y);

                    Palm(builder, at, height, leanDirection, lean, Range(0f, 6.283f), Range);
                }
            }

            SimMeshes.Assign(GetComponent<MeshFilter>(), builder.ToMesh($"{name}_Palms"));
        }

        float GroundAt(Vector3 local)
        {
            if (m_Ground == null)
                return local.y;

            var world = transform.TransformPoint(local);
            var onGround = m_Ground.transform.InverseTransformPoint(world);
            onGround.y = m_Ground.HeightAt(onGround.x, onGround.z);
            return transform.InverseTransformPoint(m_Ground.transform.TransformPoint(onGround)).y;
        }

        void Palm(SimMeshes.Builder b, Vector3 foot, float height, Vector3 leanDirection, float lean, float phase,
            System.Func<float, float, float> range)
        {
            const int rings = 10;
            const int sides = 7;
            float radius = height * 0.026f;
            float bend = height * Mathf.Tan(lean);

            // The trunk leans, curving more toward the top, the way a palm grows out of the wind.
            Vector3 Spine(float s) => foot + Vector3.up * (height * s) + leanDirection * (bend * (0.2f * s + 0.8f * s * s));

            for (int i = 0; i < rings; i++)
            {
                float s = i / (float)(rings - 1);
                var centre = Spine(s);
                var tangent = (Spine(s + 0.01f) - Spine(s - 0.01f)).normalized;
                var u = Vector3.Cross(tangent, Vector3.forward).normalized;
                if (u.sqrMagnitude < 0.01f)
                    u = Vector3.Cross(tangent, Vector3.right).normalized;
                var v = Vector3.Cross(tangent, u);

                float r = radius * (1.25f - 0.45f * s + 0.6f * Mathf.Exp(-s * 12f));
                var colour = i % 2 == 0 ? m_Trunk : m_TrunkRing;
                var sway = new Vector2(k_TrunkTop * s * s, phase);
                for (int j = 0; j < sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    b.Add(centre + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r, colour, Vector2.zero, Vector2.zero, sway);
                }
            }

            int first = b.Count - rings * sides;
            for (int i = 0; i < rings - 1; i++)
                b.Band(first + i * sides, first + (i + 1) * sides, sides);

            var top = Spine(1f);
            var topSway = new Vector2(k_TrunkTop, phase);
            SimMeshes.Icosphere(b, top, Vector3.one * (radius * 1.6f), m_TrunkRing, Vector2.zero, topSway);

            for (int c = 0; c < 3; c++)
            {
                float a = c * 2.1f + phase;
                var nut = top + new Vector3(Mathf.Cos(a), -1.2f, Mathf.Sin(a)) * (radius * 1.4f);
                SimMeshes.Icosphere(b, nut, Vector3.one * (radius * 0.9f), m_Coconut, Vector2.zero, topSway);
            }

            for (int k = 0; k < m_Fronds; k++)
            {
                float azimuth = (k + range(-0.3f, 0.3f)) / m_Fronds * Mathf.PI * 2f + phase;
                Frond(b, top, azimuth, height * range(0.32f, 0.44f), range(0.3f, 0.75f), phase + k * 0.7f);
            }
        }

        /// <summary>
        /// One frond: out from the crown, up a little and then drooping, folded down along its spine
        /// so cel shading gives it a lit side and a shaded side, its edge cut ragged for the leaflets.
        /// </summary>
        void Frond(SimMeshes.Builder b, Vector3 top, float azimuth, float length, float rise, float phase)
        {
            const int segments = 16;
            var out_ = new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth));
            var side = Vector3.Cross(Vector3.up, out_).normalized;

            int start = b.Count;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                var spine = top + out_ * (length * u * 0.95f) + Vector3.up * (length * (rise * u - 0.95f * u * u));

                float width = length * 0.17f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.8f)), 0.8f);
                width *= 1f - 0.4f * (i % 2);                           // the ragged edge of the leaflets
                var droop = Vector3.up * (width * 0.4f);

                var colour = Color.Lerp(m_Frond, m_FrondTip, u);
                var sway = new Vector2(k_TrunkTop + (1f - k_TrunkTop) * u, phase);
                b.Add(spine + side * width - droop, colour, Vector2.zero, Vector2.zero, sway);
                b.Add(spine, Color.Lerp(colour, Color.white, 0.05f), Vector2.zero, Vector2.zero, sway);
                b.Add(spine - side * width - droop, colour, Vector2.zero, Vector2.zero, sway);
            }

            for (int i = 0; i < segments; i++)
            {
                int l = start + i * 3;
                int c = l + 1;
                int r = l + 2;
                int l2 = l + 3;
                int c2 = l + 4;
                int r2 = l + 5;
                b.Triangle(l, c, l2);
                b.Triangle(c, c2, l2);
                b.Triangle(c, r, c2);
                b.Triangle(r, r2, c2);
            }
        }
    }
}
