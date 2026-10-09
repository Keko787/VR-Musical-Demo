using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// The glass room's one picture, painted in code: pale floorboards. Written as a PNG so it is an
    /// ordinary texture asset, and deterministic, so a rebuild writes the same bytes and version
    /// control sees no change. Everything outside the glass is coloured by its shaders instead.
    /// </summary>
    static class GlassRoomTextures
    {
        public struct Set
        {
            public Texture2D Floorboards;
        }

        public static Set EnsureAll(string folder)
        {
            return new Set
            {
                Floorboards = Ensure($"{folder}/T_Glass_Floorboards.png", 256, 256, Floorboards),
            };
        }

        static Texture2D Ensure(string path, int width, int height, System.Func<float, float, Color> paint)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = (Color32)paint((x + 0.5f) / width, (y + 0.5f) / height);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            StringInstrumentBuilder.EnsureFolder(path);
            string full = Path.GetFullPath(path);
            if (!File.Exists(full) || !Same(File.ReadAllBytes(full), png))
                File.WriteAllBytes(full, png);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.wrapMode != TextureWrapMode.Repeat || importer.anisoLevel != 4 || !importer.mipmapEnabled))
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 4;
                importer.mipmapEnabled = true;
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

        static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>
        /// Four whitewashed oak boards running toward the front wall, with fine grain, a little
        /// colour between boards, thin dark seams, and staggered end joints. Tiles both ways.
        /// </summary>
        static Color Floorboards(float u, float v)
        {
            const int boards = 4;
            float across = u * boards;
            int board = Mathf.FloorToInt(across);
            float inBoard = across - board;

            // End joints, staggered board to board.
            float along = v + Hash(board, 3.1f) * 0.9f;
            float plank = Mathf.Floor(along);
            float inPlank = along - plank;

            var oak = new Color(0.86f, 0.8f, 0.71f);
            float tone = 0.94f + 0.1f * Hash(board, plank + 7.7f);
            float grain = 0.5f + 0.5f * Mathf.Sin((inBoard * 9f + Mathf.Sin(v * 6.283f * 3f + board) * 0.6f) * 6.283f);
            float fleck = Hash(Mathf.Floor(u * 256f), Mathf.Floor(v * 64f));
            var colour = oak * (tone * (0.95f + 0.05f * grain) * (0.985f + 0.015f * fleck));

            float seam = Mathf.Min(inBoard, 1f - inBoard) < 0.012f ? 0.72f : 1f;
            float joint = Mathf.Min(inPlank, 1f - inPlank) < 0.004f ? 0.8f : 1f;
            colour *= seam * joint;
            colour.a = 1f;
            return colour;
        }
    }
}
