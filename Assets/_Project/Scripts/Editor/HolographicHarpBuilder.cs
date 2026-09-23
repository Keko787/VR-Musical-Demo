using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.Harp;
using VRShootingGallery.Strings;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the holographic harp from the CAVE music deck: a wall of strings on one plane, standing
    /// inside the room within arm's reach of the tracked user, played by sweeping the wand through
    /// them. Strings are graduated in length under a curved neck the way a real harp's are, so the
    /// silhouette reads as a harp from anywhere in the room, glasses or not.
    ///
    /// Everything it makes lives under <c>CAVE_User_Room/Holographic_Harp</c> plus the shared
    /// <c>Wand_Plectrum</c> root, and is registered with Undo. The harp root's own position and
    /// rotation are the author's — move it to suit the player and rebuild, and it stays put.
    /// </summary>
    static class HolographicHarpBuilder
    {
        const string k_Tag = "Harp";
        const string k_CreateMenu = "VR Shooting Gallery/Harp/Create Harp Scene";
        const string k_BuildMenu = "VR Shooting Gallery/Harp/Rebuild Harp";
        const string k_ScenePath = "Assets/_Project/Scenes/Demos/Harp MiddleVR.unity";

        const string k_HarpRoot = "Holographic_Harp";
        const string k_FrameName = "Frame";
        const string k_StringsName = "Strings";
        const string k_LightName = "Pulse_Light";

        const string k_MeshPath = "Assets/_Project/Art/Meshes/HarpString.asset";
        const string k_StringMaterialPath = "Assets/_Project/Art/Materials/M_Harp_String.mat";
        const string k_StringCMaterialPath = "Assets/_Project/Art/Materials/M_Harp_String_C.mat";
        const string k_StringFMaterialPath = "Assets/_Project/Art/Materials/M_Harp_String_F.mat";
        const string k_FrameMaterialPath = "Assets/_Project/Art/Materials/M_Harp_Frame.mat";

        // ---------------------------------------------------------------- tuning

        /// <summary>C3. Three octaves of C major up to C6, which is the range of a lever harp's middle.</summary>
        const int k_LowestMidi = 48;
        const int k_Octaves = 3;

        static readonly int[] k_MajorScale = { 0, 2, 4, 5, 7, 9, 11 };
        static readonly string[] k_NoteNames = { "C", "D", "E", "F", "G", "A", "B" };

        /// <summary>Seconds to −60 dB. A real harp's bass strings ring for several seconds; the top ones do not.</summary>
        const float k_BassRing = 5f;
        const float k_TrebleRing = 2.2f;

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Metres in front of the room's centre the string plane stands. The tracked user plays from
        /// the middle of the floor, and this puts the strings a forearm away — inside the volume the
        /// deck calls arm's reach, and well clear of the front wall at 1.11 m.
        /// </summary>
        const float k_HarpZ = 0.5f;

        /// <summary>
        /// Centre-to-centre string spacing. A real harp's is about 1.4 cm; this is four times that,
        /// because the thing plucking is a wand held at arm's length, not a fingertip, and the catch
        /// radius round each string has to leave clear air between neighbours.
        /// </summary>
        const float k_StringSpacing = 0.055f;

        const float k_StringRadius = 0.004f;
        const int k_StringRings = 16;
        const int k_StringSides = 8;

        // The soundboard rises toward the treble and the neck falls toward it, so the bass string is
        // 1.28 m long and the top one 0.48 m. Every string's middle lands at chest height.
        const float k_SoundboardBassY = 0.62f;
        const float k_SoundboardTrebleY = 1.0f;
        const float k_NeckBassY = 1.9f;
        const float k_NeckTrebleY = 1.48f;

        const float k_FrameDepth = 0.1f;
        const float k_SoundboardThickness = 0.06f;
        const float k_NeckThickness = 0.045f;
        const float k_PillarRadius = 0.035f;

        /// <summary>How far outside the bass string the pillar stands.</summary>
        const float k_PillarInset = 0.09f;

        /// <summary>How far the soundboard and neck run past the outermost strings.</summary>
        const float k_FrameOverhang = 0.05f;

        // ---------------------------------------------------------------- response

        const float k_BassSwingDecay = 0.9f;
        const float k_TrebleSwingDecay = 0.45f;
        const float k_BassSwingRate = 8f;
        const float k_TrebleSwingRate = 13f;
        const float k_RestGlow = 0.5f;

        // ---------------------------------------------------------------- look

        // Hologram cyan, with the harpist's own colour code on top: C strings are red and F strings
        // are blue on every real harp, and it is how a player finds their place among forty strings.
        static readonly Color k_StringBase = new(0.75f, 0.95f, 1f);
        static readonly Color k_StringGlow = new(0.45f, 0.9f, 1f);
        static readonly Color k_CBase = new(1f, 0.62f, 0.7f);
        static readonly Color k_CGlow = new(1f, 0.3f, 0.45f);
        static readonly Color k_FBase = new(0.62f, 0.66f, 1f);
        static readonly Color k_FGlow = new(0.35f, 0.42f, 1f);

        static readonly Color k_FrameBase = new(0.5f, 0.85f, 1f);
        static readonly Color k_FrameGlow = new(0.12f, 0.35f, 0.5f);
        const float k_FrameAlpha = 0.3f;

        /// <summary>One string as planned: what it plays and where it runs, in harp space.</summary>
        readonly struct StringSpec
        {
            public readonly int Index;
            public readonly int Midi;
            public readonly string Name;
            public readonly int Degree;
            public readonly float X;
            public readonly float BottomY;
            public readonly float TopY;

            /// <summary>0 at the bass string, 1 at the top string.</summary>
            public readonly float T;

            public StringSpec(int index, int midi, string name, int degree, float x, float bottomY, float topY, float t)
            {
                Index = index;
                Midi = midi;
                Name = name;
                Degree = degree;
                X = x;
                BottomY = bottomY;
                TopY = topY;
                T = t;
            }
        }

        // ================================================================== menus

        [MenuItem(k_CreateMenu, false, 400)]
        static void CreateHarpScene()
        {
            CreateScene(k_Tag, k_ScenePath, "harp", Build,
                "Enter play mode and sweep the wand through the strings (or play the number row, Q..P and A..L at a desk).");
        }

        [MenuItem(k_BuildMenu, false, 410)]
        static void Rebuild() => RebuildInOpenScene(Build);

        // ================================================================== build

        static bool Build(Scene scene)
        {
            var room = RequireRoom(scene, k_Tag, "harp");
            if (room == null)
                return false;

            Undo.SetCurrentGroupName("Build Holographic Harp");
            int group = Undo.GetCurrentGroup();

            // Dropped so the materials are re-authored once per build rather than once per session —
            // otherwise editing a colour above and rebuilding would do nothing.
            s_Materials.Clear();

            var mesh = EnsureStringMesh(k_MeshPath, k_StringRadius, k_StringRings, k_StringSides);

            var harp = FindOrCreateInstrument(room, k_HarpRoot, new Vector3(0f, 0f, k_HarpZ), k_Tag);
            Clear(harp);

            var plan = Plan();
            BuildFrame(NewChild(harp, k_FrameName), plan);
            var strings = BuildStrings(NewChild(harp, k_StringsName), plan, mesh);
            var light = PulseLight(harp, k_LightName, new Vector3(0f, 1.2f, -0.25f), 3.5f);
            var plectrum = EnsurePlectrum(scene);

            Wire(harp, strings, light, plectrum);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = harp.gameObject;

            Debug.Log($"[{k_Tag}] Built {strings.Count} strings, {plan[0].Name} to {plan[plan.Count - 1].Name}, " +
                      $"{plan[plan.Count - 1].X - plan[0].X:0.##} m across at z {harp.localPosition.z:0.##}.", harp);
            return true;
        }

        /// <summary>
        /// Where every string goes. Evenly spaced across the harp, bass on the left as the player faces
        /// it, bottoms on a rising soundboard and tops on a neck that falls away as a parabola — flat
        /// beside the pillar, dropping fastest at the treble end, the way a harp's harmonic curve does.
        /// </summary>
        static List<StringSpec> Plan()
        {
            int count = k_Octaves * k_MajorScale.Length + 1;
            float span = (count - 1) * k_StringSpacing;
            var plan = new List<StringSpec>(count);

            for (int i = 0; i < count; i++)
            {
                int octave = i / k_MajorScale.Length;
                int degree = i % k_MajorScale.Length;
                int midi = k_LowestMidi + 12 * octave + k_MajorScale[degree];
                string name = k_NoteNames[degree] + (midi / 12 - 1);

                float t = count > 1 ? (float)i / (count - 1) : 0f;
                float x = -span * 0.5f + i * k_StringSpacing;
                float bottom = Mathf.Lerp(k_SoundboardBassY, k_SoundboardTrebleY, t);
                float top = k_NeckBassY - (k_NeckBassY - k_NeckTrebleY) * t * t;

                plan.Add(new StringSpec(i, midi, name, degree, x, bottom, top, t));
            }

            return plan;
        }

        /// <summary>
        /// Pillar, soundboard, neck and a foot, as translucent glowing slabs. The frame carries no
        /// sound and no collision; it is there so the strings hang from something and the whole reads
        /// as a harp in silhouette.
        /// </summary>
        static void BuildFrame(Transform frame, List<StringSpec> plan)
        {
            var first = plan[0];
            var last = plan[plan.Count - 1];
            var material = MaterialFor(k_FrameMaterialPath);

            float pillarX = first.X - k_PillarInset;
            float pillarHeight = k_NeckBassY + k_FrameOverhang;

            var pillar = Primitive(frame, PrimitiveType.Cylinder, "Pillar", material);
            pillar.localPosition = new Vector3(pillarX, pillarHeight * 0.5f, 0f);
            pillar.localScale = new Vector3(k_PillarRadius * 2f, pillarHeight * 0.5f, k_PillarRadius * 2f);

            var foot = Primitive(frame, PrimitiveType.Cube, "Foot", material);
            foot.localPosition = new Vector3(pillarX + 0.1f, 0.02f, 0f);
            foot.localScale = new Vector3(0.36f, 0.04f, k_FrameDepth + 0.08f);

            // Soundboard: one slab along the line of string bottoms, run past both ends, sitting just
            // under them so each string looks planted in it.
            float slope = (last.BottomY - first.BottomY) / (last.X - first.X);
            var boardStart = new Vector3(first.X - k_FrameOverhang, first.BottomY - slope * k_FrameOverhang, 0f);
            var boardEnd = new Vector3(last.X + k_FrameOverhang, last.BottomY + slope * k_FrameOverhang, 0f);
            Slab(frame, "Soundboard", boardStart, boardEnd, k_SoundboardThickness, k_FrameDepth,
                -k_SoundboardThickness * 0.45f, material);

            // Neck: a chain of short slabs from the pillar top along the string tops. Straight pieces,
            // but at one per string the curve reads as continuous.
            float lift = k_NeckThickness * 0.4f;
            var neck = new List<Vector3>(plan.Count + 2) { new(pillarX, first.TopY + lift, 0f) };
            foreach (var spec in plan)
                neck.Add(new Vector3(spec.X, spec.TopY + lift, 0f));
            neck.Add(new Vector3(last.X + k_FrameOverhang, last.TopY + lift, 0f));

            for (int i = 0; i < neck.Count - 1; i++)
                Slab(frame, $"Neck_{i:00}", neck[i], neck[i + 1], k_NeckThickness, k_FrameDepth, 0f, material);
        }

        static List<PluckedString> BuildStrings(Transform root, List<StringSpec> plan, Mesh mesh)
        {
            var strings = new List<PluckedString>(plan.Count);

            foreach (var spec in plan)
            {
                var settings = new StringSettings
                {
                    Midi = spec.Midi,
                    NoteName = spec.Name,
                    RingSeconds = Mathf.Lerp(k_BassRing, k_TrebleRing, spec.T),
                    PluckPosition = PluckedStringSynth.k_HarpPluckPosition,
                    Tone = PluckedStringSynth.k_HarpTone,
                    Radius = k_StringRadius,
                    Glow = StringGlow(spec.Degree),
                    SwingDecay = Mathf.Lerp(k_BassSwingDecay, k_TrebleSwingDecay, spec.T),
                    SwingRate = Mathf.Lerp(k_BassSwingRate, k_TrebleSwingRate, spec.T),
                    RestGlow = k_RestGlow,
                };

                var pluckedString = CreateString(k_Tag, root, $"String_{spec.Index:00}_{spec.Name}", mesh,
                    StringMaterial(spec.Degree), settings);

                // The mesh is one unit long; the length lives in the scale so every string shares it.
                var t = pluckedString.transform;
                t.localPosition = new Vector3(spec.X, spec.BottomY, 0f);
                t.localRotation = Quaternion.identity;
                t.localScale = new Vector3(1f, spec.TopY - spec.BottomY, 1f);

                strings.Add(pluckedString);
            }

            return strings;
        }

        static void Wire(Transform harp, List<PluckedString> strings, Light light, Transform plectrum)
        {
            // Reused rather than re-added, so anything tuned on it in the Inspector survives a rebuild.
            var component = harp.GetComponent<HolographicHarp>();
            if (component == null)
                component = Undo.AddComponent<HolographicHarp>(harp.gameObject);

            var so = new SerializedObject(component);
            SetArray(k_Tag, so, "m_Strings", strings);
            SetRef(k_Tag, so, "m_PulseLight", light);
            SetRef(k_Tag, so, "m_Plectrum", plectrum);
            so.ApplyModifiedProperties();
        }

        // ================================================================== palette

        static Material StringMaterial(int degree)
        {
            switch (degree)
            {
                case 0: return MaterialFor(k_StringCMaterialPath);
                case 3: return MaterialFor(k_StringFMaterialPath);
                default: return MaterialFor(k_StringMaterialPath);
            }
        }

        static Color StringGlow(int degree)
        {
            switch (degree)
            {
                case 0: return k_CGlow;
                case 3: return k_FGlow;
                default: return k_StringGlow;
            }
        }

        /// <summary>Cached because a build asks for the frame material once per slab, and each author is a SaveAssets.</summary>
        static readonly Dictionary<string, Material> s_Materials = new();

        /// <summary>
        /// The one place the palette is applied. The strings' authored emission is the rest glow the
        /// runtime starts from, so the harp looks the same in the editor as it does idle in play mode.
        /// </summary>
        static Material MaterialFor(string path)
        {
            if (s_Materials.TryGetValue(path, out var cached) && cached != null)
                return cached;

            Material material;
            switch (path)
            {
                case k_StringCMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_CBase, k_CGlow * k_RestGlow, 1f, 0.6f);
                    break;
                case k_StringFMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_FBase, k_FGlow * k_RestGlow, 1f, 0.6f);
                    break;
                case k_StringMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_StringBase, k_StringGlow * k_RestGlow, 1f, 0.6f);
                    break;
                default:
                    material = EnsureMaterial(k_Tag, path, k_FrameBase, k_FrameGlow, k_FrameAlpha, 0.7f);
                    break;
            }

            s_Materials[path] = material;
            return material;
        }
    }
}
