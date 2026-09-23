using UnityEngine;

namespace VRShootingGallery.Haunted
{
    /// <summary>
    /// Anything in the room the haunting can take over. It sleeps until the room's possession
    /// passes its <see cref="Threshold"/> — sooner if the ghost is near — and then answers to one
    /// part of the music: a group of bands (<see cref="Listen"/>) or a single band.
    /// </summary>
    /// <remarks>
    /// Subclasses do their moving in <see cref="Animate"/>, called once a frame after
    /// <see cref="Woken"/> and <see cref="Level"/> are fresh. They must not declare their own
    /// <c>Update</c>, which would hide this one.
    /// </remarks>
    public abstract class Possessable : MonoBehaviour
    {
        [SerializeField] protected Haunting m_Haunting;

        [SerializeField, Range(0f, 1f), Tooltip("Room possession at which this wakes. Lower wakes earlier; the order of the thresholds " +
            "is the order the room comes alive in.")]
        protected float m_Threshold = 0.3f;

        [SerializeField, Tooltip("The part of the music this answers to, once awake.")]
        protected Listen m_Listen = Listen.Mid;

        [SerializeField, Range(-1f, 1f), Tooltip("Negative uses Listen. From 0 to 1 it listens to a single band instead, bass to treble — " +
            "how a shelf of books becomes a spectrum.")]
        protected float m_Band = -1f;

        [SerializeField, Tooltip("Whether the ghost comes here. Off for the small things — a book, a teacup — so it haunts the " +
            "furniture rather than the crockery.")]
        bool m_Visit = true;

        [SerializeField, Tooltip("Where the ghost hovers when it comes for this, relative to it in room axes. Zero picks a point in " +
            "front of it, toward the middle of the room.")]
        Vector3 m_HauntOffset;

        /// <summary>How awake this is, 0 asleep to 1 fully possessed.</summary>
        public float Woken { get; private set; }

        /// <summary>What it is hearing, already scaled by <see cref="Woken"/>.</summary>
        public float Level { get; private set; }

        /// <summary>The most awake it has been since the room last went quiet. The last gasp picks from these.</summary>
        public float PeakWoken { get; private set; }

        public float Threshold => m_Threshold;
        public bool Visit => m_Visit;
        public Haunting Haunting => m_Haunting;

        /// <summary>Where the ghost goes when it comes for this.</summary>
        public virtual Vector3 HauntPoint
        {
            get
            {
                var here = transform.position;
                if (m_HauntOffset != Vector3.zero)
                    return here + m_HauntOffset;

                var centre = m_Haunting != null ? m_Haunting.transform.TransformPoint(new Vector3(0f, 0f, 0.2f)) : Vector3.zero;
                var toward = centre - here;
                toward.y = 0f;
                if (toward.sqrMagnitude > 1e-4f)
                    here += toward.normalized * 0.6f;

                here.y += 0.55f;
                return here;
            }
        }

        protected virtual void OnEnable()
        {
            if (m_Haunting != null)
                m_Haunting.Register(this);
        }

        protected virtual void OnDisable()
        {
            if (m_Haunting != null)
                m_Haunting.Unregister(this);
        }

        void Update()
        {
            if (m_Haunting == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Woken = m_Haunting.WokenFor(m_Threshold, transform.position);
            float heard = m_Band >= 0f ? m_Haunting.Band(m_Band) : m_Haunting.Level(m_Listen);
            Level = heard * Woken;
            PeakWoken = Mathf.Max(PeakWoken, Woken);

            // A hitch must not fling the springs.
            Animate(Mathf.Min(dt, 0.05f));
        }

        /// <summary>Moves the thing, once a frame, with <see cref="Woken"/> and <see cref="Level"/> fresh.</summary>
        protected abstract void Animate(float dt);

        /// <summary>One push from outside the music: the last gasp in the silence after a song.</summary>
        public abstract void Nudge(float strength);

        public void ClearPeak() => PeakWoken = 0f;
    }
}
