using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The hi-hats: a woodpecker clinging to a trunk at waist height, right of the viewer, pecking once
    /// per hat note. An open hat lifts its head and raises its red crest for as long as the hat rings.
    /// The fastest part in the arrangement goes to the animal whose whole act is a fast, sharp tap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the peck is so short.</b> Hats come in sixteenths — about 7 a second at 110 BPM — so every
    /// peck has to fit its windup, strike and recovery inside one gap. The lead is 70 ms, under half a
    /// sixteenth, and <see cref="Performer.TryHit"/> shortens it further for anything faster; the strike
    /// pose is reached exactly on the note, which is the frame the eye compares with the tick it hears.
    /// </para>
    /// <para>
    /// The rig rocks the whole upper body from the feet and snaps the neck on top of that, the way a real
    /// woodpecker drives a peck from its body braced on its tail. The root's local +Z is the peck
    /// direction; the bird clings on the trunk's −Z side, so the builder turns the root until the viewer
    /// sees it in profile.
    /// </para>
    /// <para>
    /// The glow on the bark answers only the accents, and fades faster than the accents can come, capped
    /// by the room's flash rate — a light on every sixteenth would be a strobe.
    /// </para>
    /// </remarks>
    public sealed class WoodpeckerPerformer : Performer
    {
        // Pitch (degrees about local X) of the upper body from the feet and of the head on the neck, per pose.
        const float k_RestBody = -6f, k_WindupBody = -16f, k_StrikeBody = 2f;
        const float k_RestNeck = -8f, k_WindupNeck = -26f, k_StrikeNeck = 6f;
        const float k_LiftNeck = -38f, k_LiftBody = -4f;
        const float k_CrestDown = -62f, k_CrestUp = -12f;

        [Header("Peck")]
        [SerializeField, Range(0.02f, 0.2f), Tooltip("Seconds from the start of a peck to the strike. Kept under half a " +
            "sixteenth so each hat gets a whole peck.")]
        float m_Lead = 0.07f;

        [SerializeField, Range(0.02f, 0.3f), Tooltip("Seconds to come back to rest after a strike.")]
        float m_Recover = 0.065f;

        [SerializeField, Range(0f, 1f), Tooltip("Hats at or above this velocity are accents: the bark glows where they land.")]
        float m_AccentVelocity = 0.7f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Upper;
        [SerializeField] Transform m_Neck;
        [SerializeField] Transform m_Crest;
        [SerializeField] CreatureTint m_CrestTint;
        [SerializeField] CreatureTint m_Spot;

        readonly List<Note> m_Active = new();

        float m_Live, m_Lift;

        // ================================================================== body

        public override void Build(CreatureKit kit)
        {
            var bark = new Color(0.40f, 0.28f, 0.18f);
            var barkDark = new Color(0.26f, 0.18f, 0.12f);
            var barkLight = new Color(0.58f, 0.44f, 0.30f);
            var leaf = new Color(0.32f, 0.60f, 0.24f);
            var black = new Color(0.07f, 0.07f, 0.09f);
            var white = new Color(0.95f, 0.95f, 0.93f);
            var red = new Color(0.95f, 0.10f, 0.12f);
            var grey = new Color(0.55f, 0.55f, 0.58f);
            var feet = new Color(0.40f, 0.40f, 0.44f);

            // The trunk: a flared base, a broken-off top, a stub of a branch with two leaves, a few knots.
            kit.Solid(transform, "Trunk", kit.Cylinder, new Vector3(0f, 0.625f, 0f), new Vector3(0.14f, 0.625f, 0.14f), bark);
            kit.Ball(transform, "Trunk_Flare", new Vector3(0f, 0.03f, 0f), new Vector3(0.24f, 0.12f, 0.24f), bark);
            kit.Ball(transform, "Trunk_Top", new Vector3(0f, 1.25f, 0f), new Vector3(0.15f, 0.06f, 0.15f), barkLight);
            kit.Solid(transform, "Branch", kit.Cylinder, new Vector3(0.09f, 1.08f, 0f), new Vector3(0.045f, 0.07f, 0.045f), bark,
                new Vector3(0f, 0f, -55f));
            kit.Ball(transform, "Leaf_0", new Vector3(0.15f, 1.13f, 0.02f), new Vector3(0.06f, 0.015f, 0.035f), leaf, new Vector3(0f, 30f, 20f));
            kit.Ball(transform, "Leaf_1", new Vector3(0.14f, 1.14f, -0.03f), new Vector3(0.05f, 0.015f, 0.03f), leaf, new Vector3(0f, -40f, -15f));
            kit.Ball(transform, "Knot_0", new Vector3(0.065f, 0.48f, 0.02f), new Vector3(0.02f, 0.05f, 0.04f), barkDark);
            kit.Ball(transform, "Knot_1", new Vector3(-0.05f, 0.72f, 0.05f), new Vector3(0.035f, 0.045f, 0.03f), barkDark, new Vector3(0f, -45f, 0f));
            kit.Ball(transform, "Knot_2", new Vector3(0.02f, 1.12f, 0.066f), new Vector3(0.04f, 0.03f, 0.015f), barkDark);

            // The bird: its feet on the bark of the −Z face, facing +Z into the trunk. In this frame the
            // bark is at z ≈ +0.005.
            var bird = kit.Node(transform, "Woodpecker", new Vector3(0f, 0.95f, -0.075f));

            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                kit.Ball(bird, "Foot_" + side, new Vector3(0.018f * s, 0f, -0.004f), new Vector3(0.016f, 0.012f, 0.016f), feet);
                kit.Ball(bird, "Toe_" + side, new Vector3(0.018f * s, 0.012f, -0.002f), new Vector3(0.01f, 0.014f, 0.01f), feet);
            }

            // Stiff tail, braced on the bark below the feet.
            kit.Spike(bird, "Tail", new Vector3(0f, 0f, -0.025f), new Vector3(0.04f, 0.09f, 0.015f), black, new Vector3(165f, 0f, 0f));

            // The upper body rocks from the feet.
            m_Upper = kit.Node(bird, "Upper", new Vector3(0f, 0f, -0.03f), new Vector3(k_RestBody, 0f, 0f));
            kit.Ball(m_Upper, "Body", new Vector3(0f, 0.05f, -0.015f), new Vector3(0.07f, 0.14f, 0.07f), black);
            kit.Ball(m_Upper, "Breast", new Vector3(0f, 0.04f, 0.005f), new Vector3(0.05f, 0.10f, 0.03f), white);
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                kit.Ball(m_Upper, "Wing_" + side, new Vector3(0.034f * s, 0.055f, -0.02f), new Vector3(0.02f, 0.10f, 0.06f), black);
                for (int i = 0; i < 3; i++)
                    kit.Ball(m_Upper, $"Wing_Bar_{side}{i}", new Vector3(0.045f * s, 0.07f - i * 0.025f, -0.025f - i * 0.005f),
                        Vector3.one * 0.009f, white);
            }

            // Head on the neck: black cap, white cheek stripe and eye ring, red crest, long grey beak whose
            // tip just reaches the bark when the neck and body are at zero.
            m_Neck = kit.Node(m_Upper, "Neck", new Vector3(0f, 0.115f, -0.032f), new Vector3(k_RestNeck, 0f, 0f));
            kit.Ball(m_Neck, "Head", new Vector3(0f, 0.02f, 0f), new Vector3(0.055f, 0.055f, 0.06f), black);
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "L" : "R";
                kit.Ball(m_Neck, "Cheek_" + side, new Vector3(0.026f * s, 0.010f, 0.004f), new Vector3(0.012f, 0.014f, 0.045f), white);
                kit.Ball(m_Neck, "Eye_Ring_" + side, new Vector3(0.025f * s, 0.028f, 0.012f), Vector3.one * 0.016f, white);
                kit.Ball(m_Neck, "Eye_" + side, new Vector3(0.030f * s, 0.028f, 0.013f), Vector3.one * 0.010f, black);
                kit.Ball(m_Neck, "Eye_Glint_" + side, new Vector3(0.0345f * s, 0.031f, 0.016f), Vector3.one * 0.004f, Color.white);
            }

            kit.Spike(m_Neck, "Beak", new Vector3(0f, 0.018f, 0.024f), new Vector3(0.016f, 0.043f, 0.016f), grey, new Vector3(90f, 0f, 0f));

            m_Crest = kit.Node(m_Neck, "Crest", new Vector3(0f, 0.04f, -0.005f), new Vector3(k_CrestDown, 0f, 0f));
            m_CrestTint = kit.Lit(m_Crest, "Crest_Feathers", kit.Cone, Vector3.zero, new Vector3(0.03f, 0.05f, 0.028f), red,
                new Color(1f, 0.25f, 0.15f) * 1.2f);
            m_CrestTint.Glow = 0f;

            // Where the beak lands: a spot of light on the bark, emission only.
            m_Spot = kit.Glowing(bird, "Peck_Glow", kit.Sphere, new Vector3(0f, 0.132f, 0.002f), new Vector3(0.03f, 0.03f, 0.006f),
                new Color(0f, 0f, 0f, 0f), new Color(1.0f, 0.72f, 0.36f) * 2f);
            m_Spot.Glow = 0f;
        }

        // ================================================================== performance

        protected override void Perform(float deltaTime)
        {
            if (m_Upper == null)
                return;

            m_Live = Motion.Damp(m_Live, Live ? 1f : 0f, 0.3f, deltaTime);

            // The peck, from the song's hats or, idle, from an occasional tap of its own.
            float windup = 0f, strike = 0f;
            if (Live)
            {
                if (TryHit(m_Lead, m_Recover, out double tau, out float lead, out _, out _))
                    Motion.Hit(tau, lead, m_Recover, out windup, out strike);
            }
            else if (TryIdleTap(out double idleTau))
            {
                Motion.Hit(idleTau, 0.12f, 0.12f, out windup, out strike);
            }

            float body = Mathf.Lerp(Mathf.Lerp(k_RestBody, k_WindupBody, windup), k_StrikeBody, strike);
            float neck = Mathf.Lerp(Mathf.Lerp(k_RestNeck, k_WindupNeck, windup), k_StrikeNeck, strike);

            // An open hat lifts the head and the crest once its peck has landed, for as long as it rings.
            float liftTarget = 0f;
            ActiveNotes(m_Active);
            foreach (var note in m_Active)
                if (GmDrum.IsOpenHat(note.Pitch) && SongTime - note.Time > 0.06)
                    liftTarget = 1f;

            m_Lift = Motion.Damp(m_Lift, liftTarget, 0.05f, deltaTime);
            body += k_LiftBody * m_Lift;
            neck = Mathf.Lerp(neck, k_LiftNeck, m_Lift);

            // Idle: the head tilts to listen.
            float idle = 1f - m_Live;
            float roll = 14f * Mathf.Sin(Clock * 0.6f) * idle;
            float yaw = 10f * Mathf.Sin(Clock * 0.23f + 1.1f) * idle;

            m_Upper.localRotation = Quaternion.Euler(body, 0f, 0f);
            m_Neck.localRotation = Quaternion.Euler(neck, yaw, roll);

            float crestRaise = Mathf.Max(m_Lift, Mathf.Clamp01(Level()) * 0.3f);
            m_Crest.localRotation = Quaternion.Euler(Mathf.Lerp(k_CrestDown, k_CrestUp, crestRaise), 0f, 0f);
            m_CrestTint.Glow = m_Lift * Intensity;

            m_Spot.Glow = AccentGlow() * Intensity;
        }

        /// <summary>
        /// The bark's glow from the last accent: a quick flash that fades in under a tenth of a second,
        /// scaled down when accents come faster than the room's flash cap allows.
        /// </summary>
        float AccentGlow()
        {
            if (!TryLast(out var note, out int index) || note.Velocity < m_AccentVelocity)
                return 0f;

            float amount = 1f;
            for (int j = index - 1; j >= 0 && j > index - 16; j--)
            {
                if (Notes[j].Velocity < m_AccentVelocity)
                    continue;

                amount = Mathf.Clamp01((float)(note.Time - Notes[j].Time) * MaxFlashHz);
                break;
            }

            return Motion.Pulse(SongTime - note.Time, 0.005f, 0.07f) * amount;
        }

        /// <summary>
        /// An occasional tap between songs: one per cycle of a few seconds, at a varying point in it, and
        /// none in some cycles. Checks this cycle's tap and the next's, so a windup that starts before a
        /// cycle boundary is not cut off.
        /// </summary>
        static bool TryIdleTap(out double tau)
        {
            const float period = 3.1f;
            int cycle = Mathf.FloorToInt(Clock / period);
            for (int c = cycle; c <= cycle + 1; c++)
            {
                if (Motion.Hash01(c + 31) < 0.35f)
                    continue;

                float tapTime = c * period + 0.5f + Motion.Hash01(c) * 1.6f;
                tau = Clock - tapTime;
                if (tau > -0.12 && tau < 0.12)
                    return true;
            }

            tau = 0.0;
            return false;
        }
    }
}
