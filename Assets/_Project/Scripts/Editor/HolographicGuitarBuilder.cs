using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.Guitar;
using VRShootingGallery.Strings;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the holographic guitar: six strings across a neck and body, hung in the air in front of
    /// the tracked user like a guitar on a wall — neck to the player's left, low E at the top — so a
    /// downstroke of the wand is a downstroke. The chord shapes do the fretting; the builder only
    /// draws the neck the shapes are shown on and the six markers they move.
    ///
    /// Everything it makes lives under <c>CAVE_User_Room/Holographic_Guitar</c> plus the shared
    /// <c>Wand_Plectrum</c> root, and is registered with Undo. The guitar root's own position and
    /// rotation are the author's — tilt the neck up if a playing angle reads better, and it stays.
    /// </summary>
    static class HolographicGuitarBuilder
    {
        const string k_Tag = "Guitar";
        const string k_CreateMenu = "VR Shooting Gallery/Guitar/Create Guitar Scene";
        const string k_BuildMenu = "VR Shooting Gallery/Guitar/Rebuild Guitar";
        const string k_ScenePath = "Assets/_Project/Scenes/Demos/Guitar MiddleVR.unity";

        const string k_GuitarRoot = "Holographic_Guitar";
        const string k_BodyName = "Body";
        const string k_NeckName = "Neck";
        const string k_StringsName = "Strings";
        const string k_DotsName = "Chord_Dots";
        const string k_LightName = "Pulse_Light";
        const string k_ChordNameText = "Chord_Name";
        const string k_ProgressionText = "Progression";

        const string k_MeshPath = "Assets/_Project/Art/Meshes/GuitarString.asset";
        const string k_WoundMaterialPath = "Assets/_Project/Art/Materials/M_Guitar_String_Wound.mat";
        const string k_PlainMaterialPath = "Assets/_Project/Art/Materials/M_Guitar_String_Plain.mat";
        const string k_BodyMaterialPath = "Assets/_Project/Art/Materials/M_Guitar_Body.mat";
        const string k_NeckMaterialPath = "Assets/_Project/Art/Materials/M_Guitar_Neck.mat";
        const string k_FretMaterialPath = "Assets/_Project/Art/Materials/M_Guitar_Fret.mat";
        const string k_HoleMaterialPath = "Assets/_Project/Art/Materials/M_Guitar_Hole.mat";
        const string k_DotMaterialPath = "Assets/_Project/Art/Materials/M_Guitar_Dot.mat";

        // ---------------------------------------------------------------- tuning

        /// <summary>Standard tuning, low E first: E2 A2 D3 G3 B3 E4.</summary>
        static readonly int[] k_Midi = { 40, 45, 50, 55, 59, 64 };
        static readonly string[] k_Names = { "E2", "A2", "D3", "G3", "B3", "E4" };

        /// <summary>Seconds to −60 dB per string. Steel strings on a box ring longer than a harp's.</summary>
        static readonly float[] k_Ring = { 6f, 5.5f, 5f, 4.5f, 4f, 3.5f };

        /// <summary>
        /// A pick near the bridge, not a thumb near the middle: the comb this puts on the attack keeps
        /// the upper harmonics a harp's pluck position removes, which is most of the difference
        /// between the two instruments' sounds before the body is even considered.
        /// </summary>
        const float k_PluckPosition = 0.15f;

        /// <summary>Attack brightness. The wound bass strings are darker than the plain trebles.</summary>
        const float k_BassTone = 0.6f;
        const float k_TrebleTone = 0.85f;

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Where the guitar's nut sits in the room. Chest height, half way to the front wall like the
        /// harp, and left of centre so the body — where the strumming happens — lands in front of a
        /// player standing in the middle of the floor.
        /// </summary>
        static readonly Vector3 k_GuitarPosition = new(-0.5f, 1.25f, 0.5f);

        /// <summary>
        /// Nut to bridge. Roughly double a real guitar's 0.65 m, because the strings have to be far
        /// enough apart for a wand and the neck has to stay in proportion to them.
        /// </summary>
        const float k_ScaleLength = 1.2f;

        /// <summary>Centre-to-centre string spacing — a real guitar's 1.1 cm would be one catch radius wide.</summary>
        const float k_StringSpacing = 0.05f;

        const float k_StringRadius = 0.004f;
        const int k_StringRings = 16;
        const int k_StringSides = 8;

        /// <summary>Frets drawn on the neck. The progression uses 0–3; twelve is where the neck meets the body.</summary>
        const int k_FretsShown = 12;

        const float k_NeckMargin = 0.05f;      // fretboard past the outer strings
        const float k_NeckThickness = 0.04f;
        const float k_FretWidth = 0.006f;
        const float k_NutWidth = 0.015f;
        const float k_HeadstockLength = 0.28f;
        const float k_BodyThickness = 0.06f;
        const float k_SoundholeRadius = 0.11f;
        const float k_DotRadius = 0.02f;
        const float k_InlayRadius = 0.008f;

        /// <summary>Where an open string's marker sits: just behind the nut, where the app draws its circle.</summary>
        const float k_OpenMarkerX = -0.05f;

        // Two overlapping ellipses make a guitar in silhouette: the smaller upper bout where the neck
        // joins, the larger lower bout below the bridge, and a waist where they meet.
        static readonly Vector2 k_UpperBoutCentre = new(0.80f, 0f);
        static readonly Vector2 k_UpperBoutRadii = new(0.21f, 0.27f);
        static readonly Vector2 k_LowerBoutCentre = new(1.10f, 0f);
        static readonly Vector2 k_LowerBoutRadii = new(0.25f, 0.33f);

        // ---------------------------------------------------------------- response

        const float k_BassSwingDecay = 1.0f;
        const float k_TrebleSwingDecay = 0.5f;
        const float k_BassSwingRate = 7f;
        const float k_TrebleSwingRate = 12f;
        const float k_RestGlow = 0.5f;

        // ---------------------------------------------------------------- look

        // Steel strings: the three wound bass strings amber, the three plain trebles silver-white,
        // which is also what they look like on a real guitar. The rest of the instrument is the
        // hologram cyan the harp uses, and the chord markers are the orange the chord app draws.
        static readonly Color k_WoundBase = new(1f, 0.85f, 0.6f);
        static readonly Color k_WoundGlow = new(1f, 0.7f, 0.3f);
        static readonly Color k_PlainBase = new(0.9f, 0.97f, 1f);
        static readonly Color k_PlainGlow = new(0.7f, 0.95f, 1f);

        static readonly Color k_BodyBase = new(0.5f, 0.85f, 1f);
        static readonly Color k_BodyGlow = new(0.1f, 0.3f, 0.45f);
        const float k_BodyAlpha = 0.28f;

        static readonly Color k_NeckBase = new(0.35f, 0.65f, 0.85f);
        static readonly Color k_NeckGlow = new(0.06f, 0.2f, 0.32f);
        const float k_NeckAlpha = 0.5f;

        static readonly Color k_FretBase = new(0.8f, 0.95f, 1f);
        static readonly Color k_FretGlow = new(0.3f, 0.7f, 0.9f);

        static readonly Color k_HoleBase = new(0.02f, 0.04f, 0.06f);
        const float k_HoleAlpha = 0.75f;

        static readonly Color k_DotBase = new(1f, 0.6f, 0.2f);
        static readonly Color k_DotGlow = new(2.2f, 1.1f, 0.3f);

        static readonly Color k_NameColour = new(0.93f, 0.95f, 1f);
        static readonly Color k_LineColour = new(0.55f, 0.62f, 0.72f);

        // ================================================================== menus

        [MenuItem(k_CreateMenu, false, 500)]
        static void CreateGuitarScene()
        {
            CreateScene(k_Tag, k_ScenePath, "guitar", Build,
                "Enter play mode, pick a chord on a wand button and sweep the wand down across the strings " +
                "(at a desk: digits pick, the up and down arrows strum).");
        }

        [MenuItem(k_BuildMenu, false, 510)]
        static void Rebuild() => RebuildInOpenScene(Build);

        // ================================================================== build

        static bool Build(Scene scene)
        {
            var room = RequireRoom(scene, k_Tag, "guitar");
            if (room == null)
                return false;

            Undo.SetCurrentGroupName("Build Holographic Guitar");
            int group = Undo.GetCurrentGroup();

            s_Materials.Clear();

            var mesh = EnsureStringMesh(k_MeshPath, k_StringRadius, k_StringRings, k_StringSides);

            var guitar = FindOrCreateInstrument(room, k_GuitarRoot, k_GuitarPosition, k_Tag);
            Clear(guitar);

            BuildBody(NewChild(guitar, k_BodyName));
            BuildNeck(NewChild(guitar, k_NeckName));
            var strings = BuildStrings(NewChild(guitar, k_StringsName), mesh);
            var dots = BuildDots(NewChild(guitar, k_DotsName));
            var light = PulseLight(guitar, k_LightName, new Vector3(k_UpperBoutCentre.x, 0f, -0.3f), 3f);
            var plectrum = EnsurePlectrum(scene);

            // Signs: the chord's name over the body, the whole progression under it. Text reads
            // correctly when its forward points away from the viewer, and the viewer is on the −Z
            // side, so the default rotation is already right.
            float top = StringY(0) + k_LowerBoutRadii.y + 0.12f;
            float bottom = StringY(5) - k_LowerBoutRadii.y - 0.05f;
            var name = Text(k_Tag, guitar, k_ChordNameText, new Vector3(k_UpperBoutCentre.x, top, 0f), 2.2f,
                "A", k_NameColour, new Vector2(2f, 0.5f));
            var line = Text(k_Tag, guitar, k_ProgressionText, new Vector3(k_UpperBoutCentre.x, bottom, 0f), 0.9f,
                "", k_LineColour, new Vector2(3f, 0.3f));

            Wire(guitar, strings, dots, light, plectrum, name, line);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = guitar.gameObject;

            Debug.Log($"[{k_Tag}] Built the guitar: {strings.Count} strings over a {k_ScaleLength} m scale, " +
                      $"{k_FretsShown} frets, at {guitar.localPosition}.", guitar);
            return true;
        }

        // ---------------------------------------------------------------- geometry

        static float FretX(int fret) => k_ScaleLength * (1f - Mathf.Pow(2f, -fret / 12f));

        /// <summary>Low E at the top, as a guitar hung on a wall in front of you: strings count down the Y axis.</summary>
        static float StringY(int index) => (2.5f - index) * k_StringSpacing;

        static float NeckHeight => 5f * k_StringSpacing + 2f * k_NeckMargin;

        /// <summary>
        /// Body, soundhole, bridge. The body sits just behind the string plane so the strings and the
        /// chord markers draw in front of it; the soundhole is a dark disc a whisker in front of the
        /// body's face so the two translucent surfaces never fight over depth.
        /// </summary>
        static void BuildBody(Transform body)
        {
            var material = MaterialFor(k_BodyMaterialPath);
            float behind = k_BodyThickness * 0.5f + 0.005f;

            Ellipse(body, "Upper_Bout", k_UpperBoutCentre, k_UpperBoutRadii, k_BodyThickness, behind, material);
            Ellipse(body, "Lower_Bout", k_LowerBoutCentre, k_LowerBoutRadii, k_BodyThickness, behind, material);

            Ellipse(body, "Soundhole", k_UpperBoutCentre, Vector2.one * k_SoundholeRadius, 0.006f, 0f,
                MaterialFor(k_HoleMaterialPath));

            var bridge = Primitive(body, PrimitiveType.Cube, "Bridge", MaterialFor(k_FretMaterialPath));
            bridge.localPosition = new Vector3(k_ScaleLength, 0f, 0f);
            bridge.localScale = new Vector3(0.035f, NeckHeight + 0.02f, 0.03f);
        }

        /// <summary>A flat cylinder lying in the XY plane: an ellipse <paramref name="radii"/> across, <paramref name="thickness"/> deep.</summary>
        static void Ellipse(Transform parent, string name, Vector2 centre, Vector2 radii, float thickness, float z,
            Material material)
        {
            var disc = Primitive(parent, PrimitiveType.Cylinder, name, material);
            disc.localPosition = new Vector3(centre.x, centre.y, z);

            // Unity's cylinder stands on Y and is two units tall; laid over on X it faces Z, and its
            // height becomes the depth.
            disc.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.localScale = new Vector3(radii.x * 2f, thickness * 0.5f, radii.y * 2f);
        }

        /// <summary>Fretboard, nut, frets, inlays, headstock and pegs, all behind the string plane.</summary>
        static void BuildNeck(Transform neck)
        {
            var neckMaterial = MaterialFor(k_NeckMaterialPath);
            var fretMaterial = MaterialFor(k_FretMaterialPath);
            float height = NeckHeight;
            float behind = k_NeckThickness * 0.5f + 0.003f;

            float boardStart = -0.02f;
            float boardEnd = FretX(k_FretsShown) + 0.04f;
            var board = Primitive(neck, PrimitiveType.Cube, "Fretboard", neckMaterial);
            board.localPosition = new Vector3((boardStart + boardEnd) * 0.5f, 0f, behind);
            board.localScale = new Vector3(boardEnd - boardStart, height, k_NeckThickness);

            var nut = Primitive(neck, PrimitiveType.Cube, "Nut", fretMaterial);
            nut.localPosition = new Vector3(0f, 0f, 0.002f);
            nut.localScale = new Vector3(k_NutWidth, height, 0.02f);

            for (int f = 1; f <= k_FretsShown; f++)
            {
                var fret = Primitive(neck, PrimitiveType.Cube, $"Fret_{f:00}", fretMaterial);
                fret.localPosition = new Vector3(FretX(f), 0f, 0.001f);
                fret.localScale = new Vector3(k_FretWidth, height, 0.012f);
            }

            // Position markers where every guitar has them: single at 3, 5, 7 and 9, double at 12.
            foreach (int f in new[] { 3, 5, 7, 9 })
                Inlay(neck, $"Inlay_{f:00}", f, 0f, fretMaterial);
            Inlay(neck, "Inlay_12a", 12, k_StringSpacing, fretMaterial);
            Inlay(neck, "Inlay_12b", 12, -k_StringSpacing, fretMaterial);

            var head = Primitive(neck, PrimitiveType.Cube, "Headstock", neckMaterial);
            head.localPosition = new Vector3(boardStart - k_HeadstockLength * 0.5f, 0f, behind);
            head.localScale = new Vector3(k_HeadstockLength, height - 0.04f, k_NeckThickness * 0.75f);

            for (int i = 0; i < 6; i++)
            {
                float x = boardStart - 0.06f - (i % 3) * 0.08f;
                float y = (i < 3 ? 1f : -1f) * (height * 0.5f + 0.01f);
                var peg = Primitive(neck, PrimitiveType.Sphere, $"Peg_{i}", fretMaterial);
                peg.localPosition = new Vector3(x, y, behind);
                peg.localScale = Vector3.one * 0.03f;
            }
        }

        static void Inlay(Transform neck, string name, int fret, float y, Material material)
        {
            var inlay = Primitive(neck, PrimitiveType.Sphere, name, material);
            inlay.localPosition = new Vector3((FretX(fret - 1) + FretX(fret)) * 0.5f, y, 0f);
            inlay.localScale = Vector3.one * (k_InlayRadius * 2f);
        }

        static List<PluckedString> BuildStrings(Transform root, Mesh mesh)
        {
            var strings = new List<PluckedString>(6);

            for (int i = 0; i < 6; i++)
            {
                float blend = i / 5f;
                bool wound = i < 3;

                var settings = new StringSettings
                {
                    Midi = k_Midi[i],
                    NoteName = k_Names[i],
                    RingSeconds = k_Ring[i],
                    PluckPosition = k_PluckPosition,
                    Tone = Mathf.Lerp(k_BassTone, k_TrebleTone, blend),
                    Radius = k_StringRadius,
                    Glow = wound ? k_WoundGlow : k_PlainGlow,
                    SwingDecay = Mathf.Lerp(k_BassSwingDecay, k_TrebleSwingDecay, blend),
                    SwingRate = Mathf.Lerp(k_BassSwingRate, k_TrebleSwingRate, blend),
                    RestGlow = k_RestGlow,
                };

                var pluckedString = CreateString(k_Tag, root, $"String_{i}_{k_Names[i]}", mesh,
                    MaterialFor(wound ? k_WoundMaterialPath : k_PlainMaterialPath), settings);

                // The tube runs along its own Y; turned to lie along the guitar's X, nut to bridge.
                // Its Z — the way it bends — is still the plane's normal.
                var placement = pluckedString.transform;
                placement.localPosition = new Vector3(-k_NutWidth * 0.5f, StringY(i), 0f);
                placement.localRotation = Quaternion.Euler(0f, 0f, -90f);
                placement.localScale = new Vector3(1f, k_ScaleLength + k_NutWidth * 0.5f, 1f);

                strings.Add(pluckedString);
            }

            return strings;
        }

        /// <summary>One marker per string, parked at the open position; the guitar moves them per chord.</summary>
        static List<Transform> BuildDots(Transform root)
        {
            var dots = new List<Transform>(6);
            var material = MaterialFor(k_DotMaterialPath);

            for (int i = 0; i < 6; i++)
            {
                var dot = Primitive(root, PrimitiveType.Sphere, $"Dot_{i}_{k_Names[i]}", material);
                dot.localPosition = new Vector3(k_OpenMarkerX, StringY(i), -0.015f);
                dot.localScale = Vector3.one * (k_DotRadius * 2f);
                dots.Add(dot);
            }

            return dots;
        }

        static void Wire(Transform guitar, List<PluckedString> strings, List<Transform> dots, Light light,
            Transform plectrum, TMPro.TextMeshPro name, TMPro.TextMeshPro line)
        {
            // Reused rather than re-added, so the chord list and anything else tuned in the Inspector
            // survive a rebuild.
            var component = guitar.GetComponent<HolographicGuitar>();
            if (component == null)
                component = Undo.AddComponent<HolographicGuitar>(guitar.gameObject);

            var so = new SerializedObject(component);
            SetArray(k_Tag, so, "m_Strings", strings);
            SetArray(k_Tag, so, "m_ChordDots", dots);
            SetRef(k_Tag, so, "m_PulseLight", light);
            SetRef(k_Tag, so, "m_Plectrum", plectrum);
            SetRef(k_Tag, so, "m_ChordName", name);
            SetRef(k_Tag, so, "m_Progression", line);
            SetFloat(k_Tag, so, "m_ScaleLength", k_ScaleLength);
            SetFloat(k_Tag, so, "m_OpenMarkerX", k_OpenMarkerX);
            so.ApplyModifiedProperties();
        }

        // ================================================================== palette

        static readonly Dictionary<string, Material> s_Materials = new();

        static Material MaterialFor(string path)
        {
            if (s_Materials.TryGetValue(path, out var cached) && cached != null)
                return cached;

            Material material;
            switch (path)
            {
                case k_WoundMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_WoundBase, k_WoundGlow * k_RestGlow, 1f, 0.7f);
                    break;
                case k_PlainMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_PlainBase, k_PlainGlow * k_RestGlow, 1f, 0.8f);
                    break;
                case k_NeckMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_NeckBase, k_NeckGlow, k_NeckAlpha, 0.5f);
                    break;
                case k_FretMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_FretBase, k_FretGlow, 1f, 0.6f);
                    break;
                case k_HoleMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_HoleBase, Color.black, k_HoleAlpha, 0.2f);
                    break;
                case k_DotMaterialPath:
                    material = EnsureMaterial(k_Tag, path, k_DotBase, k_DotGlow, 1f, 0.6f);
                    break;
                default:
                    material = EnsureMaterial(k_Tag, path, k_BodyBase, k_BodyGlow, k_BodyAlpha, 0.7f);
                    break;
            }

            s_Materials[path] = material;
            return material;
        }
    }
}
