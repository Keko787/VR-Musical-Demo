using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Strings
{
    /// <summary>
    /// Finds the strings a tracked point crossed this frame. The tip's path since the last frame is
    /// treated as a line segment and measured against every string's segment; a string is caught
    /// when that distance drops inside the catch radius, and reported once until the tip has moved
    /// off it again and its cooldown has passed.
    /// </summary>
    /// <remarks>
    /// Geometric, not physical, on purpose. A trigger collider only sees the tip where the physics
    /// step happens to sample it, and a wand moving at 3 m/s crosses a 4 mm string in under two
    /// milliseconds — most plucks would fall between two 50 Hz ticks. The sweep never misses, costs
    /// nothing, and runs on the same frame's tracking data as the render. It also knows <em>where
    /// along the frame's movement</em> each string was met, which is what lets a strum across six
    /// strings keep its spread instead of landing as one chord on the next audio buffer.
    /// </remarks>
    public sealed class StringSweep
    {
        /// <summary>One string met by this frame's movement.</summary>
        public struct Crossing
        {
            public int Index;

            /// <summary>Tip speed over the frame, metres per second.</summary>
            public float Speed;

            /// <summary>Positive if the tip was moving along the string's forward axis, negative if against it.</summary>
            public float Direction;

            /// <summary>Where along the frame's movement the string was met, 0 at the last frame's position and 1 at this one.</summary>
            public float Along;
        }

        /// <summary>Metres from a string's surface at which the tip counts as touching it.</summary>
        public float CatchRadius = 0.02f;

        /// <summary>Metres the tip must move away before the same string can be caught again.</summary>
        public float ReleaseRadius = 0.04f;

        /// <summary>Seconds after a crossing before the same string is reported again.</summary>
        public float Cooldown = 0.08f;

        /// <summary>Longest tip movement in one frame that is believed; anything further is a tracking jump.</summary>
        public float MaxSweep = 1.5f;

        Vector3 m_PreviousTip;
        Transform m_TipParent;
        bool m_HaveTip;
        bool[] m_Touching = new bool[0];
        float[] m_ReadyAt = new float[0];

        /// <summary>Forgets the tip's trail, so the next frame starts fresh rather than sweeping from wherever it was.</summary>
        public void Reset()
        {
            m_HaveTip = false;
            System.Array.Clear(m_Touching, 0, m_Touching.Length);
        }

        /// <summary>
        /// Advances one frame. Crossings found are appended to <paramref name="crossings"/> in string
        /// order; sort by <see cref="Crossing.Along"/> for the order the tip met them.
        /// </summary>
        public void Step(Transform tip, IReadOnlyList<PluckedString> strings, float now, float deltaTime,
            List<Crossing> crossings)
        {
            if (tip == null || strings == null)
                return;

            if (m_Touching.Length != strings.Count)
            {
                m_Touching = new bool[strings.Count];
                m_ReadyAt = new float[strings.Count];
            }

            Vector3 position = tip.position;

            // The tip starts life at the scene root and is reparented under the wand's node by
            // MVRAttachToNode a frame or two in. The jump from the floor to the hand is not a sweep,
            // however short it is, so a change of parent restarts the trail.
            if (!m_HaveTip || tip.parent != m_TipParent)
            {
                m_TipParent = tip.parent;
                m_PreviousTip = position;
                m_HaveTip = true;
                System.Array.Clear(m_Touching, 0, m_Touching.Length);
                return;
            }

            Vector3 sweep = position - m_PreviousTip;
            Vector3 from = m_PreviousTip;
            m_PreviousTip = position;

            if (sweep.sqrMagnitude > MaxSweep * MaxSweep)
            {
                System.Array.Clear(m_Touching, 0, m_Touching.Length);
                return;
            }

            float speed = sweep.magnitude / Mathf.Max(deltaTime, 1e-4f);

            for (int i = 0; i < strings.Count; i++)
            {
                var pluckedString = strings[i];
                if (pluckedString == null)
                    continue;

                Vector3 start = pluckedString.Start;
                Vector3 end = pluckedString.End;

                if (m_Touching[i])
                {
                    // Only where the tip is now matters for letting go — the path it took to get there
                    // is what caught the string in the first place.
                    if (SegmentDistance(position, position, start, end, out _) > ReleaseRadius + pluckedString.Radius)
                        m_Touching[i] = false;

                    continue;
                }

                if (SegmentDistance(from, position, start, end, out float along) > CatchRadius + pluckedString.Radius)
                    continue;

                m_Touching[i] = true;
                if (now < m_ReadyAt[i])
                    continue;

                m_ReadyAt[i] = now + Cooldown;
                crossings.Add(new Crossing
                {
                    Index = i,
                    Speed = speed,
                    Direction = Vector3.Dot(sweep, pluckedString.transform.forward),
                    Along = along,
                });
            }
        }

        /// <summary>
        /// Shortest distance between segments p0–p1 and q0–q1, and where on the first segment it
        /// occurs. A point is a segment with both ends the same. Ericson, <em>Real-Time Collision
        /// Detection</em>, §5.1.9.
        /// </summary>
        public static float SegmentDistance(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, out float s)
        {
            const float epsilon = 1e-8f;

            Vector3 d1 = p1 - p0;
            Vector3 d2 = q1 - q0;
            Vector3 r = p0 - q0;

            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            float t;
            s = 0f;

            if (a <= epsilon && e <= epsilon)
                return r.magnitude;

            if (a <= epsilon)
            {
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denominator = a * e - b * b;

                    // Parallel segments have no single closest pair; any s will do.
                    s = denominator > epsilon ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;

                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            return ((p0 + d1 * s) - (q0 + d2 * t)).magnitude;
        }
    }
}
