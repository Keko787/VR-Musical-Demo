using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// Snare and toms: a gorilla standing beyond the left wall, facing into the room, with a hollow log at its
    /// feet. A snare is a chest beat — one fist winds out and strikes the chest on the note, hands taking
    /// turns — and a tom is a fist raised and brought down on the log. The chest and the log glow where
    /// they are struck, so each hit has a visible cause from anywhere in the room.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the arms are solved, not keyed.</b> Each pose is a place for the fist — at rest by the knee,
    /// wound out at the shoulder, on the chest, on the log — and a two-bone solve turns that into shoulder
    /// and elbow angles every frame. Blending between places keeps the fist on a believable path, and a
    /// strike lands exactly on its target whatever the pose it started from.
    /// </para>
    /// <para>
    /// <b>Why hands are tracked separately.</b> Hits alternate hands by note index, and each hand reads its
    /// own last and next note. So one hand can still be recovering from its hit while the other winds up
    /// for the next — a snare roll is two arms working, not one arm teleporting between poses.
    /// </para>
    /// </remarks>
    public sealed class GorillaPerformer : Performer
    {
        const float k_UpperArm = 0.38f;
        const float k_Forearm = 0.36f;
        static readonly Vector3 k_BodyRest = new(0f, 0.45f, 0f);
        static readonly Vector3 k_Shoulder = new(0.42f, 0.68f, 0f);

        // Log at the gorilla's feet, lying across in front of it.
        static readonly Vector3 k_LogCentre = new(0f, 0.42f, 0.36f);
        const float k_LogRadius = 0.09f;

        static readonly Color k_Fur = new(0.24f, 0.26f, 0.33f);
        static readonly Color k_FurDark = new(0.18f, 0.19f, 0.25f);
        static readonly Color k_Skin = new(0.48f, 0.46f, 0.52f);
        static readonly Color k_Hand = new(0.36f, 0.35f, 0.42f);
        static readonly Color k_Silver = new(0.62f, 0.64f, 0.72f);
        static readonly Color k_Eye = new(0.12f, 0.08f, 0.06f);
        static readonly Color k_Wood = new(0.46f, 0.31f, 0.21f);
        static readonly Color k_WoodLight = new(0.70f, 0.55f, 0.38f);
        static readonly Color k_Rock = new(0.33f, 0.31f, 0.39f);
        static readonly Color k_ChestGlow = new(1.4f, 0.85f, 0.45f);
        static readonly Color k_LogGlow = new(1.6f, 0.75f, 0.25f);

        [Header("Hits")]
        [SerializeField, Tooltip("Seconds from the start of a windup to the fist landing on the note. Under 0.2 s, per the plan.")]
        float m_Lead = 0.16f;

        [SerializeField, Tooltip("Seconds for a fist to return to rest after a hit.")]
        float m_Recover = 0.3f;

        [SerializeField, Tooltip("How long a hit's glow takes to halve, seconds.")]
        float m_GlowHalfLife = 0.09f;

        [Header("Rig (set by Build)")]
        [SerializeField] Transform m_Body;
        [SerializeField] Transform m_Torso;
        [SerializeField] Transform m_Head;
        [SerializeField] Transform m_ShoulderLeft, m_ShoulderRight;
        [SerializeField] Transform m_ElbowLeft, m_ElbowRight;
        [SerializeField] CreatureTint m_Chest;
        [SerializeField] CreatureTint m_Log;

        float m_Seed;
        float m_LiveBlend;
        float m_Lean;

        void Awake() => m_Seed = Motion.Hash01(GetInstanceID()) * 100f;

        // ================================================================== perform

        protected override void Perform(float deltaTime)
        {
            if (m_Body == null)
                return;

            float clock = Clock + m_Seed;
            m_LiveBlend = Motion.Damp(m_LiveBlend, Live ? 1f : 0f, 0.5f, deltaTime);

            int last = Live ? Notes.LastAtOrBefore(SongTime) : -1;
            float chest = 0f, pound = 0f;

            for (int hand = 0; hand < 2; hand++)
            {
                float sx = hand == 0 ? -1f : 1f;
                var target = RestTarget(sx, clock, hand);
                if (Live)
                    target = HandTarget(hand, sx, last, target, ref chest, ref pound);

                var shoulder = hand == 0 ? m_ShoulderLeft : m_ShoulderRight;
                var elbow = hand == 0 ? m_ElbowLeft : m_ElbowRight;
                if (shoulder != null && elbow != null)
                    SolveArm(shoulder, elbow, target, new Vector3(sx, -0.8f, -0.35f));
            }

            // A pound throws the body forward over the log; a chest beat puffs it back.
            m_Lean = Motion.Damp(m_Lean, 6f * pound - 3f * chest, 0.04f, deltaTime);
            float sway = Mathf.Lerp(2f * Motion.Wave(clock, 0.12f), 2f * Motion.Wave((float)Beats, 0.5f), m_LiveBlend);
            m_Body.localPosition = k_BodyRest;
            m_Body.localRotation = Quaternion.Euler(m_Lean, 0f, sway);

            float breathe = Motion.Wave(clock, 0.25f);
            if (m_Torso != null)
                m_Torso.localScale = new Vector3(0.78f * (1f + 0.01f * breathe), 0.82f * (1f + 0.015f * breathe), 0.52f);

            PoseHead(clock, chest);
            PoseGlows();
        }

        /// <summary>Where a fist hangs at rest: by the knee, slightly forward, drifting a little.</summary>
        static Vector3 RestTarget(float sx, float clock, int hand) =>
            new(sx * 0.5f, 0.02f * Motion.Wave(clock, 0.3f, hand * 0.5f), 0.12f + 0.03f * Motion.Wave(clock, 0.17f, hand * 0.3f));

        /// <summary>
        /// This hand's fist target now: its last hit's recovery as the base, and its next hit's windup and
        /// strike on top. Hands alternate by note index, so a hand's notes are every other index.
        /// </summary>
        Vector3 HandTarget(int hand, float sx, int last, Vector3 rest, ref float chest, ref float pound)
        {
            int lastHand = last;
            if (lastHand >= 0 && (lastHand & 1) != hand)
                lastHand--;

            int nextHand = last + 1;
            if ((nextHand & 1) != hand)
                nextHand++;

            var pose = rest;
            if (lastHand >= 0)
            {
                var note = Notes[lastHand];
                Motion.Hit(SongTime - note.Time, m_Lead, m_Recover, out _, out float settle);
                if (settle > 0f)
                {
                    bool tom = GmDrum.IsTom(note.Pitch);
                    pose = Vector3.Lerp(rest, Strike(sx, tom), settle);
                    Accumulate(tom, settle, ref chest, ref pound);
                }
            }

            if (nextHand < Notes.Count)
            {
                var note = Notes[nextHand];
                double gap = lastHand >= 0 ? note.Time - Notes[lastHand].Time : double.MaxValue;
                float lead = (float)System.Math.Min(m_Lead, gap * 0.75);
                Motion.Hit(SongTime - note.Time, lead, m_Recover, out float windup, out float strike);
                if (windup > 0f)
                {
                    bool tom = GmDrum.IsTom(note.Pitch);

                    // A soft hit winds up less far.
                    var wound = Vector3.Lerp(pose, Windup(sx, tom), 0.6f + 0.4f * note.Velocity);
                    pose = Vector3.Lerp(Vector3.Lerp(pose, wound, windup), Strike(sx, tom), strike);
                    Accumulate(tom, strike, ref chest, ref pound);
                }
            }

            return pose;
        }

        static void Accumulate(bool tom, float amount, ref float chest, ref float pound)
        {
            if (tom)
                pound = Mathf.Max(pound, amount);
            else
                chest = Mathf.Max(chest, amount);
        }

        // Fist targets in the body's frame. The chest plate's face is at z ≈ 0.25; the log's top at body y ≈ 0.06.
        static Vector3 Windup(float sx, bool tom) => tom ? new Vector3(sx * 0.26f, 0.8f, 0.22f) : new Vector3(sx * 0.48f, 0.66f, 0.36f);

        static Vector3 Strike(float sx, bool tom) => tom
            ? new Vector3(sx * 0.2f, k_LogCentre.y - k_BodyRest.y + k_LogRadius + 0.07f, k_LogCentre.z)
            : new Vector3(sx * 0.11f, 0.56f, 0.33f);

        /// <summary>
        /// Two-bone solve: shoulder and elbow angles that put the fist on <paramref name="target"/> (body
        /// frame), with the elbow bending toward <paramref name="pole"/>. Limbs hang along their joint's −Y.
        /// </summary>
        static void SolveArm(Transform shoulder, Transform elbow, Vector3 target, Vector3 pole)
        {
            var origin = shoulder.localPosition;
            var toTarget = target - origin;
            float reach = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(k_UpperArm - k_Forearm) + 1e-3f, k_UpperArm + k_Forearm - 1e-3f);
            var direction = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector3.down;

            float cos = Mathf.Clamp((k_UpperArm * k_UpperArm + reach * reach - k_Forearm * k_Forearm) / (2f * k_UpperArm * reach), -1f, 1f);
            float sin = Mathf.Sqrt(1f - cos * cos);

            var bend = pole - direction * Vector3.Dot(pole, direction);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.Cross(direction, Vector3.right).normalized;

            var elbowAt = origin + k_UpperArm * (direction * cos + bend * sin);
            var fistAt = origin + direction * reach;

            var upper = Quaternion.FromToRotation(Vector3.down, elbowAt - origin);
            shoulder.localRotation = upper;
            elbow.localRotation = Quaternion.FromToRotation(Vector3.down, Quaternion.Inverse(upper) * (fistAt - elbowAt));
        }

        void PoseHead(float clock, float chest)
        {
            if (m_Head == null)
                return;

            // Idle it looks about; playing it nods on the beat and throws its head back on a chest beat.
            float look = 30f * (Mathf.PerlinNoise(clock * 0.11f, 3.7f) - 0.5f) * 2f * (1f - 0.6f * m_LiveBlend);
            float tilt = 6f * (Mathf.PerlinNoise(clock * 0.07f, 9.1f) - 0.5f) * 2f;
            float nod = m_LiveBlend * 2.5f * Motion.Wave((float)Beats, 1f);
            m_Head.localRotation = Quaternion.Euler(nod - 6f * chest, look, tilt);
        }

        void PoseGlows()
        {
            float chestGlow = 0f, logGlow = 0f;

            if (Live)
            {
                int last = Notes.LastAtOrBefore(SongTime);
                bool chestFound = false, logFound = false;
                for (int i = last; i >= 0 && i > last - 8 && !(chestFound && logFound); i--)
                {
                    bool tom = GmDrum.IsTom(Notes[i].Pitch);
                    if (tom && !logFound)
                    {
                        logGlow = Flash(i, true);
                        logFound = true;
                    }
                    else if (!tom && !chestFound)
                    {
                        chestGlow = Flash(i, false);
                        chestFound = true;
                    }
                }
            }

            if (m_Chest != null)
                m_Chest.Glow = chestGlow * Intensity;
            if (m_Log != null)
                m_Log.Glow = logGlow * Intensity;
        }

        /// <summary>The glow of note <paramref name="index"/>, capped so a fast roll does not flicker past the flash limit.</summary>
        float Flash(int index, bool tom)
        {
            var note = Notes[index];
            double gap = double.MaxValue;
            for (int i = index - 1; i >= 0 && i > index - 8; i--)
            {
                if (GmDrum.IsTom(Notes[i].Pitch) == tom)
                {
                    gap = note.Time - Notes[i].Time;
                    break;
                }
            }

            float cap = Mathf.Clamp01((float)gap * MaxFlashHz);
            return Motion.Pulse(SongTime - note.Time, 0f, m_GlowHalfLife) * note.Velocity * cap;
        }

        // ================================================================== build

        public override void Build(CreatureKit kit)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                kit.Limb(transform, s < 0 ? "Leg_L" : "Leg_R", new Vector3(0.16f * s, 0.46f, -0.02f), new Vector3(0f, 0f, 4f * s),
                    0.42f, 0.2f, k_Fur);
                kit.Ball(transform, s < 0 ? "Foot_L" : "Foot_R", new Vector3(0.17f * s, 0.04f, 0.05f), new Vector3(0.18f, 0.08f, 0.26f), k_Hand);
            }

            m_Body = kit.Node(transform, "Body", k_BodyRest);
            m_Torso = kit.Ball(m_Body, "Torso", new Vector3(0f, 0.42f, -0.04f), new Vector3(0.78f, 0.82f, 0.52f), k_Fur).transform;
            kit.Ball(m_Body, "Belly", new Vector3(0f, 0.18f, 0.02f), new Vector3(0.58f, 0.42f, 0.4f), k_Fur);

            // The drum: a lighter plate on the chest that glows on every snare.
            m_Chest = kit.Lit(m_Body, "Chest", kit.Sphere, new Vector3(0f, 0.55f, 0.19f), new Vector3(0.46f, 0.36f, 0.12f), k_Skin, k_ChestGlow);
            m_Chest.Glow = 0f;

            for (int s = -1; s <= 1; s += 2)
                kit.Ball(m_Body, "Shoulder", new Vector3(0.36f * s, 0.7f, -0.05f), new Vector3(0.32f, 0.28f, 0.32f), k_Silver);

            BuildHead(kit);
            BuildArm(kit, -1f, out m_ShoulderLeft, out m_ElbowLeft);
            BuildArm(kit, 1f, out m_ShoulderRight, out m_ElbowRight);

            // The log, on two stones, with lighter rings at the cut ends.
            m_Log = kit.Lit(transform, "Log", kit.Cylinder, k_LogCentre, new Vector3(k_LogRadius * 2f, 0.34f, k_LogRadius * 2f), k_Wood, k_LogGlow,
                new Vector3(0f, 0f, 90f));
            m_Log.Glow = 0f;
            for (int s = -1; s <= 1; s += 2)
            {
                kit.Ball(transform, "Log_End", k_LogCentre + new Vector3(0.335f * s, 0f, 0f), new Vector3(0.02f, 0.15f, 0.15f), k_WoodLight);
                kit.Ball(transform, "Log_Stone", new Vector3(0.24f * s, 0.16f, k_LogCentre.z), new Vector3(0.2f, 0.32f, 0.2f), k_Rock);
            }

            // Arms hanging at rest, so the Editor shows the pose the scene starts in.
            SolveArm(m_ShoulderLeft, m_ElbowLeft, RestTarget(-1f, 0f, 0), new Vector3(-1f, -0.8f, -0.35f));
            SolveArm(m_ShoulderRight, m_ElbowRight, RestTarget(1f, 0f, 1), new Vector3(1f, -0.8f, -0.35f));
        }

        void BuildHead(CreatureKit kit)
        {
            m_Head = kit.Node(m_Body, "Head", new Vector3(0f, 0.88f, 0.04f));
            kit.Ball(m_Head, "Skull", new Vector3(0f, 0.08f, 0f), new Vector3(0.32f, 0.34f, 0.3f), k_Fur);
            kit.Ball(m_Head, "Crest", new Vector3(0f, 0.19f, -0.04f), new Vector3(0.15f, 0.12f, 0.2f), k_Fur);
            kit.Ball(m_Head, "Brow", new Vector3(0f, 0.12f, 0.11f), new Vector3(0.28f, 0.07f, 0.1f), k_FurDark);
            kit.Ball(m_Head, "Muzzle", new Vector3(0f, -0.02f, 0.12f), new Vector3(0.22f, 0.16f, 0.14f), k_Skin);
            kit.Ball(m_Head, "Mouth", new Vector3(0f, -0.065f, 0.185f), new Vector3(0.1f, 0.018f, 0.03f), k_FurDark);

            for (int s = -1; s <= 1; s += 2)
            {
                kit.Ball(m_Head, "Nostril", new Vector3(0.03f * s, 0.02f, 0.19f), new Vector3(0.035f, 0.025f, 0.02f), k_FurDark);
                kit.Ball(m_Head, "Eye", new Vector3(0.065f * s, 0.075f, 0.14f), new Vector3(0.04f, 0.035f, 0.03f), k_Eye);
                kit.Ball(m_Head, "Eye_Glint", new Vector3(0.058f * s, 0.083f, 0.153f), new Vector3(0.012f, 0.012f, 0.008f), Color.white);
            }
        }

        void BuildArm(CreatureKit kit, float s, out Transform shoulder, out Transform elbow)
        {
            string side = s < 0f ? "L" : "R";
            shoulder = kit.Limb(m_Body, "Arm_" + side, new Vector3(k_Shoulder.x * s, k_Shoulder.y, k_Shoulder.z), Vector3.zero, k_UpperArm, 0.17f, k_Fur);
            elbow = kit.Limb(shoulder, "Forearm_" + side, new Vector3(0f, -k_UpperArm, 0f), Vector3.zero, k_Forearm, 0.15f, k_Fur);
            kit.Ball(elbow, "Fist_" + side, new Vector3(0f, -k_Forearm, 0f), new Vector3(0.17f, 0.15f, 0.17f), k_Hand);
        }
    }
}
