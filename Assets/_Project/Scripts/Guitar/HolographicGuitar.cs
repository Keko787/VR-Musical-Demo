using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VRShootingGallery.Strings;

namespace VRShootingGallery.Guitar
{
    /// <summary>A chord as a guitarist writes it: one fret per string, low E first.</summary>
    [System.Serializable]
    public class GuitarChord
    {
        public string Name = "A";

        [Tooltip("Fret per string, low E first. -1 mutes the string (the X above the nut), 0 is open.")]
        public int[] Frets = { -1, -1, -1, 2, 2, -1 };

        [Tooltip("Wand button that selects this chord when the guitar is in Direct selection. Negative for none. " +
            "Indices are whatever the .vrx says; the VisBox wand has 0-4, 6-8, 11 and 15, and 0 is the trigger.")]
        public int Button = -1;

        public int FretOf(int stringIndex)
        {
            return Frets != null && stringIndex >= 0 && stringIndex < Frets.Length ? Frets[stringIndex] : -1;
        }
    }

    /// <summary>How the wand picks a chord.</summary>
    public enum ChordSelection
    {
        /// <summary>Every chord has its own wand button (<see cref="GuitarChord.Button"/>). Press it and that chord is up.</summary>
        Direct,

        /// <summary>Two buttons step through the progression, next and previous, wrapping at the ends.</summary>
        Step,
    }

    /// <summary>
    /// A holographic guitar that the wand strums and a chord shape frets. The room has one wand, so
    /// the fretting hand is the software's: a list of chord shapes, each on its own wand button (or
    /// two buttons stepping through them), drawn on the neck as dots, with the chord's name above.
    /// Sweeping the wand across the six strings sounds whichever chord is up, each string at its
    /// fretted pitch.
    /// </summary>
    /// <remarks>
    /// Detection is the harp's <see cref="StringSweep"/>. What is different is timing: a strum
    /// crosses all six strings in a frame or two, and if every string simply played on the next
    /// audio buffer the strum would land as one chord. The sweep reports where along the frame each
    /// string was met, and the strings are scheduled on the audio clock at those offsets, so a
    /// downstroke keeps its rake from low E to high.
    /// </remarks>
    [DefaultExecutionOrder(-50)] // LateUpdate ahead of PluckedString's, so a pluck shows the frame it is found
    public class HolographicGuitar : MonoBehaviour
    {
        [Header("Wand")]
        [SerializeField, Tooltip("The tracked point that strums. Attached to MiddleVR's HandNode at run time by " +
            "MVRAttachToNode; its own position is the offset from the wand's tracked origin.")]
        Transform m_Plectrum;

        [SerializeField, Tooltip("Metres from a string's surface at which the tip counts as touching it.")]
        float m_CatchRadius = 0.02f;

        [SerializeField, Tooltip("Metres the tip must move away from a string before it can catch it again.")]
        float m_ReleaseRadius = 0.04f;

        [SerializeField, Tooltip("Seconds after a pluck before the same string can sound again.")]
        float m_Cooldown = 0.08f;

        [SerializeField, Tooltip("Longest tip movement in one frame that is believed; anything further is a tracking jump.")]
        float m_MaxSweep = 1.5f;

        [Header("Touch")]
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the gentlest possible pluck. A slow touch still sounds.")]
        float m_SoftestPluck = 0.4f;

        [SerializeField, Tooltip("Tip speed, in m/s, at and above which a pluck is full strength.")]
        float m_FullPluckSpeed = 2f;

        [Header("Strings")]
        [SerializeField, Tooltip("Low E first. Filled in by the builder.")]
        PluckedString[] m_Strings;

        [SerializeField, Range(0f, 1f), Tooltip("Stereo width across the strings. Narrow — a guitar is one instrument, not a harp's row of them.")]
        float m_StereoSpread = 0.2f;

        [SerializeField, Range(0f, 1f), Tooltip("0 plays straight to the speakers; see the harp for why that is the default.")]
        float m_SpatialBlend;

        [Header("Chords")]
        [SerializeField, Tooltip("The progression, in order. Picked by wand button, or stepped through; stepping wraps at both ends.")]
        GuitarChord[] m_Chords =
        {
            new() { Name = "A", Frets = new[] { -1, -1, -1, 2, 2, -1 }, Button = 1 },
            new() { Name = "D/A", Frets = new[] { -1, -1, -1, 2, 3, 2 }, Button = 2 },
            new() { Name = "Asus4", Frets = new[] { -1, -1, -1, 2, 3, 0 }, Button = 3 },
            new() { Name = "E7sus4", Frets = new[] { -1, -1, 2, 2, 3, 0 }, Button = 4 },
            new() { Name = "E9sus4", Frets = new[] { -1, -1, 2, 2, 3, 2 }, Button = 6 },
        };

        [SerializeField, Tooltip("Direct: each chord sits on its own wand button (set per chord above). Step: two " +
            "buttons walk the progression. Direct is the demo setting — a visitor can jump to any chord — and " +
            "the progression sign shows which button plays which.")]
        ChordSelection m_Selection = ChordSelection.Direct;

        [SerializeField, Tooltip("Step selection only: wand button that moves to the next chord. Negative disables it.")]
        int m_NextChordButton = 1;

        [SerializeField, Tooltip("Step selection only: wand button that moves to the previous chord. Negative disables it.")]
        int m_PreviousChordButton = 2;

        [Header("Neck display")]
        [SerializeField, Tooltip("One marker per string, low E first. Moved to the fretted position on each chord change; " +
            "hidden for a muted string. Filled in by the builder.")]
        Transform[] m_ChordDots;

        [SerializeField, Tooltip("Nut to bridge, in the guitar's own metres. Fret positions follow from it.")]
        float m_ScaleLength = 1.2f;

        [SerializeField, Tooltip("Where an open string's marker sits, in the guitar's X: just behind the nut.")]
        float m_OpenMarkerX = -0.05f;

        [SerializeField] TMP_Text m_ChordName;
        [SerializeField] TMP_Text m_Progression;

        [SerializeField, Tooltip("Colour the current chord takes in the progression line.")]
        Color m_CurrentColour = new(1f, 0.83f, 0.45f);

        [SerializeField, Tooltip("Colour the other chords take in the progression line.")]
        Color m_OtherColour = new(0.45f, 0.55f, 0.65f);

        [Header("Room pulse")]
        [SerializeField, Tooltip("A real light that flashes on the body when the guitar is strummed — the part " +
            "of the response that reads without stereo glasses.")]
        Light m_PulseLight;

        [SerializeField] float m_PulseIntensity = 8f;
        [SerializeField] float m_PulseDecay = 0.35f;

        [Header("Desk")]
        [SerializeField, Tooltip("Up arrow strums down (low E to high), down arrow strums up; left and right arrows " +
            "change chord, and the number-row keys stand in for the wand buttons with the same digit. For a " +
            "desk with no tracker, and for checking the room's audio routing.")]
        bool m_KeyboardPlay = true;

        [SerializeField, Tooltip("Seconds between strings on a keyboard strum.")]
        float m_DeskStrumSpread = 0.014f;

        readonly StringSweep m_Sweep = new();
        readonly List<StringSweep.Crossing> m_Crossings = new();
        readonly Dictionary<(int, int), AudioClip> m_Clips = new();
        readonly StringBuilder m_Text = new();

        int m_Chord;
        float m_PulseTime = float.NegativeInfinity;
        float m_PulseStrength;

        public PluckedString[] Strings => m_Strings;
        public GuitarChord[] Chords => m_Chords;
        public int ChordIndex => m_Chord;
        public GuitarChord CurrentChord => m_Chords != null && m_Chords.Length > 0 ? m_Chords[m_Chord] : null;

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

            RenderFrettedNotes();

            if (m_PulseLight != null)
            {
                m_PulseLight.intensity = 0f;
                m_PulseLight.enabled = false;
            }

            SetChord(0);
        }

        /// <summary>
        /// Every note the progression can ask of every string, synthesised now so a chord change costs
        /// nothing at strum time. Keyed by string as well as pitch: the same note on a wound bass string
        /// and a plain treble one is not the same sound, and the strings carry their own timbre.
        /// Open strings are left to the strings themselves, which already render their own note.
        /// </summary>
        void RenderFrettedNotes()
        {
            m_Clips.Clear();
            if (m_Strings == null || m_Chords == null)
                return;

            int sampleRate = AudioSettings.outputSampleRate;

            foreach (var chord in m_Chords)
            {
                if (chord == null)
                    continue;

                for (int i = 0; i < m_Strings.Length; i++)
                {
                    int fret = chord.FretOf(i);
                    var pluckedString = m_Strings[i];
                    if (fret <= 0 || pluckedString == null)
                        continue;

                    int midi = pluckedString.Midi + fret;
                    if (m_Clips.ContainsKey((i, midi)))
                        continue;

                    m_Clips[(i, midi)] = PluckedStringSynth.Render($"{pluckedString.NoteName} string fret {fret}",
                        PluckedString.FrequencyOf(midi), pluckedString.RingSeconds, sampleRate, midi * 7 + i,
                        pluckedString.PluckPosition, pluckedString.Tone);
                }
            }
        }

        // After MiddleVR has moved HandNode for this frame, so the sweep uses the same wand pose the
        // walls are about to draw.
        void LateUpdate()
        {
            FadePulse();
            ReadButtons();

            if (m_KeyboardPlay)
                ReadKeyboard();

            if (m_Plectrum == null || m_Strings == null)
                return;

            m_Sweep.CatchRadius = m_CatchRadius;
            m_Sweep.ReleaseRadius = m_ReleaseRadius;
            m_Sweep.Cooldown = m_Cooldown;
            m_Sweep.MaxSweep = m_MaxSweep;

            m_Crossings.Clear();
            m_Sweep.Step(m_Plectrum, m_Strings, Time.time, Time.deltaTime, m_Crossings);
            if (m_Crossings.Count == 0)
                return;

            // In the order the tip met them, spread over the frame on the audio clock, so a strum
            // keeps its rake instead of landing as one chord on the next audio buffer.
            m_Crossings.Sort((a, b) => a.Along.CompareTo(b.Along));
            double now = AudioSettings.dspTime;
            float first = m_Crossings[0].Along;

            foreach (var crossing in m_Crossings)
                Pluck(crossing.Index, crossing.Speed, crossing.Direction,
                    now + (crossing.Along - first) * Time.deltaTime);
        }

        // ------------------------------------------------------------------ playing

        /// <summary>
        /// Sounds string <paramref name="index"/> at the current chord's fret, as if the tip had crossed
        /// it at <paramref name="speed"/> metres per second. A muted string is bent but not sounded, so
        /// the wand still reads as having touched it.
        /// </summary>
        public void Pluck(int index, float speed, float direction = 1f, double dspTime = 0d)
        {
            if (m_Strings == null || index < 0 || index >= m_Strings.Length || m_Strings[index] == null)
                return;

            var pluckedString = m_Strings[index];
            float strength = Mathf.Lerp(m_SoftestPluck, 1f, Mathf.Clamp01(speed / Mathf.Max(0.01f, m_FullPluckSpeed)));

            var chord = CurrentChord;
            int fret = chord != null ? chord.FretOf(index) : 0;

            if (fret < 0)
            {
                pluckedString.Pluck(null, strength * 0.4f, direction);
                return;
            }

            if (fret == 0)
                pluckedString.Pluck(strength, direction, dspTime);
            else if (m_Clips.TryGetValue((index, pluckedString.Midi + fret), out var clip))
                pluckedString.Pluck(clip, strength, direction, dspTime);
            else
                pluckedString.Pluck(strength, direction, dspTime);

            if (m_PulseLight != null)
            {
                m_PulseLight.color = pluckedString.Glow;
                m_PulseLight.enabled = true;
                m_PulseStrength = Mathf.Max(strength, m_PulseStrength * 0.5f);
                m_PulseTime = Time.time;
            }
        }

        /// <summary>A whole strum from the keyboard: low E to high for a downstroke, the reverse for an upstroke.</summary>
        public void Strum(bool downstroke)
        {
            if (m_Strings == null)
                return;

            double now = AudioSettings.dspTime;
            float speed = m_FullPluckSpeed * 0.7f;
            int count = m_Strings.Length;

            for (int k = 0; k < count; k++)
            {
                int index = downstroke ? k : count - 1 - k;
                Pluck(index, speed, downstroke ? 1f : -1f, now + k * m_DeskStrumSpread);
            }
        }

        // ------------------------------------------------------------------ chords

        public void NextChord() => SetChord(m_Chord + 1);
        public void PreviousChord() => SetChord(m_Chord - 1);

        /// <summary>Makes chord <paramref name="index"/> current (wrapping), mutes and marks the strings to match, and updates the signs.</summary>
        public void SetChord(int index)
        {
            if (m_Chords == null || m_Chords.Length == 0)
                return;

            m_Chord = ((index % m_Chords.Length) + m_Chords.Length) % m_Chords.Length;
            var chord = m_Chords[m_Chord];

            for (int i = 0; m_Strings != null && i < m_Strings.Length; i++)
            {
                int fret = chord != null ? chord.FretOf(i) : 0;

                if (m_Strings[i] != null)
                    m_Strings[i].Muted = fret < 0;

                if (m_ChordDots == null || i >= m_ChordDots.Length || m_ChordDots[i] == null)
                    continue;

                var dot = m_ChordDots[i];
                dot.gameObject.SetActive(fret >= 0);
                if (fret >= 0)
                {
                    var local = dot.localPosition;
                    local.x = MarkerX(fret);
                    dot.localPosition = local;
                }
            }

            if (m_ChordName != null)
                m_ChordName.text = chord != null ? chord.Name : "";

            if (m_Progression != null)
                m_Progression.text = ProgressionText();
        }

        /// <summary>
        /// Where a fret's marker sits along the neck: in the middle of the fret space, which for fret
        /// <paramref name="fret"/> lies between wire <c>fret − 1</c> and wire <c>fret</c> on a
        /// twelfth-root-of-two scale. Open strings mark just behind the nut.
        /// </summary>
        public float MarkerX(int fret)
        {
            if (fret <= 0)
                return m_OpenMarkerX;

            return (FretX(fret - 1) + FretX(fret)) * 0.5f;
        }

        public float FretX(int fret) => m_ScaleLength * (1f - Mathf.Pow(2f, -fret / 12f));

        string ProgressionText()
        {
            m_Text.Clear();
            string current = ColorUtility.ToHtmlStringRGB(m_CurrentColour);
            string other = ColorUtility.ToHtmlStringRGB(m_OtherColour);

            for (int i = 0; i < m_Chords.Length; i++)
            {
                if (i > 0)
                    m_Text.Append("    ");

                m_Text.Append("<color=#").Append(i == m_Chord ? current : other).Append('>');

                // In Direct selection the sign doubles as the button map, so a visitor handed the
                // wand can see which button plays which chord without being told.
                if (m_Selection == ChordSelection.Direct && m_Chords[i] != null && m_Chords[i].Button >= 0)
                    m_Text.Append("<size=55%>").Append(m_Chords[i].Button).Append("</size> ");

                m_Text.Append(m_Chords[i] != null ? m_Chords[i].Name : "?")
                      .Append("</color>");
            }

            return m_Text.ToString();
        }

        // ------------------------------------------------------------------ input

        void ReadButtons()
        {
            if (m_Selection == ChordSelection.Direct)
            {
                for (int i = 0; m_Chords != null && i < m_Chords.Length; i++)
                {
                    int button = m_Chords[i] != null ? m_Chords[i].Button : -1;
                    if (button >= 0 && MiddleVRWand.PressedThisFrame(button))
                    {
                        SetChord(i);
                        return;
                    }
                }

                return;
            }

            if (m_NextChordButton >= 0 && MiddleVRWand.PressedThisFrame(m_NextChordButton))
                NextChord();
            else if (m_PreviousChordButton >= 0 && MiddleVRWand.PressedThisFrame(m_PreviousChordButton))
                PreviousChord();
        }

        void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            // The arrows walk the progression whichever mode is on; the digits stand in for whatever
            // wand buttons the mode uses.
            if (keyboard.rightArrowKey.wasPressedThisFrame)
                NextChord();
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
                PreviousChord();

            if (m_Selection == ChordSelection.Direct)
            {
                for (int i = 0; m_Chords != null && i < m_Chords.Length; i++)
                {
                    if (m_Chords[i] != null && DigitPressed(keyboard, m_Chords[i].Button))
                    {
                        SetChord(i);
                        break;
                    }
                }
            }
            else if (DigitPressed(keyboard, m_NextChordButton))
            {
                NextChord();
            }
            else if (DigitPressed(keyboard, m_PreviousChordButton))
            {
                PreviousChord();
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
                Strum(true);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                Strum(false);
        }

        /// <summary>The number-row key printed with the same digit as a wand button, the way the marble run does it.</summary>
        static bool DigitPressed(Keyboard keyboard, int button)
        {
            if (button < 0 || button > 9)
                return false;

            var key = button == 0 ? Key.Digit0 : Key.Digit1 + (button - 1);
            return keyboard[key].wasPressedThisFrame;
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
                m_PulseStrength = 0f;
                return;
            }

            m_PulseLight.intensity = intensity;
        }
    }
}
