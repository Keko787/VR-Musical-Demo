using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// One look a world can take: its sky, the nebula in it, and the colours its plants, rocks,
    /// planets and the room's light are painted in. A world with variants switches between them with
    /// a glitch on the glass instead of the full rebuild between worlds.
    /// </summary>
    [System.Serializable]
    public class WorldVariant
    {
        public string Name = "Variant";

        [Header("Sky")]
        public Color Zenith = new(0.05f, 0.06f, 0.2f);
        public Color Horizon = new(0.25f, 0.3f, 0.6f);
        public Color Ground = new(0.05f, 0.1f, 0.15f);
        [ColorUsage(false, true)] public Color SunColour = new(1.2f, 1.1f, 0.9f);
        public Color ShadowColour = new(0.2f, 0.2f, 0.45f);

        [Header("Nebula")]
        public Color NebulaA = new(0.45f, 0.25f, 0.7f);
        public Color NebulaB = new(0.2f, 0.45f, 0.9f);
        public Color NebulaC = new(0.9f, 0.45f, 0.75f);

        [Tooltip("Where in the nebula's noise this variant's swirls come from, so each sky has its own shapes.")]
        public Vector2 NebulaOffset;

        [Header("Paint")]
        [Tooltip("Ground and grass, light leaves, dark leaves, rocks and tubes. The flora's materials pick a slot.")]
        public Color[] Palette = { new(0.25f, 0.85f, 0.85f), new(0.2f, 0.75f, 0.6f), new(0.1f, 0.3f, 0.6f), new(0.35f, 0.7f, 0.85f) };

        [ColorUsage(false, true)] public Color PlanetTint = Color.white;
        [ColorUsage(false, true)] public Color StarTint = Color.white;

        [Header("The room")]
        public Color RoomAmbient = new(0.3f, 0.32f, 0.45f);
        public Color RoomLight = new(0.85f, 0.85f, 1f);
    }

    /// <summary>
    /// One of the worlds outside the glass: its sky, how it lights the room, and where its beat rings
    /// start. The landscape and the things in it that play to the music are its children; this holds
    /// what the <see cref="Simulation"/> needs to switch to it.
    /// </summary>
    /// <remarks>
    /// Only one world is awake at a time. Every world is built round the same point — the glass
    /// room's place outside — so switching is waking one child and putting the others to sleep while
    /// the windows show the blank page between them.
    /// </remarks>
    public class SimWorld : MonoBehaviour
    {
        [SerializeField] string m_Title = "World";

        [Header("Sky")]
        [SerializeField] Color m_Zenith = new(0.07f, 0.22f, 0.72f);
        [SerializeField] Color m_Horizon = new(0.62f, 0.8f, 0.98f);
        [SerializeField, Tooltip("Below the horizon, where nothing is in the way.")] Color m_Ground = new(0.55f, 0.42f, 0.36f);

        [SerializeField, Tooltip("Toward the sun, in the room's frame: +z is through the front wall, +x the right.")]
        Vector3 m_SunDirection = new(-0.6f, 0.65f, -0.45f);

        [SerializeField, ColorUsage(false, true)] Color m_SunColour = new(1.15f, 1.08f, 0.94f);

        [SerializeField, Tooltip("Angular radius of the sun's disc, degrees. 0 hides it (a night world lit by its moon).")]
        float m_SunDegrees = 1.2f;

        [SerializeField, Tooltip("What a shadow is lit by. The painted look is in this: violet, not grey.")]
        Color m_ShadowColour = new(0.36f, 0.3f, 0.52f);

        [SerializeField, Range(0f, 1f)] float m_Clouds = 0.5f;
        [SerializeField] float m_CloudDrift = 0.003f;

        [SerializeField, Tooltip("What the cirrus is lit to. White by day; pink and orange for a sunset.")]
        Color m_CloudColour = Color.white;
        [SerializeField, Range(0f, 2f)] float m_Stars;
        [SerializeField, Range(0f, 1f)] float m_MilkyWay;
        [SerializeField] Vector3 m_MoonDirection = new(-0.75f, 0.38f, 0.35f);

        [SerializeField, Tooltip("Angular radius of the moon, degrees. 0 for no moon.")]
        float m_MoonDegrees;

        [SerializeField, Tooltip("Metres of air that haze a distant thing to 63% of the horizon colour.")]
        float m_FogDistance = 3500f;

        [SerializeField, Range(0f, 1f)] float m_FogAmount = 0.85f;
        [SerializeField, Range(0f, 2f), Tooltip("How much the haze warms looking toward the sun.")] float m_FogSunGlow = 0.25f;

        [Header("Music")]
        [SerializeField, Tooltip("Where the beat rings run out across the ground from. None for no rings.")]
        Transform m_RingOrigin;

        [SerializeField, ColorUsage(false, true)] Color m_RingColour = new(0.6f, 1f, 1.1f);
        [SerializeField, Tooltip("Metres a second a ring travels.")] float m_RingSpeed = 240f;

        [SerializeField, Tooltip("Seconds between the beat and its ring setting off: the time the light takes to fall down the pillar.")]
        float m_RingDelay = 0.35f;

        [SerializeField, Tooltip("Metres a second the music's swells roll outward across a cloud sea.")]
        float m_WaveSpeed = 45f;

        [SerializeField, Tooltip("Metres from the room at which the swells start.")]
        float m_WaveInner = 20f;

        [Header("The room")]
        [SerializeField, Tooltip("Ambient light in the glass room while this world is outside.")]
        Color m_RoomAmbient = new(0.58f, 0.55f, 0.55f);

        [SerializeField, Tooltip("The colour of the light coming in from outside: the scene's directional light, turned to match the sun.")]
        Color m_RoomLight = new(1f, 0.95f, 0.86f);

        [SerializeField] float m_RoomLightIntensity = 1.15f;

        [SerializeField, Range(0f, 1.5f), Tooltip("How bright the pendant lamps burn. Low by day, full at night.")]
        float m_LampLevel = 0.55f;

        [Header("Space")]
        [SerializeField, Range(0f, 2f), Tooltip("How much nebula glows across the sky. 0 for none.")]
        float m_Nebula;

        [SerializeField, Tooltip("Size of the nebula's swirls: higher is finer.")]
        float m_NebulaScale = 1.6f;

        [Header("Variants")]
        [SerializeField, Tooltip("Looks this world can switch between with a glitch (V, or the variant wand button). Empty for one look: " +
            "the sky above.")]
        WorldVariant[] m_Variants;

        [SerializeField] int m_Variant;

        public string Title => m_Title;
        public Transform RingOrigin => m_RingOrigin;
        public Color RingColour => m_RingColour;
        public float RingDelay => m_RingDelay;
        public Color RoomAmbient => Current?.RoomAmbient ?? m_RoomAmbient;
        public Color RoomLight => Current?.RoomLight ?? m_RoomLight;
        public float RoomLightIntensity => m_RoomLightIntensity;
        public float LampLevel => m_LampLevel;

        public int VariantCount => m_Variants?.Length ?? 0;
        public int VariantIndex => VariantCount > 0 ? Mathf.Clamp(m_Variant, 0, VariantCount - 1) : -1;
        public string VariantName => Current?.Name;

        WorldVariant Current => VariantCount > 0 ? m_Variants[VariantIndex] : null;

        /// <summary>Changes to a variant's look; the next <see cref="PushSky"/> paints it. Wraps round.</summary>
        public void SetVariant(int index)
        {
            if (VariantCount > 0)
                m_Variant = ((index % VariantCount) + VariantCount) % VariantCount;
        }

        /// <summary>The direction the room's light comes from, as a rotation for the directional light.</summary>
        public Quaternion RoomLightRotation
        {
            get
            {
                var toward = m_SunDirection.sqrMagnitude > 1e-6f ? m_SunDirection.normalized : Vector3.up;
                return Quaternion.LookRotation(-toward, Mathf.Abs(toward.y) > 0.99f ? Vector3.forward : Vector3.up);
            }
        }

        static readonly int k_SunDir = Shader.PropertyToID("_GR_SunDir");
        static readonly int k_SunColor = Shader.PropertyToID("_GR_SunColor");
        static readonly int k_ShadowColor = Shader.PropertyToID("_GR_ShadowColor");
        static readonly int k_Zenith = Shader.PropertyToID("_GR_Zenith");
        static readonly int k_Horizon = Shader.PropertyToID("_GR_Horizon");
        static readonly int k_Ground = Shader.PropertyToID("_GR_Ground");
        static readonly int k_Fog = Shader.PropertyToID("_GR_Fog");
        static readonly int k_Sky = Shader.PropertyToID("_GR_Sky");
        static readonly int k_Moon = Shader.PropertyToID("_GR_Moon");
        static readonly int k_CloudColor = Shader.PropertyToID("_GR_CloudColor");
        static readonly int k_RingColor = Shader.PropertyToID("_GR_RingColor");
        static readonly int k_RingOrigin = Shader.PropertyToID("_GR_RingOrigin");
        static readonly int k_Nebula = Shader.PropertyToID("_GR_Nebula");
        static readonly int k_NebulaA = Shader.PropertyToID("_GR_NebulaA");
        static readonly int k_NebulaB = Shader.PropertyToID("_GR_NebulaB");
        static readonly int k_NebulaC = Shader.PropertyToID("_GR_NebulaC");
        static readonly int k_Palette = Shader.PropertyToID("_GR_Palette");
        static readonly int k_PlanetTint = Shader.PropertyToID("_GR_PlanetTint");
        static readonly int k_StarTint = Shader.PropertyToID("_GR_StarTint");

        readonly Vector4[] m_PaletteValues = new Vector4[4];

        /// <summary>Hands this world's sky to every outside shader. Called when the world wakes, and each frame while it is out.</summary>
        public void PushSky(Quaternion frame)
        {
            var look = Current;
            var sun = frame * (m_SunDirection.sqrMagnitude > 1e-6f ? m_SunDirection.normalized : Vector3.up);
            float disc = m_SunDegrees > 0f ? Mathf.Cos(m_SunDegrees * Mathf.Deg2Rad) : 2f;
            Shader.SetGlobalVector(k_SunDir, new Vector4(sun.x, sun.y, sun.z, disc));
            Shader.SetGlobalColor(k_SunColor, look?.SunColour ?? m_SunColour);
            Shader.SetGlobalColor(k_ShadowColor, look?.ShadowColour ?? m_ShadowColour);
            Shader.SetGlobalColor(k_Zenith, look?.Zenith ?? m_Zenith);
            Shader.SetGlobalColor(k_Horizon, look?.Horizon ?? m_Horizon);
            Shader.SetGlobalColor(k_Ground, look?.Ground ?? m_Ground);

            // Space: the nebula and the paint the flora and planets take. A world without variants has
            // no nebula and plain white paint, so nothing that reads them changes.
            var offset = look?.NebulaOffset ?? Vector2.zero;
            Shader.SetGlobalVector(k_Nebula, new Vector4(m_Nebula, m_NebulaScale, offset.x, offset.y));
            Shader.SetGlobalColor(k_NebulaA, look?.NebulaA ?? Color.black);
            Shader.SetGlobalColor(k_NebulaB, look?.NebulaB ?? Color.black);
            Shader.SetGlobalColor(k_NebulaC, look?.NebulaC ?? Color.black);
            for (int i = 0; i < m_PaletteValues.Length; i++)
                m_PaletteValues[i] = look?.Palette != null && i < look.Palette.Length ? (Vector4)look.Palette[i] : Vector4.one;
            Shader.SetGlobalVectorArray(k_Palette, m_PaletteValues);
            Shader.SetGlobalColor(k_PlanetTint, look?.PlanetTint ?? Color.white);
            Shader.SetGlobalColor(k_StarTint, look?.StarTint ?? Color.white);
            Shader.SetGlobalVector(k_Fog, new Vector4(m_FogDistance, m_FogAmount, m_FogSunGlow, 0f));
            Shader.SetGlobalVector(k_Sky, new Vector4(m_Clouds, m_Stars, m_MilkyWay, m_CloudDrift));
            Shader.SetGlobalColor(k_CloudColor, m_CloudColour);

            var moon = frame * (m_MoonDirection.sqrMagnitude > 1e-6f ? m_MoonDirection.normalized : Vector3.up);
            float moonDisc = m_MoonDegrees > 0f ? Mathf.Cos(m_MoonDegrees * Mathf.Deg2Rad) : 0f;
            Shader.SetGlobalVector(k_Moon, new Vector4(moon.x, moon.y, moon.z, moonDisc));

            // A world with no ring origin has no rings: black adds nothing.
            var origin = m_RingOrigin != null ? m_RingOrigin.position : transform.position;
            Shader.SetGlobalVector(k_RingOrigin, origin);
            var ring = m_RingOrigin != null ? m_RingColour : Color.black;
            Shader.SetGlobalVector(k_RingColor, new Vector4(ring.r, ring.g, ring.b, m_RingSpeed));
        }

        /// <summary>The cloud sea's swells: how fast they roll out and where they start.</summary>
        public Vector2 Waves => new(m_WaveSpeed, m_WaveInner);
    }
}
