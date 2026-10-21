using UnityEngine;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// One glass wall: a camera standing outside, at the glass room's place in the world, whose
    /// picture is drawn flat on the CAVE screen this wall is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camera's frustum is cut to the wall as seen from one fixed eye in the room — an off-axis
    /// projection through the wall's own corners — so the picture is exactly what a real window in
    /// that wall would show someone standing at the eye, and the three walls' pictures, taken from
    /// the same eye, meet at the corners as one panorama.
    /// </para>
    /// <para>
    /// The eye stays put however anyone moves. That is the point: drawn flat on the screen, the
    /// outside looks the same from everywhere in the room, so the untracked visitors never see the
    /// landscape skew and swim the way 3D content does for them. <see cref="m_Parallax"/> lets the eye
    /// follow the tracked head part or all of the way, for a demo with one person in glasses.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Renderer))]
    public class WindowView : MonoBehaviour
    {
        [SerializeField, Tooltip("The glass room's place in the outside worlds. The camera stands here, at the eye.")]
        Transform m_Outside;

        [SerializeField, Tooltip("The glass room, whose frame the wall and the eye are measured in.")]
        Transform m_Room;

        [SerializeField, Tooltip("The CAVE screen this wall is, in the room's frame.")]
        CaveScreen m_Screen;

        [SerializeField, Tooltip("The fixed eye every wall's picture is taken from, room metres. The middle of the room at standing eye height.")]
        Vector3 m_Eye = new(0f, 1.6f, -0.15f);

        [SerializeField, Range(0f, 1f), Tooltip("How far the eye follows the tracked head. 0 keeps the outside flat and the same for everyone; " +
            "1 makes the walls true windows for the person in glasses and swims for everyone else.")]
        float m_Parallax;

        [SerializeField] string m_HeadNode = "HeadNode";

        [Header("Picture")]
        [SerializeField, Tooltip("Render texture pixels per metre of wall. 576 gives the front wall 2048 across.")]
        float m_PixelsPerMetre = 576f;

        [SerializeField, Range(1, 8)] int m_Msaa = 4;
        [SerializeField] float m_Near = 1f;
        [SerializeField] float m_Far = 10000f;

        [Header("Glass")]
        [SerializeField, Tooltip("Metres round the room from the back of the left wall to this wall's left edge, for the wave between worlds.")]
        float m_SweepOffset;

        [SerializeField, Tooltip("Metres round all three walls.")]
        float m_SweepLength = 8.3312f;

        [SerializeField, Tooltip("Metres across a lamp's reflection in the glass.")]
        float m_LampBlob = 0.11f;

        [SerializeField, Range(0f, 1f)] float m_LampReflection = 0.22f;

        [SerializeField, Tooltip("Other screens that show this wall's picture: the tablet on the desk.")]
        Renderer[] m_Mirrors;

        public Camera Camera => m_Camera;
        public RenderTexture Texture => m_Texture;

        static readonly int k_MainTex = Shader.PropertyToID("_MainTex");
        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int k_WallSize = Shader.PropertyToID("_WallSize");
        static readonly int k_Sweep = Shader.PropertyToID("_Sweep");
        static readonly int k_Dissolve = Shader.PropertyToID("_Dissolve");
        static readonly int k_VoidColor = Shader.PropertyToID("_VoidColor");
        static readonly int k_Lamps = Shader.PropertyToID("_Lamps");
        static readonly int k_Glitch = Shader.PropertyToID("_Glitch");
        static readonly int k_GlitchSeed = Shader.PropertyToID("_GlitchSeed");
        static readonly int k_GlitchStep = Shader.PropertyToID("_GlitchStep");

        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;
        MaterialPropertyBlock m_MirrorBlock;
        Camera m_Camera;
        RenderTexture m_Texture;
        Transform m_Head;
        float m_HeadSearchAt;
        readonly Vector4[] m_LampSpots = new Vector4[4];
        float m_Glitch;
        float m_GlitchSeed;
        bool m_GlitchStepped = true;

        void OnEnable()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Block ??= new MaterialPropertyBlock();
            m_MirrorBlock ??= new MaterialPropertyBlock();

            // The cameras exist only in play mode: in the editor the glass shows the material's own
            // pale default, and nothing is created that could be saved with the scene.
            if (Application.isPlaying)
                CreateCamera();

            Push(0f, Color.white);
        }

        void OnDisable()
        {
            if (m_Camera != null)
                Destroy(m_Camera.gameObject);
            if (m_Texture != null)
            {
                m_Texture.Release();
                Destroy(m_Texture);
            }

            m_Camera = null;
            m_Texture = null;
        }

        void CreateCamera()
        {
            int width = Mathf.Clamp(Mathf.RoundToInt(m_Screen.Width * m_PixelsPerMetre), 64, 8192);
            int height = Mathf.Clamp(Mathf.RoundToInt(m_Screen.Height * m_PixelsPerMetre), 64, 8192);

            m_Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = $"{name}_View",
                antiAliasing = Mathf.ClosestPowerOfTwo(Mathf.Clamp(m_Msaa, 1, 8)),
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            m_Texture.Create();

            var go = new GameObject($"{name}_Camera");
            if (m_Outside != null)
                go.transform.SetParent(m_Outside, false);

            m_Camera = go.AddComponent<Camera>();
            m_Camera.targetTexture = m_Texture;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Color.black;
            m_Camera.nearClipPlane = m_Near;
            m_Camera.farClipPlane = m_Far;
            m_Camera.allowHDR = false;
            m_Camera.allowMSAA = true;
            m_Camera.useOcclusionCulling = false;
            m_Camera.depth = -50f;               // before the CAVE's cameras draw the walls
            m_Camera.stereoTargetEye = StereoTargetEyeMask.None;

            Aim();
        }

        void LateUpdate()
        {
            FindHead();

            if (m_Camera != null)
            {
                // Something may switch scene cameras off as it takes over the display; this one is ours.
                if (!m_Camera.enabled)
                    m_Camera.enabled = true;

                Aim();
            }
        }

        /// <summary>Stands the camera at the eye and cuts its frustum to the wall's corners.</summary>
        void Aim()
        {
            var eye = Eye();
            var normal = m_Screen.Normal;            // into the room, toward the eye
            var right = m_Screen.Right;
            var up = m_Screen.Up;

            var toLowerLeft = m_Screen.Point(0f, 0f) - eye;
            var toLowerRight = m_Screen.Point(1f, 0f) - eye;
            var toUpperLeft = m_Screen.Point(0f, 1f) - eye;
            float distance = -Vector3.Dot(toLowerLeft, normal);
            if (distance < 0.01f)
                return;                                // the eye is at or beyond the glass

            float n = m_Near;
            float scale = n / distance;
            float l = Vector3.Dot(right, toLowerLeft) * scale;
            float r = Vector3.Dot(right, toLowerRight) * scale;
            float b = Vector3.Dot(up, toLowerLeft) * scale;
            float t = Vector3.Dot(up, toUpperLeft) * scale;

            var frame = m_Outside != null ? m_Outside : transform;
            m_Camera.transform.SetPositionAndRotation(frame.TransformPoint(eye),
                frame.rotation * Quaternion.LookRotation(-normal, up));
            m_Camera.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, n, m_Far);
        }

        /// <summary>The fixed eye, or part way to the tracked head.</summary>
        Vector3 Eye()
        {
            if (m_Parallax <= 0f || m_Head == null || m_Room == null || m_Head.position.y < 0.5f)
                return m_Eye;

            return Vector3.Lerp(m_Eye, m_Room.InverseTransformPoint(m_Head.position), m_Parallax);
        }

        void FindHead()
        {
            if (m_Parallax <= 0f || m_Head != null || Time.time < m_HeadSearchAt)
                return;

            m_HeadSearchAt = Time.time + 1f;
            var node = GameObject.Find(m_HeadNode);
            if (node != null)
                m_Head = node.transform;
        }

        // ---------------------------------------------------------------- driven by the simulation

        /// <summary>The change between worlds: how far it has got (0–2), and the blank page's colour.</summary>
        public void SetTransition(float dissolve, Color blank) => Push(dissolve, blank);

        /// <summary>
        /// The glitch between a world's variants: its strength (0–1), a seed so each glitch looks its own, and
        /// whether its pattern jumps (off in low intensity, where it holds still). Applied with the next transition push.
        /// </summary>
        public void SetGlitch(float amount, float seed, bool stepped)
        {
            m_Glitch = amount;
            m_GlitchSeed = seed;
            m_GlitchStepped = stepped;
        }

        /// <summary>
        /// Puts each lamp's reflection where the eye would see it in this glass: the lamp mirrored
        /// through the wall's plane, and the point where the line from the eye to that image crosses it.
        /// </summary>
        public void SetLamps(PendantLamp[] lamps)
        {
            for (int i = 0; i < m_LampSpots.Length; i++)
                m_LampSpots[i] = Vector4.zero;

            if (lamps == null || m_Room == null)
                return;

            var eye = m_Eye;
            var normal = m_Screen.Normal;
            float eyeDepth = Vector3.Dot(eye - m_Screen.Centre, normal);

            for (int i = 0; i < lamps.Length && i < m_LampSpots.Length; i++)
            {
                var lamp = lamps[i];
                if (lamp == null)
                    continue;

                var bulb = m_Room.InverseTransformPoint(lamp.BulbPosition);
                float lampDepth = Vector3.Dot(bulb - m_Screen.Centre, normal);
                if (lampDepth <= 0f || eyeDepth <= 0f)
                    continue;

                var image = bulb - 2f * lampDepth * normal;
                var hit = eye + (image - eye) * (eyeDepth / (eyeDepth + lampDepth));
                var local = hit - m_Screen.Centre;
                float u = Vector3.Dot(local, m_Screen.Right) / m_Screen.Width + 0.5f;
                float v = Vector3.Dot(local, m_Screen.Up) / m_Screen.Height + 0.5f;

                // Further from the glass, smaller and fainter.
                float fade = 1f / (1f + lampDepth * 0.8f);
                m_LampSpots[i] = new Vector4(u, v, m_LampBlob * (0.6f + 0.6f * fade), lamp.Brightness * m_LampReflection * fade);
            }
        }

        void Push(float dissolve, Color blank)
        {
            if (m_Renderer == null)
                return;

            m_Renderer.GetPropertyBlock(m_Block);
            if (m_Texture != null)
                m_Block.SetTexture(k_MainTex, m_Texture);
            m_Block.SetVector(k_WallSize, new Vector4(m_Screen.Width, m_Screen.Height, 0f, 0f));
            m_Block.SetVector(k_Sweep, new Vector4(m_SweepOffset, m_SweepLength, 0f, 0f));
            m_Block.SetFloat(k_Dissolve, dissolve);
            m_Block.SetColor(k_VoidColor, blank);
            m_Block.SetVectorArray(k_Lamps, m_LampSpots);
            m_Block.SetFloat(k_Glitch, m_Glitch);
            m_Block.SetFloat(k_GlitchSeed, m_GlitchSeed);
            m_Block.SetFloat(k_GlitchStep, m_GlitchStepped ? 1f : 0f);
            m_Renderer.SetPropertyBlock(m_Block);

            if (m_Mirrors == null || m_Texture == null)
                return;

            foreach (var mirror in m_Mirrors)
            {
                if (mirror == null)
                    continue;

                mirror.GetPropertyBlock(m_MirrorBlock);
                m_MirrorBlock.SetTexture(k_BaseMap, m_Texture);
                m_MirrorBlock.SetTexture(k_MainTex, m_Texture);
                mirror.SetPropertyBlock(m_MirrorBlock);
            }
        }
    }
}
