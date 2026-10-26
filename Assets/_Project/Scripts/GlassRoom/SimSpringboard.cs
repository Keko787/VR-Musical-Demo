using UnityEngine;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The springboard at the near end of the sky pool, reaching out over the water from just past the
    /// glass. It springs on the beat: each beat it dips and rebounds, dying away, the way a board does
    /// when somebody has just gone off it. The ripples of their going in are the world's beat rings,
    /// which the pool's water draws spreading from a little beyond the tip.
    /// </summary>
    /// <remarks>
    /// This object is the stand on the deck: the block the board is hinged to and the fulcrum it rests
    /// on. The board is a child, hinged at this object's back end and turned about it as it springs.
    /// Both meshes are generated when the world wakes, on the land material, painted with vertex colours.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimSpringboard : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Tooltip("The board: a child, turned about its hinge as it springs. Its mesh is made here.")]
        MeshFilter m_Board;

        [Header("Shape")]
        [SerializeField, Tooltip("Metres from the hinge to the tip.")]
        float m_Length = 4.5f;

        [SerializeField] float m_Width = 0.5f;
        [SerializeField] float m_Thickness = 0.07f;

        [SerializeField, Tooltip("Height of the board's top above the deck.")]
        float m_Height = 0.45f;

        [SerializeField, Tooltip("Metres from the hinge to the fulcrum the board rests on.")]
        float m_Fulcrum = 1f;

        [Header("Spring")]
        [SerializeField, Tooltip("Degrees the tip dips on a full-strength beat.")]
        float m_Dip = 2.2f;

        [SerializeField, Tooltip("Times a second the board bounces.")]
        float m_Frequency = 2.4f;

        [SerializeField, Tooltip("How fast the bounce dies away, per second.")]
        float m_Damping = 3.2f;

        [Header("Colours")]
        [SerializeField] Color m_BoardColour = new(0.88f, 0.84f, 0.72f);
        [SerializeField] Color m_StandColour = new(0.8f, 0.8f, 0.78f);
        [SerializeField] Color m_FulcrumColour = new(0.36f, 0.42f, 0.5f);

        float m_Kick;
        float m_Since = 99f;

        void Awake() => Generate();

        void OnDestroy()
        {
            foreach (var filter in new[] { GetComponent<MeshFilter>(), m_Board })
                if (filter != null && filter.sharedMesh != null && (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                    SimMeshes.Destroy(filter.sharedMesh);
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            // The stand: a block under the hinge, and the fulcrum on its base plate, just clear of the board.
            // (Full alpha throughout: on the land shader a vertex below it glows.)
            float under = m_Height - m_Thickness;
            var plate = m_FulcrumColour * 0.8f;
            plate.a = 1f;
            var b = new SimMeshes.Builder();
            SimMeshes.Box(b, new Vector3(0f, under * 0.5f, 0.2f), new Vector3(0.62f, under, 0.5f), m_StandColour);
            SimMeshes.Box(b, new Vector3(0f, 0.02f, m_Fulcrum), new Vector3(0.9f, 0.04f, 0.3f), plate);
            SimMeshes.Box(b, new Vector3(0f, (under - 0.01f) * 0.5f, m_Fulcrum), new Vector3(0.75f, under - 0.01f, 0.16f), m_FulcrumColour);
            SimMeshes.Assign(GetComponent<MeshFilter>(), b.ToMesh($"{name}_Stand"));

            if (m_Board == null)
                return;

            // The board, its top at the hinge's height, running out along +z.
            var board = new SimMeshes.Builder();
            SimMeshes.Box(board, new Vector3(0f, -m_Thickness * 0.5f, m_Length * 0.5f), new Vector3(m_Width, m_Thickness, m_Length), m_BoardColour);
            SimMeshes.Assign(m_Board, board.ToMesh($"{name}_Board"));
            m_Board.transform.localPosition = new Vector3(0f, m_Height, 0f);
        }

        void Update()
        {
            if (m_Board == null)
                return;

            if (m_Simulation != null && m_Simulation.Beat)
            {
                m_Kick = 0.45f + 0.55f * m_Simulation.BeatStrength;
                m_Since = 0f;
            }
            else
            {
                m_Since += Time.deltaTime;
            }

            // Down first, as the diver drives it, then up past level, and so on, dying away.
            float angle = m_Dip * m_Kick * Mathf.Exp(-m_Since * m_Damping) * Mathf.Sin(2f * Mathf.PI * m_Frequency * m_Since);
            m_Board.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
