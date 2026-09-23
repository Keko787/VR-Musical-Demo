using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// The ghost, made of the music — a spectre drawn by spectral analysis. A sheet of points over
    /// nothing: the treble in the head, the bass in the billowing hem, the live waveform running
    /// round the hem's edge, two eye holes where there are no points at all, and a trail of
    /// ectoplasm behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is always somewhere in the room — the curtains, candles, shadows and portraits all react
    /// to where it is — but it is only <em>seen</em> when the haunting reveals it, and as a faint
    /// shimmer when the room is deeply possessed. It travels on a critically damped spring toward
    /// the target the director gives it, dances around that point in a figure-eight that widens with
    /// the music, bobs and hops on the beat, twirls now and then while it is out, and always keeps
    /// its distance from the tracked viewer's head: too close and stereo breaks down.
    /// </para>
    /// <para>
    /// The points are written by hand into a particle system, the same way the visualizer's clouds
    /// are, in the ghost's own space so the whole figure turns and leans as one.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-70)] // after the director, before the things that react to where it is
    [RequireComponent(typeof(ParticleSystem))]
    public class SpectreGhost : MonoBehaviour
    {
        [SerializeField] Haunting m_Haunting;

        [Header("Form")]
        [SerializeField, Tooltip("Head top to hem, metres.")]
        float m_Height = 0.8f;

        [SerializeField] float m_HeadRadius = 0.15f;
        [SerializeField] float m_HemRadius = 0.3f;
        [SerializeField, Range(200, 6000)] int m_SheetPoints = 1500;
        [SerializeField, Range(0, 1000)] int m_TrailPoints = 320;

        [SerializeField, Range(0, 200), Tooltip("Points outlining the eyes and mouth. Holes alone vanish among sparse points; " +
            "a bright rim round each makes the face read from across the room.")]
        int m_FacePoints = 108;
        [SerializeField, Tooltip("Sprite size of a point. Small and sparse enough that the eye holes stay open.")]
        float m_PointSize = 0.013f;

        [Header("Travel")]
        [SerializeField, Tooltip("Spring rate toward the target, per second. Higher arrives sooner.")]
        float m_Stiffness = 1.8f;

        [SerializeField, Tooltip("Top speed, metres per second.")]
        float m_MaxSpeed = 1.1f;

        [SerializeField, Tooltip("Closest it comes to the tracked head, metres.")]
        float m_KeepAway = 0.8f;

        [Header("Dance")]
        [SerializeField, Tooltip("Radius of the figure-eight it dances around its target at full energy, metres.")]
        float m_Dance = 0.22f;

        [SerializeField] float m_Bob = 0.035f;
        [SerializeField, Tooltip("Metres it hops on a strong beat.")]
        float m_BeatHop = 0.07f;

        [SerializeField, Range(0f, 1f), Tooltip("Chance a beat sets it twirling, while it is on show.")]
        float m_TwirlChance = 0.25f;

        [SerializeField, Tooltip("Degrees it sways with the low mids.")]
        float m_Sway = 8f;

        [Header("Look")]
        [SerializeField, Tooltip("Colour by height: hem (bass) at 0, head (treble) at 1. Left plain white for pale violet to white.")]
        Gradient m_Palette;

        [SerializeField] float m_Brightness = 1.1f;

        [SerializeField, Range(0f, 0.3f), Tooltip("How visible it is before a reveal, when the room is deeply possessed: a shimmer, like heat.")]
        float m_Shimmer = 0.07f;

        static readonly int k_Color = Shader.PropertyToID("_Color");
        const int k_History = 64;

        // The face, as places on the sheet: centre angle from the front and half-width in degrees,
        // centre height and half-height in s.
        const float k_EyeAngle = 20f, k_EyeHalfWidth = 14f, k_EyeS = 0.16f, k_EyeHalfHeight = 0.08f;
        const float k_MouthHalfWidth = 13f, k_MouthS = 0.3f, k_MouthHalfHeight = 0.04f;
        const float k_HistoryRate = 30f;

        ParticleSystem m_System;
        ParticleSystemRenderer m_Renderer;
        ParticleSystem.Particle[] m_Particles;
        MaterialPropertyBlock m_Block;
        System.Random m_Random;

        // The sheet: each point's place on it, as height (0 head top, 1 hem) and angle round it.
        float[] m_S;
        float[] m_A;
        float[] m_Jitter;

        Vector3 m_Centre;
        Vector3 m_Velocity;
        Vector3 m_Target;
        float m_DancePhase;
        float m_Hop;
        float m_HopVelocity;
        float m_Yaw;
        float m_TwirlLeft;

        readonly Vector3[] m_History = new Vector3[k_History];
        int m_HistoryHead;
        int m_HistoryCount;
        float m_HistoryClock;

        public Vector3 Position => transform.position;
        public Vector3 Velocity => m_Velocity;

        public void SetTarget(Vector3 world) => m_Target = world;

        void Awake()
        {
            m_System = GetComponent<ParticleSystem>();
            m_Renderer = GetComponent<ParticleSystemRenderer>();
            ManualParticles.Configure(m_System, m_Renderer);

            int count = m_SheetPoints + m_TrailPoints + m_FacePoints;
            ManualParticles.Reserve(m_System, count);
            m_Particles = new ParticleSystem.Particle[count];
            for (int i = 0; i < count; i++)
            {
                m_Particles[i].startLifetime = ManualParticles.Lifetime;
                m_Particles[i].remainingLifetime = ManualParticles.Lifetime;
            }

            m_Block = new MaterialPropertyBlock();
            m_Random = new System.Random(20260923);

            if (Palettes.IsUnset(m_Palette))
            {
                m_Palette = new Gradient();
                m_Palette.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.45f, 0.35f, 1f), 0f),
                        new GradientColorKey(new Color(0.4f, 0.8f, 1f), 0.5f),
                        new GradientColorKey(new Color(0.9f, 1f, 1f), 1f),
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            }

            LayOutSheet();

            m_Centre = transform.position;
            m_Target = m_Centre;
            m_Yaw = transform.eulerAngles.y;
        }

        void OnEnable()
        {
            if (m_System != null && !m_System.isPlaying)
                m_System.Play();
        }

        /// <summary>
        /// Spreads the points evenly over the sheet by area: rings down its height, each with a
        /// share of the points in proportion to its circumference, so the wide hem is no sparser
        /// than the head.
        /// </summary>
        void LayOutSheet()
        {
            m_S = new float[m_SheetPoints];
            m_A = new float[m_SheetPoints];
            m_Jitter = new float[m_SheetPoints];

            const int rings = 48;
            var weight = new float[rings];
            float total = 0f;
            for (int r = 0; r < rings; r++)
            {
                weight[r] = Radius((r + 0.5f) / rings, 0f);
                total += weight[r];
            }

            int i = 0;
            for (int r = 0; r < rings && i < m_SheetPoints; r++)
            {
                int share = r == rings - 1 ? m_SheetPoints - i : Mathf.RoundToInt(m_SheetPoints * weight[r] / total);
                for (int k = 0; k < share && i < m_SheetPoints; k++, i++)
                {
                    m_S[i] = (r + (float)m_Random.NextDouble()) / rings;
                    m_A[i] = (k + (float)m_Random.NextDouble() * 0.8f) / Mathf.Max(1, share) * Mathf.PI * 2f;
                    m_Jitter[i] = (float)m_Random.NextDouble();
                }
            }

            for (; i < m_SheetPoints; i++)
            {
                m_S[i] = (float)m_Random.NextDouble();
                m_A[i] = (float)m_Random.NextDouble() * Mathf.PI * 2f;
                m_Jitter[i] = (float)m_Random.NextDouble();
            }
        }

        /// <summary>The sheet's radius at height <paramref name="s"/>: a round head, then a skirt widening to the hem.</summary>
        float Radius(float s, float bass)
        {
            const float head = 0.3f;
            if (s < head)
            {
                float polar = s / head * Mathf.PI * 0.5f;
                return m_HeadRadius * Mathf.Sin(Mathf.Max(polar, 0.05f));
            }

            float t = (s - head) / (1f - head);
            return Mathf.Lerp(m_HeadRadius, m_HemRadius, Mathf.Pow(t, 0.8f)) * (1f + 0.35f * bass * t * t);
        }

        /// <summary>Height of the sheet at <paramref name="s"/>, from the ghost's centre.</summary>
        float Height(float s)
        {
            const float head = 0.3f;
            float headCentre = m_Height * 0.5f - m_HeadRadius;
            if (s < head)
                return headCentre + m_HeadRadius * Mathf.Cos(s / head * Mathf.PI * 0.5f);

            float t = (s - head) / (1f - head);
            return headCentre - (m_Height - m_HeadRadius) * t;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f || m_Haunting == null)
                return;

            Travel(dt);
            Dance(dt);
            Record(dt);
            Draw();
        }

        // ------------------------------------------------------------------ motion

        void Travel(float dt)
        {
            // Critically damped: arrives without overshooting, and never faster than a drift.
            float k = m_Stiffness;
            var accel = k * k * (m_Target - m_Centre) - 2f * k * m_Velocity;
            m_Velocity += accel * dt;
            if (m_Velocity.magnitude > m_MaxSpeed)
                m_Velocity = m_Velocity.normalized * m_MaxSpeed;

            m_Centre += m_Velocity * dt;

            // Out of the tracked viewer's face.
            var head = m_Haunting.HeadPosition;
            var fromHead = m_Centre - head;
            if (fromHead.magnitude < m_KeepAway)
            {
                var push = fromHead.sqrMagnitude > 1e-4f ? fromHead.normalized : Vector3.forward;
                m_Centre = head + push * m_KeepAway;
                m_Velocity -= Vector3.Project(m_Velocity, push) * (Vector3.Dot(m_Velocity, push) < 0f ? 1f : 0f);
            }

            m_Centre = m_Haunting.ClampGhost(m_Centre);
        }

        void Dance(float dt)
        {
            float energy = m_Haunting.Level(Listen.Loudness);
            float reveal = m_Haunting.Reveal;
            float time = Time.time;

            // A figure-eight around where it is, wider and quicker with the music.
            m_DancePhase += dt * (0.5f + 1.3f * energy);
            float reach = m_Dance * (0.3f + 0.7f * energy);
            var offset = new Vector3(Mathf.Sin(m_DancePhase), 0f, 0.5f * Mathf.Sin(2f * m_DancePhase)) * reach;

            if (m_Haunting.Beat)
                m_HopVelocity += m_BeatHop * (0.5f + 0.5f * m_Haunting.BeatStrength) * 9f;

            m_HopVelocity += (-60f * m_Hop - 9f * m_HopVelocity) * dt;
            m_Hop += m_HopVelocity * dt;

            float bob = m_Bob * Mathf.Sin(time * 2.6f);
            transform.position = m_Centre + offset + Vector3.up * (bob + m_Hop);

            // Face the viewer, turning slowly; twirl now and then while on show.
            var toHead = m_Haunting.HeadPosition - transform.position;
            toHead.y = 0f;
            if (toHead.sqrMagnitude > 1e-4f)
            {
                float facing = Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
                m_Yaw = Mathf.MoveTowardsAngle(m_Yaw, facing, 60f * dt);
            }

            if (m_Haunting.Beat && reveal > 0.5f && m_TwirlLeft <= 0f &&
                m_Random.NextDouble() < m_TwirlChance * m_Haunting.BeatStrength)
                m_TwirlLeft = 360f;

            if (m_TwirlLeft > 0f)
            {
                float turn = Mathf.Min(m_TwirlLeft, 330f * dt);
                m_Yaw += turn;
                m_TwirlLeft -= turn;
            }

            // Lean into its travel, and sway with the low mids.
            var travel = new Vector3(m_Velocity.x, 0f, m_Velocity.z);
            var lean = Quaternion.identity;
            if (travel.sqrMagnitude > 1e-4f)
                lean = Quaternion.AngleAxis(14f * travel.magnitude / m_MaxSpeed, Vector3.Cross(Vector3.up, travel.normalized));

            float sway = m_Sway * m_Haunting.Level(Listen.LowMid) * Mathf.Sin(time * 2.1f);
            transform.rotation = lean * Quaternion.AngleAxis(m_Yaw, Vector3.up) * Quaternion.AngleAxis(sway, Vector3.forward);
        }

        void Record(float dt)
        {
            m_HistoryClock += dt;
            float interval = 1f / k_HistoryRate;
            while (m_HistoryClock >= interval)
            {
                m_HistoryClock -= interval;
                m_History[m_HistoryHead] = transform.TransformPoint(new Vector3(0f, Height(1f), 0f));
                m_HistoryHead = (m_HistoryHead + 1) % k_History;
                m_HistoryCount = Mathf.Min(m_HistoryCount + 1, k_History);
            }
        }

        // ------------------------------------------------------------------ drawing

        void Draw()
        {
            float possession = m_Haunting.Possession;
            float shimmer = m_Shimmer * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.85f, possession)) *
                            (0.6f + 0.4f * Mathf.Sin(Time.time * 1.3f));
            float seen = Mathf.Max(Mathf.SmoothStep(0f, 1f, m_Haunting.Reveal), shimmer);

            m_Renderer.enabled = seen > 0.003f;
            if (!m_Renderer.enabled)
                return;

            var analyser = m_Haunting.Analyser;
            var wave = analyser != null ? analyser.Waveform : null;
            int bands = analyser != null ? analyser.BandCount : 0;
            float bass = m_Haunting.Level(Listen.Bass);
            float time = Time.time;

            for (int i = 0; i < m_SheetPoints; i++)
            {
                float s = m_S[i];
                float a = m_A[i];
                float level = bands > 0 ? analyser.BandAt((1f - s) * (bands - 1)) : 0f;

                ref var p = ref m_Particles[i];

                // The face is holes: no points at the eyes and the open mouth on the face (+Z) — nor
                // on the back of the head behind them, which would otherwise show straight through,
                // the points being light rather than solid.
                float degrees = a * Mathf.Rad2Deg;
                float fromFront = Mathf.Abs(Mathf.DeltaAngle(degrees, 90f));
                float fromBack = Mathf.Abs(Mathf.DeltaAngle(degrees, 270f));
                bool eyes = Mathf.Abs(s - k_EyeS) < k_EyeHalfHeight &&
                            (Mathf.Abs(fromFront - k_EyeAngle) < k_EyeHalfWidth || Mathf.Abs(fromBack - k_EyeAngle) < k_EyeHalfWidth);
                bool mouth = Mathf.Abs(s - k_MouthS) < k_MouthHalfHeight && (fromFront < k_MouthHalfWidth || fromBack < k_MouthHalfWidth);
                if (eyes || mouth)
                {
                    p.startSize = 0f;
                    continue;
                }

                float r = Radius(s, bass) + 0.015f * level;
                float y = Height(s);

                // The hem: billowing, and edged with the live waveform running round it.
                float hem = s * s * s;
                y += 0.03f * Mathf.Sin(a * 3f + time * 2.2f) * s * s * (1f + bass);
                if (wave != null && wave.Length > 0)
                    y += 0.06f * hem * wave[Mathf.Clamp((int)(a / (Mathf.PI * 2f) * (wave.Length - 1)), 0, wave.Length - 1)];

                float wisp = 0.006f * (m_Jitter[i] - 0.5f);
                p.position = new Vector3(Mathf.Cos(a) * (r + wisp), y, Mathf.Sin(a) * (r + wisp));
                p.startSize = m_PointSize * (0.8f + 0.6f * level);
                p.startColor = m_Palette.Evaluate(1f - s) * (0.3f + 0.6f * level);
                p.remainingLifetime = ManualParticles.Lifetime;
            }

            DrawTrail();
            DrawFace();

            m_System.SetParticles(m_Particles, m_Particles.Length);
            float gain = m_Brightness * seen;
            m_Block.SetColor(k_Color, new Color(gain, gain, gain, 1f));
            m_Renderer.SetPropertyBlock(m_Block);
        }

        /// <summary>
        /// A bright rim round each eye hole and the mouth, a little proud of the sheet: the cartoon
        /// ghost's face, which is what makes a cloud of points read as someone looking at you.
        /// </summary>
        void DrawFace()
        {
            if (m_FacePoints == 0)
                return;

            int offset = m_SheetPoints + m_TrailPoints;
            int perEye = m_FacePoints * 3 / 8;
            float treble = m_Haunting.Level(Listen.High);
            var rim = Color.Lerp(m_Palette.Evaluate(1f), Color.white, 0.5f) * (0.8f + 0.4f * treble);

            for (int j = 0; j < m_FacePoints; j++)
            {
                float angle, halfWidth, s, halfHeight, t;
                if (j < perEye * 2)
                {
                    int side = j < perEye ? -1 : 1;
                    t = (j % perEye) / (float)perEye * Mathf.PI * 2f;
                    angle = 90f + side * k_EyeAngle;
                    halfWidth = k_EyeHalfWidth;
                    s = k_EyeS;
                    halfHeight = k_EyeHalfHeight;
                }
                else
                {
                    int count = m_FacePoints - perEye * 2;
                    t = (j - perEye * 2) / (float)Mathf.Max(1, count) * Mathf.PI * 2f;
                    angle = 90f;
                    halfWidth = k_MouthHalfWidth;
                    s = k_MouthS;
                    halfHeight = k_MouthHalfHeight;
                }

                float a = (angle + halfWidth * Mathf.Cos(t)) * Mathf.Deg2Rad;
                float sAt = s + halfHeight * Mathf.Sin(t);
                float r = Radius(sAt, 0f) + 0.006f;

                ref var p = ref m_Particles[offset + j];
                p.position = new Vector3(Mathf.Cos(a) * r, Height(sAt), Mathf.Sin(a) * r);
                p.startSize = m_PointSize * 0.9f;
                p.startColor = rim;
                p.remainingLifetime = ManualParticles.Lifetime;
            }
        }

        /// <summary>Ectoplasm: points strung out along where the hem has been, drifting and fading.</summary>
        void DrawTrail()
        {
            if (m_TrailPoints == 0)
                return;

            for (int j = 0; j < m_TrailPoints; j++)
            {
                ref var p = ref m_Particles[m_SheetPoints + j];
                p.remainingLifetime = ManualParticles.Lifetime;

                if (m_HistoryCount < 2)
                {
                    p.startSize = 0f;
                    continue;
                }

                // Oldest entries get the fewest points, so the trail thins out.
                float along = Mathf.Pow((j + 0.5f) / m_TrailPoints, 1.6f);
                int back = Mathf.Min(m_HistoryCount - 1, 1 + Mathf.FloorToInt(along * (m_HistoryCount - 1)));
                var world = m_History[(m_HistoryHead - 1 - back + k_History * 2) % k_History];
                float age = back / (float)k_History;

                float jx = Mathf.Sin(j * 12.9898f) * 0.5f;
                float jz = Mathf.Sin(j * 78.233f) * 0.5f;
                world += new Vector3(jx, -0.2f * age, jz) * (0.08f + 0.2f * age);

                p.position = transform.InverseTransformPoint(world);
                p.startSize = m_PointSize * (1f - 0.6f * age);
                p.startColor = m_Palette.Evaluate(0f) * 0.55f * (1f - age) * (1f - age);
            }
        }
    }
}
