using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The noise and the mesh plumbing the outside worlds are generated from. The worlds are built at
    /// run time from a handful of numbers rather than saved as mesh assets: a terrain is tens of
    /// thousands of vertices, which as a scene-referenced asset is megabytes of text in version
    /// control, and the same numbers always generate the same land.
    /// </summary>
    public static class SimMeshes
    {
        // ================================================================== noise

        /// <summary>Fractal Perlin noise, 0–1. Offset well away from zero, where Unity's Perlin mirrors.</summary>
        public static float Fbm(float x, float y, int octaves = 4)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float norm = 0f;
            x += 1000.3f;
            y += 2000.7f;

            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * Mathf.PerlinNoise(x, y);
                norm += amplitude;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 31.7f;
                amplitude *= 0.5f;
            }

            return sum / norm;
        }

        /// <summary>Ridged fractal noise, 0–1: sharp crests where plain noise crosses its middle — mountain ridges.</summary>
        public static float Ridged(float x, float y, int octaves = 5)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float norm = 0f;
            float weight = 1f;
            x += 3000.1f;
            y += 4000.9f;

            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x, y) * 2f - 1f);
                n *= n * weight;
                weight = Mathf.Clamp01(n * 1.6f);
                sum += amplitude * n;
                norm += amplitude;
                x = x * 2.1f + 7.7f;
                y = y * 2.1f + 3.3f;
                amplitude *= 0.5f;
            }

            return sum / norm;
        }

        /// <summary>Noise round a circle, so a value that varies with angle meets itself again at 360°.</summary>
        public static float Around(float angle, float frequency, float seed, int octaves = 3)
        {
            return Fbm(Mathf.Cos(angle) * frequency + seed * 7.31f, Mathf.Sin(angle) * frequency + seed * 3.17f, octaves);
        }

        /// <summary>Steps: mostly flat ledges, each rising steeply into the next. Canyon rims read as strata this way.</summary>
        public static float Terrace(float height, float step)
        {
            if (step <= 0f)
                return height;

            float f = height / step;
            float floor = Mathf.Floor(f);
            float t = f - floor;
            return (floor + t * t * t * t) * step;
        }

        public static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // ================================================================== polar grid

        /// <summary>
        /// A flat or height-mapped disc of rings round the origin, the rings spaced geometrically so
        /// the vertex density falls off with distance the way the screen density of a fixed viewpoint
        /// does. UV0 is (angle 0–1 from straight ahead, clockwise from above; radius in metres).
        /// </summary>
        public static Mesh PolarGrid(string name, float innerRadius, float outerRadius, int rings, int segments,
            System.Func<float, float, float, float> height)
        {
            rings = Mathf.Max(2, rings);
            segments = Mathf.Max(3, segments);
            innerRadius = Mathf.Max(0.01f, innerRadius);

            int columns = segments + 1;
            var vertices = new Vector3[rings * columns];
            var uvs = new Vector2[vertices.Length];
            float ratio = outerRadius / innerRadius;

            for (int i = 0; i < rings; i++)
            {
                float r = innerRadius * Mathf.Pow(ratio, i / (float)(rings - 1));
                for (int j = 0; j < columns; j++)
                {
                    float u = j / (float)segments;
                    float angle = u * Mathf.PI * 2f;
                    float x = Mathf.Sin(angle) * r;
                    float z = Mathf.Cos(angle) * r;
                    float y = height != null ? height(x, z, r) : 0f;
                    vertices[i * columns + j] = new Vector3(x, y, z);
                    uvs[i * columns + j] = new Vector2(u, r);
                }
            }

            var triangles = new int[(rings - 1) * segments * 6];
            int k = 0;
            for (int i = 0; i < rings - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = i * columns + j;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    // Angle runs clockwise seen from above, so these face up.
                    triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                    triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                }
            }

            var mesh = NewMesh(name, vertices.Length);
            mesh.vertices = vertices;
            mesh.uv = uvs;

            // Every channel the land shader reads, filled: a missing one is not zero but whatever
            // the GPU finds there, and an empty second UV once lit a whole sector of canyon floor.
            mesh.uv2 = new Vector2[vertices.Length];
            var white = new Color32[vertices.Length];
            for (int i = 0; i < white.Length; i++)
                white[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = white;

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ================================================================== builders for loose geometry

        /// <summary>Vertices, triangles and the optional channels, gathered piece by piece and turned into one mesh.</summary>
        public class Builder
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<int> Triangles = new();
            public readonly List<Color> Colours = new();
            public readonly List<Vector2> Uv = new();
            public readonly List<Vector2> Uv2 = new();

            public int Count => Vertices.Count;

            public int Add(Vector3 position, Color colour, Vector2 uv = default, Vector2 uv2 = default)
            {
                Vertices.Add(position);
                Colours.Add(colour);
                Uv.Add(uv);
                Uv2.Add(uv2);
                return Vertices.Count - 1;
            }

            public void Triangle(int a, int b, int c)
            {
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }

            /// <summary>Two triangles between rings <paramref name="lower"/> and <paramref name="upper"/>, each <paramref name="count"/> vertices round, closed.</summary>
            public void Band(int lower, int upper, int count)
            {
                for (int j = 0; j < count; j++)
                {
                    int a = lower + j;
                    int b = lower + (j + 1) % count;
                    int c = upper + j;
                    int d = upper + (j + 1) % count;
                    Triangle(a, b, c);
                    Triangle(b, d, c);
                }
            }

            public Mesh ToMesh(string name, bool recalculateNormals = true)
            {
                var mesh = NewMesh(name, Vertices.Count);
                mesh.SetVertices(Vertices);
                mesh.SetColors(Colours);
                mesh.SetUVs(0, Uv);
                mesh.SetUVs(1, Uv2);
                mesh.SetTriangles(Triangles, 0);
                if (recalculateNormals)
                    mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ================================================================== spheres

        static Vector3[] s_IcoVertices;
        static int[] s_IcoTriangles;

        /// <summary>
        /// Adds a once-subdivided icosahedron, 42 vertices: round enough that cel shading reads it as
        /// a ball, cheap enough for a few thousand of them in one mesh.
        /// </summary>
        public static void Icosphere(Builder builder, Vector3 centre, Vector3 radii, Color colour, Vector2 uv2)
        {
            if (s_IcoVertices == null)
                BuildIcosphere();

            int start = builder.Count;
            foreach (var v in s_IcoVertices)
                builder.Add(centre + Vector3.Scale(v, radii), colour, Vector2.zero, uv2);

            for (int i = 0; i < s_IcoTriangles.Length; i += 3)
                builder.Triangle(start + s_IcoTriangles[i], start + s_IcoTriangles[i + 1], start + s_IcoTriangles[i + 2]);
        }

        static void BuildIcosphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vertices = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var midpoints = new Dictionary<long, int>();
            int Midpoint(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int index))
                    return index;

                vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                midpoints[key] = vertices.Count - 1;
                return vertices.Count - 1;
            }

            var subdivided = new List<int>(faces.Count * 4);
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                subdivided.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            s_IcoVertices = vertices.ToArray();
            s_IcoTriangles = subdivided.ToArray();
        }

        /// <summary>A latitude-longitude sphere for the sky dome. Its winding does not matter; the sky draws both sides.</summary>
        public static Mesh Dome(string name, float radius, int latitudes, int longitudes)
        {
            var builder = new Builder();
            for (int i = 0; i <= latitudes; i++)
            {
                float phi = Mathf.PI * i / latitudes - Mathf.PI * 0.5f;
                for (int j = 0; j <= longitudes; j++)
                {
                    float theta = Mathf.PI * 2f * j / longitudes;
                    var p = new Vector3(Mathf.Cos(phi) * Mathf.Sin(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Cos(theta));
                    builder.Add(p * radius, Color.white, new Vector2(j / (float)longitudes, i / (float)latitudes));
                }
            }

            int stride = longitudes + 1;
            for (int i = 0; i < latitudes; i++)
                for (int j = 0; j < longitudes; j++)
                {
                    int a = i * stride + j;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    builder.Triangle(a, c, b);
                    builder.Triangle(b, c, d);
                }

            return builder.ToMesh(name, false);
        }

        // ================================================================== mesh housekeeping

        /// <summary>A mesh that is never saved with the scene, with 32-bit indices once it needs them.</summary>
        public static Mesh NewMesh(string name, int vertexCount)
        {
            return new Mesh
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                indexFormat = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
        }

        /// <summary>
        /// Puts a generated mesh on a filter, replacing (and destroying) whatever generated mesh was
        /// there before, so regenerating does not leak.
        /// </summary>
        public static void Assign(MeshFilter filter, Mesh mesh)
        {
            if (filter == null)
                return;

            var old = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (old != null && old != mesh && (old.hideFlags & HideFlags.DontSave) != 0)
                Destroy(old);
        }

        public static void Destroy(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        /// <summary>Bounds that a shader-displaced mesh can never move out of, so it is not culled while it is on screen.</summary>
        public static void Inflate(Mesh mesh, float margin)
        {
            var bounds = mesh.bounds;
            bounds.Expand(margin * 2f);
            mesh.bounds = bounds;
        }
    }
}
