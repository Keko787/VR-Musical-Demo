using UnityEngine;
using UnityEngine.Rendering;
using VRShootingGallery.TrackingDemo;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The sub bass: a glowing whale swimming a slow figure of eight just under the water on the floor.
    /// It swims faster and glows brighter as the low band swells, and every sub note sends a pulse of light
    /// down the spots on its back from head to tail — the low end, which is more felt than heard, made
    /// into something seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a flat mesh of its own.</b> The whale is a top-down silhouette — head, tapering body, tail
    /// flukes with a notch, two long flippers — built here in <see cref="Awake"/> and rebuilt every frame,
    /// like <see cref="RippleFloor"/>. Each vertex is a place on the body (how far back from the head, how
    /// far out from the spine), so the whole body follows the swim path like a train on a track: it bends
    /// round the turns instead of swinging through them as a rigid card, and it can never cut a corner
    /// out of the floor. A travelling wave across the path, growing toward the tail, is the swimming.
    /// </para>
    /// <para>
    /// <b>Why just above the water.</b> The water sheet is opaque, so the whale lies a centimetre over its
    /// rest surface: crests poke through and hide pieces of it, which reads as the whale under rippling
    /// water. It is drawn with the glow material and the shading in vertex colour — bright along the
    /// spine, fading toward the edges — so it lights the water rather than covering it.
    /// </para>
    /// </remarks>
    public sealed class WhalePerformer : Performer
    {
        const int k_Rows = 48;
        const int k_Across = 7;
        const int k_FinVertices = 8;
        const int k_Spots = 10;
        const int k_PathPoints = 512;
        const int k_PathSamples = 2048;
        const int k_Pulses = 4;

        /// <summary>The length the silhouette's proportions were drawn at; other lengths scale its width with it.</summary>
        const float k_ReferenceLength = 1.6f;

        static readonly Color k_Body = new(0.30f, 0.55f, 0.95f, 0.55f);
        static readonly Color k_BodyGlow = new(0.10f, 0.32f, 0.75f);
        static readonly Color k_Spot = new(0.60f, 0.95f, 1.00f, 0.5f);
        static readonly Color k_SpotGlow = new(0.35f, 0.90f, 1.00f);

        [Header("Path")]
        [SerializeField, Tooltip("Half the loop's span across the room, metres. The path is a figure of eight: the " +
            "whale crosses the middle of the floor under the inner performers and turns near each side wall.")]
        float m_PathHalfWidth = 1.1f;

        [SerializeField, Tooltip("The loop's full depth front to back, metres.")]
        float m_PathDepth = 1.2f;

        [SerializeField, Tooltip("Where the loop is centred front to back, metres from the room's centre.")]
        float m_PathCentreZ = -0.03f;

        [Header("Swim")]
        [SerializeField, Tooltip("Nose to fluke tips, metres.")]
        float m_Length = 1.6f;

        [SerializeField, Tooltip("Metres per second with no low end — between songs, or a song without a sub bass.")]
        float m_IdleSpeed = 0.12f;

        [SerializeField, Tooltip("Metres per second with the low band at full.")]
        float m_MaxSpeed = 0.5f;

        [SerializeField, Tooltip("Seconds for the speed to get halfway to a new target. Long, because a whale does not jerk.")]
        float m_SpeedHalfLife = 0.6f;

        [SerializeField, Tooltip("Sideways swing of the tail at the end of the body, metres.")]
        float m_Wave = 0.06f;

        [SerializeField, Tooltip("How much of a wave fits along the body. Under one: the body flexes as a whole rather than wriggling.")]
        float m_WavesPerBody = 0.9f;

        [SerializeField, Tooltip("Tail beats per metre travelled, so the beat speeds up with the swim.")]
        float m_BeatsPerMetre = 1.1f;

        [Header("Glow")]
        [SerializeField, Tooltip("Body glow with no low end.")]
        float m_IdleGlow = 0.45f;

        [SerializeField, Tooltip("Extra body glow with the low band at full.")]
        float m_LowGlow = 1.2f;

        [SerializeField, Tooltip("Seconds a note's pulse takes to run from the first spot to the last.")]
        float m_PulseTravel = 0.45f;

        [SerializeField, Tooltip("How long a spot's flash takes to halve, seconds.")]
        float m_PulseHalfLife = 0.16f;

        [Header("Splash")]
        [SerializeField, Range(0f, 1f), Tooltip("Sub notes at or above this velocity flick the tail hard enough to ripple the water.")]
        float m_SplashVelocity = 0.9f;

        [SerializeField] float m_SplashAmplitude = 0.01f;
        [SerializeField] float m_SplashRadius = 0.15f;

        [Header("Rig (set by Build)")]
        [SerializeField] RippleFloor m_Floor;

        [SerializeField, Tooltip("The water's rest surface, metres up in this transform's frame.")]
        float m_SurfaceY;

        [SerializeField, Tooltip("How far above the water's rest surface the whale lies. Crests higher than this hide it.")]
        float m_HeightAboveWater = 0.012f;

        [SerializeField] MeshFilter m_Body;
        [SerializeField] CreatureTint m_BodyTint;
        [SerializeField] Transform[] m_Spots = new Transform[0];
        [SerializeField] CreatureTint[] m_SpotTints = new CreatureTint[0];

        Mesh m_Mesh;
        Vector3[] m_Vertices;
        float[] m_U, m_Lateral, m_FinSweep;

        Vector3[] m_Path;
        float m_PathLength;

        float m_HeadS, m_Speed, m_Phase, m_Glow;

        readonly double[] m_PulseTime = new double[k_Pulses];
        readonly float[] m_PulseAmount = new float[k_Pulses];
        int m_NextPulse;
        double m_LastPulse = -1000.0;
        int m_LastFired = -1;
        float m_Seed;

        void Awake()
        {
            m_Seed = Motion.Hash01(GetInstanceID()) * 100f;
            BuildPath();
            ClearPulses();
            m_Speed = m_IdleSpeed;
            m_Glow = m_IdleGlow;

            if (m_Body != null)
            {
                m_Mesh = BuildMesh();
                m_Body.sharedMesh = m_Mesh;
            }
        }

        void OnDestroy()
        {
            if (m_Mesh != null)
                Destroy(m_Mesh);
        }

        protected override void OnSongChanged()
        {
            m_LastFired = -1;
            m_LastPulse = -1000.0;
            ClearPulses();
        }

        void ClearPulses()
        {
            for (int p = 0; p < k_Pulses; p++)
            {
                m_PulseTime[p] = -1000.0;
                m_PulseAmount[p] = 0f;
            }
        }

        // ================================================================== perform

        protected override void Perform(float deltaTime)
        {
            if (m_Path == null)
                return;

            float low = Mathf.Clamp01(Level(Band.Low));
            float clock = Clock + m_Seed;

            m_Speed = Motion.Damp(m_Speed, Mathf.Lerp(m_IdleSpeed, m_MaxSpeed, low), m_SpeedHalfLife, deltaTime);
            m_HeadS = Mathf.Repeat(m_HeadS + m_Speed * deltaTime, m_PathLength);

            // Accumulated, so the tail never skips a beat when the speed changes.
            m_Phase = Mathf.Repeat(m_Phase + (0.18f + m_Speed * m_BeatsPerMetre) * deltaTime, 1f);

            FireNotes();
            PoseBody();
            PoseSpots(low, clock);

            m_Glow = Motion.Damp(m_Glow, m_IdleGlow + m_LowGlow * low, 0.25f, deltaTime);
            if (m_BodyTint != null)
                m_BodyTint.Glow = m_Glow * (1f + 0.08f * Motion.Wave(clock, 0.15f)) * Intensity;
        }

        /// <summary>One pulse per sub note, on the first frame at or after it; a hard one flicks the tail into the water.</summary>
        void FireNotes()
        {
            if (!Live)
                return;

            int last = Notes.LastAtOrBefore(SongTime);
            for (int i = m_LastFired + 1; i <= last; i++)
            {
                var note = Notes[i];
                if (SongTime - note.Time > 0.3)
                    continue;

                // Capped so notes faster than the flash limit pulse more softly, not more often.
                double gap = note.Time - m_LastPulse;
                m_PulseTime[m_NextPulse] = note.Time;
                m_PulseAmount[m_NextPulse] = note.Velocity * Mathf.Clamp01((float)gap * MaxFlashHz);
                m_NextPulse = (m_NextPulse + 1) % k_Pulses;
                m_LastPulse = note.Time;

                if (note.Velocity >= m_SplashVelocity && m_Floor != null)
                    m_Floor.Splash(transform.TransformPoint(BodyPoint(0.95f, 0f)),
                        m_SplashAmplitude * Mathf.Clamp(Intensity, 0.3f, 1.5f), m_SplashRadius);
            }

            if (last > m_LastFired)
                m_LastFired = last;
        }

        void PoseBody()
        {
            if (m_Mesh == null)
                return;

            float sweep = 0.025f * Mathf.Sin(m_Phase * 2f * Mathf.PI);
            for (int v = 0; v < m_Vertices.Length; v++)
                m_Vertices[v] = BodyPoint(m_U[v] + m_FinSweep[v] * sweep, m_Lateral[v]);

            m_Mesh.SetVertices(m_Vertices, 0, m_Vertices.Length, MeshUpdateFlags.DontRecalculateBounds);
        }

        void PoseSpots(float low, float clock)
        {
            float baseGlow = 0.35f + 0.8f * low;
            int count = Mathf.Min(m_Spots.Length, m_SpotTints.Length);

            for (int i = 0; i < count; i++)
            {
                float u = SpotU(i);
                if (m_Spots[i] != null)
                    m_Spots[i].localPosition = BodyPoint(u, SpotLateral(i, u)) + Vector3.up * 0.004f;

                // A slow shimmer running down the back, plus every pulse still travelling.
                float glow = baseGlow * (1f + 0.15f * Motion.Wave(clock, 0.12f, -i * 0.07f));
                for (int p = 0; p < k_Pulses; p++)
                    glow += 2f * m_PulseAmount[p] * Motion.Pulse(SongTime - m_PulseTime[p] - u * m_PulseTravel, 0.04f, m_PulseHalfLife);

                if (m_SpotTints[i] != null)
                    m_SpotTints[i].Glow = glow * Intensity;
            }
        }

        static float SpotU(int i) => 0.1f + i * 0.068f;

        float SpotLateral(int i, float u) => ((i & 1) == 0 ? 0.045f : -0.045f) * (1f - u) * (m_Length / k_ReferenceLength);

        /// <summary>
        /// A point on the body in this transform's frame: <paramref name="u"/> along it (0 nose, 1 flukes),
        /// <paramref name="lateral"/> metres out from the spine. The spine follows the path behind the head
        /// and swings across it in the swimming wave.
        /// </summary>
        Vector3 BodyPoint(float u, float lateral)
        {
            var spine = PathAt(m_HeadS - u * m_Length, out var tangent);
            var across = new Vector3(tangent.z, 0f, -tangent.x);
            float bend = m_Wave * (0.12f + u * u) * Mathf.Sin(2f * Mathf.PI * (m_WavesPerBody * u - m_Phase));

            var p = spine + across * (bend + lateral);
            p.y = m_SurfaceY + m_HeightAboveWater;
            return p;
        }

        // ================================================================== path

        /// <summary>
        /// A figure of eight (a lemniscate of Gerono) resampled to equal steps along its length, so a
        /// distance along it is a constant speed and the body keeps its length round the turns.
        /// </summary>
        void BuildPath()
        {
            var fine = new Vector3[k_PathSamples + 1];
            var length = new float[k_PathSamples + 1];
            for (int i = 0; i <= k_PathSamples; i++)
            {
                float theta = i / (float)k_PathSamples * 2f * Mathf.PI;
                fine[i] = new Vector3(m_PathHalfWidth * Mathf.Sin(theta), 0f,
                    m_PathCentreZ + m_PathDepth * Mathf.Sin(theta) * Mathf.Cos(theta));
                if (i > 0)
                    length[i] = length[i - 1] + Vector3.Distance(fine[i - 1], fine[i]);
            }

            m_PathLength = Mathf.Max(0.01f, length[k_PathSamples]);
            m_Path = new Vector3[k_PathPoints];

            int j = 0;
            for (int i = 0; i < k_PathPoints; i++)
            {
                float target = i / (float)k_PathPoints * m_PathLength;
                while (j < k_PathSamples - 1 && length[j + 1] < target)
                    j++;

                float span = Mathf.Max(1e-6f, length[j + 1] - length[j]);
                m_Path[i] = Vector3.Lerp(fine[j], fine[j + 1], (target - length[j]) / span);
            }
        }

        Vector3 PathAt(float s, out Vector3 tangent)
        {
            float f = Mathf.Repeat(s, m_PathLength) / m_PathLength * k_PathPoints;
            int i = Mathf.FloorToInt(f);
            float t = f - i;
            i %= k_PathPoints;
            int next = (i + 1) % k_PathPoints;

            var a = m_Path[i];
            var b = m_Path[next];
            var d = b - a;
            tangent = d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
            return Vector3.Lerp(a, b, t);
        }

        // ================================================================== mesh

        /// <summary>
        /// Half the body's width at <paramref name="u"/>, at the reference length: a round snout, a broad
        /// body tapering to the tail stock, and the flukes spreading out past it.
        /// </summary>
        static float HalfWidth(float u)
        {
            float body = 0.19f * Mathf.Sqrt(Mathf.Clamp01(u / 0.1f)) * (1f - 0.8f * Ramp(0.35f, 0.86f, u));
            float fluke = 0.27f * Ramp(0.86f, 0.98f, u);
            return Mathf.Max(body, fluke);
        }

        static float Ramp(float from, float to, float x) => Motion.Smooth01((x - from) / (to - from));

        /// <summary>
        /// The silhouette as a grid along the body plus two flipper quads. Every vertex keeps its place on
        /// the body (<see cref="m_U"/>, <see cref="m_Lateral"/>); <see cref="PoseBody"/> lays them on the path.
        /// Vertex colour is the shading: bright along the spine, dimmer and more see-through at the edges
        /// and toward the tail.
        /// </summary>
        Mesh BuildMesh()
        {
            float scale = m_Length / k_ReferenceLength;
            int bodyCount = k_Rows * k_Across;
            int count = bodyCount + k_FinVertices;

            m_U = new float[count];
            m_Lateral = new float[count];
            m_FinSweep = new float[count];
            m_Vertices = new Vector3[count];
            var colors = new Color[count];
            var normals = new Vector3[count];
            var uvs = new Vector2[count];

            for (int r = 0; r < k_Rows; r++)
            {
                float u = r / (k_Rows - 1f);
                for (int a = 0; a < k_Across; a++)
                {
                    int c = r * k_Across + a;
                    float v = -1f + 2f * a / (k_Across - 1f);
                    float edge = Mathf.Abs(v);

                    // The flukes' trailing edge is pulled forward in the middle: the notch.
                    float notch = 0.03f * (1f - edge) * Ramp(0.95f, 1f, u);
                    m_U[c] = u - notch;
                    m_Lateral[c] = v * HalfWidth(u) * scale;

                    float bright = Mathf.Lerp(1f, 0.3f, Mathf.Pow(edge, 1.5f)) * Mathf.Lerp(1f, 0.75f, Ramp(0.6f, 1f, u));
                    colors[c] = new Color(bright, bright, bright, Mathf.Lerp(1f, 0.25f, edge * edge));
                    uvs[c] = new Vector2(u, (v + 1f) * 0.5f);
                }
            }

            // Flippers: root front, root back, tip back, tip front — long and swept back, like a humpback's.
            float[] finU = { 0.24f, 0.34f, 0.56f, 0.52f };
            float[] finOut = { HalfWidth(0.24f) * 0.85f, HalfWidth(0.34f) * 0.85f, 0.40f, 0.44f };
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                for (int k = 0; k < 4; k++)
                {
                    int c = bodyCount + side * 4 + k;
                    bool tip = k >= 2;
                    m_U[c] = finU[k];
                    m_Lateral[c] = s * finOut[k] * scale;
                    m_FinSweep[c] = tip ? 1f : 0f;
                    float bright = tip ? 0.4f : 0.85f;
                    colors[c] = new Color(bright, bright, bright, tip ? 0.35f : 0.9f);
                    uvs[c] = new Vector2(finU[k], tip ? (side == 0 ? 0f : 1f) : 0.5f);
                }
            }

            for (int v = 0; v < count; v++)
            {
                normals[v] = Vector3.up;
                m_Vertices[v] = BodyPoint(m_U[v], m_Lateral[v]);
            }

            var triangles = new int[(k_Rows - 1) * (k_Across - 1) * 6 + 2 * 6];
            int t = 0;
            for (int r = 0; r < k_Rows - 1; r++)
            {
                for (int a = 0; a < k_Across - 1; a++)
                {
                    int v00 = r * k_Across + a;
                    int v01 = v00 + 1;
                    int v10 = v00 + k_Across;
                    int v11 = v10 + 1;

                    triangles[t++] = v00; triangles[t++] = v01; triangles[t++] = v10;
                    triangles[t++] = v01; triangles[t++] = v11; triangles[t++] = v10;
                }
            }

            for (int side = 0; side < 2; side++)
            {
                int f = bodyCount + side * 4;
                triangles[t++] = f; triangles[t++] = f + 1; triangles[t++] = f + 2;
                triangles[t++] = f; triangles[t++] = f + 2; triangles[t++] = f + 3;
            }

            var mesh = new Mesh { name = "Whale (runtime)" };
            mesh.MarkDynamic();
            mesh.SetVertices(m_Vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);

            // Fixed rather than recalculated per frame: the whole floor, so the whale is never culled
            // wherever it swims.
            mesh.bounds = new Bounds(new Vector3(0f, m_SurfaceY, m_PathCentreZ),
                new Vector3(2f * m_PathHalfWidth + 2f, 0.4f, m_PathDepth + 2f));
            return mesh;
        }

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            m_Floor = kit.Floor;

            // The builder puts this at the room's origin, so the room frame is this frame.
            var frame = kit.Room != null ? kit.Room : transform;
            m_SurfaceY = kit.Floor != null ? frame.InverseTransformPoint(kit.Floor.transform.position).y : 0f;

            // The body's mesh is made at runtime (Awake); here only its renderer, with the vertex-colour
            // glow material and its tint.
            var body = kit.Node(transform, "Whale_Body", Vector3.zero);
            m_Body = body.gameObject.AddComponent<MeshFilter>();
            var renderer = body.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = kit.Whale;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            m_BodyTint = body.gameObject.AddComponent<CreatureTint>();
            m_BodyTint.Set(k_Body, k_BodyGlow);

            // Spots laid out along a straight whale facing +Z for the Editor; Perform puts them on the body.
            float y = m_SurfaceY + m_HeightAboveWater + 0.004f;
            m_Spots = new Transform[k_Spots];
            m_SpotTints = new CreatureTint[k_Spots];
            for (int i = 0; i < k_Spots; i++)
            {
                float u = SpotU(i);
                var position = new Vector3(SpotLateral(i, u), y, m_Length * (0.5f - u));
                m_SpotTints[i] = kit.Glowing(transform, $"Spot_{i}", kit.Sphere, position, new Vector3(0.045f, 0.01f, 0.045f), k_Spot, k_SpotGlow);
                m_Spots[i] = m_SpotTints[i].transform;
            }
        }
    }
}
