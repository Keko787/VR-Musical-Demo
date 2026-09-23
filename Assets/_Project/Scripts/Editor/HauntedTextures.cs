using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRShootingGallery.Haunted;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// The haunted room's pictures, painted in code: wallpaper, floorboards, a rug, a portrait with
    /// hollows where its real eyes sit, a night landscape, the view from the window, and the ghost's
    /// shadow. Written as PNGs so they are ordinary texture assets, and deterministic, so a rebuild
    /// writes the same bytes and version control sees no change.
    /// </summary>
    /// <remarks>
    /// Painted rather than bought, so the scene builds from a fresh clone with nothing to download.
    /// Every one is a placeholder in the sense that a real texture can replace it at the same path.
    /// </remarks>
    static class HauntedTextures
    {
        public struct Set
        {
            public Texture2D Wallpaper;
            public Texture2D Floorboards;
            public Texture2D Rug;
            public Texture2D Portrait;
            public Texture2D Landscape;
            public Texture2D NightWindow;
            public Texture2D GhostShadow;
            public Texture2D Static;
        }

        /// <summary>Where on the portrait its eyes are, in UV. The builder sets the real eyeballs here.</summary>
        public static readonly Vector2 PortraitLeftEye = new(0.415f, 0.615f);
        public static readonly Vector2 PortraitRightEye = new(0.585f, 0.615f);

        public static Set EnsureAll(string folder)
        {
            return new Set
            {
                Wallpaper = Ensure($"{folder}/T_Wallpaper.png", 256, 256, true, false, Wallpaper),
                Floorboards = Ensure($"{folder}/T_Floorboards.png", 256, 256, true, false, Floorboards),
                Rug = Ensure($"{folder}/T_Rug.png", 256, 176, false, false, Rug),
                Portrait = Ensure($"{folder}/T_Portrait.png", 192, 240, false, false, Portrait),
                Landscape = Ensure($"{folder}/T_Landscape.png", 192, 144, false, false, Landscape),
                NightWindow = Ensure($"{folder}/T_NightWindow.png", 128, 160, false, false, NightWindow),
                GhostShadow = Ensure($"{folder}/T_GhostShadow.png", 128, 128, false, true, GhostShadow),
                Static = Ensure($"{folder}/T_Static.png", 96, 72, false, false, Static),
            };
        }

        // ================================================================== plumbing

        static Texture2D Ensure(string path, int width, int height, bool tile, bool alpha, Func<float, float, Color> paint)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var c = paint((x + 0.5f) / width, (y + 0.5f) / height);
                    pixels[y * width + x] = (Color32)new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b),
                        alpha ? Mathf.Clamp01(c.a) : 1f);
                }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            StringInstrumentBuilder.EnsureFolder(path);
            string full = Path.GetFullPath(path);
            if (!File.Exists(full) || !Same(File.ReadAllBytes(full), png))
                File.WriteAllBytes(full, png);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool dirty = false;
                dirty |= Change(importer.textureType != TextureImporterType.Default, () => importer.textureType = TextureImporterType.Default);
                dirty |= Change(importer.wrapMode != (tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp),
                    () => importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
                dirty |= Change(importer.alphaIsTransparency != alpha, () => importer.alphaIsTransparency = alpha);
                dirty |= Change(importer.alphaSource != (alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None),
                    () => importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None);
                dirty |= Change(importer.anisoLevel != 4, () => importer.anisoLevel = 4);
                dirty |= Change(!importer.mipmapEnabled, () => importer.mipmapEnabled = true);

                if (dirty)
                    importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;

            return true;
        }

        static bool Change(bool needed, Action apply)
        {
            if (needed)
                apply();
            return needed;
        }

        // ================================================================== maths for painting

        static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Value noise, tileable over <paramref name="period"/> cells so a repeating texture has no seam.</summary>
        static float Noise(float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float Corner(int cx, int cy) => Hash(Wrap(cx, period), Wrap(cy, period));
            float a = Corner(x0, y0), b = Corner(x0 + 1, y0), c = Corner(x0, y0 + 1), d = Corner(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Wrap(int v, int period) => period > 0 ? ((v % period) + period) % period : v;

        static float Fbm(float x, float y, int period)
        {
            float sum = 0f, amplitude = 0.5f;
            for (int o = 0; o < 4; o++)
            {
                sum += amplitude * Noise(x, y, period);
                x *= 2f;
                y *= 2f;
                period *= 2;
                amplitude *= 0.5f;
            }

            return sum;
        }

        static float Ellipse(float u, float v, float cu, float cv, float ru, float rv)
        {
            float a = (u - cu) / ru, b = (v - cv) / rv;
            return Mathf.Sqrt(a * a + b * b) - 1f;
        }

        static float Fill(float distance, float soft) => 1f - Step(-soft, soft, distance);

        static float Step(float edge0, float edge1, float x) => GhostSilhouette.Step(edge0, edge1, x);

        // ================================================================== the pictures

        /// <summary>
        /// Victorian damask in bottle green: a stripe, and a lozenge motif with a dot, on a paper
        /// with a little mottle. Tiles in both directions.
        /// </summary>
        static Color Wallpaper(float u, float v)
        {
            var paper = new Color(0.12f, 0.2f, 0.15f);
            var motif = new Color(0.2f, 0.3f, 0.22f);
            var gilt = new Color(0.42f, 0.36f, 0.2f);

            float mottle = Fbm(u * 8f, v * 8f, 8);
            var c = paper * (0.88f + 0.24f * mottle);

            // Two stripes a tile, thin gilt lines either side of a faintly lighter band.
            float su = Mathf.Repeat(u * 2f, 1f);
            float band = Step(0.44f, 0.46f, su) * (1f - Step(0.54f, 0.56f, su));
            c = Color.Lerp(c, c * 1.12f, band);
            float lines = Mathf.Max(1f - Step(0.002f, 0.008f, Mathf.Abs(su - 0.43f)),
                1f - Step(0.002f, 0.008f, Mathf.Abs(su - 0.57f)));
            c = Color.Lerp(c, gilt, lines * 0.7f);

            // A lozenge midway between each pair of stripes.
            float cu = Mathf.Repeat(u * 2f, 1f);
            float cv = Mathf.Repeat(v * 2f, 1f);
            float du = Mathf.Min(cu, 1f - cu);
            float lozenge = du / 0.14f + Mathf.Abs(cv - 0.5f) / 0.3f;
            float shape = Fill(lozenge - 1f, 0.06f) - Fill(lozenge - 0.7f, 0.06f) * 0.6f;
            c = Color.Lerp(c, motif, Mathf.Clamp01(shape));
            float dot = Fill(Mathf.Sqrt(du * du + (cv - 0.5f) * (cv - 0.5f)) / 0.035f - 1f, 0.3f);
            c = Color.Lerp(c, gilt * 0.9f, dot);

            return c;
        }

        /// <summary>Four boards across a tile, running along V, each its own shade, with grain and staggered end joints.</summary>
        static Color Floorboards(float u, float v)
        {
            float board = Mathf.Floor(u * 4f);
            float across = Mathf.Repeat(u * 4f, 1f);
            float shade = 0.8f + 0.35f * Hash(board, 3.3f);
            var wood = new Color(0.27f, 0.16f, 0.09f) * shade;

            float grain = Fbm(across * 3f + board * 7f, v * 22f, 22);
            grain += 0.5f * Mathf.Sin((across * 9f + grain * 3f) * Mathf.PI);
            var c = wood * (0.82f + 0.2f * grain);

            float gap = 1f - Step(0f, 0.035f, Mathf.Min(across, 1f - across));
            c = Color.Lerp(c, new Color(0.05f, 0.03f, 0.02f), gap);

            float joint = Mathf.Repeat(v + Hash(board, 9.1f), 1f);
            float end = 1f - Step(0f, 0.006f, Mathf.Min(joint, 1f - joint));
            return Color.Lerp(c, new Color(0.06f, 0.035f, 0.02f), end);
        }

        /// <summary>A worn Persian-ish rug: red field, dark blue border with gilt lines, a medallion in the middle.</summary>
        static Color Rug(float u, float v)
        {
            var field = new Color(0.42f, 0.07f, 0.07f);
            var border = new Color(0.07f, 0.09f, 0.22f);
            var gilt = new Color(0.62f, 0.48f, 0.22f);
            var cream = new Color(0.72f, 0.64f, 0.5f);

            float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 1.47f, Mathf.Min(v, 1f - v));
            float wear = Fbm(u * 12f, v * 9f, 0);
            var c = field * (0.8f + 0.35f * wear);

            if (edge < 0.1f)
                c = border * (0.85f + 0.3f * wear);

            float line = Mathf.Min(Mathf.Abs(edge - 0.1f), Mathf.Abs(edge - 0.035f));
            c = Color.Lerp(c, gilt, 1f - Step(0.002f, 0.008f, line));

            // The border's repeating pattern: small diamonds.
            if (edge < 0.1f && edge > 0.035f)
            {
                float along = (Mathf.Min(u, 1f - u) < Mathf.Min(v, 1f - v) ? v : u) * 18f;
                float d = Mathf.Abs(Mathf.Repeat(along, 1f) - 0.5f) + Mathf.Abs(edge - 0.0675f) / 0.03f * 0.5f;
                c = Color.Lerp(c, cream, Fill(d - 0.35f, 0.05f) * 0.7f);
            }

            // The medallion: nested lozenges.
            float m = Mathf.Abs(u - 0.5f) / 0.26f + Mathf.Abs(v - 0.5f) / 0.36f;
            if (m < 1f)
            {
                c = Color.Lerp(c, border * 1.2f, Fill(m - 1f, 0.02f) - Fill(m - 0.8f, 0.02f));
                c = Color.Lerp(c, cream * 0.9f, Fill(m - 0.45f, 0.02f) - Fill(m - 0.3f, 0.02f));
                c = Color.Lerp(c, gilt, Fill(m - 0.15f, 0.03f));
            }

            return c * (0.9f + 0.1f * Hash(u * 256f, v * 176f));
        }

        /// <summary>
        /// An oil portrait of someone long dead: a pale face in a dark coat against a brown ground,
        /// lit from the upper left, with shadowed hollows where the real eyes are set.
        /// </summary>
        static Color Portrait(float u, float v)
        {
            float brush = Fbm(u * 14f, v * 18f, 0);
            float vignette = 1f - 0.6f * Mathf.Clamp01(Ellipse(u, v, 0.5f, 0.55f, 0.55f, 0.6f) + 0.4f);
            var c = new Color(0.16f, 0.12f, 0.07f) * (0.7f + 0.5f * brush) * vignette;

            // Shoulders and coat.
            float coat = Ellipse(u, v, 0.5f, 0.02f, 0.52f, 0.34f);
            c = Color.Lerp(c, new Color(0.05f, 0.05f, 0.06f) * (0.8f + 0.4f * brush), Fill(coat, 0.02f));

            // Collar.
            float collar = Mathf.Abs(u - 0.5f) / 0.1f + Mathf.Abs(v - 0.29f) / 0.06f;
            c = Color.Lerp(c, new Color(0.78f, 0.74f, 0.66f) * (0.8f + 0.25f * brush), Fill(collar - 1f, 0.1f));

            // Neck.
            float neck = Ellipse(u, v, 0.5f, 0.36f, 0.075f, 0.09f);
            var skin = new Color(0.72f, 0.58f, 0.48f);
            c = Color.Lerp(c, skin * 0.6f, Fill(neck, 0.03f));

            // Face: an oval, lit from the upper left.
            float face = Ellipse(u, v, 0.5f, 0.58f, 0.15f, 0.2f);
            float light = Mathf.Clamp01(0.75f - 1.6f * (u - 0.44f) + 0.8f * (v - 0.58f));
            var faceColour = skin * (0.55f + 0.5f * light) * (0.92f + 0.12f * brush);
            c = Color.Lerp(c, faceColour, Fill(face, 0.03f));

            // Hair: a dark cap over the top of the face.
            float hair = Ellipse(u, v, 0.5f, 0.7f, 0.17f, 0.14f);
            float hairline = v - (0.7f + 0.03f * Mathf.Sin(u * 30f));
            c = Color.Lerp(c, new Color(0.08f, 0.05f, 0.03f) * (0.8f + 0.5f * brush), Fill(hair, 0.02f) * Step(-0.02f, 0.02f, hairline));

            // Eye hollows: where the real eyes sit, shadowed so they look set into the face.
            float hollow = Mathf.Min(Ellipse(u, v, PortraitLeftEye.x, PortraitLeftEye.y, 0.045f, 0.03f),
                Ellipse(u, v, PortraitRightEye.x, PortraitRightEye.y, 0.045f, 0.03f));
            c = Color.Lerp(c, faceColour * 0.35f, Fill(hollow, 0.35f));

            // Nose shadow and a thin, unsmiling mouth.
            float nose = Ellipse(u, v, 0.52f, 0.54f, 0.012f, 0.045f);
            c = Color.Lerp(c, faceColour * 0.7f, Fill(nose, 0.4f) * 0.8f);
            float mouth = Ellipse(u, v, 0.5f, 0.47f, 0.045f, 0.006f);
            c = Color.Lerp(c, new Color(0.35f, 0.16f, 0.14f), Fill(mouth, 0.4f));

            // Old varnish: yellowed and crazed.
            float crackle = Mathf.Abs(Mathf.Sin(u * 90f + Fbm(u * 6f, v * 6f, 0) * 12f) * Mathf.Sin(v * 70f));
            c *= 0.93f + 0.07f * Step(0.02f, 0.08f, crackle);
            return c * new Color(1f, 0.96f, 0.82f);
        }

        /// <summary>A moonlit landscape: hills, a bare tree, and a house with one window lit.</summary>
        static Color Landscape(float u, float v)
        {
            var sky = Color.Lerp(new Color(0.1f, 0.12f, 0.2f), new Color(0.03f, 0.03f, 0.08f), v);
            float brush = Fbm(u * 10f, v * 8f, 0);
            var c = sky * (0.85f + 0.3f * brush);

            float moon = Mathf.Sqrt((u - 0.72f) * (u - 0.72f) + (v - 0.72f) * (v - 0.72f) * 0.56f);
            c += new Color(0.5f, 0.5f, 0.42f) * Fill(moon - 0.06f, 0.01f);
            c += new Color(0.12f, 0.12f, 0.14f) * Mathf.Exp(-moon * 9f);

            float far = 0.42f + 0.06f * Mathf.Sin(u * 7f + 1f) + 0.03f * Mathf.Sin(u * 17f);
            if (v < far)
                c = new Color(0.06f, 0.07f, 0.09f) * (0.9f + 0.2f * brush);

            float near = 0.26f + 0.08f * Mathf.Sin(u * 4f + 2.5f);
            if (v < near)
                c = new Color(0.03f, 0.035f, 0.04f);

            // A house on the far hill, one window lit.
            float house = Mathf.Max(Mathf.Abs(u - 0.3f) - 0.05f, Mathf.Abs(v - 0.46f) - 0.035f);
            float roof = (v - 0.495f) + Mathf.Abs(u - 0.3f) * 0.8f - 0.045f;
            if (house < 0f || (roof < 0f && v > 0.49f))
                c = new Color(0.02f, 0.02f, 0.025f);
            if (Mathf.Abs(u - 0.31f) < 0.012f && Mathf.Abs(v - 0.465f) < 0.012f)
                c = new Color(0.9f, 0.7f, 0.3f);

            // A bare tree.
            float trunk = Mathf.Abs(u - 0.14f - 0.02f * (v - 0.26f)) - 0.006f * (1.2f - v);
            float branch = Mathf.Abs(Mathf.Sin((u - 0.14f) * 40f + v * 20f)) * 0.02f + Mathf.Abs(v - 0.55f - (u - 0.14f) * 0.5f) - 0.01f;
            if ((trunk < 0f && v < 0.62f) || (branch < 0f && Mathf.Abs(u - 0.14f) < 0.08f && v > 0.45f && v < 0.66f))
                c = new Color(0.015f, 0.015f, 0.02f);

            return c;
        }

        /// <summary>The view from the window at night: a deep sky, a hazy moon, a few stars, and a branch across it.</summary>
        static Color NightWindow(float u, float v)
        {
            var c = Color.Lerp(new Color(0.07f, 0.1f, 0.2f), new Color(0.02f, 0.025f, 0.06f), v);

            float moon = Mathf.Sqrt((u - 0.66f) * (u - 0.66f) + (v - 0.74f) * (v - 0.74f) * 0.64f);
            c += new Color(0.75f, 0.75f, 0.65f) * Fill(moon - 0.09f, 0.01f);
            c += new Color(0.18f, 0.2f, 0.26f) * Mathf.Exp(-moon * 7f);

            if (Hash(Mathf.Floor(u * 64f), Mathf.Floor(v * 80f)) > 0.985f && moon > 0.15f)
                c += new Color(0.5f, 0.5f, 0.55f) * Hash(u * 3f, v * 5f);

            float branch = Mathf.Abs(v - (0.3f + 0.35f * u + 0.03f * Mathf.Sin(u * 20f))) - 0.012f * (1.3f - u);
            float twig = Mathf.Abs(u - 0.42f - (v - 0.45f) * 0.6f) - 0.005f;
            if (branch < 0f || (twig < 0f && v > 0.45f && v < 0.62f))
                c = new Color(0.01f, 0.012f, 0.02f);

            return c;
        }

        /// <summary>A frame of dim snow for the television outside play mode; the running set draws its own.</summary>
        static Color Static(float u, float v)
        {
            float snow = Hash(Mathf.Floor(u * 96f), Mathf.Floor(v * 72f)) * 0.22f;
            float scan = Mathf.Repeat(v * 72f, 2f) < 1f ? 1f : 0.62f;
            float value = snow * scan;
            return new Color(value * 0.78f, value * 0.9f, value);
        }

        /// <summary>The ghost's shadow: its silhouette, soft-edged, black with the shape in alpha.</summary>
        static Color GhostShadow(float u, float v)
        {
            float cover = GhostSilhouette.Coverage(u, v, 0f, 0.035f, true);
            return new Color(0f, 0f, 0f, cover);
        }
    }
}
