using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Pipes
{
    /// <summary>
    /// The part of the room the machine may throw through, in the rig's own space (the room's, in
    /// metres): the room box pulled in off every wall, a height cap under the top of the walls, and
    /// a cylinder round the standing area that nothing flies through. The solver and the validator
    /// both read it, so the numbers live in one place.
    /// </summary>
    /// <remarks>
    /// There is no ceiling screen and the back of the room is open, so anything above the walls or
    /// behind the people has no surface to be drawn on and simply vanishes. Measure the real wall
    /// height and room extents in MiddleVR coordinates before trusting the defaults.
    /// </remarks>
    [System.Serializable]
    public struct RigVolume
    {
        [Tooltip("Least corner of the box a flight keeps to: the left wall pulled in, the floor, the open back pulled in.")]
        public Vector3 Min;

        [Tooltip("Greatest corner. Its y is the height cap, comfortably under the top of the walls.")]
        public Vector3 Max;

        [Tooltip("Where the people stand, x and z. Nothing flies through a cylinder round it.")]
        public Vector2 KeepOutCentre;

        public float KeepOutRadius;

        [Tooltip("Head height: the keep-out runs from the floor to here.")]
        public float KeepOutHeight;

        public float Cap => Max.y;

        /// <summary>Inside the box, with a ball of <paramref name="radius"/> wholly inside it.</summary>
        public bool InBox(Vector3 p, float radius = 0f)
        {
            return p.x >= Min.x + radius && p.x <= Max.x - radius &&
                   p.y >= Min.y - 0.001f && p.y <= Max.y - radius &&
                   p.z >= Min.z + radius && p.z <= Max.z - radius;
        }

        /// <summary>Whether a ball of <paramref name="radius"/> at <paramref name="p"/> touches the people's cylinder.</summary>
        public bool InKeepOut(Vector3 p, float radius = 0f)
        {
            if (p.y - radius > KeepOutHeight)
                return false;

            float dx = p.x - KeepOutCentre.x;
            float dz = p.z - KeepOutCentre.y;
            float reach = KeepOutRadius + radius;
            return dx * dx + dz * dz < reach * reach;
        }

        /// <summary>The CAVE's defaults: a quarter metre off the side walls, a fifth off the front, a cap at 1.9 m, and the people at the back middle.</summary>
        public static RigVolume Default => new()
        {
            Min = new Vector3(-1.53f, 0f, -0.85f),
            Max = new Vector3(1.53f, 1.9f, 0.91f),
            KeepOutCentre = new Vector2(0f, -0.45f),
            KeepOutRadius = 0.5f,
            KeepOutHeight = 2f,
        };
    }

    /// <summary>
    /// Ballistic arcs under the rig's reduced gravity. A ball's launch velocity is solved backward
    /// from the note it must hit, so it is a pure function of where it starts, where it lands and
    /// how high it is allowed to go.
    /// </summary>
    public static class RigTrajectory
    {
        public const float MinFlight = 0.2f;
        public const float MaxFlight = 3f;

        /// <summary>
        /// The launch velocity that carries a ball from <paramref name="p0"/> to <paramref name="p1"/>
        /// over the highest arc whose apex stays at or under <paramref name="capY"/>, and how long it
        /// flies. The longest such flight is the slowest, most readable arc, and it always comes down
        /// onto the target from above. False when the cap is below either end: the target is
        /// misplaced, and is flagged rather than clamped.
        /// </summary>
        /// <remarks>
        /// The design notes find this flight time by bisection. It has a closed form — the time to
        /// rise from the launch to the cap plus the time to fall from the cap to the target — which
        /// is exact and costs two square roots.
        /// </remarks>
        public static bool Solve(Vector3 p0, Vector3 p1, float gravity, float capY, out Vector3 velocity, out float flight)
        {
            float rise = capY - p0.y;
            float fall = capY - p1.y;
            if (gravity <= 0f || rise < 0f || fall < 0f)
            {
                velocity = Vector3.zero;
                flight = 0f;
                return false;
            }

            flight = (Mathf.Sqrt(2f * gravity * rise) + Mathf.Sqrt(2f * gravity * fall)) / gravity;

            // Very short hops and very tall caps are held to sensible times; either way the apex only
            // comes down, never above the cap.
            flight = Mathf.Clamp(flight, MinFlight, MaxFlight);
            velocity = (p1 - p0 + 0.5f * gravity * flight * flight * Vector3.up) / flight;
            return true;
        }

        public static Vector3 At(Vector3 p0, Vector3 velocity, float gravity, float t)
        {
            return p0 + velocity * t + 0.5f * gravity * t * t * Vector3.down;
        }

        public static Vector3 VelocityAt(Vector3 velocity, float gravity, float t) => velocity + gravity * t * Vector3.down;
    }

    /// <summary>
    /// Every path the machine can take, worked out once from the layout: each pipe's arc to each
    /// target it serves, and each target's hop off and roll home. Built at load and checked against
    /// the <see cref="RigVolume"/>; a path that leaves the volume or crosses the people is reported,
    /// so a bad layout fails in the editor, never in front of an audience.
    /// </summary>
    public class RigPaths
    {
        /// <summary>A pipe's arc to a target.</summary>
        public struct Flight
        {
            public bool Valid;
            public Vector3 Start, Velocity, End;
            public float Time;
        }

        /// <summary>What a ball does after its strike: a hop to where it lands, then a roll home along a polyline.</summary>
        public struct Return
        {
            public Vector3 Start, Velocity, Landing;
            public float HopTime;
            public Vector3[] Roll;
            public float[] RollAt;                   // distance along the roll at each point
            public float RollLength;
        }

        /// <summary>Points each path is sampled at when it is checked.</summary>
        public const int Samples = 30;

        Flight[,] m_Flights = new Flight[0, 0];
        Return[] m_Returns = new Return[0];

        public float Gravity { get; private set; }
        public float BallRadius { get; private set; }

        /// <summary>The longest flight of any valid path: how far ahead the machine has to know a note.</summary>
        public float LongestFlight { get; private set; }

        public int PipeCount => m_Flights.GetLength(0);
        public int TargetCount => m_Flights.GetLength(1);

        public Flight FlightOf(int pipe, int target) =>
            pipe >= 0 && pipe < PipeCount && target >= 0 && target < TargetCount ? m_Flights[pipe, target] : default;

        public Return ReturnOf(int target) => target >= 0 && target < m_Returns.Length ? m_Returns[target] : default;

        /// <summary>
        /// Solves and checks every pipe-to-target arc the layout allows and every target's way
        /// home. Anything wrong is added to <paramref name="problems"/> as a sentence naming the
        /// pieces, and the arc is marked invalid so it is never thrown.
        /// </summary>
        public static RigPaths Build(Transform space, RigPipe[] pipes, RigTarget[] targets, RigVolume volume, float gravity,
            float ballRadius, List<string> problems)
        {
            pipes ??= new RigPipe[0];
            targets ??= new RigTarget[0];

            var paths = new RigPaths
            {
                Gravity = gravity,
                BallRadius = ballRadius,
                m_Flights = new Flight[pipes.Length, targets.Length],
                m_Returns = new Return[targets.Length],
            };

            for (int t = 0; t < targets.Length; t++)
            {
                var target = targets[t];
                if (target == null)
                {
                    problems?.Add($"Target {t} is missing.");
                    continue;
                }

                var end = target.HitPointIn(space);
                bool any = false;
                foreach (int p in target.Pipes ?? new int[0])
                {
                    if (p < 0 || p >= pipes.Length || pipes[p] == null)
                    {
                        problems?.Add($"{target.name} is served by pipe {p}, which does not exist.");
                        continue;
                    }

                    var start = pipes[p].MouthIn(space);
                    float cap = Mathf.Min(volume.Cap - ballRadius, target.Apex);
                    if (!RigTrajectory.Solve(start, end, gravity, cap, out var velocity, out float time))
                    {
                        problems?.Add($"{target.name} cannot be reached from {pipes[p].name}: its arc would have to top " +
                                      $"{Mathf.Max(start.y, end.y):0.00} m, over the {cap:0.00} m cap. Move it lower or lower the pipe.");
                        continue;
                    }

                    var flight = new Flight { Valid = true, Start = start, Velocity = velocity, End = end, Time = time };
                    if (!CheckFlight(flight, gravity, volume, ballRadius, out string why))
                    {
                        problems?.Add($"{pipes[p].name} → {target.name}: {why}");
                        continue;
                    }

                    paths.m_Flights[p, t] = flight;
                    paths.LongestFlight = Mathf.Max(paths.LongestFlight, time);
                    any = true;
                }

                if (!any)
                    problems?.Add($"{target.name} has no pipe that can reach it, so the machine never plays it.");

                paths.m_Returns[t] = SolveReturn(target, end, space, gravity, ballRadius, volume, problems);
            }

            return paths;
        }

        static bool CheckFlight(Flight flight, float gravity, RigVolume volume, float radius, out string why)
        {
            for (int i = 0; i <= Samples; i++)
            {
                float t = flight.Time * i / Samples;
                var p = RigTrajectory.At(flight.Start, flight.Velocity, gravity, t);

                // The mouth sits inside the bank and the target may sit right on the box's face; only
                // the open air between them must stay inside.
                if (i > 0 && i < Samples && !volume.InBox(p, radius))
                {
                    why = $"leaves the room's volume at ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}), {t:0.00} s into its {flight.Time:0.00} s flight.";
                    return false;
                }

                if (volume.InKeepOut(p, radius))
                {
                    why = $"passes through the people's space at ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}).";
                    return false;
                }
            }

            why = null;
            return true;
        }

        static Return SolveReturn(RigTarget target, Vector3 start, Transform space, float gravity, float radius, RigVolume volume,
            List<string> problems)
        {
            var result = new Return { Start = start, Landing = start, Roll = new[] { start }, RollAt = new[] { 0f } };

            var landing = target.Landing;
            float cap = Mathf.Max(start.y, landing.y) + Mathf.Max(0f, target.Hop);
            if (RigTrajectory.Solve(start, landing, gravity, cap, out var velocity, out float time))
            {
                result.Velocity = velocity;
                result.HopTime = time;
                result.Landing = landing;

                for (int i = 1; i <= Samples; i++)
                {
                    var p = RigTrajectory.At(start, velocity, gravity, time * i / Samples);
                    if (volume.InKeepOut(p, radius))
                    {
                        problems?.Add($"{target.name}'s hop off passes through the people's space.");
                        break;
                    }
                }
            }
            else
            {
                problems?.Add($"{target.name}'s landing is above where it is struck; its ball would have to fall upward.");
            }

            var roll = new List<Vector3> { result.Landing };
            if (target.Roll != null)
                roll.AddRange(target.Roll);

            result.Roll = roll.ToArray();
            result.RollAt = new float[roll.Count];
            for (int i = 1; i < roll.Count; i++)
            {
                result.RollAt[i] = result.RollAt[i - 1] + Vector3.Distance(roll[i - 1], roll[i]);
                if (volume.InKeepOut(roll[i], radius))
                    problems?.Add($"{target.name}'s roll home crosses the people's space at ({roll[i].x:0.00}, {roll[i].z:0.00}).");
            }

            result.RollLength = result.RollAt[roll.Count - 1];
            return result;
        }

        /// <summary>Where a ball rolling home is, <paramref name="distance"/> metres along <paramref name="ret"/>'s roll, and which way it is going.</summary>
        public static Vector3 AlongRoll(in Return ret, float distance, out Vector3 direction)
        {
            var roll = ret.Roll;
            direction = Vector3.forward;
            if (roll == null || roll.Length == 0)
                return ret.Landing;
            if (roll.Length == 1)
                return roll[0];

            for (int i = 1; i < roll.Length; i++)
            {
                if (distance > ret.RollAt[i] && i < roll.Length - 1)
                    continue;

                float span = Mathf.Max(1e-5f, ret.RollAt[i] - ret.RollAt[i - 1]);
                float t = Mathf.Clamp01((distance - ret.RollAt[i - 1]) / span);
                direction = (roll[i] - roll[i - 1]) / span;
                return Vector3.Lerp(roll[i - 1], roll[i], t);
            }

            return roll[roll.Length - 1];
        }
    }
}
