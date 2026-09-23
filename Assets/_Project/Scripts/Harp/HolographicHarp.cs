using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VRShootingGallery.Strings;

namespace VRShootingGallery.Harp
{
    /// <summary>
    /// The holographic harp from the CAVE music deck: strings on one plane, fixed in room
    /// coordinates, sounded when the tracked wand tip crosses them. This component owns the
    /// detection (through <see cref="StringSweep"/>) and the room's light pulse; each
    /// <see cref="PluckedString"/> owns its own note and response.
    /// </summary>
    [DefaultExecutionOrder(-50)] // LateUpdate ahead of PluckedString's, so a pluck shows the frame it is found
    public class HolographicHarp : MonoBehaviour
    {
        [Header("Wand")]
        [SerializeField, Tooltip("The tracked point that plucks. Attached to MiddleVR's HandNode at run time by " +
            "MVRAttachToNode; its own position is the offset from the wand's tracked origin.")]
        Transform m_Plectrum;

        [SerializeField, Tooltip("Metres from a string's surface at which the tip counts as touching it. Wider " +
            "than the visible string, because a tracked wand in mid-air is not steady to the millimetre.")]
        float m_CatchRadius = 0.02f;

        [SerializeField, Tooltip("Metres the tip must move away from a string before it can catch that string " +
            "again. Larger than the catch radius so tracking jitter on a tip resting against a string does " +
            "not re-pluck it.")]
        float m_ReleaseRadius = 0.04f;

        [SerializeField, Tooltip("Seconds after a pluck before the same string can sound again.")]
        float m_Cooldown = 0.08f;

        [SerializeField, Tooltip("Longest tip movement in one frame that is believed. Anything further is a " +
            "tracking dropout or the wand being picked up, and sweeping it would strum every string in " +
            "between.")]
        float m_MaxSweep = 1.5f;

        [Header("Touch")]
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the gentlest possible pluck. A slow touch still sounds.")]
        float m_SoftestPluck = 0.35f;

        [SerializeField, Tooltip("Tip speed, in m/s, at and above which a pluck is full strength.")]
        float m_FullPluckSpeed = 2.5f;

        [Header("Strings")]
        [SerializeField, Tooltip("Bass first. Filled in by the builder.")]
        PluckedString[] m_Strings;

        [SerializeField, Range(0f, 1f), Tooltip("Stereo width: the bass string pans this far left, the top string " +
            "this far right. Assumes the harp faces the front wall.")]
        float m_StereoSpread = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("0 plays every string straight to the speakers; 1 places each at " +
            "its string's position relative to the audio listener. The listener sits on the template camera " +
            "at the room's origin, not on the tracked head, so leave this at 0 unless that changes.")]
        float m_SpatialBlend;

        [Header("Room pulse")]
        [SerializeField, Tooltip("A real light that jumps to the plucked string and flashes its colour on the " +
            "walls and floor. This is the part of the response that reads without stereo glasses.")]
        Light m_PulseLight;

        [SerializeField] float m_PulseIntensity = 8f;

        [SerializeField, Tooltip("Seconds for the flash to fall to a third.")]
        float m_PulseDecay = 0.35f;

        [SerializeField, Tooltip("Metres the light stands off the string plane, on the player's side, so it " +
            "lights the strings rather than sitting inside them.")]
        float m_PulseStandoff = 0.25f;

        [Header("Desk")]
        [SerializeField, Tooltip("Number row, then Q..P, then A..L pluck the strings in order, bass first. For a " +
            "desk with no tracker, and for checking the room's audio routing without picking up the wand.")]
        bool m_KeyboardPlay = true;

        static readonly Key[] k_DeskKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0,
            Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P,
            Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L,
        };

        readonly StringSweep m_Sweep = new();
        readonly List<StringSweep.Crossing> m_Crossings = new();

        float m_PulseTime = float.NegativeInfinity;
        float m_PulseStrength;

        public PluckedString[] Strings => m_Strings;

        void Awake()
        {
            int count = m_Strings != null ? m_Strings.Length : 0;
            for (int i = 0; i < count; i++)
            {
                var source = m_Strings[i] != null ? m_Strings[i].Source : null;
                if (source == null)
                    continue;

                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                source.panStereo = Mathf.Lerp(-m_StereoSpread, m_StereoSpread, t);
                source.spatialBlend = m_SpatialBlend;
            }

            if (m_PulseLight != null)
            {
                m_PulseLight.intensity = 0f;
                m_PulseLight.enabled = false;
            }
        }

        // After MiddleVR has moved HandNode for this frame, so the sweep uses the same wand pose the
        // walls are about to draw.
        void LateUpdate()
        {
            FadePulse();

            if (m_KeyboardPlay)
                ReadKeyboard();

            if (m_Plectrum == null || m_Strings == null)
                return;

            // Copied every frame so Inspector tweaks take effect while playing.
            m_Sweep.CatchRadius = m_CatchRadius;
            m_Sweep.ReleaseRadius = m_ReleaseRadius;
            m_Sweep.Cooldown = m_Cooldown;
            m_Sweep.MaxSweep = m_MaxSweep;

            m_Crossings.Clear();
            m_Sweep.Step(m_Plectrum, m_Strings, Time.time, Time.deltaTime, m_Crossings);
            if (m_Crossings.Count == 0)
                return;

            // A glissando crosses several strings in one frame. Sounding them in the order the tip
            // met them, spread over the frame on the audio clock, keeps it a run rather than a chord.
            m_Crossings.Sort((a, b) => a.Along.CompareTo(b.Along));
            double now = AudioSettings.dspTime;
            float first = m_Crossings[0].Along;

            foreach (var crossing in m_Crossings)
                Pluck(crossing.Index, crossing.Speed, crossing.Direction,
                    now + (crossing.Along - first) * Time.deltaTime);
        }

        /// <summary>
        /// Sounds string <paramref name="index"/> as if the tip had crossed it at <paramref name="speed"/>
        /// metres per second. Public so a sequencer or a shootable button can play the harp too.
        /// </summary>
        public void Pluck(int index, float speed, float direction = 1f, double dspTime = 0d)
        {
            if (m_Strings == null || index < 0 || index >= m_Strings.Length || m_Strings[index] == null)
                return;

            var pluckedString = m_Strings[index];
            float strength = Mathf.Lerp(m_SoftestPluck, 1f, Mathf.Clamp01(speed / Mathf.Max(0.01f, m_FullPluckSpeed)));

            pluckedString.Pluck(strength, direction, dspTime);

            if (m_PulseLight != null)
            {
                m_PulseLight.transform.position = pluckedString.Middle - pluckedString.transform.forward * m_PulseStandoff;
                m_PulseLight.color = pluckedString.Glow;
                m_PulseLight.enabled = true;
                m_PulseStrength = strength;
                m_PulseTime = Time.time;
            }
        }

        void FadePulse()
        {
            if (m_PulseLight == null || !m_PulseLight.enabled)
                return;

            float t = Time.time - m_PulseTime;
            float intensity = m_PulseIntensity * m_PulseStrength * Mathf.Exp(-t / Mathf.Max(0.01f, m_PulseDecay));

            if (intensity < 0.02f)
            {
                m_PulseLight.intensity = 0f;
                m_PulseLight.enabled = false;
                return;
            }

            m_PulseLight.intensity = intensity;
        }

        void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || m_Strings == null)
                return;

            int count = Mathf.Min(m_Strings.Length, k_DeskKeys.Length);
            for (int i = 0; i < count; i++)
                if (keyboard[k_DeskKeys[i]].wasPressedThisFrame)
                    Pluck(i, m_FullPluckSpeed * 0.6f);
        }
    }
}
