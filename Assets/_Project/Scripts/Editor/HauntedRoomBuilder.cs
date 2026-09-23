using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRShootingGallery.Haunted;
using VRShootingGallery.Visualizer;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the haunted living room: an ordinary Victorian parlour laid onto the CAVE at one to one,
    /// so its walls are the screens — wallpaper, a fireplace, portraits and sconces on the front wall,
    /// a window and a bookcase on the left, a piano on the right — with a rocking chair, a coffee
    /// table, a television and a chandelier in the room, every one of them possessable, and the ghost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything lives under <c>CAVE_User_Room/Haunted_Room</c> and is registered with Undo. A
    /// rebuild clears the room's children; the <see cref="Haunting"/> on the root is reused, so
    /// its Inspector settings survive. Layout is in constants and in the per-piece methods below;
    /// the textures are painted by <see cref="HauntedTextures"/>.
    /// </para>
    /// <para>
    /// Each wall is built in its own frame — x along the wall as seen from inside, y up, −z into the
    /// room — placed from <see cref="CaveScreens"/>, the CAVE's own screen geometry, so the three
    /// walls are authored the same way and land exactly on the screens. Furniture stands against
    /// the walls or low in the middle, leaving the floor for the people in the room.
    /// </para>
    /// </remarks>
    public static class HauntedRoomBuilder
    {
        const string k_Tag = "Haunted";
        const string k_CreateMenu = "VR Shooting Gallery/Haunted Room/Create Haunted Room Scene";
        const string k_BuildMenu = "VR Shooting Gallery/Haunted Room/Rebuild Haunted Room";
        const string k_ScenePath = "Assets/_Project/Scenes/Demos/HauntedRoom MiddleVR.unity";

        const string k_Root = "Haunted_Room";
        const string k_Art = "Assets/_Project/Art/Haunted";
        const string k_BeamShaderPath = "Assets/_Project/Art/Shaders/LaserBeam.shader";

        /// <summary>Metres the wall surfaces sit in front of the screens: over the template's room, too little to read as depth.</summary>
        const float k_Inset = 0.005f;

        /// <summary>Height of the dado rail: wood panelling below, wallpaper above.</summary>
        const float k_Dado = 0.9f;

        static float Height => CaveScreens.Height;

        static readonly Color k_Warm = new(1f, 0.74f, 0.46f);
        static readonly Color k_ShadeGlow = new(1f, 0.7f, 0.4f);
        static readonly Color k_SignColour = new(0.4f, 0.42f, 0.46f);

        // ================================================================== menus

        [MenuItem(k_CreateMenu, false, 700)]
        static void CreateHauntedScene()
        {
            CreateScene(k_Tag, k_ScenePath, "haunted room", Build,
                "Drop a track into Assets/_Project/Audio/Music and enter play mode. G summons the ghost, L toggles low " +
                "intensity, N skips, Space pauses.");
        }

        [MenuItem(k_BuildMenu, false, 710)]
        static void Rebuild() => RebuildInOpenScene(Build);

        /// <summary>
        /// For a build machine, or to generate the scene without opening the editor:
        /// <c>Unity -batchmode -projectPath . -executeMethod VRShootingGallery.EditorTools.HauntedRoomBuilder.CreateFromCommandLine -quit</c>.
        /// Batch mode answers every dialog with Cancel, so the overwrite prompt Create shows for an
        /// existing scene would stop it; an existing scene is rebuilt in place and saved instead.
        /// </summary>
        public static void CreateFromCommandLine()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_ScenePath) == null)
            {
                CreateHauntedScene();
            }
            else
            {
                var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
                if (Build(scene))
                    EditorSceneManager.SaveScene(scene);
            }

            // The build list is project settings, which an interactive editor writes on quit; a batch
            // run may never get there.
            AssetDatabase.SaveAssets();
        }

        // ================================================================== build

        static bool Build(Scene scene)
        {
            var room = RequireRoom(scene, k_Tag, "haunted room");
            if (room == null)
                return false;

            var beam = AssetDatabase.LoadAssetAtPath<Shader>(k_BeamShaderPath);
            if (beam == null)
            {
                Debug.LogError($"[{k_Tag}] Missing '{k_BeamShaderPath}', the additive glow the fire and the ghost are drawn with.");
                return false;
            }

            Undo.SetCurrentGroupName("Build Haunted Room");
            int group = Undo.GetCurrentGroup();

            var textures = HauntedTextures.EnsureAll($"{k_Art}/Textures");
            var p = MakePalette(textures, beam);
            if (p == null)
                return false;

            var root = FindOrCreateInstrument(room, k_Root, Vector3.zero, k_Tag);
            Clear(root);

            var haunting = root.GetComponent<Haunting>();
            if (haunting == null)
                haunting = Undo.AddComponent<Haunting>(root.gameObject);

            var (deck, analyser) = BuildAudio(root);

            var front = WallFrame(root, CaveScreens.Front);
            var left = WallFrame(root, CaveScreens.Left);
            var right = WallFrame(root, CaveScreens.Right);
            Walls(front, CaveScreens.Front, p.WallpaperFront, p);
            Walls(left, CaveScreens.Left, p.WallpaperSide, p);
            Walls(right, CaveScreens.Right, p.WallpaperSide, p);
            Floor(root, p);

            BuildFireplace(front, haunting, p);
            BuildMantel(front, haunting, p);
            BuildPortrait(front, haunting, p);
            BuildSconces(front, haunting, p);
            BuildFrames(front, haunting, p);

            BuildWindow(left, haunting, p);
            BuildBookcase(left, haunting, p);
            BuildFloorLamp(root, haunting, p);
            BuildRockingChair(root, haunting, p);

            BuildPiano(right, haunting, p);
            BuildTelevisionCorner(root, haunting, p);
            BuildCoffeeTable(root, haunting, p);
            BuildChandelier(root, haunting, p);

            var ghost = BuildGhost(root, haunting, p);
            BuildShadows(root, haunting, p, (front, CaveScreens.Front), (left, CaveScreens.Left), (right, CaveScreens.Right));
            var sign = BuildSign(root);

            var so = new SerializedObject(haunting);
            SetRef(k_Tag, so, "m_Analyser", analyser);
            SetRef(k_Tag, so, "m_Deck", deck);
            SetRef(k_Tag, so, "m_Ghost", ghost);
            SetRef(k_Tag, so, "m_Sign", sign);
            so.ApplyModifiedProperties();

            Atmosphere(scene);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;

            int possessed = root.GetComponentsInChildren<Possessable>(true).Length;
            Debug.Log($"[{k_Tag}] Built the haunted room: {possessed} possessable things, " +
                      $"{deck.Playlist?.Length ?? 0} track(s) in the playlist.", root);
            return true;
        }

        // ================================================================== audio

        static (MusicDeck, AudioAnalyser) BuildAudio(Transform root)
        {
            var audio = NewChild(root, "Audio");

            var source = Undo.AddComponent<AudioSource>(audio.gameObject);
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 0;

            var deck = Undo.AddComponent<MusicDeck>(audio.gameObject);
            var so = new SerializedObject(deck);
            SetRef(k_Tag, so, "m_Source", source);
            var clips = MusicDeck.ClipsInMusicFolder();
            if (clips.Length > 0)
                SetArray(k_Tag, so, "m_Playlist", clips);
            so.ApplyModifiedProperties();

            var analyser = Undo.AddComponent<AudioAnalyser>(audio.gameObject);
            so = new SerializedObject(analyser);
            SetRef(k_Tag, so, "m_Source", source);
            so.ApplyModifiedProperties();

            return (deck, analyser);
        }

        // ================================================================== the shell

        /// <summary>A wall's own frame: origin at the foot of the screen's centre, x along it, y up, −z into the room.</summary>
        static Transform WallFrame(Transform root, CaveScreen screen)
        {
            var frame = NewChild(root, $"Wall_{screen.Name}");
            frame.localPosition = screen.Point(0.5f, 0f);
            frame.localRotation = screen.FacingRotation;
            return frame;
        }

        /// <summary>Wallpaper above the dado, panelling below, and the skirting, rail and cornice that make it a room.</summary>
        static void Walls(Transform wall, CaveScreen screen, Material paper, Palette p)
        {
            float w = screen.Width;
            float h = Height;

            Quad(wall, "Wallpaper", new Vector3(0f, k_Dado + (h - k_Dado) * 0.5f, -k_Inset), new Vector2(w, h - k_Dado), paper);
            Quad(wall, "Wainscot", new Vector3(0f, k_Dado * 0.5f, -k_Inset), new Vector2(w, k_Dado), p.Wainscot);

            Box(wall, "Skirting", new Vector3(0f, 0.06f, -0.014f), new Vector3(w, 0.12f, 0.018f), p.Trim);
            Box(wall, "Dado_Rail", new Vector3(0f, k_Dado, -0.016f), new Vector3(w, 0.035f, 0.022f), p.Trim);
            Box(wall, "Cornice", new Vector3(0f, h - 0.035f, -0.022f), new Vector3(w, 0.07f, 0.035f), p.Trim);

            // Panel stiles every half metre or so, so the panelling reads as panelling.
            int stiles = Mathf.Max(2, Mathf.RoundToInt(w / 0.5f));
            for (int i = 1; i < stiles; i++)
            {
                float x = -w * 0.5f + w * i / stiles;
                Box(wall, $"Stile_{i}", new Vector3(x, 0.12f + (k_Dado - 0.14f) * 0.5f, -0.01f),
                    new Vector3(0.03f, k_Dado - 0.14f, 0.012f), p.DarkWood);
            }
        }

        static void Floor(Transform root, Palette p)
        {
            var floor = CaveScreens.Floor;
            Quad(root, "Floorboards", floor.Point(0.5f, 0.5f, 0.003f), new Vector2(floor.Width, floor.Height), p.Floor,
                floor.FacingRotation);
            Quad(root, "Rug", new Vector3(0f, 0.006f, 0.08f), new Vector2(2.2f, 1.5f), p.Rug, floor.FacingRotation);
        }

        // ================================================================== front wall

        /// <summary>A brick fireplace with a stone hearth and a wooden mantel, a fire burning to the music in the grate.</summary>
        static void BuildFireplace(Transform wall, Haunting haunting, Palette p)
        {
            var place = NewChild(wall, "Fireplace");

            Box(place, "Pillar_L", new Vector3(-0.5f, 0.53f, -0.11f), new Vector3(0.3f, 1.06f, 0.22f), p.Brick);
            Box(place, "Pillar_R", new Vector3(0.5f, 0.53f, -0.11f), new Vector3(0.3f, 1.06f, 0.22f), p.Brick);
            Box(place, "Lintel", new Vector3(0f, 0.86f, -0.11f), new Vector3(0.7f, 0.4f, 0.22f), p.Brick);
            Box(place, "Firebox_Back", new Vector3(0f, 0.33f, -0.015f), new Vector3(0.7f, 0.66f, 0.02f), p.Soot);
            Box(place, "Firebox_Floor", new Vector3(0f, 0.02f, -0.11f), new Vector3(0.7f, 0.04f, 0.22f), p.Soot);
            Box(place, "Hearth", new Vector3(0f, 0.02f, -0.34f), new Vector3(1.5f, 0.04f, 0.24f), p.Stone);
            Box(place, "Mantel", new Vector3(0f, 1.085f, -0.14f), new Vector3(1.5f, 0.05f, 0.28f), p.DarkWood);
            Box(place, "Mantel_Moulding", new Vector3(0f, 1.05f, -0.12f), new Vector3(1.42f, 0.03f, 0.24f), p.DarkWood);

            Cyl(place, "Log_A", new Vector3(-0.03f, 0.08f, -0.12f), 0.045f, 0.42f, p.Bark, new Vector3(0f, 0f, 90f));
            Cyl(place, "Log_B", new Vector3(0.05f, 0.14f, -0.1f), 0.04f, 0.36f, p.Bark, new Vector3(0f, 25f, 90f));

            var fire = new GameObject("Fire", typeof(ParticleSystem));
            Undo.RegisterCreatedObjectUndo(fire, "Create fire");
            fire.transform.SetParent(place, false);
            fire.transform.localPosition = new Vector3(0f, 0.08f, -0.12f);
            QuietParticles(fire, p.Fire, 256);

            var light = Lamp(place, "Fire_Light", new Vector3(0f, 0.35f, -0.42f), 3.2f, 1.3f, new Color(1f, 0.55f, 0.22f));

            var component = Undo.AddComponent<HauntedFireplace>(fire);
            var so = new SerializedObject(component);
            SetRef(k_Tag, so, "m_Haunting", haunting);
            SetRef(k_Tag, so, "m_Light", light);
            so.ApplyModifiedProperties();
        }

        /// <summary>A mantel clock whose hands spin with the music, and a candle either side that the ghost's passing bends.</summary>
        static void BuildMantel(Transform wall, Haunting haunting, Palette p)
        {
            const float top = 1.11f;

            var clock = NewChild(wall, "Mantel_Clock");
            clock.localPosition = new Vector3(0f, top, -0.14f);
            Box(clock, "Case", new Vector3(0f, 0.13f, 0f), new Vector3(0.24f, 0.26f, 0.1f), p.DarkWood);
            Box(clock, "Case_Top", new Vector3(0f, 0.275f, 0f), new Vector3(0.18f, 0.03f, 0.08f), p.DarkWood);
            Cyl(clock, "Bezel", new Vector3(0f, 0.15f, -0.05f), 0.088f, 0.006f, p.Brass, new Vector3(90f, 0f, 0f));
            Cyl(clock, "Dial", new Vector3(0f, 0.15f, -0.053f), 0.08f, 0.006f, p.Porcelain, new Vector3(90f, 0f, 0f));

            Hand(clock, "Hour_Hand", -60f, 0.045f, 0.008f, 120f, Listen.Mid, haunting, p);
            Hand(clock, "Minute_Hand", -160f, 0.068f, 0.005f, 540f, Listen.HighMid, haunting, p);

            foreach (float x in new[] { -0.52f, 0.52f })
            {
                var candle = NewChild(wall, x < 0f ? "Candle_L" : "Candle_R");
                candle.localPosition = new Vector3(x, top, -0.14f);
                Cyl(candle, "Holder", new Vector3(0f, 0.015f, 0f), 0.035f, 0.03f, p.Brass);
                Cyl(candle, "Wax", new Vector3(0f, 0.13f, 0f), 0.018f, 0.2f, p.Wax);

                var wick = NewChild(candle, "Wick");
                wick.localPosition = new Vector3(0f, 0.232f, 0f);
                var flame = Ball(wick, "Flame", new Vector3(0f, 0.024f, 0f), new Vector3(0.018f, 0.046f, 0.018f), p.Flame);

                var component = Undo.AddComponent<CandleFlame>(wick.gameObject);
                var so = new SerializedObject(component);
                SetRef(k_Tag, so, "m_Haunting", haunting);
                SetRef(k_Tag, so, "m_Flame", flame.GetComponent<Renderer>());
                so.ApplyModifiedProperties();
            }
        }

        static void Hand(Transform clock, string name, float angle, float length, float width, float degreesPerSecond,
            Listen listen, Haunting haunting, Palette p)
        {
            var hand = NewChild(clock, name);
            hand.localPosition = new Vector3(0f, 0.15f, -0.058f);
            hand.localRotation = Quaternion.Euler(0f, 0f, angle);
            Box(hand, "Blade", new Vector3(0f, length * 0.45f, 0f), new Vector3(width, length, 0.003f), p.Ebony);
            Possess(hand, haunting, 0.15f, listen, Moves.Of(Movement.Spin, Vector3.forward, degreesPerSecond), false);
        }

        /// <summary>The portrait over the mantel, with real eyes set into the painted face.</summary>
        static void BuildPortrait(Transform wall, Haunting haunting, Palette p)
        {
            const float width = 0.42f;
            const float height = 0.52f;
            const float border = 0.05f;

            var portrait = NewChild(wall, "Portrait");
            portrait.localPosition = new Vector3(0f, 1.72f, 0f);
            Quad(portrait, "Canvas", new Vector3(0f, 0f, -0.02f), new Vector2(width, height), p.Portrait);
            Frame(portrait, width, height, border, 0.035f, -0.025f, p.Brass);

            var eyes = new List<Transform>();
            foreach (var uv in new[] { HauntedTextures.PortraitLeftEye, HauntedTextures.PortraitRightEye })
            {
                var eye = NewChild(portrait, eyes.Count == 0 ? "Eye_L" : "Eye_R");
                eye.localPosition = new Vector3((uv.x - 0.5f) * width, (uv.y - 0.5f) * height, -0.02f);
                Ball(eye, "White", Vector3.zero, Vector3.one * 0.026f, p.Eye);
                Ball(eye, "Pupil", new Vector3(0f, 0f, -0.011f), Vector3.one * 0.011f, p.Pupil);
                eyes.Add(eye);
            }

            var component = Undo.AddComponent<PortraitEyes>(portrait.gameObject);
            var so = new SerializedObject(component);
            Common(so, haunting, 0.2f, Listen.Mid, -1f, true);
            SetArray(k_Tag, so, "m_Eyes", eyes);
            so.ApplyModifiedProperties();
        }

        /// <summary>A brass sconce either side of the chimney breast, each a lamp the haunting can flicker.</summary>
        static void BuildSconces(Transform wall, Haunting haunting, Palette p)
        {
            foreach (float x in new[] { -0.95f, 0.95f })
            {
                var sconce = NewChild(wall, x < 0f ? "Sconce_L" : "Sconce_R");
                sconce.localPosition = new Vector3(x, 1.5f, 0f);
                Box(sconce, "Plate", new Vector3(0f, 0f, -0.011f), new Vector3(0.07f, 0.14f, 0.012f), p.Brass);
                Box(sconce, "Arm", new Vector3(0f, -0.02f, -0.065f), new Vector3(0.018f, 0.018f, 0.1f), p.Brass);
                Cyl(sconce, "Cup", new Vector3(0f, 0f, -0.12f), 0.025f, 0.03f, p.Brass);
                var shade = Cyl(sconce, "Shade", new Vector3(0f, 0.07f, -0.12f), 0.065f, 0.11f, p.Shade);
                var light = Lamp(sconce, "Light", new Vector3(0f, 0.02f, -0.2f), 2.2f, 0.8f, k_Warm);

                PossessLight(sconce, haunting, light, new[] { shade.GetComponent<Renderer>() }, 0.45f, Listen.HighMid, true);
            }
        }

        /// <summary>Two small landscapes, hung a little crooked, that bang against the wall on the kick.</summary>
        static void BuildFrames(Transform wall, Haunting haunting, Palette p)
        {
            foreach (float x in new[] { -1.4f, 1.4f })
            {
                var nail = NewChild(wall, x < 0f ? "Painting_L" : "Painting_R");
                nail.localPosition = new Vector3(x, 1.66f, -0.012f);
                nail.localRotation = Quaternion.Euler(0f, 0f, x < 0f ? 2.5f : -3f);

                // Hung from its nail: the picture is below the pivot, so a tilt swings its bottom off the wall.
                var picture = NewChild(nail, "Picture");
                picture.localPosition = new Vector3(0f, -0.2f, 0f);
                Quad(picture, "Canvas", new Vector3(0f, 0f, -0.004f), new Vector2(0.38f, 0.28f), p.Landscape);
                Frame(picture, 0.38f, 0.28f, 0.04f, 0.03f, -0.008f, p.DarkWood);

                Possess(nail, haunting, 0.5f, Listen.Bass,
                    Moves.Of(Movement.Tilt, Vector3.right, 9f, frequency: 2.4f, damping: 0.25f, kick: 1.1f, chance: 0.6f,
                        restitution: 0.3f), true, seed: x < 0f ? 11 : 12);
            }
        }

        // ================================================================== left wall

        /// <summary>A night window with velvet curtains either side, which billow when the ghost goes by.</summary>
        static void BuildWindow(Transform wall, Haunting haunting, Palette p)
        {
            const float cx = 0.62f;
            const float sill = 0.85f;
            const float top = 1.85f;
            const float width = 0.72f;
            float cy = (sill + top) * 0.5f;
            float height = top - sill;

            var window = NewChild(wall, "Window");
            Quad(window, "Night", new Vector3(cx, cy, -0.008f), new Vector2(width, height), p.Night);
            Box(window, "Head", new Vector3(cx, top + 0.03f, -0.022f), new Vector3(width + 0.12f, 0.06f, 0.04f), p.Trim);
            Box(window, "Sill", new Vector3(cx, sill - 0.025f, -0.05f), new Vector3(width + 0.16f, 0.05f, 0.1f), p.Trim);
            Box(window, "Jamb_L", new Vector3(cx - width * 0.5f - 0.03f, cy, -0.022f), new Vector3(0.06f, height, 0.04f), p.Trim);
            Box(window, "Jamb_R", new Vector3(cx + width * 0.5f + 0.03f, cy, -0.022f), new Vector3(0.06f, height, 0.04f), p.Trim);
            Box(window, "Mullion", new Vector3(cx, cy, -0.014f), new Vector3(0.025f, height, 0.015f), p.Trim);
            Box(window, "Transom", new Vector3(cx, cy + 0.12f, -0.014f), new Vector3(width, 0.025f, 0.015f), p.Trim);

            Cyl(window, "Rod", new Vector3(cx, 1.98f, -0.075f), 0.012f, 1.12f, p.Brass, new Vector3(0f, 0f, 90f));
            Ball(window, "Finial_L", new Vector3(cx - 0.57f, 1.98f, -0.075f), Vector3.one * 0.04f, p.Brass);
            Ball(window, "Finial_R", new Vector3(cx + 0.57f, 1.98f, -0.075f), Vector3.one * 0.04f, p.Brass);

            var mesh = SaveMesh(HauntedCurtain.BuildMesh(0.36f, 1.86f, 12, 18, 0.022f, 3), $"{k_Art}/Meshes/Curtain.asset");
            foreach (float x in new[] { 0.1f, 0.8f })
            {
                var go = new GameObject(x < cx ? "Curtain_L" : "Curtain_R", typeof(MeshFilter), typeof(MeshRenderer));
                Undo.RegisterCreatedObjectUndo(go, "Create curtain");
                go.transform.SetParent(window, false);
                go.transform.localPosition = new Vector3(x, 1.97f, -0.07f);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = p.Curtain;

                var curtain = Undo.AddComponent<HauntedCurtain>(go);
                var so = new SerializedObject(curtain);
                Common(so, haunting, 0.35f, Listen.Mid, -1f, x < cx);
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// A bookcase whose books are a spectrum: each one listens to its own band, bass on the
        /// bottom shelf to treble at the top, and slides out with it, so a possessed bookcase is an
        /// equaliser nobody built.
        /// </summary>
        static void BuildBookcase(Transform wall, Haunting haunting, Palette p)
        {
            const float cx = -0.667f;
            const float width = 0.8f;
            const float height = 1.85f;
            const float depth = 0.3f;
            float[] shelves = { 0.1f, 0.46f, 0.82f, 1.18f, 1.52f };

            var bookcase = NewChild(wall, "Bookcase");
            bookcase.localPosition = new Vector3(cx, 0f, 0f);
            var body = NewChild(bookcase, "Body");

            float z = -depth * 0.5f - 0.005f;
            Box(body, "Side_L", new Vector3(-width * 0.5f + 0.01f, height * 0.5f, z), new Vector3(0.02f, height, depth), p.DarkWood);
            Box(body, "Side_R", new Vector3(width * 0.5f - 0.01f, height * 0.5f, z), new Vector3(0.02f, height, depth), p.DarkWood);
            Box(body, "Top", new Vector3(0f, height - 0.01f, z), new Vector3(width, 0.02f, depth), p.DarkWood);
            Box(body, "Plinth", new Vector3(0f, 0.05f, z), new Vector3(width, 0.1f, depth), p.DarkWood);
            Box(body, "Back", new Vector3(0f, height * 0.5f, -0.012f), new Vector3(width - 0.02f, height, 0.01f), p.Wood);
            for (int s = 1; s < shelves.Length; s++)
                Box(body, $"Shelf_{s}", new Vector3(0f, shelves[s] - 0.01f, z), new Vector3(width - 0.04f, 0.02f, depth - 0.02f), p.DarkWood);

            var random = new System.Random(7);
            float start = -width * 0.5f + 0.03f;
            float end = width * 0.5f - 0.03f;
            int count = 0;

            for (int row = 0; row < shelves.Length; row++)
            {
                float room = (row + 1 < shelves.Length ? shelves[row + 1] - 0.02f : height - 0.02f) - shelves[row];
                float x = start;
                while (x < end - 0.02f)
                {
                    float w = 0.022f + 0.024f * (float)random.NextDouble();
                    if (x + w > end)
                        break;

                    // Now and then a gap, the way a real shelf is never full.
                    if (random.NextDouble() < 0.07)
                    {
                        x += 0.03f + 0.03f * (float)random.NextDouble();
                        continue;
                    }

                    float h = Mathf.Min(room - 0.02f, 0.2f + 0.1f * (float)random.NextDouble());
                    float d = 0.18f + 0.05f * (float)random.NextDouble();

                    var book = NewChild(body, $"Book_{row}_{count}");
                    book.localPosition = new Vector3(x + w * 0.5f, shelves[row], -0.018f);
                    Box(book, "Volume", new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, d),
                        p.Books[random.Next(p.Books.Length)]);

                    float band = (row + (x - start) / (end - start)) / shelves.Length;
                    Possess(book, haunting, 0.4f + 0.08f * (float)random.NextDouble(), Listen.Mid,
                        Moves.Of(Movement.Slide, Vector3.back, 0.06f, frequency: 3f, damping: 0.5f, kick: 0.4f, chance: 0.35f),
                        false, band, 100 + count);

                    x += w + 0.002f;
                    count++;
                }
            }

            Possess(body, haunting, 0.6f, Listen.Bass, Moves.Of(Movement.Rattle, Vector3.up, 0.0015f, frequency: 14f), true, seed: 99);
        }

        static void BuildFloorLamp(Transform root, Haunting haunting, Palette p)
        {
            var lamp = NewChild(root, "Floor_Lamp");
            lamp.localPosition = new Vector3(-1.47f, 0f, -0.15f);
            Cyl(lamp, "Base", new Vector3(0f, 0.0125f, 0f), 0.14f, 0.025f, p.Brass);
            Cyl(lamp, "Pole", new Vector3(0f, 0.75f, 0f), 0.012f, 1.46f, p.Brass);
            var shade = Cyl(lamp, "Shade", new Vector3(0f, 1.52f, 0f), 0.17f, 0.26f, p.Shade);
            var light = Lamp(lamp, "Light", new Vector3(0f, 1.36f, 0.05f), 2.6f, 1f, k_Warm);

            // The first thing in the room to go wrong.
            PossessLight(lamp, haunting, light, new[] { shade.GetComponent<Renderer>() }, 0.02f, Listen.Mid, true);
        }

        /// <summary>
        /// A rocking chair in the front corner by the window, turned to face whoever is standing in
        /// the room. It rocks on its runners — rolling, not pivoting — at its own natural rate, to
        /// the low mids.
        /// </summary>
        static void BuildRockingChair(Transform root, Haunting haunting, Palette p)
        {
            const float radius = 0.85f;

            var chair = NewChild(root, "Rocking_Chair");
            chair.localPosition = new Vector3(-1f, 0f, 0.45f);
            chair.localRotation = Facing(chair.localPosition, new Vector3(0.2f, 0f, -0.4f));

            // The body's origin is the centre of the runners' arc, so rocking is a rotation about it.
            var rockers = NewChild(chair, "Rockers");
            rockers.localPosition = new Vector3(0f, radius, 0f);
            float floor = -radius;

            foreach (float side in new[] { -0.24f, 0.24f })
            {
                const int segments = 6;
                for (int k = 0; k < segments; k++)
                {
                    float a0 = Mathf.Lerp(-28f, 28f, k / (float)segments) * Mathf.Deg2Rad;
                    float a1 = Mathf.Lerp(-28f, 28f, (k + 1) / (float)segments) * Mathf.Deg2Rad;
                    var from = new Vector3(side, -radius * Mathf.Cos(a0) + 0.0125f, radius * Mathf.Sin(a0));
                    var to = new Vector3(side, -radius * Mathf.Cos(a1) + 0.0125f, radius * Mathf.Sin(a1));
                    Slab(rockers, $"Runner_{(side < 0f ? "L" : "R")}_{k}", from, to, 0.025f, 0.03f, 0f, p.DarkWood);
                }
            }

            Box(rockers, "Seat", new Vector3(0f, floor + 0.42f, 0.02f), new Vector3(0.5f, 0.04f, 0.46f), p.DarkWood);
            Box(rockers, "Cushion", new Vector3(0f, floor + 0.465f, 0.03f), new Vector3(0.46f, 0.05f, 0.42f), p.Cushion);

            foreach (float x in new[] { -0.22f, 0.22f })
                foreach (float zz in new[] { -0.18f, 0.18f })
                    Cyl(rockers, "Leg", new Vector3(x, floor + 0.215f, zz), 0.018f, 0.39f, p.DarkWood);

            foreach (float x in new[] { -0.22f, 0.22f })
            {
                Slab(rockers, "Back_Post", new Vector3(x, floor + 0.42f, -0.2f), new Vector3(x * 1.12f, floor + 1.1f, -0.34f),
                    0.035f, 0.035f, 0f, p.DarkWood);
                Slab(rockers, "Arm", new Vector3(x * 1.18f, floor + 0.64f, -0.2f), new Vector3(x * 1.18f, floor + 0.64f, 0.2f),
                    0.03f, 0.05f, 0f, p.DarkWood);
                Cyl(rockers, "Arm_Post", new Vector3(x * 1.18f, floor + 0.53f, 0.17f), 0.015f, 0.22f, p.DarkWood);
            }

            Slab(rockers, "Top_Rail", new Vector3(-0.26f, floor + 1.08f, -0.335f), new Vector3(0.26f, floor + 1.08f, -0.335f),
                0.06f, 0.03f, 0f, p.DarkWood);
            for (int s = 0; s < 5; s++)
            {
                float x = Mathf.Lerp(-0.14f, 0.14f, s / 4f);
                Slab(rockers, "Spindle", new Vector3(x, floor + 0.44f, -0.205f), new Vector3(x * 1.1f, floor + 1.06f, -0.33f),
                    0.015f, 0.015f, 0f, p.DarkWood);
            }

            Possess(rockers, haunting, 0.1f, Listen.LowMid,
                Moves.Of(Movement.Roll, Vector3.right, 10f, frequency: 0.7f, damping: 0.12f, kick: 0.8f, chance: 0.5f,
                    rollRadius: radius), true, seed: 3);
        }

        // ================================================================== right wall and corner

        /// <summary>
        /// An upright piano with all 88 keys, each its own pivot, that plays itself — every key goes
        /// down with its own note — and a bench in front that scoots back when it starts.
        /// </summary>
        static void BuildPiano(Transform wall, Haunting haunting, Palette p)
        {
            const float cx = 0.417f;
            const float width = 1.45f;
            const float height = 1.25f;
            const float keyWidth = 0.0235f;

            var piano = NewChild(wall, "Piano");
            piano.localPosition = new Vector3(cx, 0f, 0f);
            Box(piano, "Case", new Vector3(0f, height * 0.5f, -0.175f), new Vector3(width, height, 0.34f), p.DarkWood);
            Box(piano, "Lid", new Vector3(0f, height + 0.012f, -0.175f), new Vector3(width + 0.03f, 0.025f, 0.36f), p.DarkWood);
            Box(piano, "Keybed", new Vector3(0f, 0.66f, -0.43f), new Vector3(width - 0.05f, 0.06f, 0.18f), p.DarkWood);
            Box(piano, "Cheek_L", new Vector3(-0.665f, 0.73f, -0.43f), new Vector3(0.07f, 0.1f, 0.18f), p.DarkWood);
            Box(piano, "Cheek_R", new Vector3(0.665f, 0.73f, -0.43f), new Vector3(0.07f, 0.1f, 0.18f), p.DarkWood);
            Box(piano, "Nameboard", new Vector3(0f, 0.78f, -0.355f), new Vector3(1.26f, 0.14f, 0.025f), p.DarkWood);
            Box(piano, "Music_Desk", new Vector3(0f, 0.97f, -0.36f), new Vector3(0.9f, 0.2f, 0.02f), p.DarkWood);
            Box(piano, "Kneeboard", new Vector3(0f, 0.33f, -0.345f), new Vector3(width - 0.1f, 0.6f, 0.02f), p.DarkWood);
            Cyl(piano, "Leg_L", new Vector3(-0.64f, 0.315f, -0.49f), 0.03f, 0.63f, p.DarkWood);
            Cyl(piano, "Leg_R", new Vector3(0.64f, 0.315f, -0.49f), 0.03f, 0.63f, p.DarkWood);

            // Lowest key on the player's left: along this wall's +x, which is toward the back of the room.
            var keys = new List<Transform>(88);
            float start = -52 * keyWidth * 0.5f;
            int whites = 0;
            for (int note = 21; note <= 108; note++)
            {
                int pitch = note % 12;
                bool black = pitch == 1 || pitch == 3 || pitch == 6 || pitch == 8 || pitch == 10;

                var key = NewChild(piano, $"Key_{note}");
                if (black)
                {
                    key.localPosition = new Vector3(start + whites * keyWidth, 0.7f, -0.365f);
                    Box(key, "Top", new Vector3(0f, 0.02f, -0.045f), new Vector3(0.012f, 0.022f, 0.09f), p.Ebony);
                }
                else
                {
                    key.localPosition = new Vector3(start + (whites + 0.5f) * keyWidth, 0.69f, -0.365f);
                    Box(key, "Top", new Vector3(0f, 0.011f, -0.075f), new Vector3(keyWidth - 0.002f, 0.022f, 0.15f), p.Ivory);
                    whites++;
                }

                keys.Add(key);
            }

            var component = Undo.AddComponent<PossessedPiano>(piano.gameObject);
            var so = new SerializedObject(component);
            Common(so, haunting, 0.2f, Listen.Mid, -1f, true);
            SetArray(k_Tag, so, "m_Keys", keys);
            SetInt(k_Tag, so, "m_LowestNote", 21);
            so.ApplyModifiedProperties();

            var bench = NewChild(wall, "Piano_Bench");
            bench.localPosition = new Vector3(cx, 0f, -0.75f);
            Box(bench, "Seat", new Vector3(0f, 0.47f, 0f), new Vector3(0.85f, 0.05f, 0.36f), p.DarkWood);
            Box(bench, "Cushion", new Vector3(0f, 0.505f, 0f), new Vector3(0.8f, 0.03f, 0.32f), p.Cushion);
            foreach (float x in new[] { -0.38f, 0.38f })
                foreach (float z in new[] { -0.14f, 0.14f })
                    Cyl(bench, "Leg", new Vector3(x, 0.225f, z), 0.022f, 0.45f, p.DarkWood);

            Possess(bench, haunting, 0.3f, Listen.Bass,
                Moves.Of(Movement.Slide, Vector3.back, 0.04f, frequency: 1.5f, damping: 0.7f, kick: 0.5f, chance: 0.4f), false, seed: 5);
        }

        /// <summary>
        /// The front-right corner: a sideboard whose doors fly open, an old television that turns
        /// itself on with the ghost in its static, and a table lamp. Angled to face the room.
        /// </summary>
        static void BuildTelevisionCorner(Transform root, Haunting haunting, Palette p)
        {
            var corner = NewChild(root, "Television_Corner");
            corner.localPosition = new Vector3(1.32f, 0f, 0.66f);
            corner.localRotation = Facing(corner.localPosition, new Vector3(0f, 0f, -0.3f));

            // The sideboard. In this corner's frame +z is its front, toward the room.
            var board = NewChild(corner, "Sideboard");
            Box(board, "Top", new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.03f, 0.38f), p.DarkWood);
            Box(board, "Carcass", new Vector3(0f, 0.335f, -0.01f), new Vector3(0.68f, 0.4f, 0.34f), p.Wood);
            foreach (float x in new[] { -0.31f, 0.31f })
                foreach (float z in new[] { -0.15f, 0.15f })
                    Cyl(board, "Leg", new Vector3(x, 0.0675f, z), 0.02f, 0.135f, p.DarkWood);

            foreach (int side in new[] { -1, 1 })
            {
                // Hinged at its outer edge, so the free edge swings out toward the room.
                var door = NewChild(board, side < 0 ? "Door_L" : "Door_R");
                door.localPosition = new Vector3(0.335f * side, 0.335f, 0.162f);
                Box(door, "Panel", new Vector3(-0.1675f * side, 0f, 0.008f), new Vector3(0.33f, 0.38f, 0.018f), p.Wood);
                Ball(door, "Knob", new Vector3(-0.3f * side, 0f, 0.024f), Vector3.one * 0.02f, p.Brass);

                Possess(door, haunting, 0.55f, Listen.Mid,
                    Moves.Of(Movement.Flap, side < 0 ? Vector3.down : Vector3.up, 38f, frequency: 1.6f, damping: 0.35f, kick: 1f,
                        chance: 0.45f, restitution: 0.3f), false, seed: side < 0 ? 21 : 22);
            }

            // The television: a deep wooden CRT with rabbit ears.
            var tv = NewChild(corner, "Television");
            tv.localPosition = new Vector3(-0.08f, 0.565f, -0.02f);
            Box(tv, "Cabinet", new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.4f), p.TVBody);
            var screen = Quad(tv, "Screen", new Vector3(-0.045f, 0.215f, 0.2015f), new Vector2(0.36f, 0.27f), p.Screen,
                Quaternion.Euler(0f, 180f, 0f));
            Cyl(tv, "Knob_A", new Vector3(0.19f, 0.27f, 0.205f), 0.018f, 0.015f, p.Brass, new Vector3(90f, 0f, 0f));
            Cyl(tv, "Knob_B", new Vector3(0.19f, 0.17f, 0.205f), 0.018f, 0.015f, p.Brass, new Vector3(90f, 0f, 0f));
            Ball(tv, "Antenna_Base", new Vector3(0f, 0.41f, -0.05f), new Vector3(0.06f, 0.03f, 0.06f), p.Ebony);
            Slab(tv, "Antenna_L", new Vector3(-0.01f, 0.41f, -0.05f), new Vector3(-0.17f, 0.72f, -0.08f), 0.006f, 0.006f, 0f, p.Brass);
            Slab(tv, "Antenna_R", new Vector3(0.01f, 0.41f, -0.05f), new Vector3(0.14f, 0.73f, -0.06f), 0.006f, 0.006f, 0f, p.Brass);

            var television = Undo.AddComponent<HauntedTV>(tv.gameObject);
            var so = new SerializedObject(television);
            Common(so, haunting, 0.25f, Listen.Loudness, -1f, true);
            SetRef(k_Tag, so, "m_Screen", screen.GetComponent<Renderer>());
            so.ApplyModifiedProperties();

            var lamp = NewChild(corner, "Table_Lamp");
            lamp.localPosition = new Vector3(0.27f, 0.565f, -0.02f);
            Cyl(lamp, "Base", new Vector3(0f, 0.015f, 0f), 0.055f, 0.03f, p.Brass);
            Cyl(lamp, "Stem", new Vector3(0f, 0.15f, 0f), 0.01f, 0.25f, p.Brass);
            var shade = Cyl(lamp, "Shade", new Vector3(0f, 0.32f, 0f), 0.095f, 0.14f, p.Shade);
            var light = Lamp(lamp, "Light", new Vector3(0f, 0.22f, 0.12f), 1.8f, 0.6f, k_Warm);

            PossessLight(lamp, haunting, light, new[] { shade.GetComponent<Renderer>() }, 0.35f, Listen.High, false);
        }

        // ================================================================== the middle of the room

        /// <summary>A low table set for tea: the cups rattle on the treble, and the whole table hops on the kick.</summary>
        static void BuildCoffeeTable(Transform root, Haunting haunting, Palette p)
        {
            var table = NewChild(root, "Coffee_Table");
            table.localPosition = new Vector3(0f, 0f, 0.26f);

            var body = NewChild(table, "Body");
            Box(body, "Top", new Vector3(0f, 0.41f, 0f), new Vector3(0.95f, 0.04f, 0.48f), p.DarkWood);
            Box(body, "Apron", new Vector3(0f, 0.365f, 0f), new Vector3(0.88f, 0.05f, 0.42f), p.DarkWood);
            foreach (float x in new[] { -0.41f, 0.41f })
                foreach (float z in new[] { -0.18f, 0.18f })
                    Cyl(body, "Leg", new Vector3(x, 0.17f, z), 0.022f, 0.34f, p.DarkWood);

            var pot = NewChild(body, "Teapot");
            pot.localPosition = new Vector3(0.22f, 0.43f, 0.04f);
            Ball(pot, "Belly", new Vector3(0f, 0.06f, 0f), new Vector3(0.15f, 0.11f, 0.15f), p.Porcelain);
            Ball(pot, "Lid", new Vector3(0f, 0.115f, 0f), new Vector3(0.08f, 0.03f, 0.08f), p.Porcelain);
            Ball(pot, "Knob", new Vector3(0f, 0.135f, 0f), Vector3.one * 0.02f, p.Porcelain);
            Slab(pot, "Spout", new Vector3(0.06f, 0.05f, 0f), new Vector3(0.13f, 0.1f, 0f), 0.016f, 0.016f, 0f, p.Porcelain);
            Slab(pot, "Handle", new Vector3(-0.07f, 0.1f, 0f), new Vector3(-0.1f, 0.04f, 0f), 0.014f, 0.01f, 0f, p.Porcelain);
            Possess(pot, haunting, 0.3f, Listen.High, Moves.Of(Movement.Rattle, Vector3.up, 0.0018f, frequency: 20f, chance: 0.8f), false, seed: 31);

            var cups = new[] { new Vector3(-0.2f, 0.43f, 0.08f), new Vector3(-0.04f, 0.43f, -0.11f) };
            for (int i = 0; i < cups.Length; i++)
            {
                var cup = NewChild(body, $"Teacup_{i}");
                cup.localPosition = cups[i];
                Cyl(cup, "Saucer", new Vector3(0f, 0.004f, 0f), 0.055f, 0.008f, p.Porcelain);
                Cyl(cup, "Cup", new Vector3(0f, 0.033f, 0f), 0.034f, 0.05f, p.Porcelain);
                Box(cup, "Handle", new Vector3(0.04f, 0.035f, 0f), new Vector3(0.012f, 0.028f, 0.006f), p.Porcelain);
                Possess(cup, haunting, 0.3f, Listen.High,
                    Moves.Of(Movement.Rattle, Vector3.up, 0.0025f, frequency: 24f, chance: 0.8f), false, seed: 40 + i);
            }

            Possess(body, haunting, 0.7f, Listen.Bass,
                Moves.Of(Movement.Hop, Vector3.up, 0.035f, kick: 1f, chance: 0.85f, restitution: 0.2f), true, seed: 7);
        }

        /// <summary>A brass chandelier hanging over the table: a lamp that also swings, like a pendulum, on the bass.</summary>
        static void BuildChandelier(Transform root, Haunting haunting, Palette p)
        {
            var mount = NewChild(root, "Chandelier");
            mount.localPosition = new Vector3(0f, Height, 0.2f);
            Cyl(mount, "Canopy", new Vector3(0f, -0.01f, 0f), 0.06f, 0.02f, p.Brass);

            // Hung from the ceiling: the swing's origin is the fixing point. Short, so the fixture
            // stays above a standing visitor's line of sight to the portrait over the mantel.
            var swing = NewChild(mount, "Swing");
            Cyl(swing, "Chain", new Vector3(0f, -0.08f, 0f), 0.005f, 0.14f, p.Brass);
            Ball(swing, "Bowl", new Vector3(0f, -0.18f, 0f), new Vector3(0.12f, 0.06f, 0.12f), p.Brass);

            var bulbs = new List<Renderer>();
            const int arms = 5;
            for (int a = 0; a < arms; a++)
            {
                float angle = a * Mathf.PI * 2f / arms;
                var tip = new Vector3(Mathf.Cos(angle) * 0.18f, -0.15f, Mathf.Sin(angle) * 0.18f);
                Slab(swing, $"Arm_{a}", new Vector3(0f, -0.18f, 0f), tip, 0.012f, 0.012f, 0f, p.Brass);
                Cyl(swing, $"Cup_{a}", tip + new Vector3(0f, 0.012f, 0f), 0.018f, 0.025f, p.Brass);
                var bulb = Ball(swing, $"Bulb_{a}", tip + new Vector3(0f, 0.045f, 0f), new Vector3(0.03f, 0.05f, 0.03f), p.Bulb);
                bulbs.Add(bulb.GetComponent<Renderer>());
            }

            var light = Lamp(swing, "Light", new Vector3(0f, -0.26f, 0f), 3f, 0.9f, k_Warm);

            Possess(swing, haunting, 0.6f, Listen.Bass,
                Moves.Of(Movement.Swing, new Vector3(1f, 0f, 0.5f), 6f, frequency: 1.1f, damping: 0.08f, kick: 0.6f, chance: 0.5f),
                false, seed: 9);
            PossessLight(swing, haunting, light, bulbs.ToArray(), 0.5f, Listen.Loudness, false);
        }

        // ================================================================== the ghost

        static SpectreGhost BuildGhost(Transform root, Haunting haunting, Palette p)
        {
            var go = new GameObject("Spectre", typeof(ParticleSystem));
            Undo.RegisterCreatedObjectUndo(go, "Create ghost");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 1.38f, 0.35f);
            QuietParticles(go, p.Ghost, 4096);

            var ghost = Undo.AddComponent<SpectreGhost>(go);
            var so = new SerializedObject(ghost);
            SetRef(k_Tag, so, "m_Haunting", haunting);
            so.ApplyModifiedProperties();
            return ghost;
        }

        /// <summary>One shadow quad per wall, hidden until the haunting moves it.</summary>
        static void BuildShadows(Transform root, Haunting haunting, Palette p, params (Transform frame, CaveScreen screen)[] walls)
        {
            var holder = NewChild(root, "Ghost_Shadows");
            var renderers = new List<Renderer>();
            var sizes = new List<Vector2>();

            foreach (var (frame, screen) in walls)
            {
                var shadow = Quad(frame, "Ghost_Shadow", new Vector3(0f, 1.2f, -0.011f), new Vector2(0.7f, 0.7f), p.Shadow);
                var renderer = shadow.GetComponent<Renderer>();
                renderer.enabled = false;
                renderers.Add(renderer);
                sizes.Add(new Vector2(screen.Width, Height));
            }

            var component = Undo.AddComponent<GhostShadows>(holder.gameObject);
            var so = new SerializedObject(component);
            SetRef(k_Tag, so, "m_Haunting", haunting);
            SetArray(k_Tag, so, "m_Shadows", renderers);
            var sizeArray = so.FindProperty("m_WallSizes");
            sizeArray.arraySize = sizes.Count;
            for (int i = 0; i < sizes.Count; i++)
                sizeArray.GetArrayElementAtIndex(i).vector2Value = sizes[i];
            so.ApplyModifiedProperties();
        }

        /// <summary>A dim status line on the floor at the open back, readable by whoever runs the demo from the doorway.</summary>
        static TMPro.TextMeshPro BuildSign(Transform root)
        {
            var sign = Text(k_Tag, root, "Status_Sign", new Vector3(0f, 0.009f, -0.97f), 0.5f, "", k_SignColour,
                new Vector2(3f, 0.2f));
            sign.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return sign;
        }

        /// <summary>
        /// Night outside: the template's sun turned down to a faint blue moonlight, a dim warm ambient,
        /// and a black clear colour, so the lamps and the fire light the room.
        /// </summary>
        static void Atmosphere(Scene scene)
        {
            var sun = FindRoot(scene, "Directional Light");
            if (sun != null && sun.TryGetComponent<Light>(out var moon))
            {
                Undo.RecordObject(moon, "Moonlight");
                moon.intensity = 0.12f;
                moon.color = new Color(0.55f, 0.62f, 0.85f);
                moon.shadows = LightShadows.None;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.062f, 0.055f);
            RenderSettings.reflectionIntensity = 0.25f;
            RenderSettings.fog = false;

            var cameraRoot = FindRoot(scene, "MainCamera");
            if (cameraRoot != null && cameraRoot.TryGetComponent<Camera>(out var camera))
            {
                Undo.RecordObject(camera, "Darken camera");
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
        }

        // ================================================================== possession

        struct Moves
        {
            public Movement Motion;
            public Vector3 Axis;
            public float Amount;
            public float Frequency;
            public float Damping;
            public float Kick;
            public float Chance;
            public float Restitution;
            public float RollRadius;

            public static Moves Of(Movement motion, Vector3 axis, float amount, float frequency = 1f, float damping = 0.3f,
                float kick = 1f, float chance = 0.7f, float restitution = 0.25f, float rollRadius = 0.85f)
            {
                return new Moves
                {
                    Motion = motion,
                    Axis = axis,
                    Amount = amount,
                    Frequency = frequency,
                    Damping = damping,
                    Kick = kick,
                    Chance = chance,
                    Restitution = restitution,
                    RollRadius = rollRadius,
                };
            }
        }

        static PossessedObject Possess(Transform target, Haunting haunting, float threshold, Listen listen, Moves moves,
            bool visit, float band = -1f, int seed = 0)
        {
            var component = Undo.AddComponent<PossessedObject>(target.gameObject);
            var so = new SerializedObject(component);
            Common(so, haunting, threshold, listen, band, visit);
            SetEnum(k_Tag, so, "m_Motion", (int)moves.Motion);
            SetVector3(k_Tag, so, "m_Axis", moves.Axis);
            SetFloat(k_Tag, so, "m_Amount", moves.Amount);
            SetFloat(k_Tag, so, "m_Frequency", moves.Frequency);
            SetFloat(k_Tag, so, "m_Damping", moves.Damping);
            SetFloat(k_Tag, so, "m_BeatKick", moves.Kick);
            SetFloat(k_Tag, so, "m_BeatChance", moves.Chance);
            SetFloat(k_Tag, so, "m_Restitution", moves.Restitution);
            SetFloat(k_Tag, so, "m_RollRadius", moves.RollRadius);
            SetInt(k_Tag, so, "m_Seed", seed);
            so.ApplyModifiedProperties();
            return component;
        }

        static void PossessLight(Transform target, Haunting haunting, Light light, Renderer[] glows, float threshold, Listen listen,
            bool visit)
        {
            var component = Undo.AddComponent<PossessedLight>(target.gameObject);
            var so = new SerializedObject(component);
            Common(so, haunting, threshold, listen, -1f, visit);
            SetRef(k_Tag, so, "m_Light", light);
            SetArray(k_Tag, so, "m_Glows", glows);
            SetColor(k_Tag, so, "m_GlowColour", k_ShadeGlow);
            so.ApplyModifiedProperties();
        }

        /// <summary>What every <see cref="Possessable"/> is given: its director, when it wakes, and what it listens to.</summary>
        static void Common(SerializedObject so, Haunting haunting, float threshold, Listen listen, float band, bool visit)
        {
            SetRef(k_Tag, so, "m_Haunting", haunting);
            SetFloat(k_Tag, so, "m_Threshold", threshold);
            SetEnum(k_Tag, so, "m_Listen", (int)listen);
            SetFloat(k_Tag, so, "m_Band", band);
            SetBool(k_Tag, so, "m_Visit", visit);
        }

        /// <summary>The rotation that turns something at <paramref name="from"/> to face <paramref name="toward"/> across the floor.</summary>
        static Quaternion Facing(Vector3 from, Vector3 toward)
        {
            var direction = toward - from;
            direction.y = 0f;
            return Quaternion.Euler(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f);
        }

        // ================================================================== pieces

        static Transform Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var box = Primitive(parent, PrimitiveType.Cube, name, material);
            box.localPosition = centre;
            box.localScale = size;
            return box;
        }

        /// <summary>A cylinder <paramref name="height"/> long on its own Y — turned by <paramref name="euler"/> — and <paramref name="radius"/> round.</summary>
        static Transform Cyl(Transform parent, string name, Vector3 centre, float radius, float height, Material material,
            Vector3 euler = default)
        {
            var cylinder = Primitive(parent, PrimitiveType.Cylinder, name, material);
            cylinder.localPosition = centre;
            cylinder.localRotation = Quaternion.Euler(euler);
            cylinder.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            return cylinder;
        }

        static Transform Ball(Transform parent, string name, Vector3 centre, Vector3 diameter, Material material)
        {
            var ball = Primitive(parent, PrimitiveType.Sphere, name, material);
            ball.localPosition = centre;
            ball.localScale = diameter;
            return ball;
        }

        /// <summary>A quad facing −Z of its parent — into the room, on a wall — unless turned.</summary>
        static Transform Quad(Transform parent, string name, Vector3 centre, Vector2 size, Material material)
        {
            return Quad(parent, name, centre, size, material, Quaternion.identity);
        }

        static Transform Quad(Transform parent, string name, Vector3 centre, Vector2 size, Material material, Quaternion rotation)
        {
            var quad = Primitive(parent, PrimitiveType.Quad, name, material);
            quad.localPosition = centre;
            quad.localRotation = rotation;
            quad.localScale = new Vector3(size.x, size.y, 1f);
            return quad;
        }

        /// <summary>A picture frame: four mouldings round a <paramref name="width"/> by <paramref name="height"/> opening.</summary>
        static void Frame(Transform parent, float width, float height, float border, float depth, float z, Material material)
        {
            float outerW = width + border * 2f;
            Box(parent, "Frame_Top", new Vector3(0f, (height + border) * 0.5f, z), new Vector3(outerW, border, depth), material);
            Box(parent, "Frame_Bottom", new Vector3(0f, -(height + border) * 0.5f, z), new Vector3(outerW, border, depth), material);
            Box(parent, "Frame_Left", new Vector3(-(width + border) * 0.5f, 0f, z), new Vector3(border, height, depth), material);
            Box(parent, "Frame_Right", new Vector3((width + border) * 0.5f, 0f, z), new Vector3(border, height, depth), material);
        }

        static Light Lamp(Transform parent, string name, Vector3 position, float range, float intensity, Color colour)
        {
            var holder = NewChild(parent, name);
            holder.localPosition = position;
            var light = Undo.AddComponent<Light>(holder.gameObject);
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = colour;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>A particle system the runtime writes by hand, built quiet so the scene view does not fill with default particles.</summary>
        static void QuietParticles(GameObject go, Material material, int max)
        {
            var system = go.GetComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = max;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            EnsureFolder(path);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ================================================================== palette

        class Palette
        {
            public Material WallpaperFront, WallpaperSide, Wainscot, Trim, Floor, Rug;
            public Material Wood, DarkWood, Bark, Brick, Soot, Stone, Brass, Shade, Bulb, Porcelain, Ivory, Ebony;
            public Material Curtain, Cushion, Portrait, Landscape, Eye, Pupil, TVBody, Wax, Flame;
            public Material Night, Screen, Shadow, Fire, Ghost;
            public Material[] Books;
        }

        static string MaterialPath(string name) => $"{k_Art}/Materials/M_Haunt_{name}.mat";

        static Palette MakePalette(HauntedTextures.Set t, Shader beam)
        {
            var p = new Palette
            {
                WallpaperFront = Textured("Wallpaper_Front", t.Wallpaper,
                    new Vector2(CaveScreens.FrontWidth / 0.6f, (Height - k_Dado) / 0.6f), 0.15f),
                WallpaperSide = Textured("Wallpaper_Side", t.Wallpaper,
                    new Vector2(CaveScreens.SideWidth / 0.6f, (Height - k_Dado) / 0.6f), 0.15f),
                Floor = Textured("Floorboards", t.Floorboards, new Vector2(CaveScreens.FrontWidth / 0.8f, CaveScreens.FloorDepth / 0.8f), 0.35f),
                Rug = Textured("Rug", t.Rug, Vector2.one, 0.05f),
                Portrait = Textured("Portrait", t.Portrait, Vector2.one, 0.3f),
                Landscape = Textured("Landscape", t.Landscape, Vector2.one, 0.3f),

                Wainscot = Lit("Wainscot", new Color(0.16f, 0.09f, 0.05f), 0.4f),
                Trim = Lit("Trim", new Color(0.72f, 0.68f, 0.6f), 0.35f),
                Wood = Lit("Wood", new Color(0.3f, 0.18f, 0.1f), 0.4f),
                DarkWood = Lit("Dark_Wood", new Color(0.13f, 0.07f, 0.04f), 0.5f),
                Bark = Lit("Bark", new Color(0.17f, 0.1f, 0.06f), 0.1f),
                Brick = Lit("Brick", new Color(0.34f, 0.13f, 0.09f), 0.15f),
                Soot = Lit("Soot", new Color(0.03f, 0.025f, 0.02f), 0.05f),
                Stone = Lit("Stone", new Color(0.33f, 0.31f, 0.28f), 0.2f),
                Brass = Lit("Brass", new Color(0.58f, 0.44f, 0.2f), 0.7f),
                Porcelain = Lit("Porcelain", new Color(0.86f, 0.85f, 0.8f), 0.8f),
                Ivory = Lit("Ivory", new Color(0.88f, 0.85f, 0.76f), 0.6f),
                Ebony = Lit("Ebony", new Color(0.03f, 0.03f, 0.03f), 0.7f),
                Curtain = Lit("Curtain", new Color(0.33f, 0.05f, 0.07f), 0.3f),
                Cushion = Lit("Cushion", new Color(0.3f, 0.06f, 0.08f), 0.2f),
                Eye = Lit("Eye", new Color(0.9f, 0.88f, 0.84f), 0.85f),
                Pupil = Lit("Pupil", new Color(0.02f, 0.015f, 0.01f), 0.9f),
                TVBody = Lit("TV_Body", new Color(0.11f, 0.075f, 0.045f), 0.5f),
                Wax = Lit("Wax", new Color(0.88f, 0.84f, 0.72f), 0.3f),

                Shade = Lit("Shade", new Color(0.9f, 0.78f, 0.6f), 0.2f, k_ShadeGlow * 0.9f),
                Bulb = Lit("Bulb", new Color(1f, 0.92f, 0.75f), 0.4f, k_ShadeGlow * 1.2f),
                Flame = Lit("Flame", new Color(1f, 0.7f, 0.3f), 0.1f, new Color(1f, 0.6f, 0.2f)),

                Night = Unlit("Night", t.NightWindow, Color.white, false),
                Screen = Unlit("TV_Screen", t.Static, Color.white, false),
                Shadow = Unlit("Ghost_Shadow", t.GhostShadow, new Color(0f, 0f, 0f, 0.5f), true),

                // Almost no white core: stacked flames summed to white would read as neither fire nor ghost-fire.
                Fire = Glow("Fire", beam, 1.3f, 0.08f),
                Ghost = Glow("Ghost", beam, 2.2f, 0.35f),
            };

            var bookColours = new[]
            {
                new Color(0.32f, 0.06f, 0.05f), new Color(0.07f, 0.1f, 0.24f), new Color(0.08f, 0.2f, 0.1f),
                new Color(0.45f, 0.33f, 0.12f), new Color(0.22f, 0.13f, 0.07f), new Color(0.05f, 0.05f, 0.05f),
            };
            p.Books = new Material[bookColours.Length];
            for (int i = 0; i < bookColours.Length; i++)
                p.Books[i] = Lit($"Book_{i}", bookColours[i], 0.25f);

            foreach (var field in typeof(Palette).GetFields())
            {
                if (field.FieldType == typeof(Material) && field.GetValue(p) == null)
                {
                    Debug.LogError($"[{k_Tag}] Could not make the {field.Name} material; the room would render pink.");
                    return null;
                }
            }

            AssetDatabase.SaveAssets();
            return p;
        }

        static Material Lit(string name, Color colour, float smoothness, Color emission = default)
        {
            return EnsureMaterial(k_Tag, MaterialPath(name), colour, emission, 1f, smoothness);
        }

        static Material Textured(string name, Texture2D texture, Vector2 tiling, float smoothness)
        {
            var material = EnsureMaterial(k_Tag, MaterialPath(name), Color.white, Color.black, 1f, smoothness);
            if (material == null)
                return null;

            material.mainTexture = texture;
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
            material.mainTextureScale = tiling;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP Unlit, for what is a picture of light rather than a lit surface: the window, the TV, the shadow.</summary>
        static Material Unlit(string name, Texture texture, Color colour, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;

            string path = MaterialPath(name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                EnsureFolder(path);
                material = new Material(shader);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            if (transparent)
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>The visualizer's additive glow, round: flames, embers and the ghost's points.</summary>
        static Material Glow(string name, Shader shader, float softness, float core)
        {
            string path = MaterialPath(name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                EnsureFolder(path);
                material = new Material(shader);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetFloat("_Softness", softness);
            material.SetFloat("_Core", core);
            material.SetFloat("_Radial", 1f);
            material.EnableKeyword("_RADIAL");

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);

            return material;
        }
    }
}
