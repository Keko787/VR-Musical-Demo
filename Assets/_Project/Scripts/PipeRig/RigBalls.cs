using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.Pipes
{
    /// <summary>What a throw is for: a note in the track, a hand on the trigger, the loop, or the backing beat.</summary>
    public enum RigShotSource
    {
        Track,
        Loop,
        Groove,
    }

    /// <summary>One throw: which pipe, at which target, and the moment on the machine's clock the ball must strike.</summary>
    public struct RigShot
    {
        public int Target;
        public int Pipe;
        public double Hit;
        public float Strength;
        public RigShotSource Source;

        /// <summary>The voice its sound was scheduled on, so taking the throw back can silence it; −1 for none.</summary>
        public int Voice;
    }

    /// <summary>
    /// The balls. Each throw's ball is placed every frame as a pure function of the machine's clock
    /// minus the moment it was thrown — on its arc, hopping off what it struck, or rolling home —
    /// with no rigidbody anywhere. A dropped frame cannot make a ball miss, a paused track freezes
    /// every ball where it is, and a seek needs nothing but clearing the old throws.
    /// </summary>
    /// <remarks>
    /// A fixed pool of balls is made from the prototype at load; when it runs dry a throw still
    /// strikes its target on time, it just has no ball to show. Each ball carries a short trail
    /// while it flies and casts a soft shadow on the floor, which is the depth cue for the tracked
    /// viewer and the rhythm for everyone else.
    /// </remarks>
    public class RigBalls : MonoBehaviour
    {
        [SerializeField, Tooltip("A ball: a sphere with a trail. Copied into the pool at load and kept hidden itself.")]
        GameObject m_BallPrototype;

        [SerializeField, Tooltip("A ball's shadow: a flat quad on the floor.")]
        GameObject m_ShadowPrototype;

        [SerializeField, Tooltip("Balls in the pool: the most that can be in the air and rolling home at once.")]
        int m_PoolSize = 160;

        [SerializeField, Tooltip("A ball's material for each instrument group, in RigGroup order, so it is plain which pipe feeds what.")]
        Material[] m_GroupMaterials = new Material[4];

        [SerializeField, Tooltip("A ball's trail colour for each instrument group, in RigGroup order.")]
        Color[] m_TrailColours = new Color[4];

        [SerializeField, Tooltip("Metres a second a ball rolls home.")]
        float m_RollSpeed = 1f;

        [SerializeField, Tooltip("Height of the shadows over the floor.")]
        float m_ShadowHeight = 0.008f;

        [SerializeField, Range(0f, 1f)] float m_ShadowOpacity = 0.8f;

        [SerializeField, Tooltip("Seconds before a throw that its pipe's nozzle starts to swing round to it.")]
        float m_AimLead = 0.45f;

        static readonly int k_Color = Shader.PropertyToID("_Color");

        struct Ball
        {
            public Transform Transform;
            public Renderer Renderer;
            public TrailRenderer Trail;
            public Transform Shadow;
            public Renderer ShadowRenderer;
        }

        struct Live
        {
            public RigShot Shot;
            public RigPaths.Flight Flight;
            public double Spawn;
            public int Ball;
            public bool Fired;
            public bool Struck;
            public float Rolled;
        }

        readonly List<Live> m_Live = new();
        readonly Stack<int> m_Free = new();
        Ball[] m_Balls = new Ball[0];
        MaterialPropertyBlock m_Block;

        RigPaths m_Paths;
        RigTarget[] m_Targets = new RigTarget[0];
        RigPipe[] m_Pipes = new RigPipe[0];
        Transform m_Space;
        float m_Radius = 0.035f;
        double[] m_NextSpawn = new double[0];
        Vector3[] m_NextDirection = new Vector3[0];

        /// <summary>Raised when a ball strikes its target.</summary>
        public event Action<RigShot> Struck;

        /// <summary>Throws waiting, in the air or rolling home.</summary>
        public int Count => m_Live.Count;

        /// <summary>Balls showing now.</summary>
        public int Showing => m_Balls.Length - m_Free.Count;

        public void Init(RigPaths paths, RigTarget[] targets, RigPipe[] pipes, Transform space)
        {
            m_Paths = paths;
            m_Targets = targets ?? new RigTarget[0];
            m_Pipes = pipes ?? new RigPipe[0];
            m_Space = space;
            m_Radius = paths != null ? paths.BallRadius : m_Radius;
            m_NextSpawn = new double[m_Pipes.Length];
            m_NextDirection = new Vector3[m_Pipes.Length];
            m_Block ??= new MaterialPropertyBlock();

            if (m_Balls.Length == 0)
                MakePool();
        }

        void MakePool()
        {
            if (m_BallPrototype == null)
                return;

            m_BallPrototype.SetActive(false);
            if (m_ShadowPrototype != null)
                m_ShadowPrototype.SetActive(false);

            m_Balls = new Ball[Mathf.Max(1, m_PoolSize)];
            for (int i = 0; i < m_Balls.Length; i++)
            {
                var ball = Instantiate(m_BallPrototype, transform, false);
                ball.name = $"Ball_{i:000}";
                var entry = new Ball
                {
                    Transform = ball.transform,
                    Renderer = ball.GetComponent<Renderer>(),
                    Trail = ball.GetComponent<TrailRenderer>(),
                };

                if (m_ShadowPrototype != null)
                {
                    var shadow = Instantiate(m_ShadowPrototype, transform, false);
                    shadow.name = $"Shadow_{i:000}";
                    entry.Shadow = shadow.transform;
                    entry.ShadowRenderer = shadow.GetComponent<Renderer>();
                }

                m_Balls[i] = entry;
            }

            for (int i = m_Balls.Length - 1; i >= 0; i--)
                m_Free.Push(i);
        }

        /// <summary>Queues a throw. False if its pipe cannot reach its target, which the layout check will already have said.</summary>
        public bool Add(RigShot shot)
        {
            if (m_Paths == null)
                return false;

            var flight = m_Paths.FlightOf(shot.Pipe, shot.Target);
            if (!flight.Valid)
                return false;

            m_Live.Add(new Live { Shot = shot, Flight = flight, Spawn = shot.Hit - flight.Time, Ball = -1 });
            return true;
        }

        /// <summary>When a throw from <paramref name="pipe"/> at <paramref name="target"/> would have to leave to strike on time.</summary>
        public double SpawnFor(int pipe, int target, double hit)
        {
            var flight = m_Paths != null ? m_Paths.FlightOf(pipe, target) : default;
            return flight.Valid ? hit - flight.Time : double.PositiveInfinity;
        }

        /// <summary>Every throw and every ball gone at once: a change of mode, a seek, a new track.</summary>
        public void Clear()
        {
            for (int i = 0; i < m_Live.Count; i++)
                Release(m_Live[i].Ball);
            m_Live.Clear();
        }

        /// <summary>
        /// Takes back the throws from <paramref name="source"/> that have not left their pipes yet,
        /// adding each to <paramref name="removed"/> so its scheduled sound can be stopped too.
        /// Balls already in the air carry on.
        /// </summary>
        public void RemoveUnlaunched(RigShotSource source, List<RigShot> removed)
        {
            for (int i = m_Live.Count - 1; i >= 0; i--)
            {
                var live = m_Live[i];
                if (live.Fired || live.Shot.Source != source)
                    continue;

                removed?.Add(live.Shot);
                m_Live.RemoveAt(i);
            }
        }

        /// <summary>Moves everything to <paramref name="clock"/>: throws leave, fly, strike, hop off and roll home.</summary>
        public void Tick(double clock)
        {
            if (m_Paths == null)
                return;

            for (int p = 0; p < m_NextSpawn.Length; p++)
                m_NextSpawn[p] = double.PositiveInfinity;

            float gravity = m_Paths.Gravity;
            for (int i = m_Live.Count - 1; i >= 0; i--)
            {
                var live = m_Live[i];
                double since = clock - live.Spawn;

                if (since < 0.0)
                {
                    int pipe = live.Shot.Pipe;
                    if (pipe >= 0 && pipe < m_NextSpawn.Length && live.Spawn < m_NextSpawn[pipe])
                    {
                        m_NextSpawn[pipe] = live.Spawn;
                        m_NextDirection[pipe] = live.Flight.Velocity;
                    }

                    continue;
                }

                if (!live.Fired)
                {
                    live.Fired = true;
                    live.Ball = Take(live.Shot.Target);

                    // A throw only queued once it should already have left (the start of a track) appears
                    // part way along its arc, without the pipe pretending to have fired it.
                    if (since < 0.1 && live.Shot.Pipe >= 0 && live.Shot.Pipe < m_Pipes.Length && m_Pipes[live.Shot.Pipe] != null)
                        m_Pipes[live.Shot.Pipe].Fire(live.Shot.Strength);
                }

                float t = (float)since;
                Vector3 position;
                bool flying = t < live.Flight.Time;
                bool done = false;
                if (flying)
                {
                    position = RigTrajectory.At(live.Flight.Start, live.Flight.Velocity, gravity, t);
                }
                else
                {
                    if (!live.Struck)
                    {
                        live.Struck = true;
                        var target = Target(live.Shot.Target);
                        if (target != null)
                            target.Strike(live.Shot.Strength);
                        Struck?.Invoke(live.Shot);
                    }

                    var ret = m_Paths.ReturnOf(live.Shot.Target);
                    float after = t - live.Flight.Time;
                    if (after < ret.HopTime)
                    {
                        position = RigTrajectory.At(ret.Start, ret.Velocity, gravity, after);
                    }
                    else
                    {
                        float distance = (after - ret.HopTime) * m_RollSpeed;
                        position = RigPaths.AlongRoll(ret, distance, out var direction);
                        if (live.Ball >= 0)
                            Spin(live.Ball, direction, distance - live.Rolled);
                        live.Rolled = distance;
                        done = distance >= ret.RollLength || live.Ball < 0;
                    }
                }

                if (done)
                {
                    Release(live.Ball);
                    m_Live.RemoveAt(i);
                    continue;
                }

                if (live.Ball >= 0)
                    Place(live.Ball, position, flying);

                m_Live[i] = live;
            }

            Aim(clock);
        }

        void Aim(double clock)
        {
            for (int p = 0; p < m_Pipes.Length; p++)
            {
                var pipe = m_Pipes[p];
                if (pipe == null)
                    continue;

                double wait = m_NextSpawn[p] - clock;
                if (double.IsInfinity(wait) || wait > m_AimLead)
                {
                    pipe.Aim(Vector3.up, 0f);
                    continue;
                }

                var direction = m_Space != null ? m_Space.TransformDirection(m_NextDirection[p]) : m_NextDirection[p];
                pipe.Aim(direction, 1f - (float)(wait / Mathf.Max(0.01f, m_AimLead)));
            }
        }

        RigTarget Target(int index) => index >= 0 && index < m_Targets.Length ? m_Targets[index] : null;

        int Take(int target)
        {
            if (m_Free.Count == 0)
                return -1;

            int index = m_Free.Pop();
            var ball = m_Balls[index];
            var t = Target(target);
            int group = t != null ? (int)t.Group : 0;
            if (ball.Renderer != null && group < m_GroupMaterials.Length && m_GroupMaterials[group] != null)
                ball.Renderer.sharedMaterial = m_GroupMaterials[group];

            if (ball.Trail != null)
            {
                var colour = group < m_TrailColours.Length ? m_TrailColours[group] : Color.white;
                ball.Trail.startColor = colour;
                ball.Trail.endColor = Color.black;
                ball.Trail.Clear();
                ball.Trail.emitting = false;           // until it has been placed, or it streaks from where it last was
            }

            ball.Transform.gameObject.SetActive(true);
            if (ball.Shadow != null)
                ball.Shadow.gameObject.SetActive(true);
            return index;
        }

        void Release(int index)
        {
            if (index < 0 || index >= m_Balls.Length)
                return;

            var ball = m_Balls[index];
            if (ball.Trail != null)
            {
                ball.Trail.emitting = false;
                ball.Trail.Clear();
            }

            ball.Transform.gameObject.SetActive(false);
            if (ball.Shadow != null)
                ball.Shadow.gameObject.SetActive(false);
            m_Free.Push(index);
        }

        void Place(int index, Vector3 position, bool flying)
        {
            var ball = m_Balls[index];
            ball.Transform.position = m_Space != null ? m_Space.TransformPoint(position) : position;

            if (ball.Trail != null && ball.Trail.emitting != flying)
            {
                if (flying)
                    ball.Trail.Clear();
                ball.Trail.emitting = flying;
            }

            if (ball.Shadow == null)
                return;

            // Higher is bigger and fainter, as a soft light from above would make it.
            float height = Mathf.Max(0f, position.y - m_Radius);
            var floor = new Vector3(position.x, m_ShadowHeight, position.z);
            ball.Shadow.position = m_Space != null ? m_Space.TransformPoint(floor) : floor;
            float size = m_Radius * 2f * (1.3f + height * 0.9f);
            ball.Shadow.localScale = new Vector3(size, size, 1f);

            if (ball.ShadowRenderer != null)
            {
                ball.ShadowRenderer.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_Color, new Color(0f, 0f, 0f, m_ShadowOpacity / (1f + height * 0.6f)));
                ball.ShadowRenderer.SetPropertyBlock(m_Block);
            }
        }

        /// <summary>Turns a rolling ball by the distance it has rolled, about the axis across its path.</summary>
        void Spin(int index, Vector3 direction, float distance)
        {
            if (distance <= 0f || m_Radius <= 0f)
                return;

            var transformOfBall = m_Balls[index].Transform;
            var worldDirection = m_Space != null ? m_Space.TransformDirection(direction) : direction;
            var axis = Vector3.Cross(Vector3.up, worldDirection);
            if (axis.sqrMagnitude < 1e-6f)
                return;

            transformOfBall.Rotate(axis.normalized, distance / m_Radius * Mathf.Rad2Deg, Space.World);
        }
    }
}
