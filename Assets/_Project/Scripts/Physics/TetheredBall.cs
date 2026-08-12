using UnityEngine;

namespace VRShootingGallery.PhysicsDemo
{
    /// <summary>
    /// The garage parking-guide ball: a weight on a cord hung from the ceiling. The swing itself is
    /// plain PhysX — a chain of rigidbodies linked by <see cref="ConfigurableJoint"/> — so this
    /// component only owns the two things physics cannot do for itself: drawing the cord, and
    /// re-hanging the rig when a demo gets out of hand.
    /// </summary>
    /// <remarks>
    /// The cord is drawn with real cylinder geometry rather than a LineRenderer on purpose. A
    /// LineRenderer billboards toward the camera, which reads wrong in a CAVE where the projection
    /// is off-axis and each eye sees a different view of the same cord.
    /// </remarks>
    public class TetheredBall : MonoBehaviour
    {
        [SerializeField, Tooltip("Anchor first, then each rope body in order, ball last.")]
        Transform[] m_Nodes;

        [SerializeField, Tooltip("Cord cylinders — one per gap between nodes, so one fewer than the nodes.")]
        Transform[] m_Cords;

        [SerializeField, Tooltip("Cord diameter in metres.")]
        float m_Thickness = 0.015f;

        [SerializeField, Tooltip("Length the rig was built with. Recorded so a rebuild can hang the ball " +
            "in the same place — the placeholder sphere that originally defined it gets switched off by " +
            "the first build, so it cannot be measured a second time.")]
        float m_CordLength;

        [SerializeField, Tooltip("Wand button that re-hangs the ball. Negative disables it.")]
        int m_ResetButton = 4;

        [SerializeField, Tooltip("Solver iterations for every body in the chain. This is not a serialized " +
            "Rigidbody field, so it has to be applied at runtime or PhysX uses the project default of 6 — " +
            "which lets a light chain under a heavier weight visibly stretch.")]
        int m_SolverIterations = 20;

        Vector3[] m_RestLocal;

        /// <summary>Cord length this rig was built with, so a rebuild can reproduce it exactly.</summary>
        public float CordLength => m_CordLength;

        void Awake()
        {
            // The rig is authored at rest, so this pose is the one to come back to.
            if (m_Nodes == null)
                return;

            m_RestLocal = new Vector3[m_Nodes.Length];
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                if (m_Nodes[i] == null)
                    continue;

                m_RestLocal[i] = transform.InverseTransformPoint(m_Nodes[i].position);

                if (m_Nodes[i].TryGetComponent<Rigidbody>(out var body))
                    body.solverIterations = Mathf.Max(1, m_SolverIterations);
            }
        }

        void Update()
        {
            if (m_ResetButton >= 0 && MiddleVRWand.PressedThisFrame(m_ResetButton))
                ResetTether();
        }

        // After the solver has moved the bodies for this frame, so the cord never lags a frame behind.
        void LateUpdate()
        {
            if (m_Nodes == null || m_Cords == null)
                return;

            int gaps = Mathf.Min(m_Cords.Length, m_Nodes.Length - 1);
            for (int i = 0; i < gaps; i++)
            {
                var from = m_Nodes[i];
                var to = m_Nodes[i + 1];
                var cord = m_Cords[i];
                if (from == null || to == null || cord == null)
                    continue;

                var delta = to.position - from.position;
                float length = delta.magnitude;

                cord.position = from.position + delta * 0.5f;
                if (length > 1e-5f)
                    cord.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);

                // Unity's cylinder mesh is two units tall, so half the span is the Y scale.
                cord.localScale = new Vector3(m_Thickness, length * 0.5f, m_Thickness);
            }
        }

        /// <summary>Drops everything back onto the plumb line and kills all motion.</summary>
        public void ResetTether()
        {
            if (m_Nodes == null || m_RestLocal == null)
                return;

            for (int i = 0; i < m_Nodes.Length; i++)
            {
                var node = m_Nodes[i];
                if (node == null)
                    continue;

                node.SetPositionAndRotation(transform.TransformPoint(m_RestLocal[i]), transform.rotation);

                if (!node.TryGetComponent<Rigidbody>(out var body) || body.isKinematic)
                    continue;

                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
