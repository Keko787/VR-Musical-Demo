using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRShootingGallery.GlassRoom;
using VRShootingGallery.Visualizer;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the glass room: a glass box with an iron frame laid onto the CAVE at one to one, its
    /// walls the screens, furnished like the room in Porter Robinson and Madeon's <i>Shelter</i> — a
    /// glass desk with a tablet and a teddy bear, a little cabinet with books on it, a brass bed, three
    /// glass pendant lamps — and, through the glass, a world that plays to the music.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The glass walls are not windows onto 3D content. Each is a camera's picture drawn flat on the
    /// screen (<see cref="WindowView"/>), taken from one fixed eye in the room, so the outside looks
    /// the same to everyone in the room and the three walls meet as one panorama. The worlds stand
    /// 20 km away from the room in the scene — far past the CAVE cameras' 1 km far plane, and the
    /// room far past the window cameras' — so neither ever draws the other.
    /// </para>
    /// <para>
    /// Four worlds are built, all round the same point (the room's place outside), only one awake at
    /// a time, running from day to night: <b>Red Rock</b>, the canyon and light pillar from the film's
    /// opening; <b>Above the Clouds</b>, the sea of cloud the room floats over in the film; <b>Sunset
    /// Beach</b>, surf, palms, a lamp-lit pier and fireworks; and <b>Aurora</b>, a night lake.
    /// The land is generated at run time from the numbers here, so nothing large is saved.
    /// </para>
    /// <para>
    /// Everything lives under <c>CAVE_User_Room/Glass_Room</c> and is registered with Undo. A rebuild
    /// clears it; the <see cref="Simulation"/> on the root is reused, so its Inspector settings survive.
    /// </para>
    /// </remarks>
    public static class GlassRoomBuilder
    {
        const string k_Tag = "GlassRoom";
        const string k_CreateMenu = "VR Shooting Gallery/Glass Room/Create Glass Room Scene";
        const string k_BuildMenu = "VR Shooting Gallery/Glass Room/Rebuild Glass Room";
        const string k_ScenePath = "Assets/_Project/Scenes/Demos/GlassRoom MiddleVR.unity";

        const string k_Root = "Glass_Room";
        const string k_Art = "Assets/_Project/Art/GlassRoom";
        const string k_Shaders = "Assets/_Project/Art/Shaders/GlassRoom";
        const string k_BeamShaderPath = "Assets/_Project/Art/Shaders/LaserBeam.shader";

        /// <summary>Where the outside is, from the room: far past every camera's reach in both directions.</summary>
        static readonly Vector3 k_OutsideOffset = new(0f, 0f, 20000f);

        /// <summary>The fixed eye every glass wall's picture is taken from: the middle of the room at standing eye height.</summary>
        static readonly Vector3 k_Eye = new(0f, 1.6f, -0.15f);

        /// <summary>Metres the glass sits in front of the screens: over the template's room, too little to read as depth.</summary>
        const float k_Inset = 0.005f;

        /// <summary>The iron frame's section.</summary>
        const float k_Post = 0.06f;

        static float Height => CaveScreens.Height;

        /// <summary>The three glass walls, and how far round the room each starts, for the wave between worlds.</summary>
        static readonly (CaveScreen screen, float sweep)[] k_Walls =
        {
            (CaveScreens.Left, 0f),
            (CaveScreens.Front, CaveScreens.SideWidth),
            (CaveScreens.Right, CaveScreens.SideWidth + CaveScreens.FrontWidth),
        };

        const float k_Perimeter = CaveScreens.SideWidth * 2f + CaveScreens.FrontWidth;

        static readonly Color k_SignColour = new(0.42f, 0.46f, 0.52f);

        /// <summary>The beach's sea level, below the room's floor: the sand rises from it to the deck the room stands on.</summary>
        const float k_SeaLevel = -1.2f;

        // ================================================================== menus

        [MenuItem(k_CreateMenu, false, 800)]
        static void CreateGlassRoomScene()
        {
            CreateScene(k_Tag, k_ScenePath, "glass room", Build,
                "Drop a track into Assets/_Project/Audio/Music and enter play mode. W changes the world outside, L toggles " +
                "low intensity, N skips, Space pauses.");
        }

        [MenuItem(k_BuildMenu, false, 810)]
        static void Rebuild() => RebuildInOpenScene(Build);

        /// <summary>
        /// For a build machine, or to generate the scene without opening the editor:
        /// <c>Unity -batchmode -projectPath . -executeMethod VRShootingGallery.EditorTools.GlassRoomBuilder.CreateFromCommandLine -quit</c>.
        /// Batch mode answers every dialog with Cancel, so an existing scene is rebuilt in place and saved.
        /// </summary>
        public static void CreateFromCommandLine()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_ScenePath) == null)
            {
                CreateGlassRoomScene();
            }
            else
            {
                var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
                if (Build(scene))
                    EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
        }

        // ================================================================== build

        static bool Build(Scene scene)
        {
            var room = RequireRoom(scene, k_Tag, "glass room");
            if (room == null)
                return false;

            Undo.SetCurrentGroupName("Build Glass Room");
            int group = Undo.GetCurrentGroup();

            var textures = GlassRoomTextures.EnsureAll($"{k_Art}/Textures");
            var p = MakePalette(textures);
            if (p == null)
                return false;

            var root = FindOrCreateInstrument(room, k_Root, Vector3.zero, k_Tag);
            Clear(root);

            var simulation = root.GetComponent<Simulation>();
            if (simulation == null)
                simulation = Undo.AddComponent<Simulation>(root.gameObject);

            var (deck, analyser) = BuildAudio(root);

            var outside = NewChild(root, "Outside");
            outside.localPosition = k_OutsideOffset;
            BuildSky(outside, p);
            var worlds = new[]
            {
                BuildRedRock(outside, simulation, p),
                BuildCloudSea(outside, simulation, p),
                BuildBeach(outside, simulation, p),
                BuildAurora(outside, simulation, p),
            };

            BuildFloor(root, p);
            BuildFrame(root, p);
            var tablet = BuildDesk(root, p);
            BuildCabinet(root, p);
            BuildBed(root, p);
            var lamps = BuildPendants(root, simulation, p);
            var windows = BuildWindows(root, outside, p, tablet);
            var sign = BuildSign(root);
            var sun = Atmosphere(scene, room, root);

            var so = new SerializedObject(simulation);
            SetRef(k_Tag, so, "m_Analyser", analyser);
            SetRef(k_Tag, so, "m_Deck", deck);
            SetArray(k_Tag, so, "m_Worlds", worlds);
            SetRef(k_Tag, so, "m_Outside", outside);
            SetArray(k_Tag, so, "m_Windows", windows);
            SetRef(k_Tag, so, "m_Sun", sun);
            SetArray(k_Tag, so, "m_Lamps", lamps);
            SetRef(k_Tag, so, "m_Sign", sign);
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;

            Debug.Log($"[{k_Tag}] Built the glass room: {windows.Count} glass walls, {worlds.Length} worlds outside, " +
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

        // ================================================================== the room

        static void BuildFloor(Transform root, Palette p)
        {
            var floor = CaveScreens.Floor;
            var boards = Quad(root, "Floorboards", floor.Point(0.5f, 0.5f, 0.003f), new Vector2(floor.Width, floor.Height),
                p.Floor, floor.FacingRotation);
            boards.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var rug = NewChild(root, "Rug");
            rug.localPosition = new Vector3(-0.3f, 0f, -0.3f);
            Cyl(rug, "Border", new Vector3(0f, 0.004f, 0f), 0.78f, 0.006f, p.RugEdge);
            Cyl(rug, "Field", new Vector3(0f, 0.006f, 0f), 0.68f, 0.006f, p.Rug);
        }

        /// <summary>
        /// The iron frame: a post at each corner, a rail along the top and a sill along the floor of
        /// each glass wall, and a brace across each wall's top corners — the box's bevelled profile.
        /// </summary>
        static void BuildFrame(Transform root, Palette p)
        {
            var frame = NewChild(root, "Frame");
            float half = k_Post * 0.5f;
            float h = Height;
            float x = CaveScreens.HalfWidth - half;
            float front = CaveScreens.FrontZ - half;
            float back = CaveScreens.SideCentreZ - CaveScreens.SideWidth * 0.5f + half;
            float sideLength = front - back + k_Post;
            float sideMiddle = (front + back) * 0.5f;

            Box(frame, "Post_Front_Left", new Vector3(-x, h * 0.5f, front), new Vector3(k_Post, h, k_Post), p.Iron);
            Box(frame, "Post_Front_Right", new Vector3(x, h * 0.5f, front), new Vector3(k_Post, h, k_Post), p.Iron);
            Box(frame, "Post_Back_Left", new Vector3(-x, h * 0.5f, back), new Vector3(k_Post, h, k_Post), p.Iron);
            Box(frame, "Post_Back_Right", new Vector3(x, h * 0.5f, back), new Vector3(k_Post, h, k_Post), p.Iron);

            Box(frame, "Rail_Front", new Vector3(0f, h - half, front), new Vector3(CaveScreens.FrontWidth, k_Post, k_Post), p.Iron);
            Box(frame, "Sill_Front", new Vector3(0f, half, front), new Vector3(CaveScreens.FrontWidth, k_Post, k_Post), p.Iron);
            Box(frame, "Rail_Left", new Vector3(-x, h - half, sideMiddle), new Vector3(k_Post, k_Post, sideLength), p.Iron);
            Box(frame, "Sill_Left", new Vector3(-x, half, sideMiddle), new Vector3(k_Post, k_Post, sideLength), p.Iron);
            Box(frame, "Rail_Right", new Vector3(x, h - half, sideMiddle), new Vector3(k_Post, k_Post, sideLength), p.Iron);
            Box(frame, "Sill_Right", new Vector3(x, half, sideMiddle), new Vector3(k_Post, k_Post, sideLength), p.Iron);

            foreach (var (screen, _) in k_Walls)
            {
                var wall = NewChild(frame, $"Braces_{screen.Name}");
                wall.localPosition = screen.Point(0.5f, 0f);
                wall.localRotation = screen.FacingRotation;

                float w = screen.Width * 0.5f - k_Post;
                const float reach = 0.34f;
                const float z = -0.03f;
                Slab(wall, "Brace_Left", new Vector3(-w, h - reach, z), new Vector3(-w + reach, h - k_Post, z), 0.045f, 0.045f, 0f, p.Iron);
                Slab(wall, "Brace_Right", new Vector3(w - reach, h - k_Post, z), new Vector3(w, h - reach, z), 0.045f, 0.045f, 0f, p.Iron);
            }
        }

        /// <summary>
        /// The glass desk along the front wall, an oval of blue glass on iron legs, and what is on it:
        /// a glass of water, the tablet the worlds are made on (its screen shows the front wall's view),
        /// and the teddy bear at the end.
        /// </summary>
        static Renderer BuildDesk(Transform root, Palette p)
        {
            const float top = 0.74f;
            var desk = NewChild(root, "Desk");
            desk.localPosition = new Vector3(0.2f, 0f, 0.8f);

            var glassTop = Primitive(desk, PrimitiveType.Cylinder, "Glass_Top", p.GlassBlue);
            glassTop.localPosition = new Vector3(0f, top, 0f);
            glassTop.localScale = new Vector3(2f, 0.0125f, 0.5f);

            foreach (var (lx, lz) in new[] { (-0.78f, -0.13f), (0.78f, -0.13f), (-0.78f, 0.13f), (0.78f, 0.13f) })
                Cyl(desk, "Leg", new Vector3(lx, top * 0.5f, lz), 0.012f, top, p.Iron);
            Box(desk, "Stretcher", new Vector3(0f, 0.12f, 0f), new Vector3(1.56f, 0.015f, 0.015f), p.Iron);

            float surface = top + 0.0125f;

            var glass = NewChild(desk, "Glass_Of_Water");
            glass.localPosition = new Vector3(-0.42f, surface, -0.05f);
            Cyl(glass, "Water", new Vector3(0f, 0.035f, 0f), 0.03f, 0.066f, p.Water);
            Cyl(glass, "Glass", new Vector3(0f, 0.055f, 0f), 0.034f, 0.11f, p.GlassClear);

            // The tablet leans back on its own case, its screen toward whoever stands at the desk.
            var tablet = NewChild(desk, "Tablet");
            tablet.localPosition = new Vector3(0.08f, surface + 0.032f, -0.06f);
            tablet.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.94f, 0.34f), new Vector3(0f, 0.34f, 0.94f));
            Box(tablet, "Case", Vector3.zero, new Vector3(0.27f, 0.19f, 0.008f), p.TabletBody);
            var screen = Quad(tablet, "Screen", new Vector3(0f, 0f, -0.0045f), new Vector2(0.245f, 0.165f), p.TabletScreen);

            BuildTeddy(desk, new Vector3(0.82f, surface, -0.02f), p);
            return screen.GetComponent<Renderer>();
        }

        /// <summary>The teddy bear, sitting, legs out, a little grey patch on its tummy; facing the room.</summary>
        static void BuildTeddy(Transform desk, Vector3 at, Palette p)
        {
            var bear = NewChild(desk, "Teddy_Bear");
            bear.localPosition = at;
            bear.localRotation = Quaternion.Euler(0f, 15f, 0f);

            Ball(bear, "Body", new Vector3(0f, 0.085f, 0f), new Vector3(0.15f, 0.16f, 0.12f), p.Teddy);
            Box(bear, "Patch", new Vector3(0f, 0.08f, -0.058f), new Vector3(0.05f, 0.045f, 0.01f), p.Patch);
            Ball(bear, "Head", new Vector3(0f, 0.215f, -0.005f), new Vector3(0.13f, 0.12f, 0.115f), p.Teddy);
            Ball(bear, "Ear_L", new Vector3(-0.05f, 0.27f, 0f), new Vector3(0.045f, 0.045f, 0.025f), p.Teddy);
            Ball(bear, "Ear_R", new Vector3(0.05f, 0.27f, 0f), new Vector3(0.045f, 0.045f, 0.025f), p.Teddy);
            Ball(bear, "Muzzle", new Vector3(0f, 0.2f, -0.055f), new Vector3(0.055f, 0.04f, 0.04f), p.TeddyLight);
            Ball(bear, "Nose", new Vector3(0f, 0.212f, -0.075f), new Vector3(0.016f, 0.012f, 0.01f), p.TeddyDark);
            Ball(bear, "Eye_L", new Vector3(-0.026f, 0.236f, -0.052f), Vector3.one * 0.014f, p.TeddyDark);
            Ball(bear, "Eye_R", new Vector3(0.026f, 0.236f, -0.052f), Vector3.one * 0.014f, p.TeddyDark);

            var armL = Ball(bear, "Arm_L", new Vector3(-0.075f, 0.1f, -0.015f), new Vector3(0.045f, 0.1f, 0.05f), p.Teddy);
            armL.localRotation = Quaternion.Euler(0f, 0f, -25f);
            var armR = Ball(bear, "Arm_R", new Vector3(0.075f, 0.1f, -0.015f), new Vector3(0.045f, 0.1f, 0.05f), p.Teddy);
            armR.localRotation = Quaternion.Euler(0f, 0f, 25f);

            Ball(bear, "Leg_L", new Vector3(-0.045f, 0.03f, -0.055f), new Vector3(0.055f, 0.05f, 0.09f), p.Teddy);
            Ball(bear, "Leg_R", new Vector3(0.045f, 0.03f, -0.055f), new Vector3(0.055f, 0.05f, 0.09f), p.Teddy);
        }

        /// <summary>A little two-column drawer cabinet in the front-left corner, books and a box of ink bottles on top.</summary>
        static void BuildCabinet(Transform root, Palette p)
        {
            var cabinet = NewChild(root, "Cabinet");
            cabinet.localPosition = new Vector3(-1.42f, 0f, 0.86f);

            Box(cabinet, "Body", new Vector3(0f, 0.33f, 0f), new Vector3(0.5f, 0.66f, 0.4f), p.Cabinet);
            Box(cabinet, "Top", new Vector3(0f, 0.6725f, 0f), new Vector3(0.53f, 0.025f, 0.42f), p.CabinetTop);
            Box(cabinet, "Plinth", new Vector3(0f, 0.02f, -0.005f), new Vector3(0.52f, 0.04f, 0.41f), p.CabinetTop);

            for (int column = 0; column < 2; column++)
                for (int row = 0; row < 4; row++)
                {
                    float dx = column == 0 ? -0.122f : 0.122f;
                    float dy = 0.12f + row * 0.142f;
                    Box(cabinet, $"Drawer_{column}{row}", new Vector3(dx, dy, -0.203f), new Vector3(0.22f, 0.125f, 0.012f), p.Drawer);
                    Ball(cabinet, $"Knob_{column}{row}", new Vector3(dx, dy - 0.012f, -0.214f), Vector3.one * 0.018f, p.Brass);
                    Box(cabinet, $"Label_{column}{row}", new Vector3(dx, dy + 0.032f, -0.21f), new Vector3(0.06f, 0.02f, 0.004f), p.Paper);
                }

            const float top = 0.685f;
            var bookA = Box(cabinet, "Book_Lower", new Vector3(-0.08f, top + 0.02f, -0.02f), new Vector3(0.22f, 0.04f, 0.16f), p.BookA);
            bookA.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Box(bookA, "Pages", new Vector3(0.02f, 0f, 0f), new Vector3(0.95f, 0.8f, 1.02f), p.Pages);
            var bookB = Box(cabinet, "Book_Upper", new Vector3(-0.07f, top + 0.0575f, -0.03f), new Vector3(0.2f, 0.035f, 0.15f), p.BookB);
            bookB.localRotation = Quaternion.Euler(0f, -5f, 0f);
            Box(bookB, "Pages", new Vector3(0.02f, 0f, 0f), new Vector3(0.95f, 0.8f, 1.02f), p.Pages);
            Box(bookB, "Band", new Vector3(-0.3f, 0f, 0f), new Vector3(0.06f, 1.02f, 1.01f), p.Brass);

            var box = NewChild(cabinet, "Ink_Box");
            box.localPosition = new Vector3(0.13f, top, 0.07f);
            Box(box, "Box", new Vector3(0f, 0.03f, 0f), new Vector3(0.17f, 0.06f, 0.11f), p.Wood);
            for (int i = 0; i < 3; i++)
            {
                float bx = -0.05f + i * 0.05f;
                Cyl(box, $"Bottle_{i}", new Vector3(bx, 0.095f, 0f), 0.019f, 0.1f, p.Bottle);
                Cyl(box, $"Cap_{i}", new Vector3(bx, 0.155f, 0f), 0.011f, 0.022f, p.Brass);
            }
        }

        /// <summary>A narrow brass bed along the right wall, head to the front: posts with ball finials, rails and spindles, white sheets, a pink blanket.</summary>
        static void BuildBed(Transform root, Palette p)
        {
            var bed = NewChild(root, "Bed");
            const float left = 0.99f;
            const float right = 1.72f;
            const float head = 0.42f;
            const float foot = -1.18f;
            float cx = (left + right) * 0.5f;
            float cz = (head + foot) * 0.5f;
            float width = right - left;
            float length = head - foot;

            Box(bed, "Mattress", new Vector3(cx, 0.41f, cz), new Vector3(width - 0.02f, 0.18f, length - 0.04f), p.Sheet);
            Box(bed, "Blanket", new Vector3(cx, 0.515f, cz - 0.38f), new Vector3(width + 0.01f, 0.05f, length * 0.52f), p.Blanket);
            Box(bed, "Blanket_Fold", new Vector3(cx, 0.545f, cz + 0.04f), new Vector3(width + 0.01f, 0.05f, 0.1f), p.Blanket);
            Ball(bed, "Pillow", new Vector3(cx, 0.545f, head - 0.2f), new Vector3(0.55f, 0.11f, 0.3f), p.Pillow);

            Cyl(bed, "Rail_Inner", new Vector3(left, 0.3f, cz), 0.012f, length, p.Brass, new Vector3(90f, 0f, 0f));
            Cyl(bed, "Rail_Outer", new Vector3(right, 0.3f, cz), 0.012f, length, p.Brass, new Vector3(90f, 0f, 0f));

            BedEnd(bed, "Head", head, left, right, 1.0f, 0.66f, 0.95f, p);
            BedEnd(bed, "Foot", foot, left, right, 0.8f, 0.52f, 0.76f, p);
        }

        static void BedEnd(Transform bed, string name, float z, float left, float right, float postHeight, float lowRail, float highRail, Palette p)
        {
            var end = NewChild(bed, name);
            float cx = (left + right) * 0.5f;
            float width = right - left;

            foreach (var x in new[] { left, right })
            {
                Cyl(end, "Post", new Vector3(x, postHeight * 0.5f, z), 0.018f, postHeight, p.Brass);
                Ball(end, "Finial", new Vector3(x, postHeight + 0.025f, z), Vector3.one * 0.05f, p.Brass);
            }

            Cyl(end, "Rail_High", new Vector3(cx, highRail, z), 0.014f, width, p.Brass, new Vector3(0f, 0f, 90f));
            Cyl(end, "Rail_Low", new Vector3(cx, lowRail, z), 0.012f, width, p.Brass, new Vector3(0f, 0f, 90f));

            const int spindles = 6;
            for (int i = 1; i <= spindles; i++)
            {
                float x = Mathf.Lerp(left, right, i / (float)(spindles + 1));
                Cyl(end, $"Spindle_{i}", new Vector3(x, (lowRail + highRail) * 0.5f, z), 0.007f, highRail - lowRail, p.Brass);
            }
        }

        /// <summary>Three glass pendant lamps over the desk at different heights, the left lowest and the middle highest, as in the film.</summary>
        static List<PendantLamp> BuildPendants(Transform root, Simulation simulation, Palette p)
        {
            var holder = NewChild(root, "Pendants");
            var lamps = new List<PendantLamp>();

            var hangings = new[]
            {
                ("Pendant_Left", new Vector3(-0.85f, 0f, 0.62f), 1.5f, 0f),
                ("Pendant_Middle", new Vector3(0.05f, 0f, 0.8f), 1.88f, 1.7f),
                ("Pendant_Right", new Vector3(0.85f, 0f, 0.55f), 1.68f, 3.1f),
            };

            int index = 0;
            foreach (var (name, at, bulbHeight, phase) in hangings)
            {
                var pivot = NewChild(holder, name);
                pivot.localPosition = new Vector3(at.x, Height, at.z);
                float drop = Height - bulbHeight;             // pivot to bulb
                float cord = drop - 0.1f;

                Cyl(pivot, "Cord", new Vector3(0f, -cord * 0.5f, 0f), 0.004f, cord, p.Cord);
                Cyl(pivot, "Fitting", new Vector3(0f, -cord - 0.015f, 0f), 0.02f, 0.05f, p.Brass);
                var bulb = Ball(pivot, "Bulb", new Vector3(0f, -drop, 0f), Vector3.one * 0.05f, p.Bulb);
                var globe = Ball(pivot, "Globe", new Vector3(0f, -drop, 0f), new Vector3(0.2f, 0.15f, 0.2f), p.Globe);
                bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                globe.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                var lightHolder = NewChild(pivot, "Light");
                lightHolder.localPosition = new Vector3(0f, -drop, 0f);
                var light = Undo.AddComponent<Light>(lightHolder.gameObject);
                light.type = LightType.Point;
                light.range = 2.6f;
                light.intensity = 0.9f;
                light.color = new Color(1f, 0.78f, 0.52f);
                light.shadows = LightShadows.None;

                var lamp = Undo.AddComponent<PendantLamp>(pivot.gameObject);
                var so = new SerializedObject(lamp);
                SetRef(k_Tag, so, "m_Simulation", simulation);
                SetRef(k_Tag, so, "m_Light", light);
                SetArray(k_Tag, so, "m_Glows", new[] { bulb.GetComponent<Renderer>() });
                SetArray(k_Tag, so, "m_Shades", new[] { globe.GetComponent<Renderer>() });
                SetRef(k_Tag, so, "m_Bulb", bulb);
                SetFloat(k_Tag, so, "m_CordLength", drop);
                SetFloat(k_Tag, so, "m_Phase", phase);
                SetVector3(k_Tag, so, "m_SwayAxis", index % 2 == 0 ? Vector3.right : Vector3.forward);
                so.ApplyModifiedProperties();

                lamps.Add(lamp);
                index++;
            }

            return lamps;
        }

        /// <summary>One glass wall per projected wall screen, each with its camera outside, the tablet showing the front one's view.</summary>
        static List<WindowView> BuildWindows(Transform root, Transform outside, Palette p, Renderer tablet)
        {
            var holder = NewChild(root, "Glass_Walls");
            var windows = new List<WindowView>();

            foreach (var (screen, sweep) in k_Walls)
            {
                var quad = Quad(holder, $"Glass_{screen.Name}", screen.Point(0.5f, 0.5f, k_Inset), new Vector2(screen.Width, screen.Height),
                    p.Window, screen.FacingRotation);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var window = Undo.AddComponent<WindowView>(quad.gameObject);
                var so = new SerializedObject(window);
                SetRef(k_Tag, so, "m_Outside", outside);
                SetRef(k_Tag, so, "m_Room", root);
                SetScreen(so, "m_Screen", screen);
                SetVector3(k_Tag, so, "m_Eye", k_Eye);
                SetFloat(k_Tag, so, "m_SweepOffset", sweep);
                SetFloat(k_Tag, so, "m_SweepLength", k_Perimeter);
                if (screen.Name == CaveScreens.Front.Name && tablet != null)
                    SetArray(k_Tag, so, "m_Mirrors", new[] { tablet });
                so.ApplyModifiedProperties();

                windows.Add(window);
            }

            return windows;
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
        /// Daylight through the glass: the template's sun with soft shadows (each world turns and
        /// tints it at run time), flat ambient, no fog, a black clear colour. The template's room
        /// model stops casting shadows — its wall quads would otherwise shade the room from the sun.
        /// </summary>
        static Light Atmosphere(Scene scene, Transform room, Transform root)
        {
            Light sun = null;
            var sunRoot = FindRoot(scene, "Directional Light");
            if (sunRoot != null && sunRoot.TryGetComponent(out sun))
            {
                Undo.RecordObject(sun, "Daylight");
                sun.shadows = LightShadows.Soft;
                sun.intensity = 1.15f;
                sun.color = new Color(1f, 0.95f, 0.86f);
                Undo.RecordObject(sun.transform, "Daylight");
                sun.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.55f, 0.62f, -0.55f).normalized);
            }
            else
            {
                Debug.LogWarning($"[{k_Tag}] No 'Directional Light' in the scene; the room will not be lit by the world outside.");
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.56f, 0.55f);
            RenderSettings.reflectionIntensity = 0.5f;
            RenderSettings.fog = false;

            var cameraRoot = FindRoot(scene, "MainCamera");
            if (cameraRoot != null && cameraRoot.TryGetComponent<Camera>(out var camera))
            {
                Undo.RecordObject(camera, "Darken camera");
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }

            for (int i = 0; i < room.childCount; i++)
            {
                var child = room.GetChild(i);
                if (child == root)
                    continue;

                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    Undo.RecordObject(renderer, "No room shadows");
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            return sun;
        }

        // ================================================================== outside

        static void BuildSky(Transform outside, Palette p)
        {
            var sky = MeshObject(outside, "Sky", Vector3.zero, Quaternion.identity, p.Sky);
            Undo.AddComponent<SkyDome>(sky);
        }

        /// <summary>
        /// The canyon from the film's opening: red rock buttes and mesas cut in strata, a light pillar
        /// falling into a pool in front of the middle butte, and the top of the mesa on the left made
        /// of cubes that come apart with the music. The room stands on a rock above the canyon floor.
        /// </summary>
        static SimWorld BuildRedRock(Transform outside, Simulation simulation, Palette p)
        {
            const float floor = -25f;
            var world = NewChild(outside, "World_RedRock");
            var pillarAt = new Vector3(15f, floor, 560f);
            var voxelAt = new Vector3(-240f, 0f, 345f);

            var groundObject = MeshObject(world, "Canyon_Floor", Vector3.zero, Quaternion.identity, p.Canyon);
            var ground = Undo.AddComponent<SimSurface>(groundObject);
            var so = new SerializedObject(ground);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.DesertFloor);
            SetFloat(k_Tag, so, "m_InnerRadius", 2f);
            SetFloat(k_Tag, so, "m_OuterRadius", 8200f);
            SetInt(k_Tag, so, "m_Rings", 230);
            SetInt(k_Tag, so, "m_Segments", 420);
            SetInt(k_Tag, so, "m_Seed", 3);
            SetFloat(k_Tag, so, "m_Base", floor);
            SetFloat(k_Tag, so, "m_Rim", 300f);
            SetFloat(k_Tag, so, "m_RimRadius", 2700f);
            SetFloat(k_Tag, so, "m_RimWidth", 900f);
            SetFloat(k_Tag, so, "m_Terraces", 38f);
            SetVector4(so, "m_Basin", new Vector4(pillarAt.x, pillarAt.z, 80f, 1.6f));
            SetFloat(k_Tag, so, "m_LevelAround", 10f);
            so.ApplyModifiedProperties();

            // The rock the room stands on.
            Butte(world, "Pedestal", Vector3.zero, 0f, p.Canyon, 2.3f, new Vector2(1f, 0.75f), floor, new[] { -12f, -0.02f },
                new[] { 4.5f, 1f }, 0.08f, 0.15f, 0f, 0.4f, 11, 48);

            Butte(world, "Butte_Middle", new Vector3(40f, 0f, 690f), Yaw(40f, 690f), p.Canyon, 75f, new Vector2(1.25f, 0.8f), floor,
                new[] { 70f, 118f }, new[] { 1f, 0.62f }, 0.22f, 0.08f, 0f, 0.3f, 3);
            Butte(world, "Mesa_Voxel", voxelAt, Yaw(voxelAt.x, voxelAt.z), p.Canyon, 120f, new Vector2(1.4f, 0.7f), floor,
                new[] { 50f, 72f }, new[] { 1f, 0.8f }, 0.16f, 0.06f, 0f, 0.3f, 5);
            Butte(world, "Tower_Right", new Vector3(310f, 0f, 340f), Yaw(310f, 340f), p.Canyon, 85f, new Vector2(0.9f, 1f), floor,
                new[] { 50f, 105f, 160f }, new[] { 1f, 0.72f, 0.45f }, 0.3f, 0.1f, 0f, 0.35f, 7);
            Butte(world, "Butte_Far_Left", new Vector3(-560f, 0f, 950f), Yaw(-560f, 950f), p.Canyon, 70f, new Vector2(1.1f, 0.9f), floor,
                new[] { 60f, 95f }, new[] { 1f, 0.6f }, 0.22f, 0.08f, 0f, 0.3f, 13);
            Butte(world, "Butte_Far_Right", new Vector3(520f, 0f, 1150f), Yaw(520f, 1150f), p.Canyon, 95f, new Vector2(1.2f, 0.8f), floor,
                new[] { 80f, 140f }, new[] { 1f, 0.65f }, 0.22f, 0.08f, 0f, 0.3f, 17);
            Butte(world, "Mesa_Left", new Vector3(-900f, 0f, 250f), Yaw(-900f, 250f), p.Canyon, 170f, new Vector2(0.8f, 1.3f), floor,
                new[] { 120f, 175f }, new[] { 1f, 0.7f }, 0.2f, 0.08f, 0f, 0.3f, 19);
            Butte(world, "Mesa_Right", new Vector3(950f, 0f, 500f), Yaw(950f, 500f), p.Canyon, 150f, new Vector2(1f, 1.1f), floor,
                new[] { 100f, 160f }, new[] { 1f, 0.72f }, 0.22f, 0.08f, 0f, 0.3f, 23);
            Butte(world, "Mesa_Back_Left", new Vector3(-650f, 0f, -500f), Yaw(-650f, -500f), p.Canyon, 140f, new Vector2(1f, 1f), floor,
                new[] { 90f, 150f }, new[] { 1f, 0.7f }, 0.22f, 0.08f, 0f, 0.3f, 29);
            Butte(world, "Mesa_Back_Right", new Vector3(700f, 0f, -350f), Yaw(700f, -350f), p.Canyon, 130f, new Vector2(1f, 1f), floor,
                new[] { 80f, 120f }, new[] { 1f, 0.75f }, 0.22f, 0.08f, 0f, 0.3f, 31);
            Butte(world, "Butte_Back", new Vector3(60f, 0f, -900f), Yaw(60f, -900f), p.Canyon, 110f, new Vector2(1.2f, 0.9f), floor,
                new[] { 70f, 110f }, new[] { 1f, 0.65f }, 0.22f, 0.08f, 0f, 0.3f, 37);

            // Spires, in two clusters beyond the middle butte.
            foreach (var (name, x, z, radius, top, peak, seed) in new[]
                     {
                         ("Spire_1", -210f, 1250f, 14f, 85f, 14f, 41), ("Spire_2", -178f, 1292f, 10f, 112f, 10f, 43),
                         ("Spire_3", -245f, 1300f, 9f, 70f, 8f, 47), ("Spire_4", 260f, 1650f, 16f, 95f, 12f, 53),
                         ("Spire_5", 302f, 1618f, 11f, 124f, 12f, 59),
                     })
                Butte(world, name, new Vector3(x, 0f, z), Yaw(x, z), p.Canyon, radius, Vector2.one, floor, new[] { top }, new[] { 1f },
                    0.3f, 0.25f, peak, 0.6f, seed, 24);

            // Trees: on the middle butte's top and the tower's ledges, round the pool, scattered over the floor.
            var green = new Color(0.13f, 0.32f, 0.12f);
            var deep = new Color(0.1f, 0.26f, 0.12f);
            var olive = new Color(0.2f, 0.34f, 0.13f);
            var trees = MeshObject(world, "Trees", Vector3.zero, Quaternion.identity, p.Foliage);
            var puffs = Undo.AddComponent<SimPuffs>(trees);
            so = new SerializedObject(puffs);
            SetEnum(k_Tag, so, "m_Kind", (int)PuffKind.Trees);
            SetInt(k_Tag, so, "m_Seed", 5);
            SetRef(k_Tag, so, "m_Ground", ground);
            SetClusters(so, new[]
            {
                Trees(new Vector3(40f, 118f, 690f), new Vector2(40f, 22f), 9, 7f, green, false),
                Trees(new Vector3(261f, 50f, 286f), new Vector2(16f, 8f), 4, 6f, deep, false),
                Trees(new Vector3(277f, 105f, 303f), new Vector2(10f, 6f), 3, 5f, green, false),
                Trees(new Vector3(-197f, 50f, 283f), new Vector2(30f, 6f), 6, 5f, olive, false),
                Trees(new Vector3(pillarAt.x, 0f, pillarAt.z), new Vector2(120f, 70f), 34, 6.5f, green, true),
                Trees(new Vector3(-60f, 0f, 260f), new Vector2(140f, 140f), 30, 3.2f, olive, true),
                Trees(new Vector3(160f, 0f, 240f), new Vector2(120f, 90f), 22, 3f, deep, true),
                Trees(new Vector3(0f, 0f, 70f), new Vector2(70f, 35f), 12, 2.4f, olive, true),
                Trees(new Vector3(-120f, 0f, -150f), new Vector2(150f, 100f), 20, 3f, green, true),
                Trees(new Vector3(150f, 0f, -120f), new Vector2(120f, 100f), 18, 3f, olive, true),
                Trees(new Vector3(-220f, 0f, -20f), new Vector2(110f, 160f), 26, 3.4f, green, true),
                Trees(new Vector3(230f, 0f, 30f), new Vector2(110f, 150f), 24, 3.2f, deep, true),
            });
            so.ApplyModifiedProperties();

            // The pool the light falls into.
            var poolObject = MeshObject(world, "Pool", new Vector3(pillarAt.x, floor - 0.6f, pillarAt.z), Quaternion.identity, p.Pool);
            var pool = Undo.AddComponent<SimSurface>(poolObject);
            so = new SerializedObject(pool);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.Flat);
            SetFloat(k_Tag, so, "m_InnerRadius", 0.5f);
            SetFloat(k_Tag, so, "m_OuterRadius", 85f);
            SetInt(k_Tag, so, "m_Rings", 24);
            SetInt(k_Tag, so, "m_Segments", 64);
            so.ApplyModifiedProperties();

            var pillar = Pillar(world, "Light_Pillar", pillarAt + Vector3.down, simulation, p, 16f, 3400f,
                new Color(0.8f, 0.95f, 1.25f), -1f, 150f, new Color(0.45f, 0.8f, 0.95f));

            // The top of the mesa on the left, in cubes: an equaliser coming apart.
            var voxels = ParticleObject(world, "Voxels", voxelAt + new Vector3(0f, 72f, 0f), Quaternion.Euler(0f, Yaw(voxelAt.x, voxelAt.z), 0f),
                p.Voxel, 1024);
            var mesa = Undo.AddComponent<VoxelMesa>(voxels);
            so = new SerializedObject(mesa);
            SetRef(k_Tag, so, "m_Simulation", simulation);
            SetFloat(k_Tag, so, "m_Lift", 55f);
            so.ApplyModifiedProperties();

            Motes(world, "Motes", pillarAt, simulation, p.Mote, MoteMotion.Rise, 700, new Vector3(520f, 420f, 520f), 7f, 6f, 1.8f,
                new Color(1.4f, 1.3f, 1f), 0.1f, 1.3f, null);

            var sim = Undo.AddComponent<SimWorld>(world.gameObject);
            so = new SerializedObject(sim);
            SetString(k_Tag, so, "m_Title", "Red Rock");
            SetColor(k_Tag, so, "m_Zenith", new Color(0.07f, 0.22f, 0.72f));
            SetColor(k_Tag, so, "m_Horizon", new Color(0.62f, 0.8f, 0.98f));
            SetColor(k_Tag, so, "m_Ground", new Color(0.55f, 0.42f, 0.36f));
            SetVector3(k_Tag, so, "m_SunDirection", new Vector3(-0.55f, 0.62f, -0.55f));
            SetColor(k_Tag, so, "m_SunColour", new Color(1.15f, 1.06f, 0.92f));
            SetFloat(k_Tag, so, "m_SunDegrees", 1.2f);
            SetColor(k_Tag, so, "m_ShadowColour", new Color(0.36f, 0.3f, 0.55f));
            SetFloat(k_Tag, so, "m_Clouds", 0.5f);
            SetFloat(k_Tag, so, "m_CloudDrift", 0.003f);
            SetFloat(k_Tag, so, "m_FogDistance", 3500f);
            SetFloat(k_Tag, so, "m_FogAmount", 0.85f);
            SetFloat(k_Tag, so, "m_FogSunGlow", 0.25f);
            SetRef(k_Tag, so, "m_RingOrigin", pillar.transform);
            SetColor(k_Tag, so, "m_RingColour", new Color(0.6f, 1f, 1.15f));
            SetFloat(k_Tag, so, "m_RingSpeed", 240f);
            SetFloat(k_Tag, so, "m_RingDelay", 0.35f);
            SetColor(k_Tag, so, "m_RoomAmbient", new Color(0.6f, 0.56f, 0.55f));
            SetColor(k_Tag, so, "m_RoomLight", new Color(1f, 0.95f, 0.86f));
            SetFloat(k_Tag, so, "m_RoomLightIntensity", 1.15f);
            SetFloat(k_Tag, so, "m_LampLevel", 0.5f);
            so.ApplyModifiedProperties();
            return sim;
        }

        /// <summary>
        /// The film's own outside: the room floating over a sea of cloud in a pale sky, the sun low
        /// ahead. The music rolls out from the room across the sea as swells, cumulus towers on the
        /// horizon glow from inside with their bands, and three pillars of light stand in the distance.
        /// </summary>
        static SimWorld BuildCloudSea(Transform outside, Simulation simulation, Palette p)
        {
            const float sea = -60f;
            var world = NewChild(outside, "World_CloudSea");

            var cloudObject = MeshObject(world, "Cloud_Sea", new Vector3(0f, sea, 0f), Quaternion.identity, p.CloudSea);
            var clouds = Undo.AddComponent<SimSurface>(cloudObject);
            var so = new SerializedObject(clouds);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.Flat);
            SetFloat(k_Tag, so, "m_InnerRadius", 20f);
            SetFloat(k_Tag, so, "m_OuterRadius", 8200f);
            SetInt(k_Tag, so, "m_Rings", 240);
            SetInt(k_Tag, so, "m_Segments", 420);
            so.ApplyModifiedProperties();

            // Cumulus towers round the horizon, each lit from inside by its own part of the spectrum.
            var towerList = new List<PuffCluster>();
            foreach (var (azimuth, distance, height, width, band) in new[]
                     {
                         (-70f, 3800f, 900f, 380f, 0.05f), (-40f, 4200f, 1200f, 450f, 0.2f), (-15f, 3000f, 700f, 300f, 0.35f),
                         (12f, 4600f, 1300f, 520f, 0f), (35f, 3300f, 800f, 330f, 0.5f), (60f, 4000f, 1000f, 420f, 0.65f),
                         (95f, 3600f, 900f, 380f, 0.8f), (-100f, 3500f, 750f, 350f, 0.9f), (130f, 4000f, 900f, 400f, 0.95f),
                         (-135f, 3900f, 1000f, 400f, 0.75f),
                     })
            {
                float a = azimuth * Mathf.Deg2Rad;
                towerList.Add(new PuffCluster
                {
                    Centre = new Vector3(Mathf.Sin(a) * distance, sea - 20f, Mathf.Cos(a) * distance),
                    Spread = new Vector2(width, height),
                    Count = 46,
                    Size = width * 0.55f,
                    Colour = new Color(0.98f, 0.98f, 1f),
                    Band = band,
                    Glow = 1f,
                });
            }

            var towers = MeshObject(world, "Cumulus", Vector3.zero, Quaternion.identity, p.Cumulus);
            var puffs = Undo.AddComponent<SimPuffs>(towers);
            so = new SerializedObject(puffs);
            SetEnum(k_Tag, so, "m_Kind", (int)PuffKind.Cumulus);
            SetInt(k_Tag, so, "m_Seed", 9);
            SetClusters(so, towerList.ToArray());
            so.ApplyModifiedProperties();

            foreach (var (name, azimuth, distance, listen, colour) in new[]
                     {
                         ("Pillar_Bass", -32f, 2700f, 0.05f, new Color(1.2f, 1.05f, 0.8f)),
                         ("Pillar_Mid", 16f, 3600f, 0.45f, new Color(0.8f, 1f, 1.3f)),
                         ("Pillar_Treble", 52f, 2300f, 0.85f, new Color(1.2f, 0.85f, 1.1f)),
                     })
            {
                float a = azimuth * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Sin(a) * distance, sea, Mathf.Cos(a) * distance);
                Pillar(world, name, at, simulation, p, 70f, 5000f, colour, listen, 320f, colour * 0.5f);
            }

            Motes(world, "Sparks", new Vector3(0f, sea, 0f), simulation, p.Mote, MoteMotion.Rise, 900, new Vector3(1600f, 260f, 1600f),
                5f, 8f, 1.4f, new Color(1.4f, 1.2f, 0.9f), 0.08f, 1.2f, outside);

            var sim = Undo.AddComponent<SimWorld>(world.gameObject);
            so = new SerializedObject(sim);
            SetString(k_Tag, so, "m_Title", "Above the Clouds");
            SetColor(k_Tag, so, "m_Zenith", new Color(0.2f, 0.42f, 0.85f));
            SetColor(k_Tag, so, "m_Horizon", new Color(0.72f, 0.82f, 0.95f));
            SetColor(k_Tag, so, "m_Ground", new Color(0.85f, 0.88f, 0.96f));
            SetVector3(k_Tag, so, "m_SunDirection", new Vector3(0.35f, 0.22f, 1f));
            SetColor(k_Tag, so, "m_SunColour", new Color(1.1f, 1f, 0.85f));
            SetFloat(k_Tag, so, "m_SunDegrees", 1.1f);
            SetColor(k_Tag, so, "m_ShadowColour", new Color(0.62f, 0.66f, 0.9f));
            SetFloat(k_Tag, so, "m_Clouds", 0.3f);
            SetFloat(k_Tag, so, "m_CloudDrift", 0.002f);
            SetFloat(k_Tag, so, "m_FogDistance", 6000f);
            SetFloat(k_Tag, so, "m_FogAmount", 0.7f);
            SetFloat(k_Tag, so, "m_FogSunGlow", 0.35f);
            SetFloat(k_Tag, so, "m_WaveSpeed", 45f);
            SetFloat(k_Tag, so, "m_WaveInner", 20f);
            SetColor(k_Tag, so, "m_RoomAmbient", new Color(0.68f, 0.7f, 0.78f));
            SetColor(k_Tag, so, "m_RoomLight", new Color(1f, 0.93f, 0.82f));
            SetFloat(k_Tag, so, "m_RoomLightIntensity", 1.25f);
            SetFloat(k_Tag, so, "m_LampLevel", 0.4f);
            so.ApplyModifiedProperties();
            return sim;
        }

        /// <summary>
        /// Sunset on a sandy bay: the room on a low deck at the top of the beach, the sea in front with
        /// the sun going down over it, palms along the shore, a pier running out to sea, headlands at
        /// the ends of the bay and islands on the horizon. The bass swells the sea, a beat surges the
        /// surf up the sand, the mids wave the palms, the treble sets the sun's glitter path dancing,
        /// the pier's lamps are an equaliser, and strong beats burst fireworks over the water.
        /// </summary>
        static SimWorld BuildBeach(Transform outside, Simulation simulation, Palette p)
        {
            const float shore = 7f;
            const float slope = 0.05f;
            const float bay = 900f;
            var world = NewChild(outside, "World_Beach");

            var sandObject = MeshObject(world, "Sand", Vector3.zero, Quaternion.identity, p.Sand);
            var sand = Undo.AddComponent<SimSurface>(sandObject);
            var so = new SerializedObject(sand);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.Beach);
            SetFloat(k_Tag, so, "m_InnerRadius", 1f);
            SetFloat(k_Tag, so, "m_OuterRadius", 8200f);
            SetInt(k_Tag, so, "m_Rings", 240);
            SetInt(k_Tag, so, "m_Segments", 420);
            SetInt(k_Tag, so, "m_Seed", 7);
            SetFloat(k_Tag, so, "m_Base", k_SeaLevel);
            SetFloat(k_Tag, so, "m_Rim", 6f);
            SetFloat(k_Tag, so, "m_ShoreDistance", shore);
            SetFloat(k_Tag, so, "m_BeachSlope", slope);
            SetFloat(k_Tag, so, "m_BayRadius", bay);
            so.ApplyModifiedProperties();

            var seaObject = MeshObject(world, "Sea", new Vector3(0f, k_SeaLevel, 0f), Quaternion.identity, p.Ocean);
            var sea = Undo.AddComponent<SimSurface>(seaObject);
            so = new SerializedObject(sea);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.Flat);
            SetFloat(k_Tag, so, "m_InnerRadius", 1f);
            SetFloat(k_Tag, so, "m_OuterRadius", 8200f);
            SetInt(k_Tag, so, "m_Rings", 240);
            SetInt(k_Tag, so, "m_Segments", 420);
            so.ApplyModifiedProperties();

            var ocean = Undo.AddComponent<SimOcean>(seaObject);
            so = new SerializedObject(ocean);
            SetRef(k_Tag, so, "m_Beach", sand);
            so.ApplyModifiedProperties();

            // The deck the room stands on, at the top of the beach; its front edge shows under the front wall.
            float sandAtRoom = k_SeaLevel + slope * shore;
            var deck = NewChild(world, "Deck");
            var boards = Primitive(deck, PrimitiveType.Cube, "Boards", p.Deck);
            boards.localPosition = new Vector3(0f, -0.12f, -0.05f);
            boards.localScale = new Vector3(4.6f, 0.22f, 3.3f);
            Quiet(boards.GetComponent<Renderer>());
            float postTop = -0.23f;
            float postFoot = sandAtRoom - 0.5f;
            foreach (float x in new[] { -2.1f, 2.1f })
                foreach (float z in new[] { -1.5f, 1.4f })
                {
                    var post = Primitive(deck, PrimitiveType.Cube, "Post", p.Deck);
                    post.localPosition = new Vector3(x, (postTop + postFoot) * 0.5f, z);
                    post.localScale = new Vector3(0.2f, postTop - postFoot, 0.2f);
                    Quiet(post.GetComponent<Renderer>());
                }

            // Palms: two beside the room, groves along the shore both ways, a few behind.
            var palmObject = MeshObject(world, "Palms", Vector3.zero, Quaternion.identity, p.Palm);
            var palms = Undo.AddComponent<SimPalms>(palmObject);
            so = new SerializedObject(palms);
            SetRef(k_Tag, so, "m_Ground", sand);
            SetInt(k_Tag, so, "m_Seed", 11);
            SetGroves(so, new[]
            {
                Grove(new Vector3(-9f, 0f, 2f), new Vector2(2.5f, 3f), 2, new Vector2(7f, 9.5f), 0f, 18f),
                Grove(new Vector3(10f, 0f, -3f), new Vector2(2.5f, 3.5f), 2, new Vector2(8f, 10.5f), 15f, 14f),
                Grove(new Vector3(-60f, 0f, -3f), new Vector2(40f, 7f), 10, new Vector2(8f, 13f), 5f, 20f),
                Grove(new Vector3(-180f, 0f, 8f), new Vector2(60f, 8f), 12, new Vector2(8f, 13f), 0f, 18f),
                Grove(new Vector3(85f, 0f, -6f), new Vector2(30f, 7f), 8, new Vector2(8f, 12f), 10f, 18f),
                Grove(new Vector3(210f, 0f, 12f), new Vector2(70f, 9f), 12, new Vector2(8f, 13f), 0f, 18f),
                Grove(new Vector3(-30f, 0f, -40f), new Vector2(25f, 15f), 6, new Vector2(9f, 13f), 0f, 10f),
                Grove(new Vector3(40f, 0f, -45f), new Vector2(25f, 15f), 6, new Vector2(9f, 13f), 0f, 10f),
            });
            so.ApplyModifiedProperties();

            // Headlands at the horns of the bay, islands on the horizon.
            Butte(world, "Headland_Left", new Vector3(-480f, 0f, 190f), Yaw(-480f, 190f), p.Headland, 120f, new Vector2(1.6f, 0.8f), -6f,
                new[] { 35f, 58f }, new[] { 1f, 0.7f }, 0.25f, 0.12f, 0f, 0.3f, 71);
            Butte(world, "Headland_Right", new Vector3(520f, 0f, 230f), Yaw(520f, 230f), p.Headland, 140f, new Vector2(1.6f, 0.8f), -6f,
                new[] { 45f, 75f }, new[] { 1f, 0.68f }, 0.25f, 0.12f, 0f, 0.3f, 73);
            Butte(world, "Island_Left", new Vector3(-1100f, 0f, 3400f), Yaw(-1100f, 3400f), p.Headland, 420f, new Vector2(1.6f, 0.8f), -10f,
                new[] { 60f, 140f }, new[] { 1f, 0.55f }, 0.3f, 0.1f, 0f, 0.2f, 79);
            Butte(world, "Island_Right", new Vector3(1500f, 0f, 2800f), Yaw(1500f, 2800f), p.Headland, 260f, new Vector2(1.3f, 0.8f), -10f,
                new[] { 50f, 95f }, new[] { 1f, 0.6f }, 0.3f, 0.1f, 0f, 0.2f, 83);
            Butte(world, "Island_Far", new Vector3(250f, 0f, 4600f), Yaw(250f, 4600f), p.Headland, 700f, new Vector2(2f, 0.7f), -10f,
                new[] { 40f, 90f }, new[] { 1f, 0.5f }, 0.3f, 0.1f, 0f, 0.2f, 89);

            // Scrub on the dunes and green on the headlands.
            var scrubObject = MeshObject(world, "Scrub", Vector3.zero, Quaternion.identity, p.Foliage);
            var scrub = Undo.AddComponent<SimPuffs>(scrubObject);
            so = new SerializedObject(scrub);
            SetEnum(k_Tag, so, "m_Kind", (int)PuffKind.Trees);
            SetInt(k_Tag, so, "m_Seed", 13);
            SetRef(k_Tag, so, "m_Ground", sand);
            var scrubGreen = new Color(0.22f, 0.38f, 0.16f);
            SetClusters(so, new[]
            {
                Trees(new Vector3(0f, 0f, -70f), new Vector2(220f, 25f), 40, 1.6f, scrubGreen, true),
                Trees(new Vector3(-250f, 0f, -40f), new Vector2(120f, 30f), 25, 1.8f, scrubGreen, true),
                Trees(new Vector3(260f, 0f, -40f), new Vector2(120f, 30f), 25, 1.8f, scrubGreen, true),
                Trees(new Vector3(-480f, 58f, 190f), new Vector2(70f, 40f), 12, 6f, new Color(0.18f, 0.34f, 0.15f), false),
                Trees(new Vector3(520f, 75f, 230f), new Vector2(80f, 45f), 14, 6f, new Color(0.18f, 0.34f, 0.15f), false),
            });
            so.ApplyModifiedProperties();

            // The pier, its lamps an equaliser running out to sea.
            var pierAt = new Vector3(32f, k_SeaLevel, -12f);
            var pierObject = MeshObject(world, "Pier", pierAt, Quaternion.identity, p.PierWood);
            var glows = ParticleObject(pierObject.transform, "Lamp_Glows", Vector3.zero, Quaternion.identity, p.Glint, 128);
            var lampReflections = ParticleObject(pierObject.transform, "Lamp_Reflections", Vector3.zero, Quaternion.identity,
                p.GlintReflection, 128);
            var pier = Undo.AddComponent<SimPier>(pierObject);
            so = new SerializedObject(pier);
            SetRef(k_Tag, so, "m_Simulation", simulation);
            SetRef(k_Tag, so, "m_Glows", glows.GetComponent<ParticleSystem>());
            SetRef(k_Tag, so, "m_Reflections", lampReflections.GetComponent<ParticleSystem>());
            SetFloat(k_Tag, so, "m_Length", 260f);
            SetFloat(k_Tag, so, "m_Deck", 1.6f);
            SetFloat(k_Tag, so, "m_WaterFrom", shore + pierAt.x * pierAt.x / (2f * bay) - pierAt.z);
            so.ApplyModifiedProperties();

            // Fireworks out over the water, mirrored in it.
            var fireworks = ParticleObject(world, "Fireworks", new Vector3(40f, k_SeaLevel, 650f), Quaternion.identity, p.Glint, 1024);
            var fireworkReflections = ParticleObject(fireworks.transform, "Reflections", Vector3.zero, Quaternion.identity,
                p.GlintReflection, 1024);
            var bursts = Undo.AddComponent<SimFireworks>(fireworks);
            so = new SerializedObject(bursts);
            SetRef(k_Tag, so, "m_Simulation", simulation);
            SetRef(k_Tag, so, "m_Reflections", fireworkReflections.GetComponent<ParticleSystem>());
            so.ApplyModifiedProperties();

            var sim = Undo.AddComponent<SimWorld>(world.gameObject);
            so = new SerializedObject(sim);
            SetString(k_Tag, so, "m_Title", "Sunset Beach");
            SetColor(k_Tag, so, "m_Zenith", new Color(0.1f, 0.12f, 0.34f));
            SetColor(k_Tag, so, "m_Horizon", new Color(1f, 0.56f, 0.34f));
            SetColor(k_Tag, so, "m_Ground", new Color(0.25f, 0.2f, 0.25f));
            SetVector3(k_Tag, so, "m_SunDirection", new Vector3(0.22f, 0.07f, 1f));
            SetColor(k_Tag, so, "m_SunColour", new Color(1.5f, 0.8f, 0.42f));
            SetFloat(k_Tag, so, "m_SunDegrees", 1.4f);
            SetColor(k_Tag, so, "m_ShadowColour", new Color(0.3f, 0.22f, 0.42f));
            SetFloat(k_Tag, so, "m_Clouds", 0.45f);
            SetFloat(k_Tag, so, "m_CloudDrift", 0.002f);
            SetColor(k_Tag, so, "m_CloudColour", new Color(1f, 0.6f, 0.55f));
            SetFloat(k_Tag, so, "m_Stars", 0.4f);
            SetFloat(k_Tag, so, "m_FogDistance", 4000f);
            SetFloat(k_Tag, so, "m_FogAmount", 0.6f);
            SetFloat(k_Tag, so, "m_FogSunGlow", 0.9f);
            SetColor(k_Tag, so, "m_RoomAmbient", new Color(0.4f, 0.3f, 0.3f));
            SetColor(k_Tag, so, "m_RoomLight", new Color(1f, 0.62f, 0.38f));
            SetFloat(k_Tag, so, "m_RoomLightIntensity", 0.85f);
            SetFloat(k_Tag, so, "m_LampLevel", 0.85f);
            so.ApplyModifiedProperties();
            return sim;
        }

        /// <summary>
        /// Night on a lake ringed by snowy mountains: three aurora curtains across the sky whose rays
        /// are the spectrum, mirrored in the water; stars that twinkle with the treble; a low moon off
        /// to the left; snow drifting past the glass. The room's lamps burn full.
        /// </summary>
        static SimWorld BuildAurora(Transform outside, Simulation simulation, Palette p)
        {
            const float water = -1.2f;
            var world = NewChild(outside, "World_Aurora");

            var lakeObject = MeshObject(world, "Lake", new Vector3(0f, water, 0f), Quaternion.identity, p.Lake);
            var lake = Undo.AddComponent<SimSurface>(lakeObject);
            var so = new SerializedObject(lake);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.Flat);
            SetFloat(k_Tag, so, "m_InnerRadius", 1f);
            SetFloat(k_Tag, so, "m_OuterRadius", 4500f);
            SetInt(k_Tag, so, "m_Rings", 120);
            SetInt(k_Tag, so, "m_Segments", 256);
            so.ApplyModifiedProperties();

            var mountainObject = MeshObject(world, "Mountains", Vector3.zero, Quaternion.identity, p.Mountain);
            var mountains = Undo.AddComponent<SimSurface>(mountainObject);
            so = new SerializedObject(mountains);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.MountainRing);
            SetFloat(k_Tag, so, "m_InnerRadius", 2f);
            SetFloat(k_Tag, so, "m_OuterRadius", 8200f);
            SetInt(k_Tag, so, "m_Rings", 230);
            SetInt(k_Tag, so, "m_Segments", 420);
            SetInt(k_Tag, so, "m_Seed", 4);
            SetFloat(k_Tag, so, "m_Base", -6f);
            SetFloat(k_Tag, so, "m_Rim", 480f);
            SetFloat(k_Tag, so, "m_RimRadius", 1400f);
            SetFloat(k_Tag, so, "m_RimWidth", 3000f);
            SetFloat(k_Tag, so, "m_Terraces", 0f);
            so.ApplyModifiedProperties();

            Butte(world, "Island", Vector3.zero, 0f, p.Mountain, 2.3f, new Vector2(1f, 0.75f), -5f, new[] { -0.02f }, new[] { 1f },
                0.08f, 0.35f, 0f, 0.9f, 61, 36);

            Curtain(world, "Aurora_Front", water, p, 2600f, -55f, 55f, 260f, 1000f, new Color(0.15f, 1f, 0.45f),
                new Color(0.8f, 0.25f, 0.95f), 1.4f, new Vector2(0f, 1f), 1);
            Curtain(world, "Aurora_Left", water, p, 3400f, -135f, -45f, 380f, 1250f, new Color(0.1f, 0.95f, 0.75f),
                new Color(0.45f, 0.4f, 1f), 1.1f, new Vector2(0.1f, 0.9f), 2);
            Curtain(world, "Aurora_Right", water, p, 3100f, 40f, 130f, 330f, 1100f, new Color(0.4f, 1f, 0.3f),
                new Color(1f, 0.35f, 0.65f), 1.1f, new Vector2(0.1f, 0.9f), 3);

            Motes(world, "Snow", new Vector3(0f, water, 0f), simulation, p.Snow, MoteMotion.Fall, 1400, new Vector3(120f, 40f, 120f),
                0.9f, 1.5f, 0.09f, new Color(0.9f, 0.95f, 1.1f), 0.35f, 1f, outside);

            var sim = Undo.AddComponent<SimWorld>(world.gameObject);
            so = new SerializedObject(sim);
            SetString(k_Tag, so, "m_Title", "Aurora");
            SetColor(k_Tag, so, "m_Zenith", new Color(0.006f, 0.012f, 0.04f));
            SetColor(k_Tag, so, "m_Horizon", new Color(0.03f, 0.055f, 0.1f));
            SetColor(k_Tag, so, "m_Ground", new Color(0.01f, 0.015f, 0.03f));
            SetVector3(k_Tag, so, "m_SunDirection", new Vector3(-0.85f, 0.25f, 0.45f));
            SetColor(k_Tag, so, "m_SunColour", new Color(0.3f, 0.36f, 0.55f));
            SetFloat(k_Tag, so, "m_SunDegrees", 0f);
            SetColor(k_Tag, so, "m_ShadowColour", new Color(0.05f, 0.06f, 0.12f));
            SetFloat(k_Tag, so, "m_Clouds", 0f);
            SetFloat(k_Tag, so, "m_Stars", 1f);
            SetFloat(k_Tag, so, "m_MilkyWay", 0.6f);
            SetVector3(k_Tag, so, "m_MoonDirection", new Vector3(-0.85f, 0.25f, 0.45f));
            SetFloat(k_Tag, so, "m_MoonDegrees", 1.3f);
            SetFloat(k_Tag, so, "m_FogDistance", 9000f);
            SetFloat(k_Tag, so, "m_FogAmount", 0.5f);
            SetFloat(k_Tag, so, "m_FogSunGlow", 0f);
            SetColor(k_Tag, so, "m_RoomAmbient", new Color(0.06f, 0.07f, 0.12f));
            SetColor(k_Tag, so, "m_RoomLight", new Color(0.55f, 0.65f, 0.95f));
            SetFloat(k_Tag, so, "m_RoomLightIntensity", 0.3f);
            SetFloat(k_Tag, so, "m_LampLevel", 1f);
            so.ApplyModifiedProperties();
            return sim;
        }

        // ================================================================== outside pieces

        static SimButte Butte(Transform parent, string name, Vector3 position, float yaw, Material material, float radius, Vector2 aspect,
            float ground, float[] tops, float[] radii, float jagged, float taper, float peak, float talus, int seed, int sides = 56)
        {
            var go = MeshObject(parent, name, position, Quaternion.Euler(0f, yaw, 0f), material);
            var butte = Undo.AddComponent<SimButte>(go);
            var so = new SerializedObject(butte);
            SetFloat(k_Tag, so, "m_Radius", radius);
            SetVector2(so, "m_Aspect", aspect);
            SetFloat(k_Tag, so, "m_Ground", ground);
            SetFloats(so, "m_TierTops", tops);
            SetFloats(so, "m_TierRadii", radii);
            SetFloat(k_Tag, so, "m_Jagged", jagged);
            SetFloat(k_Tag, so, "m_Taper", taper);
            SetFloat(k_Tag, so, "m_Peak", peak);
            SetFloat(k_Tag, so, "m_Talus", talus);
            SetInt(k_Tag, so, "m_Sides", sides);
            SetInt(k_Tag, so, "m_Seed", seed);
            so.ApplyModifiedProperties();
            return butte;
        }

        static PuffCluster Trees(Vector3 centre, Vector2 spread, int count, float size, Color colour, bool onGround)
        {
            return new PuffCluster { Centre = centre, Spread = spread, Count = count, Size = size, Colour = colour, OnGround = onGround };
        }

        static PalmGrove Grove(Vector3 centre, Vector2 spread, int count, Vector2 height, float leanToward, float lean)
        {
            return new PalmGrove { Centre = centre, Spread = spread, Count = count, Height = height, LeanToward = leanToward, Lean = lean };
        }

        static LightPillar Pillar(Transform parent, string name, Vector3 foot, Simulation simulation, Palette p, float width, float height,
            Color colour, float listen, float footSize, Color footColour)
        {
            var go = MeshObject(parent, name, foot, Quaternion.identity, p.Pillar);
            var glow = Quad(go.transform, "Foot_Glow", new Vector3(0f, 1.3f, 0f), Vector2.one, p.Glow, Quaternion.Euler(90f, 0f, 0f));
            Quiet(glow.GetComponent<Renderer>());

            var pillar = Undo.AddComponent<LightPillar>(go);
            var so = new SerializedObject(pillar);
            SetRef(k_Tag, so, "m_Simulation", simulation);
            SetFloat(k_Tag, so, "m_Width", width);
            SetFloat(k_Tag, so, "m_Height", height);
            SetColor(k_Tag, so, "m_Colour", colour);
            SetFloat(k_Tag, so, "m_Listen", listen);
            SetRef(k_Tag, so, "m_FootGlow", glow.GetComponent<Renderer>());
            SetFloat(k_Tag, so, "m_FootSize", footSize);
            SetColor(k_Tag, so, "m_FootColour", footColour);
            so.ApplyModifiedProperties();
            return pillar;
        }

        static void Motes(Transform parent, string name, Vector3 position, Simulation simulation, Material material, MoteMotion motion,
            int count, Vector3 volume, float speed, float sway, float size, Color colour, float rest, float byTreble, Transform room)
        {
            var go = ParticleObject(parent, name, position, Quaternion.identity, material, count);
            var motes = Undo.AddComponent<SimMotes>(go);
            var so = new SerializedObject(motes);
            SetRef(k_Tag, so, "m_Simulation", simulation);
            SetEnum(k_Tag, so, "m_Motion", (int)motion);
            SetInt(k_Tag, so, "m_Count", count);
            SetVector3(k_Tag, so, "m_Volume", volume);
            SetFloat(k_Tag, so, "m_Speed", speed);
            SetFloat(k_Tag, so, "m_Sway", sway);
            SetFloat(k_Tag, so, "m_Size", size);
            SetColor(k_Tag, so, "m_Colour", colour);
            SetFloat(k_Tag, so, "m_Rest", rest);
            SetFloat(k_Tag, so, "m_ByTreble", byTreble);
            SetRef(k_Tag, so, "m_Room", room);
            so.ApplyModifiedProperties();
        }

        /// <summary>An aurora curtain and, flipped about the water level, its reflection in the lake.</summary>
        static void Curtain(Transform world, string name, float water, Palette p, float radius, float from, float to, float baseHeight,
            float height, Color low, Color high, float brightness, Vector2 bands, int seed)
        {
            foreach (bool reflection in new[] { false, true })
            {
                var go = MeshObject(world, reflection ? $"{name}_Reflection" : name,
                    reflection ? new Vector3(0f, water * 2f, 0f) : Vector3.zero, Quaternion.identity,
                    reflection ? p.AuroraReflection : p.Aurora);
                if (reflection)
                    go.transform.localScale = new Vector3(1f, -1f, 1f);

                var curtain = Undo.AddComponent<AuroraCurtain>(go);
                var so = new SerializedObject(curtain);
                SetFloat(k_Tag, so, "m_Radius", radius);
                SetFloat(k_Tag, so, "m_FromDegrees", from);
                SetFloat(k_Tag, so, "m_ToDegrees", to);
                SetFloat(k_Tag, so, "m_Base", baseHeight);
                SetFloat(k_Tag, so, "m_Height", height);
                SetInt(k_Tag, so, "m_Seed", seed);
                SetColor(k_Tag, so, "m_LowColour", low);
                SetColor(k_Tag, so, "m_HighColour", high);
                SetFloat(k_Tag, so, "m_Brightness", brightness);
                SetVector2(so, "m_Bands", bands);
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>Degrees about the vertical that turn a thing at (x, z) to face away from the room — its long side toward it.</summary>
        static float Yaw(float x, float z) => Mathf.Atan2(x, z) * Mathf.Rad2Deg;

        static GameObject MeshObject(Transform parent, string name, Vector3 position, Quaternion rotation, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Create outside piece");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Quiet(renderer);
            return go;
        }

        /// <summary>A particle system the runtime writes by hand, built quiet so the scene view does not fill with default particles.</summary>
        static GameObject ParticleObject(Transform parent, string name, Vector3 position, Quaternion rotation, Material material, int max)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            Undo.RegisterCreatedObjectUndo(go, "Create outside particles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

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
            Quiet(renderer);
            return go;
        }

        /// <summary>Outside lights itself: no shadows, no probes.</summary>
        static void Quiet(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ================================================================== room pieces

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

        /// <summary>A quad facing −Z of its parent unless turned.</summary>
        static Transform Quad(Transform parent, string name, Vector3 centre, Vector2 size, Material material, Quaternion? rotation = null)
        {
            var quad = Primitive(parent, PrimitiveType.Quad, name, material);
            quad.localPosition = centre;
            quad.localRotation = rotation ?? Quaternion.identity;
            quad.localScale = new Vector3(size.x, size.y, 1f);
            return quad;
        }

        // ================================================================== palette

        class Palette
        {
            // The room.
            public Material Window, Iron, Brass, GlassClear, GlassBlue, Water, Floor, Rug, RugEdge;
            public Material Cabinet, CabinetTop, Drawer, Paper, BookA, BookB, Pages, Wood, Bottle;
            public Material Teddy, TeddyLight, TeddyDark, Patch, Sheet, Blanket, Pillow, Cord, Bulb, Globe, TabletBody, TabletScreen;

            // Outside.
            public Material Sky, Canyon, Foliage, Voxel, Pool, Pillar, Glow, Mote;
            public Material CloudSea, Cumulus, Lake, Mountain, Aurora, AuroraReflection, Snow;
            public Material Sand, Ocean, Palm, PierWood, Deck, Headland, Glint, GlintReflection;
        }

        static string MaterialPath(string name) => $"{k_Art}/Materials/M_Glass_{name}.mat";

        static Palette MakePalette(GlassRoomTextures.Set t)
        {
            var window = LoadShader(k_Shaders, "GlassWindow.shader");
            var sky = LoadShader(k_Shaders, "SimSky.shader");
            var land = LoadShader(k_Shaders, "SimLand.shader");
            var cloudSea = LoadShader(k_Shaders, "SimCloudSea.shader");
            var water = LoadShader(k_Shaders, "SimWater.shader");
            var aurora = LoadShader(k_Shaders, "SimAurora.shader");
            var pillar = LoadShader(k_Shaders, "SimPillar.shader");
            var ocean = LoadShader(k_Shaders, "SimOcean.shader");
            var glint = LoadShader(k_Shaders, "SimGlint.shader");
            var beam = AssetDatabase.LoadAssetAtPath<Shader>(k_BeamShaderPath);
            if (window == null || sky == null || land == null || cloudSea == null || water == null || aurora == null || pillar == null ||
                ocean == null || glint == null || beam == null)
            {
                Debug.LogError($"[{k_Tag}] Missing a shader in '{k_Shaders}' or '{k_BeamShaderPath}'.");
                return null;
            }

            var p = new Palette
            {
                Window = ShaderMaterial("Window", window, _ => { }),
                Iron = Lit("Iron", new Color(0.15f, 0.16f, 0.18f), 0.55f),
                Brass = Lit("Brass", new Color(0.78f, 0.6f, 0.28f), 0.75f),
                GlassClear = EnsureMaterial(k_Tag, MaterialPath("Glass_Clear"), new Color(0.85f, 0.95f, 1f), Color.black, 0.2f, 0.95f),
                GlassBlue = EnsureMaterial(k_Tag, MaterialPath("Glass_Blue"), new Color(0.45f, 0.72f, 0.9f), Color.black, 0.5f, 0.95f),
                Water = EnsureMaterial(k_Tag, MaterialPath("Water"), new Color(0.6f, 0.85f, 1f), Color.black, 0.4f, 0.9f),
                Floor = Textured("Floorboards", t.Floorboards, new Vector2(CaveScreens.FrontWidth / 0.56f, CaveScreens.FloorDepth / 0.56f), 0.3f),
                Rug = Lit("Rug", new Color(0.95f, 0.91f, 0.85f), 0.05f),
                RugEdge = Lit("Rug_Edge", new Color(0.93f, 0.72f, 0.75f), 0.05f),

                Cabinet = Lit("Cabinet", new Color(0.33f, 0.32f, 0.29f), 0.35f),
                CabinetTop = Lit("Cabinet_Top", new Color(0.26f, 0.24f, 0.21f), 0.4f),
                Drawer = Lit("Drawer", new Color(0.4f, 0.38f, 0.34f), 0.35f),
                Paper = Lit("Paper", new Color(0.92f, 0.9f, 0.82f), 0.1f),
                BookA = Lit("Book_A", new Color(0.42f, 0.2f, 0.1f), 0.3f),
                BookB = Lit("Book_B", new Color(0.22f, 0.14f, 0.1f), 0.3f),
                Pages = Lit("Pages", new Color(0.93f, 0.9f, 0.8f), 0.1f),
                Wood = Lit("Wood", new Color(0.55f, 0.38f, 0.22f), 0.35f),
                Bottle = Lit("Bottle", new Color(0.08f, 0.06f, 0.05f), 0.9f),

                Teddy = Lit("Teddy", new Color(0.78f, 0.58f, 0.36f), 0.1f),
                TeddyLight = Lit("Teddy_Light", new Color(0.92f, 0.8f, 0.62f), 0.1f),
                TeddyDark = Lit("Teddy_Dark", new Color(0.08f, 0.05f, 0.04f), 0.7f),
                Patch = Lit("Patch", new Color(0.55f, 0.58f, 0.62f), 0.4f),
                Sheet = Lit("Sheet", new Color(0.95f, 0.95f, 0.97f), 0.2f),
                Blanket = Lit("Blanket", new Color(0.96f, 0.72f, 0.74f), 0.15f),
                Pillow = Lit("Pillow", new Color(0.98f, 0.98f, 1f), 0.2f),
                Cord = Lit("Cord", new Color(0.05f, 0.05f, 0.05f), 0.4f),
                Bulb = Lit("Bulb", new Color(1f, 0.9f, 0.75f), 0.4f, new Color(2f, 1.3f, 0.7f)),
                Globe = EnsureMaterial(k_Tag, MaterialPath("Globe"), new Color(0.95f, 0.97f, 1f), new Color(0.24f, 0.16f, 0.08f), 0.22f, 0.95f),
                TabletBody = Lit("Tablet_Body", new Color(0.12f, 0.12f, 0.14f), 0.6f),
                TabletScreen = Unlit("Tablet_Screen"),

                Sky = ShaderMaterial("Sky", sky, _ => { }),
                Canyon = ShaderMaterial("Canyon", land, m =>
                {
                    m.SetFloat("_Strata", 1f);
                    m.SetFloat("_StrataScale", 14f);
                    m.SetColor("_StrataA", new Color(0.66f, 0.3f, 0.17f));
                    m.SetColor("_StrataB", new Color(0.5f, 0.2f, 0.13f));
                    m.SetColor("_StrataC", new Color(0.85f, 0.62f, 0.45f));
                    m.SetColor("_StrataD", new Color(0.75f, 0.42f, 0.27f));
                    m.SetColor("_TopColor", new Color(0.8f, 0.58f, 0.42f));
                    m.SetFloat("_TopAmount", 1f);
                    m.SetFloat("_Detail", 0.25f);
                    m.SetFloat("_DetailScale", 30f);
                    m.SetColor("_GlowColor", new Color(1.3f, 0.94f, 0.5f));
                    m.SetFloat("_StrataGlow", 1.4f);
                    m.SetFloat("_StrataBands", 1.6f);
                    m.SetFloat("_Rings", 1.2f);
                    m.SetFloat("_CelSoft", 0.12f);
                    m.SetFloat("_Rim", 0.3f);
                    m.SetFloat("_BandGlow", 0f);
                }),
                Foliage = ShaderMaterial("Foliage", land, m =>
                {
                    m.SetFloat("_Strata", 0f);
                    m.SetFloat("_CelSoft", 0.18f);
                    m.SetFloat("_Rim", 0.2f);
                    m.SetFloat("_BandGlow", 0f);
                    m.SetFloat("_Detail", 0.2f);
                    m.SetFloat("_DetailScale", 6f);
                    m.SetFloat("_Rings", 0.6f);
                }),
                Voxel = ShaderMaterial("Voxel", land, m =>
                {
                    m.SetFloat("_Strata", 1f);
                    m.SetFloat("_StrataScale", 14f);
                    m.SetColor("_StrataA", new Color(0.66f, 0.3f, 0.17f));
                    m.SetColor("_StrataB", new Color(0.5f, 0.2f, 0.13f));
                    m.SetColor("_StrataC", new Color(0.85f, 0.62f, 0.45f));
                    m.SetColor("_StrataD", new Color(0.75f, 0.42f, 0.27f));
                    m.SetColor("_TopColor", new Color(0.8f, 0.58f, 0.42f));
                    m.SetFloat("_TopAmount", 1f);
                    m.SetFloat("_Detail", 0f);
                    m.SetColor("_EmissionColor", new Color(0.9f, 0.85f, 1.2f));
                    m.SetFloat("_StrataGlow", 0f);
                    m.SetFloat("_Rings", 0f);
                    m.SetFloat("_CelSoft", 0.1f);
                    m.SetFloat("_Rim", 0.2f);
                    m.SetFloat("_BandGlow", 0f);
                }),
                Pool = ShaderMaterial("Pool", water, m =>
                {
                    m.SetColor("_DeepColor", new Color(0.45f, 0.78f, 0.76f));
                    m.SetFloat("_Reflectivity", 0.45f);
                    m.SetFloat("_Ripple", 0.25f);
                    m.SetFloat("_RippleScale", 5f);
                    m.SetFloat("_Glint", 1f);
                }),
                Pillar = ShaderMaterial("Pillar", pillar, _ => { }),
                Glow = ShaderMaterial("Glow", beam, m =>
                {
                    m.SetFloat("_Softness", 2.5f);
                    m.SetFloat("_Core", 0.4f);
                    m.SetFloat("_Radial", 1f);
                    m.EnableKeyword("_RADIAL");
                }),
                Mote = ShaderMaterial("Mote", beam, m =>
                {
                    m.SetFloat("_Softness", 2.2f);
                    m.SetFloat("_Core", 0.35f);
                    m.SetFloat("_Radial", 1f);
                    m.EnableKeyword("_RADIAL");
                }),

                CloudSea = ShaderMaterial("Cloud_Sea", cloudSea, _ => { }),
                Cumulus = ShaderMaterial("Cumulus", land, m =>
                {
                    m.SetFloat("_Strata", 0f);
                    m.SetFloat("_CelSoft", 0.35f);
                    m.SetFloat("_Rim", 0.8f);
                    m.SetFloat("_Detail", 0.15f);
                    m.SetFloat("_DetailScale", 60f);
                    m.SetColor("_GlowColor", new Color(1.2f, 0.94f, 0.72f));
                    m.SetFloat("_BandGlow", 0.9f);
                    m.SetFloat("_Rings", 0f);
                }),
                Lake = ShaderMaterial("Lake", water, m =>
                {
                    m.SetColor("_DeepColor", new Color(0.01f, 0.02f, 0.04f));
                    m.SetFloat("_Reflectivity", 0.85f);
                    m.SetFloat("_Ripple", 0.35f);
                    m.SetFloat("_RippleScale", 7f);
                    m.SetFloat("_Glint", 1.5f);
                }),
                Mountain = ShaderMaterial("Mountain", land, m =>
                {
                    m.SetFloat("_Strata", 0f);
                    m.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.24f));
                    m.SetFloat("_Snow", 1f);
                    m.SetFloat("_SnowLine", 230f);
                    m.SetColor("_SnowColor", new Color(0.82f, 0.88f, 1f));
                    m.SetFloat("_Detail", 0.35f);
                    m.SetFloat("_DetailScale", 120f);
                    m.SetFloat("_CelSoft", 0.15f);
                    m.SetFloat("_Rim", 0.4f);
                    m.SetFloat("_BandGlow", 0f);
                    m.SetFloat("_Rings", 0f);
                }),
                Aurora = ShaderMaterial("Aurora", aurora, m =>
                {
                    m.SetFloat("_Reflect", 0f);
                    m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                    m.SetFloat("_StencilComp", (float)CompareFunction.Always);
                }),
                AuroraReflection = ShaderMaterial("Aurora_Reflection", aurora, m =>
                {
                    m.SetFloat("_Reflect", 1f);
                    m.SetFloat("_ZTest", (float)CompareFunction.Always);
                    m.SetFloat("_StencilComp", (float)CompareFunction.Equal);
                    m.SetFloat("_StencilRef", 1f);
                }),
                Snow = ShaderMaterial("Snow", beam, m =>
                {
                    m.SetFloat("_Softness", 2f);
                    m.SetFloat("_Core", 0.5f);
                    m.SetFloat("_Radial", 1f);
                    m.EnableKeyword("_RADIAL");
                }),

                Sand = ShaderMaterial("Sand", land, m =>
                {
                    m.SetFloat("_Strata", 0f);
                    m.SetColor("_BaseColor", new Color(0.93f, 0.8f, 0.6f));
                    m.SetFloat("_Detail", 0.22f);
                    m.SetFloat("_DetailScale", 3f);
                    m.SetFloat("_WetDarken", 0.35f);
                    m.SetFloat("_WetLevel", k_SeaLevel);
                    m.SetFloat("_WetBand", 0.45f);
                    m.SetFloat("_CelSoft", 0.25f);
                    m.SetFloat("_Rim", 0.15f);
                    m.SetFloat("_BandGlow", 0f);
                    m.SetFloat("_Rings", 0f);
                }),
                Ocean = ShaderMaterial("Ocean", ocean, _ => { }),
                Palm = ShaderMaterial("Palm", land, m =>
                {
                    m.SetFloat("_Strata", 0f);
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Detail", 0.1f);
                    m.SetFloat("_DetailScale", 2f);
                    m.SetFloat("_CelSoft", 0.2f);
                    m.SetFloat("_Rim", 0.35f);
                    m.SetFloat("_Sway", 0.6f);
                    m.SetVector("_SwayWind", new Vector4(0f, 0f, -1f, 0f));
                    m.SetFloat("_BandGlow", 0f);
                    m.SetFloat("_Rings", 0f);
                }),
                PierWood = ShaderMaterial("Pier_Wood", land, m =>
                {
                    m.SetFloat("_Strata", 0f);
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Detail", 0.25f);
                    m.SetFloat("_DetailScale", 1.5f);
                    m.SetFloat("_CelSoft", 0.12f);
                    m.SetFloat("_Rim", 0.25f);
                    m.SetFloat("_BandGlow", 0f);
                    m.SetFloat("_Rings", 0f);
                }),
                Deck = ShaderMaterial("Deck", land, m =>
                {
                    m.SetFloat("_Strata", 0f);
                    m.SetColor("_BaseColor", new Color(0.5f, 0.36f, 0.24f));
                    m.SetFloat("_Detail", 0.3f);
                    m.SetFloat("_DetailScale", 0.8f);
                    m.SetFloat("_CelSoft", 0.12f);
                    m.SetFloat("_Rim", 0.2f);
                    m.SetFloat("_BandGlow", 0f);
                    m.SetFloat("_Rings", 0f);
                }),
                Headland = ShaderMaterial("Headland", land, m =>
                {
                    m.SetFloat("_Strata", 1f);
                    m.SetFloat("_StrataScale", 9f);
                    m.SetColor("_StrataA", new Color(0.5f, 0.45f, 0.4f));
                    m.SetColor("_StrataB", new Color(0.4f, 0.36f, 0.33f));
                    m.SetColor("_StrataC", new Color(0.62f, 0.56f, 0.48f));
                    m.SetColor("_StrataD", new Color(0.46f, 0.4f, 0.36f));
                    m.SetColor("_TopColor", new Color(0.32f, 0.5f, 0.24f));
                    m.SetFloat("_TopAmount", 1f);
                    m.SetFloat("_Detail", 0.25f);
                    m.SetFloat("_DetailScale", 25f);
                    m.SetFloat("_StrataGlow", 0f);
                    m.SetFloat("_CelSoft", 0.14f);
                    m.SetFloat("_Rim", 0.35f);
                    m.SetFloat("_BandGlow", 0f);
                    m.SetFloat("_Rings", 0f);
                }),
                Glint = ShaderMaterial("Glint", glint, m =>
                {
                    m.SetFloat("_Softness", 2.2f);
                    m.SetFloat("_Core", 0.45f);
                    m.SetFloat("_Waver", 0f);
                    m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                    m.SetFloat("_StencilComp", (float)CompareFunction.Always);
                }),
                GlintReflection = ShaderMaterial("Glint_Reflection", glint, m =>
                {
                    m.SetFloat("_Softness", 1.6f);
                    m.SetFloat("_Core", 0.2f);
                    m.SetFloat("_Waver", 1f);
                    m.SetFloat("_ZTest", (float)CompareFunction.Always);
                    m.SetFloat("_StencilComp", (float)CompareFunction.Equal);
                    m.SetFloat("_StencilRef", 1f);
                }),
            };

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

        static Shader LoadShader(string folder, string file) => AssetDatabase.LoadAssetAtPath<Shader>($"{folder}/{file}");

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

        /// <summary>URP Unlit, for the tablet's screen: a picture of light, not a lit surface. Its texture is the front wall's view, set at run time.</summary>
        static Material Unlit(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;

            return ShaderMaterial(name, shader, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            });
        }

        /// <summary>
        /// A material on one of the room's own shaders, re-authored on every build: the look lives in
        /// the shader's defaults and the numbers here, and a material left over from an earlier build
        /// is a bug, not a tweak.
        /// </summary>
        static Material ShaderMaterial(string name, Shader shader, System.Action<Material> configure)
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

            configure(material);

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);

            return material;
        }

        // ================================================================== serialized fields

        static void SetVector2(SerializedObject so, string path, Vector2 value)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
            else
                property.vector2Value = value;
        }

        static void SetVector4(SerializedObject so, string path, Vector4 value)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
            else
                property.vector4Value = value;
        }

        static void SetFloats(SerializedObject so, string path, float[] values)
        {
            var property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        static void SetClusters(SerializedObject so, PuffCluster[] clusters)
        {
            var property = so.FindProperty("m_Clusters");
            property.arraySize = clusters.Length;
            for (int i = 0; i < clusters.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var cluster = clusters[i];
                element.FindPropertyRelative("Centre").vector3Value = cluster.Centre;
                element.FindPropertyRelative("Spread").vector2Value = cluster.Spread;
                element.FindPropertyRelative("Count").intValue = cluster.Count;
                element.FindPropertyRelative("Size").floatValue = cluster.Size;
                element.FindPropertyRelative("Colour").colorValue = cluster.Colour;
                element.FindPropertyRelative("Band").floatValue = cluster.Band;
                element.FindPropertyRelative("Glow").floatValue = cluster.Glow;
                element.FindPropertyRelative("OnGround").boolValue = cluster.OnGround;
            }
        }

        static void SetGroves(SerializedObject so, PalmGrove[] groves)
        {
            var property = so.FindProperty("m_Groves");
            property.arraySize = groves.Length;
            for (int i = 0; i < groves.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var grove = groves[i];
                element.FindPropertyRelative("Centre").vector3Value = grove.Centre;
                element.FindPropertyRelative("Spread").vector2Value = grove.Spread;
                element.FindPropertyRelative("Count").intValue = grove.Count;
                element.FindPropertyRelative("Height").vector2Value = grove.Height;
                element.FindPropertyRelative("LeanToward").floatValue = grove.LeanToward;
                element.FindPropertyRelative("Lean").floatValue = grove.Lean;
            }
        }

        static void SetScreen(SerializedObject so, string path, CaveScreen screen)
        {
            so.FindProperty($"{path}.Name").stringValue = screen.Name;
            so.FindProperty($"{path}.Centre").vector3Value = screen.Centre;
            so.FindProperty($"{path}.Right").vector3Value = screen.Right;
            so.FindProperty($"{path}.Up").vector3Value = screen.Up;
            so.FindProperty($"{path}.Width").floatValue = screen.Width;
            so.FindProperty($"{path}.Height").floatValue = screen.Height;
            so.FindProperty($"{path}.Projected").boolValue = screen.Projected;
        }
    }
}
