using UnityEngine;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Optional aiming aids: a persistent dot at the aim point and a brief beam on each
    /// shot. Both independently toggleable (the cave preset turns both on and hides the
    /// gun model — that coupling lives in SettingsService later, not here).
    /// </summary>
    public class Tracer : MonoBehaviour
    {
        [SerializeField] GunController m_Gun;
        [SerializeField] Transform m_Muzzle;
        [SerializeField] LineRenderer m_Line;
        [SerializeField] Transform m_Dot;

        [SerializeField, Tooltip("Seconds the tracer beam stays visible after a shot.")]
        float m_LineLifetime = 0.08f;

        [SerializeField] bool m_LineEnabled;
        [SerializeField] bool m_DotEnabled;

        Camera m_Camera;
        float m_LineHideTime;

        public bool LineEnabled
        {
            get => m_LineEnabled;
            set => m_LineEnabled = value;
        }

        public bool DotEnabled
        {
            get => m_DotEnabled;
            set
            {
                m_DotEnabled = value;
                if (m_Dot != null)
                    m_Dot.gameObject.SetActive(value);
            }
        }

        void Awake() => m_Camera = Camera.main;

        void OnEnable()
        {
            if (m_Gun != null)
                m_Gun.Fired += OnFired;
        }

        void OnDisable()
        {
            if (m_Gun != null)
                m_Gun.Fired -= OnFired;
        }

        void Start()
        {
            if (m_Line != null)
                m_Line.enabled = false;
            if (m_Dot != null)
                m_Dot.gameObject.SetActive(m_DotEnabled);
        }

        void Update()
        {
            if (m_DotEnabled && m_Dot != null)
            {
                m_Dot.position = m_Gun.AimPoint;
                if (m_Camera != null)
                    m_Dot.rotation = Quaternion.LookRotation(m_Dot.position - m_Camera.transform.position);
            }

            if (m_Line != null && m_Line.enabled && Time.time >= m_LineHideTime)
                m_Line.enabled = false;
        }

        void OnFired()
        {
            if (!m_LineEnabled || m_Line == null || m_Muzzle == null)
                return;

            m_Line.positionCount = 2;
            m_Line.SetPosition(0, m_Muzzle.position);
            m_Line.SetPosition(1, m_Gun.AimPoint);
            m_Line.enabled = true;
            m_LineHideTime = Time.time + m_LineLifetime;
        }
    }
}
