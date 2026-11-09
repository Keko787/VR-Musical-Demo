using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// The CAVE's screens as one surface, for the 2D shows that run across them — rings that cross a
    /// corner, wax that rises from the floor up a wall. Built from the quads a show draws on, in the
    /// frame of the show's own object (the room's), so it follows the screens if the layout changes.
    /// </summary>
    /// <remarks>
    /// Two jobs. <see cref="ImageOn"/> says where a point on one screen lies as seen from another,
    /// with the screen it is on folded flat about the edge the two share, so a distance measured on a
    /// panel is the distance along the walls and floor; the side walls face each other with no shared
    /// edge, so a point on one reaches the other two ways round, and both are given. <see cref="Carry"/>
    /// moves a point that has stepped off its screen across an edge onto the screen on the other side,
    /// the way a ball rolling off the floor would climb the wall.
    /// </remarks>
    public sealed class SurfaceMap
    {
        /// <summary>A centre that is nowhere near: an unused second way round.</summary>
        public const float Far = 1e4f;

        /// <summary>A screen, in the map's frame.</summary>
        public struct Face
        {
            public bool Valid;
            public Vector3 Centre;
            public Vector3 Right;
            public Vector3 Up;
            public Vector3 Normal;                  // into the room
            public Vector2 Size;
            public bool Floor;

            public Vector2 Half => Size * 0.5f;
        }

        readonly Face[] m_Faces;

        public int Count => m_Faces.Length;
        public Face this[int index] => m_Faces[index];

        /// <summary>The floor's index, or −1 if none of the quads lies flat.</summary>
        public int Floor { get; }

        /// <summary>
        /// The faces of <paramref name="quads"/>, each a Unity Quad scaled to its screen's size, as seen
        /// from <paramref name="frame"/>. A missing quad is a face that is not <see cref="Face.Valid"/>.
        /// </summary>
        public SurfaceMap(Transform frame, IReadOnlyList<Renderer> quads, int max = 8)
        {
            int count = quads != null ? Mathf.Min(quads.Count, max) : 0;
            m_Faces = new Face[count];
            Floor = -1;

            for (int i = 0; i < count; i++)
            {
                if (quads[i] == null)
                    continue;

                var quad = quads[i].transform;
                var right = frame.InverseTransformDirection(quad.right).normalized;
                var up = frame.InverseTransformDirection(quad.up).normalized;
                var normal = Vector3.Cross(up, right);
                var scale = quad.localScale;

                m_Faces[i] = new Face
                {
                    Valid = true,
                    Centre = frame.InverseTransformPoint(quad.position),
                    Right = right,
                    Up = up,
                    Normal = normal,
                    Size = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)),
                    Floor = Vector3.Dot(normal, Vector3.up) > 0.7f,
                };

                if (m_Faces[i].Floor && Floor < 0)
                    Floor = i;
            }
        }

        /// <summary>A point's place on a face, in metres across and up from its middle.</summary>
        public Vector2 Local(int face, Vector3 point)
        {
            var offset = point - m_Faces[face].Centre;
            return new Vector2(Vector3.Dot(offset, m_Faces[face].Right), Vector3.Dot(offset, m_Faces[face].Up));
        }

        /// <summary>The point at <paramref name="local"/> metres across and up from a face's middle.</summary>
        public Vector3 Point(int face, Vector2 local) => m_Faces[face].Centre + m_Faces[face].Right * local.x + m_Faces[face].Up * local.y;

        public bool Adjacent(int a, int b) => Mathf.Abs(Vector3.Dot(m_Faces[a].Normal, m_Faces[b].Normal)) < 0.5f;

        /// <summary>
        /// Where <paramref name="position"/>, on face <paramref name="from"/>, lies as seen from face
        /// <paramref name="to"/>, in <paramref name="to"/>'s metres (xy), with the face it is on folded
        /// flat into <paramref name="to"/>'s plane. Two faces that face each other have no edge to fold
        /// about: the point comes round both ways, over each face that touches both, and zw is the
        /// second way; otherwise zw is <see cref="Far"/>.
        /// </summary>
        public Vector4 ImageOn(int to, Vector3 position, int from)
        {
            var target = m_Faces[to];
            if (to == from)
            {
                var on = Local(to, position);
                return new Vector4(on.x, on.y, Far, Far);
            }

            if (Adjacent(from, to))
            {
                var on = Local(to, Unfold(position, m_Faces[from], target));
                return new Vector4(on.x, on.y, Far, Far);
            }

            var first = new Vector2(Far, Far);
            var second = first;
            int routes = 0;
            for (int via = 0; via < m_Faces.Length && routes < 2; via++)
            {
                if (via == to || via == from || !m_Faces[via].Valid || !Adjacent(from, via) || !Adjacent(via, to))
                    continue;

                var flat = Unfold(position, m_Faces[from], m_Faces[via]);
                var on = Local(to, Unfold(flat, m_Faces[via], target));
                if (routes == 0)
                    first = on;
                else
                    second = on;
                routes++;
            }

            return new Vector4(first.x, first.y, second.x, second.y);
        }

        /// <summary>
        /// A point that has been moved off face <paramref name="face"/> in that face's plane, past an
        /// edge it shares with another face, is put on that face instead, as far in from the edge as
        /// it had gone past it, and <paramref name="face"/> becomes that face. Past an edge with no
        /// face beyond it (the open back of the room, the top of a wall) it is held on its own face,
        /// <paramref name="margin"/> in from the edge, and the answer is true.
        /// </summary>
        public bool Carry(ref Vector3 position, ref int face, float margin = 0f)
        {
            var local = Local(face, position);
            var half = m_Faces[face].Half;
            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y)
                return false;

            // The face it has gone past the edge of is the one whose plane it is now behind.
            int beyond = -1;
            float deepest = -1e-4f;
            for (int i = 0; i < m_Faces.Length; i++)
            {
                if (i == face || !m_Faces[i].Valid || !Adjacent(face, i))
                    continue;

                float depth = Vector3.Dot(position - m_Faces[i].Centre, m_Faces[i].Normal);
                if (depth < deepest)
                {
                    deepest = depth;
                    beyond = i;
                }
            }

            bool held = beyond < 0;
            if (!held)
            {
                position = Cross(position, m_Faces[face], m_Faces[beyond]);
                face = beyond;
            }

            // On the new face, held to it as well, in case it went past a corner.
            local = Local(face, position);
            half = m_Faces[face].Half;
            var inside = new Vector2(Mathf.Clamp(local.x, -half.x + margin, half.x - margin), Mathf.Clamp(local.y, -half.y + margin, half.y - margin));
            position = Point(face, inside);
            return held;
        }

        /// <summary>
        /// <paramref name="point"/>, which lies in <paramref name="from"/>'s plane, turned about the line
        /// where that plane meets <paramref name="to"/>'s until it lies in <paramref name="to"/>'s, on the
        /// far side of the line from <paramref name="to"/>'s middle: the two faces laid out flat.
        /// </summary>
        public static Vector3 Unfold(Vector3 point, in Face from, in Face to) => Turn(point, from, to, -1f);

        /// <summary>The same turn, but onto <paramref name="to"/>'s side of the line: a point carried over the edge.</summary>
        static Vector3 Cross(Vector3 point, in Face from, in Face to) => Turn(point, from, to, 1f);

        static Vector3 Turn(Vector3 point, in Face from, in Face to, float side)
        {
            var line = Vector3.Cross(from.Normal, to.Normal);
            float lengthSq = line.sqrMagnitude;
            if (lengthSq < 1e-6f)
                return point;

            // A point on both planes, n·x = h for each: x = (h1 (n2 × l) + h2 (l × n1)) / |l|².
            float h1 = Vector3.Dot(from.Normal, from.Centre);
            float h2 = Vector3.Dot(to.Normal, to.Centre);
            var onBoth = (h1 * Vector3.Cross(to.Normal, line) + h2 * Vector3.Cross(line, from.Normal)) / lengthSq;
            var axis = line / Mathf.Sqrt(lengthSq);

            var foot = onBoth + axis * Vector3.Dot(point - onBoth, axis);
            float distance = (point - foot).magnitude;

            var inward = to.Centre - foot;
            inward -= axis * Vector3.Dot(inward, axis);
            if (inward.sqrMagnitude < 1e-8f)
                return point;

            return foot + inward.normalized * (side * distance);
        }
    }
}
