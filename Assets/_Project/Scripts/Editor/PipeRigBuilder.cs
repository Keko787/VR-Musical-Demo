using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRShootingGallery.Pipes;
using VRShootingGallery.Visualizer;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the pipe rig: a self-playing machine in the CAVE whose pipes throw balls in arcs onto
    /// instruments all round the room, each landing on its note. A low bank of twelve pipes stands
    /// just in front of the people, in the middle; a bass frame of seven strings stands off the left
    /// wall, a shelf of fifteen mallet bars runs along the front wall, a frame of eight strings stands
    /// off the right wall, a guitar lies on a stand in the middle past the bank, and a glockenspiel
    /// stands beside the bank on the right. A kick and three toms stand on the left, a snare and a
    /// crash on the right; beside the people, a floor tom and a gong on the left and a hi-hat and a
    /// row of tubular chimes on the right. The middle of the back is left to the people.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The layout follows the design notes. Pitch runs low to high round the room — the bass from
    /// the back of the left wall forward, the bars left to right, the strings from the front of the
    /// right wall back and on into the chimes — so it matches the stereo pan. Nothing stands in a
    /// corner, and every ball flies away from the people toward a wall or the front. After a strike
    /// a ball drops into a trough at a wall's foot, or onto the floor by a drum, and rolls home to
    /// the bank along lanes drawn on the floor.
    /// </para>
    /// <para>
    /// Everything lives under <c>CAVE_User_Room/Pipe_Rig</c> and is registered with Undo. A rebuild
    /// clears the rig's children; the <see cref="PipeRig"/> on the root is reused, so its Inspector
    /// settings survive. Every path is solved and checked against the room as the last step, and
    /// anything that would leave the screens or cross the people is logged as an error.
    /// </para>
    /// </remarks>
    public static class PipeRigBuilder
    {
        const string k_Tag = "PipeRig";
        const string k_CreateMenu = "VR-Music-Demo/Pipe Rig/Create Pipe Rig Scene";
        const string k_BuildMenu = "VR-Music-Demo/Pipe Rig/Rebuild Pipe Rig";
        const string k_ScenePath = "Assets/_Project/Scenes/Demos/PipeRig MiddleVR.unity";

        const string k_Root = "Pipe_Rig";
        const string k_Art = "Assets/_Project/Art/PipeRig";
        const string k_BeamShaderPath = "Assets/_Project/Art/Shaders/LaserBeam.shader";
        const string k_ShadowShaderPath = "Assets/_Project/Art/Shaders/PipeRigShadow.shader";

        /// <summary>The menu's and the strip's materials, M_Rig_Menu_Panel and the rest, beside the rig's own.</summary>
        const string k_MenuMaterialPrefix = k_Art + "/Materials/M_Rig_";

        /// <summary>The menu stands out past the mallet shelf, whose longest bar reaches 0.5 m off the front wall.</summary>
        const float k_MenuInset = 0.52f;

        const float k_Radius = 0.035f;
        const float k_Gravity = 4f;

        // The bank, just in front of where the people stand.
        const float k_BankZ = 0.12f;
        const float k_CabinetTop = 0.16f;
        const float k_CabinetWidth = 1.4f;
        const float k_PipeRadius = 0.045f;
        const float k_PipeSpacing = 0.11f;
        const float k_Nozzle = 0.07f;
        static readonly float[] k_PipeTops = { 0.44f, 0.48f, 0.52f, 0.56f, 0.6f, 0.62f, 0.62f, 0.6f, 0.56f, 0.52f, 0.48f, 0.44f };

        // Which instrument group each pipe feeds, for its colour: the left of the bank feeds the left of the room.
        static readonly RigGroup[] k_PipeGroups =
        {
            RigGroup.Bass, RigGroup.Drums, RigGroup.Drums, RigGroup.Guitar, RigGroup.Mallets, RigGroup.Mallets, RigGroup.Mallets,
            RigGroup.Glockenspiel, RigGroup.Drums, RigGroup.Drums, RigGroup.Chimes, RigGroup.Strings,
        };

        const int k_BassPipe = 0, k_LeftDrumPipe = 1, k_BackDrumPipe = 2, k_GuitarPipe = 3, k_MalletLow = 4, k_MalletMid = 5, k_MalletHigh = 6,
            k_GlockPipe = 7, k_RightDrumPipe = 8, k_HatPipe = 9, k_ChimePipe = 10, k_StringPipe = 11;

        // The instruments.
        static readonly int[] k_BassNotes = { 36, 38, 40, 41, 43, 45, 47 };                            // C2–B2
        static readonly int[] k_GuitarNotes = { 40, 45, 50, 55, 59, 64 };                              // E2 A2 D3 G3 B3 E4, open
        static readonly int[] k_MalletNotes = { 48, 50, 52, 53, 55, 57, 59, 60, 62, 64, 65, 67, 69, 71, 72 }; // C3–C5
        static readonly int[] k_StringNotes = { 74, 76, 77, 79, 81, 83, 84, 86 };                     // D5–D6
        static readonly int[] k_ChimeNotes = { 72, 74, 76, 77, 79, 81, 83, 84 };                      // C5–C6
        static readonly int[] k_GlockNotes = { 84, 86, 88, 89, 91, 93, 95, 96 };                      // C6–C7

        const float k_SideFrameX = 1.48f;    // 0.3 m off the side walls
        const float k_TroughX = 1.7f;
        const float k_TroughBackZ = -0.85f;  // the side troughs run on past the people, for what stands beside them
        const float k_FrontTroughZ = 1.04f;
        const float k_BarBackZ = 0.92f;
        const float k_BarBackY = 1.33f;
        const float k_BarTilt = 10f;

        // The guitar, lying past the bank: the middle of its soundboard, and along it from there.
        static readonly Vector3 k_GuitarAt = new(0f, 0.72f, 0.5f);
        const float k_GuitarTilt = 25f;
        const float k_BridgeX = -0.36f, k_NutX = 0.29f, k_StrikeX = -0.25f;
        const float k_StringHeight = 0.02f;

        // The glockenspiel, beside the bank on the right: its bars' outer ends, front to back.
        const float k_GlockOuterX = 1.12f, k_GlockOuterY = 0.68f, k_GlockFrontZ = 0.41f, k_GlockStep = 0.05f;
        const float k_GlockTilt = 15f;

        // The chimes, beside the people on the right: the row of tubes, front to back, and the height of their caps.
        const float k_ChimeX = 1.4f, k_ChimeFrontZ = -0.3f, k_ChimeStep = 0.07f, k_ChimeCap = 1.6f, k_ChimeRail = 1.86f;

        // Lanes home: along the side troughs to where the lanes cross the floor in front of the bank, and into its sides.
        const float k_LaneZ = 0.07f;
        const float k_KitLaneX = 0.78f;      // the toms' and the snare's way down beside the bank

        static float R => k_Radius;
        static Vector3 LeftIntake => new(-0.74f, R, k_LaneZ);
        static Vector3 RightIntake => new(0.74f, R, k_LaneZ);
        static Vector3 BackIntake => new(0f, R, k_BankZ + 0.14f);

        static float PipeX(int pipe) => (pipe - (k_PipeTops.Length - 1) * 0.5f) * k_PipeSpacing;

        static readonly Color k_Bass = new(1f, 0.55f, 0.15f);
        static readonly Color k_Mallets = new(0.15f, 0.95f, 0.8f);
        static readonly Color k_Strings = new(1f, 0.25f, 0.75f);
        static readonly Color k_Drums = new(0.6f, 0.75f, 1f);
        static readonly Color k_Guitar = new(0.45f, 1f, 0.3f);
        static readonly Color k_Chimes = new(1f, 0.82f, 0.3f);
        static readonly Color k_Glock = new(0.7f, 0.5f, 1f);
        static readonly Color k_Gong = new(1f, 0.62f, 0.3f);
        static readonly Color k_SignColour = new(0.4f, 0.42f, 0.46f);

        static Color GroupColour(RigGroup group) => group switch
        {
            RigGroup.Bass => k_Bass,
            RigGroup.Mallets => k_Mallets,
            RigGroup.Strings => k_Strings,
            RigGroup.Guitar => k_Guitar,
            RigGroup.Chimes => k_Chimes,
            RigGroup.Glockenspiel => k_Glock,
            _ => k_Drums,
        };

        // ================================================================== menus

        [MenuItem(k_CreateMenu, false, 720)]
        static void CreatePipeRigScene()
        {
            CreateScene(k_Tag, k_ScenePath, "pipe rig", Build,
                "Enter play mode: point and click an instrument to play it (R records a loop, B a backing beat), or M for the " +
                "modes and Visualize to have the machine play along with a track from Assets/_Project/Audio/Music.");
        }

        [MenuItem(k_BuildMenu, false, 730)]
        static void Rebuild() => RebuildInOpenScene(Build);

        /// <summary>
        /// For a build machine, or to generate the scene without opening the editor:
        /// <c>Unity -batchmode -projectPath . -executeMethod VRShootingGallery.EditorTools.PipeRigBuilder.CreateFromCommandLine -quit</c>.
        /// Batch mode answers every dialog with Cancel, so an existing scene is rebuilt in place and saved instead.
        /// </summary>
        public static void CreateFromCommandLine()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_ScenePath) == null)
            {
                CreatePipeRigScene();
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
            var room = RequireRoom(scene, k_Tag, "pipe rig");
            if (room == null)
                return false;

            var beam = AssetDatabase.LoadAssetAtPath<Shader>(k_BeamShaderPath);
            var shadow = AssetDatabase.LoadAssetAtPath<Shader>(k_ShadowShaderPath);
            if (beam == null || shadow == null)
            {
                Debug.LogError($"[{k_Tag}] Missing '{k_BeamShaderPath}' or '{k_ShadowShaderPath}', the glows and the balls' shadows.");
                return false;
            }

            Undo.SetCurrentGroupName("Build Pipe Rig");
            int group = Undo.GetCurrentGroup();

            var p = MakePalette(beam, shadow);
            if (p == null)
                return false;

            var root = FindOrCreateInstrument(room, k_Root, Vector3.zero, k_Tag);
            Clear(root);

            var rig = root.GetComponent<PipeRig>();
            if (rig == null)
                rig = Undo.AddComponent<PipeRig>(root.gameObject);

            var deck = BuildAudio(root);
            Shell(root, p);
            Troughs(root, p);

            var (pipes, lamps, record) = BuildBank(root, p);
            var targets = new List<RigTarget>();
            BuildBass(root, p, targets);
            BuildGuitar(root, p, targets);
            BuildMallets(root, p, targets);
            BuildGlockenspiel(root, p, targets);
            BuildStrings(root, p, targets);
            BuildChimes(root, p, targets);
            BuildDrums(root, p, targets);

            var balls = BuildBalls(root, p);
            var (ray, cursor) = BuildPointer(root, p);

            var menu = WallMenuBuilder.BuildMenu<PipeRigMenu>(k_Tag, root, k_MenuMaterialPrefix, WallMenuBuilder.MenuHeight, k_MenuInset);
            var so = new SerializedObject(menu);
            SetRef(k_Tag, so, "m_Rig", rig);
            so.ApplyModifiedProperties();
            var indicator = WallMenuBuilder.BuildTrackIndicator(k_Tag, root, k_MenuMaterialPrefix, deck, menu);
            var sign = BuildSign(root);

            so = new SerializedObject(rig);
            SetArray(k_Tag, so, "m_Pipes", pipes);
            SetArray(k_Tag, so, "m_Targets", targets);
            SetRef(k_Tag, so, "m_Balls", balls);
            SetRef(k_Tag, so, "m_Deck", deck);
            SetRef(k_Tag, so, "m_Menu", menu);
            SetRef(k_Tag, so, "m_Indicator", indicator);
            SetRef(k_Tag, so, "m_Sign", sign);
            SetRef(k_Tag, so, "m_Ray", ray);
            SetRef(k_Tag, so, "m_Cursor", cursor);
            SetArray(k_Tag, so, "m_BeatLamps", lamps);
            SetRef(k_Tag, so, "m_RecordLamp", record);
            SetFloat(k_Tag, so, "m_Gravity", k_Gravity);
            SetFloat(k_Tag, so, "m_BallRadius", k_Radius);
            so.ApplyModifiedProperties();

            Atmosphere(scene, root);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;

            int problems = Check(rig, root, pipes, targets);
            Debug.Log($"[{k_Tag}] Built the pipe rig: {pipes.Count} pipes, {targets.Count} things to strike, " +
                      $"{deck.Playlist?.Length ?? 0} track(s) in the playlist, " +
                      (problems == 0 ? "every path inside the room." : $"{problems} path problem(s) — see the errors above."), root);
            return true;
        }

        /// <summary>Solves and checks every path the way the rig will at load, so a bad layout is an error here and not in the room.</summary>
        static int Check(PipeRig rig, Transform root, List<RigPipe> pipes, List<RigTarget> targets)
        {
            var problems = new List<string>();
            var room = new SerializedObject(rig).FindProperty("m_Room");
            var volume = RigVolume.Default;
            if (room != null)
            {
                volume.Min = room.FindPropertyRelative("Min").vector3Value;
                volume.Max = room.FindPropertyRelative("Max").vector3Value;
                volume.KeepOutCentre = room.FindPropertyRelative("KeepOutCentre").vector2Value;
                volume.KeepOutRadius = room.FindPropertyRelative("KeepOutRadius").floatValue;
                volume.KeepOutHeight = room.FindPropertyRelative("KeepOutHeight").floatValue;
            }

            var paths = RigPaths.Build(root, pipes.ToArray(), targets.ToArray(), volume, k_Gravity, k_Radius, problems);
            foreach (var problem in problems)
                Debug.LogError($"[{k_Tag}] {problem}", root);

            Debug.Log($"[{k_Tag}] Longest flight {paths.LongestFlight:0.00} s: the machine reads that far ahead of the music.", root);
            return problems.Count;
        }

        // ================================================================== audio

        static MusicDeck BuildAudio(Transform root)
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
            return deck;
        }

        // ================================================================== the room

        /// <summary>
        /// Near-black walls over the template's room, so the machine and its glows stand out, and a
        /// dark slate floor — light enough for the balls' shadows to read on it — with a faint grid.
        /// </summary>
        static void Shell(Transform root, Palette p)
        {
            var shell = NewChild(root, "Shell");
            foreach (var screen in new[] { CaveScreens.Front, CaveScreens.Left, CaveScreens.Right })
                Quad(shell, $"Wall_{screen.Name}", screen.Point(0.5f, 0.5f, 0.004f), new Vector2(screen.Width, screen.Height), p.Wall,
                    screen.FacingRotation);

            var floor = CaveScreens.Floor;
            Quad(shell, "Floor", floor.Point(0.5f, 0.5f, 0.002f), new Vector2(floor.Width, floor.Height), p.Floor, floor.FacingRotation);

            var grid = NewChild(shell, "Grid");
            for (float x = -1.5f; x <= 1.51f; x += 0.5f)
                Strip(grid, $"Grid_X_{x:0.0}", new Vector3(x, 0f, -CaveScreens.FrontZ), new Vector3(x, 0f, CaveScreens.FrontZ), 0.012f, 0.003f, p.Grid);
            for (float z = -1f; z <= 1.01f; z += 0.5f)
                Strip(grid, $"Grid_Z_{z:0.0}", new Vector3(-CaveScreens.HalfWidth, 0f, z), new Vector3(CaveScreens.HalfWidth, 0f, z), 0.012f, 0.003f, p.Grid);
        }

        /// <summary>
        /// The way home: a trough at the foot of each wall, the side ones running on past the people;
        /// a lane across the floor from each side trough into the bank's side, and one down each side
        /// of the bank for the drums by it; and a lane from the front trough's middle into the bank's
        /// back. The rolling balls are surface content too, seen by everyone on the floor screen.
        /// </summary>
        static void Troughs(Transform root, Palette p)
        {
            var troughs = NewChild(root, "Troughs");
            foreach (float side in new[] { -1f, 1f })
            {
                string name = side < 0f ? "Left" : "Right";
                Trough(troughs, $"Trough_{name}", new Vector3(side * k_TroughX, 0f, k_TroughBackZ), new Vector3(side * k_TroughX, 0f, 0.88f), p);
                Strip(troughs, $"Lane_{name}", new Vector3(side * k_TroughX, 0f, k_LaneZ), new Vector3(side * 0.74f, 0f, k_LaneZ), 0.035f, 0.006f, p.Lane);
                Strip(troughs, $"Lane_{name}_Kit", new Vector3(side * k_KitLaneX, 0f, 0.4f), new Vector3(side * k_KitLaneX, 0f, k_LaneZ), 0.035f, 0.006f,
                    p.Lane);
            }

            Trough(troughs, "Trough_Front", new Vector3(-1.22f, 0f, k_FrontTroughZ), new Vector3(1.22f, 0f, k_FrontTroughZ), p);
            Strip(troughs, "Lane_Front", new Vector3(0f, 0f, k_FrontTroughZ), new Vector3(0f, 0f, BackIntake.z), 0.035f, 0.006f, p.Lane);
        }

        /// <summary>A shallow channel on the floor from <paramref name="from"/> to <paramref name="to"/>, with a glowing line down it.</summary>
        static void Trough(Transform parent, string name, Vector3 from, Vector3 to, Palette p)
        {
            var trough = NewChild(parent, name);
            var along = (to - from).normalized;
            var across = Vector3.Cross(Vector3.up, along);
            float length = Vector3.Distance(from, to);
            var middle = (from + to) * 0.5f;
            var rotation = Quaternion.LookRotation(along, Vector3.up);

            Box(trough, "Bed", middle + Vector3.up * 0.004f, new Vector3(0.1f, 0.008f, length), p.Trough, rotation);
            Box(trough, "Rail_A", middle + across * 0.055f + Vector3.up * 0.025f, new Vector3(0.012f, 0.05f, length), p.Trough, rotation);
            Box(trough, "Rail_B", middle - across * 0.055f + Vector3.up * 0.025f, new Vector3(0.012f, 0.05f, length), p.Trough, rotation);
            Strip(trough, "Glow", from, to, 0.03f, 0.01f, p.Lane);
        }

        // ================================================================== the pipe bank

        /// <summary>
        /// A low cabinet with twelve pipes in an organ's rise and fall, each banded in the colour of what
        /// it feeds and capped with a swivelling nozzle; the beat lamps and the record lamp along its
        /// front; and an intake either side and behind, where balls roll home.
        /// </summary>
        static (List<RigPipe>, List<Renderer>, Renderer) BuildBank(Transform root, Palette p)
        {
            var bank = NewChild(root, "Pipe_Bank");
            Box(bank, "Cabinet", new Vector3(0f, k_CabinetTop * 0.5f, k_BankZ), new Vector3(k_CabinetWidth, k_CabinetTop, 0.2f), p.Cabinet);
            Box(bank, "Cabinet_Top", new Vector3(0f, k_CabinetTop + 0.006f, k_BankZ), new Vector3(k_CabinetWidth + 0.04f, 0.012f, 0.23f), p.Nozzle);

            // Each intake stands a little proud of its face of the cabinet.
            foreach (var intake in new[] { LeftIntake, RightIntake })
                Box(bank, "Intake", new Vector3(Mathf.Sign(intake.x) * (k_CabinetWidth * 0.5f + 0.02f), 0.035f, intake.z), new Vector3(0.05f, 0.07f, 0.1f),
                    p.Intake);
            Box(bank, "Intake", new Vector3(0f, 0.035f, k_BankZ + 0.12f), new Vector3(0.1f, 0.07f, 0.05f), p.Intake);

            var pipes = new List<RigPipe>();
            for (int i = 0; i < k_PipeTops.Length; i++)
            {
                float x = PipeX(i);
                float top = k_PipeTops[i];
                var holder = NewChild(bank, $"Pipe_{i}_{k_PipeGroups[i]}");
                holder.localPosition = new Vector3(x, 0f, k_BankZ);

                float tube = top - k_CabinetTop;
                Cyl(holder, "Tube", new Vector3(0f, k_CabinetTop + tube * 0.5f, 0f), k_PipeRadius, tube, p.Pipe);
                var collar = Cyl(holder, "Collar", new Vector3(0f, top - 0.06f, 0f), k_PipeRadius + 0.007f, 0.03f, p.Collar);

                var nozzle = NewChild(holder, "Nozzle");
                nozzle.localPosition = new Vector3(0f, top, 0f);
                Cyl(nozzle, "Barrel", new Vector3(0f, k_Nozzle * 0.5f, 0f), k_PipeRadius - 0.005f, k_Nozzle, p.Nozzle);
                Cyl(nozzle, "Rim", new Vector3(0f, k_Nozzle, 0f), k_PipeRadius + 0.002f, 0.012f, p.Nozzle);

                var pipe = Undo.AddComponent<RigPipe>(holder.gameObject);
                var so = new SerializedObject(pipe);
                SetRef(k_Tag, so, "m_Nozzle", nozzle);
                SetVector3(k_Tag, so, "m_Mouth", new Vector3(0f, top + k_Nozzle, 0f));
                SetArray(k_Tag, so, "m_Glow", new[] { collar.GetComponent<Renderer>() });
                SetColor(k_Tag, so, "m_GlowColour", GroupColour(k_PipeGroups[i]) * 3f);
                so.ApplyModifiedProperties();
                pipes.Add(pipe);
            }

            // A lamp per beat of the loop, and the record lamp at the left end.
            var lamps = new List<Renderer>();
            float front = k_BankZ - 0.1f - 0.006f;
            for (int i = 0; i < 8; i++)
                lamps.Add(Sphere(bank, $"Beat_Lamp_{i + 1}", new Vector3(-0.35f + 0.1f * i, 0.095f, front), 0.032f, p.Lamp).GetComponent<Renderer>());
            var record = Sphere(bank, "Record_Lamp", new Vector3(-0.465f, 0.095f, front), 0.036f, p.RecordLamp).GetComponent<Renderer>();

            return (pipes, lamps, record);
        }

        // ================================================================== the bass, on the left wall

        /// <summary>
        /// Seven bass strings, C2 to B2, low at the back, on an upright frame 0.3 m off the left wall.
        /// Balls fall through the strings' plane, setting the one they cross swinging, and drop into
        /// the trough behind.
        /// </summary>
        static void BuildBass(Transform root, Palette p, List<RigTarget> targets)
        {
            var frame = NewChild(root, "Bass_Frame");
            float x = -k_SideFrameX;
            const float bottom = 0.74f, top = 1.64f, back = -0.02f, front = 0.78f;
            Box(frame, "Post_Back", new Vector3(x, 0.85f, back), new Vector3(0.04f, 1.7f, 0.04f), p.Frame);
            Box(frame, "Post_Front", new Vector3(x, 0.85f, front), new Vector3(0.04f, 1.7f, 0.04f), p.Frame);
            Box(frame, "Rail_Top", new Vector3(x, top + 0.03f, (back + front) * 0.5f), new Vector3(0.04f, 0.04f, front - back), p.Frame);
            Box(frame, "Rail_Bottom", new Vector3(x, bottom - 0.03f, (back + front) * 0.5f), new Vector3(0.04f, 0.04f, front - back), p.Frame);
            Box(frame, "Foot", new Vector3(x, 0.01f, (back + front) * 0.5f), new Vector3(0.22f, 0.02f, front - back + 0.06f), p.Frame);

            for (int i = 0; i < k_BassNotes.Length; i++)
            {
                float z = 0.05f + 0.11f * i;
                var target = NewChild(frame, $"Bass_{RigTarget.NoteName(k_BassNotes[i])}");
                target.localPosition = new Vector3(x, 0f, z);
                var peg = Cyl(target, "Peg", new Vector3(0f, top + 0.065f, 0f), 0.014f, 0.03f, p.PegBass);
                Cyl(target, "Anchor", new Vector3(0f, bottom - 0.065f, 0f), 0.012f, 0.025f, p.Frame);

                float hit = 1.1f + 0.025f * i;
                var line = StringLine(target, p.StringBass, 0.011f);
                var flash = WallFlash(root, $"Flash_Bass_{i}", CaveScreens.Left, new Vector3(-CaveScreens.HalfWidth + 0.006f, 1.2f, z),
                    new Vector2(0.15f, 1.05f), p.FlashBass, k_Bass * 1.2f);

                targets.Add(Configure(target, new Spec
                {
                    Kind = RigKind.String, Group = RigGroup.Bass, Midi = k_BassNotes[i],
                    HitPoint = new Vector3(0f, hit, 0f), Pipes = new[] { k_BassPipe, k_LeftDrumPipe }, Apex = 1.85f, Hop = 0f,
                    Landing = new Vector3(-k_TroughX, R, z), Roll = LeftRoll(),
                    BoxCentre = new Vector3(0f, (bottom + top) * 0.5f, 0f), BoxSize = new Vector3(0.09f, top - bottom + 0.04f, 0.095f),
                    Glow = new[] { peg.GetComponent<Renderer>() }, GlowColour = k_Bass * 3f, RestGlow = 0.12f,
                    String = line, StringFrom = new Vector3(0f, bottom, 0f), StringTo = new Vector3(0f, top, 0f),
                    StringSwing = new Vector3(0.022f, 0f, 0f), StringDecay = 0.9f, Flash = flash,
                }));
            }
        }

        /// <summary>Along the left trough to the lane, and across into the bank's left side.</summary>
        static Vector3[] LeftRoll() => new[] { new Vector3(-k_TroughX, R, k_LaneZ), LeftIntake };

        static Vector3[] RightRoll() => new[] { new Vector3(k_TroughX, R, k_LaneZ), RightIntake };

        /// <summary>From beside a drum near the bank down the kit lane on that side, and into the bank.</summary>
        static Vector3[] KitRoll(float side) => new[]
        {
            new Vector3(side * k_KitLaneX, R, 0.4f), new Vector3(side * k_KitLaneX, R, k_LaneZ), side < 0f ? LeftIntake : RightIntake,
        };

        // ================================================================== the guitar, in the middle

        /// <summary>
        /// A guitar lying on a low stand in the middle, just past the bank, tipped toward the people so
        /// they see its face: six steel strings tuned as a guitar's open strings, E2 to E4, the low one
        /// farthest. A ball comes down on a string between the sound hole and the bridge, hops over the
        /// far edge and drops into the front trough. Each string's tuning peg glows when it is struck,
        /// and so does a line for it on the front wall, under the mallet shelf.
        /// </summary>
        /// <remarks>
        /// The strings are three times as far apart as a real guitar's, and close in less toward the
        /// nut, so a wand can pick one out; the pointing box of each covers it over the body.
        /// </remarks>
        static void BuildGuitar(Transform root, Palette p, List<RigTarget> targets)
        {
            var guitar = NewChild(root, "Guitar");
            guitar.localPosition = k_GuitarAt;
            guitar.localRotation = Quaternion.Euler(-k_GuitarTilt, 0f, 0f);    // its face toward the people, the far edge up

            // The body, in the guitar's own space with the soundboard at y = 0: the lower and upper bouts
            // overlapping into the waist, a soundboard over them, the sound hole and the bridge.
            const float depth = 0.09f;
            foreach (var (name, x, radius) in new[] { ("Lower_Bout", -0.27f, 0.19f), ("Upper_Bout", -0.06f, 0.15f) })
            {
                Cyl(guitar, name, new Vector3(x, -depth * 0.5f - 0.003f, 0f), radius, depth, p.GuitarSides);
                Cyl(guitar, name + "_Top", new Vector3(x, -0.0015f, 0f), radius - 0.006f, 0.003f, p.GuitarTop);
            }

            Cyl(guitar, "Sound_Hole", new Vector3(-0.13f, 0.0005f, 0f), 0.05f, 0.002f, p.SoundHole);
            Box(guitar, "Bridge", new Vector3(k_BridgeX, 0.007f, 0f), new Vector3(0.025f, 0.014f, 0.24f), p.Neck);

            // The neck and its frets, the nut, and the headstock.
            const float neckStart = 0.05f, neckWidth = 0.19f;
            Box(guitar, "Neck", new Vector3((neckStart + k_NutX) * 0.5f, -0.004f, 0f), new Vector3(k_NutX - neckStart, 0.024f, neckWidth), p.Neck);
            for (int fret = 1; fret <= 12; fret++)
            {
                float x = k_NutX - (k_NutX - k_BridgeX) * (1f - Mathf.Pow(2f, -fret / 12f));
                Box(guitar, $"Fret_{fret}", new Vector3(x, 0.009f, 0f), new Vector3(0.004f, 0.003f, x > neckStart ? neckWidth : 0.16f), p.Nozzle);
            }

            Box(guitar, "Nut", new Vector3(k_NutX, 0.011f, 0f), new Vector3(0.008f, 0.007f, neckWidth), p.Head);
            Box(guitar, "Headstock", new Vector3(k_NutX + 0.095f, -0.006f, 0f), new Vector3(0.19f, 0.02f, 0.16f), p.Neck);

            // The stand: a crossed pair of legs under the body and another under the neck, meeting under the guitar.
            var stand = NewChild(root, "Guitar_Stand");
            foreach (var (name, x, below) in new[] { ("Body", -0.28f, depth + 0.003f), ("Neck", 0.16f, 0.016f) })
            {
                var under = root.InverseTransformPoint(guitar.TransformPoint(new Vector3(x, -below, 0f)));
                foreach (float side in new[] { -1f, 1f })
                    Slab(stand, $"Leg_{name}_{(side < 0f ? "Near" : "Far")}", new Vector3(under.x, 0f, under.z + side * 0.2f), under, 0.018f, 0.018f, 0f,
                        p.Frame);
            }

            float scale = k_NutX - k_BridgeX;
            float along = (k_StrikeX - k_BridgeX) / scale;
            for (int i = 0; i < k_GuitarNotes.Length; i++)
            {
                // Low strings farthest; three times a real guitar's spacing at the bridge, closing in toward the nut.
                var bridge = new Vector3(k_BridgeX, k_StringHeight, (2.5f - i) * 0.04f);
                var nut = new Vector3(k_NutX, k_StringHeight, (2.5f - i) * 0.028f);
                var strike = Vector3.Lerp(bridge, nut, along);

                var target = NewChild(guitar, $"Guitar_{RigTarget.NoteName(k_GuitarNotes[i])}");
                target.localPosition = strike;

                // The tuning pegs, three a side of the headstock, the low strings' on the far side.
                bool far = i < 3;
                var pegAt = new Vector3(k_NutX + 0.05f + 0.055f * (far ? i : 5 - i), 0.012f, far ? 0.05f : -0.05f);
                var peg = Cyl(target, "Peg", pegAt - strike, 0.014f, 0.022f, p.PegGuitar);

                var line = StringLine(target, p.StringGuitar, 0.006f);
                var flash = WallFlash(root, $"Flash_Guitar_{i}", CaveScreens.Front, new Vector3(0f, 0.8f - 0.06f * i, CaveScreens.FrontZ - 0.006f),
                    new Vector2(0.9f, 0.07f), p.FlashGuitar, k_Guitar * 1.1f);

                // Pointing covers the string over the body, where the strings are well apart.
                const float boxEnd = 0.05f;
                targets.Add(Configure(target, new Spec
                {
                    Kind = RigKind.String, Group = RigGroup.Guitar, Midi = k_GuitarNotes[i],
                    HitPoint = new Vector3(0f, R + 0.004f, 0f), Pipes = new[] { k_GuitarPipe, k_BackDrumPipe }, Apex = 1.5f, Hop = 0.2f,
                    Landing = new Vector3(k_GuitarAt.x + k_StrikeX, R, k_FrontTroughZ), Roll = new[] { new Vector3(0f, R, k_FrontTroughZ), BackIntake },
                    BoxCentre = new Vector3((k_BridgeX - 0.02f + boxEnd) * 0.5f - strike.x, 0f, 0f),
                    BoxSize = new Vector3(boxEnd - k_BridgeX + 0.02f, 0.05f, 0.034f),
                    Glow = new[] { peg.GetComponent<Renderer>() }, GlowColour = k_Guitar * 3f, RestGlow = 0.12f,
                    String = line, StringFrom = bridge - strike, StringTo = nut - strike,
                    StringSwing = new Vector3(0f, 0.012f, 0f), StringDecay = 0.9f, Flash = flash,
                }));
            }
        }

        // ================================================================== the mallet bars, along the front wall

        /// <summary>
        /// Fifteen bars, C3 to C5, on a shelf 1.3 m up along the front wall, low on the left, tilted a
        /// little toward the people so they see the tops. The bars are cut longer for the low notes,
        /// lined up at their back edges with a resonator tube under each. A ball lands near a bar's
        /// back edge, hops back over it and drops into the front trough.
        /// </summary>
        static void BuildMallets(Transform root, Palette p, List<RigTarget> targets)
        {
            var shelf = NewChild(root, "Mallet_Shelf");
            float s = Mathf.Sin(k_BarTilt * Mathf.Deg2Rad);
            float c = Mathf.Cos(k_BarTilt * Mathf.Deg2Rad);
            var toward = new Vector3(0f, -s, -c);          // along a bar from its back edge toward the people
            var up = new Vector3(0f, c, -s);                // a bar's top surface faces this way
            var rotation = Quaternion.LookRotation(toward, up);

            int count = k_MalletNotes.Length;
            const float span = 2.24f;
            float step = span / (count - 1);
            var backNodes = new List<Vector3>();
            var frontNodes = new List<Vector3>();

            for (int i = 0; i < count; i++)
            {
                float x = -span * 0.5f + step * i;
                float length = Mathf.Lerp(0.3f, 0.18f, i / (float)(count - 1));
                var origin = new Vector3(x, k_BarBackY, k_BarBackZ);

                var target = NewChild(shelf, $"Bar_{RigTarget.NoteName(k_MalletNotes[i])}");
                target.localPosition = origin;
                target.localRotation = rotation;
                var bar = Box(target, "Bar", new Vector3(0f, -0.0125f, length * 0.5f), new Vector3(0.13f, 0.025f, length), p.Bar);

                // A resonator under the middle of the bar, longer for the low notes.
                var middle = origin + toward * (length * 0.5f) - up * 0.03f;
                float tube = Mathf.Lerp(0.42f, 0.14f, i / (float)(count - 1));
                Cyl(shelf, $"Resonator_{i}", middle + Vector3.down * (tube * 0.5f + 0.02f), 0.032f, tube, p.Resonator);

                backNodes.Add(origin + toward * (length * 0.224f) - up * 0.04f);
                frontNodes.Add(origin + toward * (length * 0.776f) - up * 0.04f);

                var flash = WallFlash(root, $"Flash_Bar_{i}", CaveScreens.Front, new Vector3(x, 1.5f, CaveScreens.FrontZ - 0.006f),
                    new Vector2(0.2f, 0.95f), p.FlashMallets, k_Mallets * 1.1f);

                int[] pipes = i < 5 ? new[] { k_MalletLow, k_MalletMid, k_MalletHigh }
                    : i < 10 ? new[] { k_MalletMid, k_MalletHigh, k_MalletLow }
                    : new[] { k_MalletHigh, k_MalletMid, k_MalletLow };

                targets.Add(Configure(target, new Spec
                {
                    Kind = RigKind.Bar, Group = RigGroup.Mallets, Midi = k_MalletNotes[i],
                    HitPoint = new Vector3(0f, R, 0.05f), Pipes = pipes, Apex = 1.85f, Hop = 0.15f,
                    Landing = new Vector3(x, R, k_FrontTroughZ), Roll = new[] { new Vector3(0f, R, k_FrontTroughZ), BackIntake },
                    BoxCentre = new Vector3(0f, 0f, length * 0.5f), BoxSize = new Vector3(step - 0.01f, 0.09f, length + 0.03f),
                    Body = bar, Glow = new[] { bar.GetComponent<Renderer>() }, GlowColour = k_Mallets * 1.6f, RestGlow = 0.04f,
                    GlowDecay = 0.45f, Flash = flash,
                }));
            }

            // The rails along the bars' nodes, and a bracket into the wall at each end.
            for (int i = 1; i < count; i++)
            {
                Slab(shelf, $"Rail_Back_{i}", backNodes[i - 1], backNodes[i], 0.025f, 0.03f, 0f, p.Rail);
                Slab(shelf, $"Rail_Front_{i}", frontNodes[i - 1], frontNodes[i], 0.025f, 0.03f, 0f, p.Rail);
            }

            foreach (int end in new[] { 0, count - 1 })
            {
                float x = backNodes[end].x + (end == 0 ? -0.09f : 0.09f);
                float z0 = frontNodes[end].z;
                Box(shelf, end == 0 ? "Bracket_Left" : "Bracket_Right", new Vector3(x, k_BarBackY - 0.07f, (z0 + CaveScreens.FrontZ) * 0.5f),
                    new Vector3(0.03f, 0.05f, CaveScreens.FrontZ - z0), p.Frame);
            }
        }

        // ================================================================== the glockenspiel, beside the bank on the right

        /// <summary>
        /// Eight steel bars, C6 to C7, on a low stand beside the bank on the right, the low one at the
        /// front, each tipped toward the people. A ball lands near a bar's outer end, hops over it and
        /// drops, under the strings' frame, into the right trough. The floor past each bar's end glows.
        /// </summary>
        static void BuildGlockenspiel(Transform root, Palette p, List<RigTarget> targets)
        {
            var glock = NewChild(root, "Glockenspiel");
            float s = Mathf.Sin(k_GlockTilt * Mathf.Deg2Rad);
            float c = Mathf.Cos(k_GlockTilt * Mathf.Deg2Rad);
            var toward = new Vector3(-c, -s, 0f);          // along a bar from its outer end toward the people
            var up = new Vector3(-s, c, 0f);                // a bar's top surface faces this way
            var rotation = Quaternion.LookRotation(toward, up);

            int count = k_GlockNotes.Length;
            var innerNodes = new List<Vector3>();
            var outerNodes = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float z = k_GlockFrontZ - k_GlockStep * i;
                float length = Mathf.Lerp(0.22f, 0.15f, i / (float)(count - 1));
                var origin = new Vector3(k_GlockOuterX, k_GlockOuterY, z);

                var target = NewChild(glock, $"Glock_{RigTarget.NoteName(k_GlockNotes[i])}");
                target.localPosition = origin;
                target.localRotation = rotation;
                var bar = Box(target, "Bar", new Vector3(0f, -0.006f, length * 0.5f), new Vector3(k_GlockStep - 0.008f, 0.012f, length), p.GlockBar);

                outerNodes.Add(origin + toward * (length * 0.224f) - up * 0.02f);
                innerNodes.Add(origin + toward * (length * 0.776f) - up * 0.02f);

                var flash = FloorFlash(root, $"Flash_Glock_{i}", new Vector3(k_GlockOuterX + 0.09f, 0f, z), 0.15f, p.FlashGlock, k_Glock);

                targets.Add(Configure(target, new Spec
                {
                    Kind = RigKind.Bar, Group = RigGroup.Glockenspiel, Midi = k_GlockNotes[i],
                    HitPoint = new Vector3(0f, R, 0.04f), Pipes = new[] { k_GlockPipe, k_RightDrumPipe }, Apex = 1.6f, Hop = 0.05f,
                    Landing = new Vector3(k_TroughX, R, z), Roll = RightRoll(),
                    BoxCentre = new Vector3(0f, 0f, length * 0.5f), BoxSize = new Vector3(k_GlockStep - 0.004f, 0.07f, length + 0.03f),
                    Body = bar, Glow = new[] { bar.GetComponent<Renderer>() }, GlowColour = k_Glock * 1.8f, RestGlow = 0.04f,
                    GlowDecay = 0.6f, Flash = flash,
                }));
            }

            // The rails along the bars' nodes, and legs down from them: the back pair two bars in, so the
            // balls rolling along the lane under the last bars pass between them.
            for (int i = 1; i < count; i++)
            {
                Slab(glock, $"Rail_Outer_{i}", outerNodes[i - 1], outerNodes[i], 0.018f, 0.022f, 0f, p.Rail);
                Slab(glock, $"Rail_Inner_{i}", innerNodes[i - 1], innerNodes[i], 0.018f, 0.022f, 0f, p.Rail);
            }

            foreach (var node in new[] { outerNodes[0], outerNodes[count - 3], innerNodes[0], innerNodes[count - 3] })
                Slab(glock, "Leg", new Vector3(node.x, 0f, node.z), node, 0.018f, 0.018f, 0f, p.Frame);
        }

        // ================================================================== the strings, on the right wall

        /// <summary>
        /// Eight strings, D5 to D6, on a frame 0.3 m off the right wall, low at the front and rising
        /// toward the back, shorter as they go up, the way a harp's are.
        /// </summary>
        static void BuildStrings(Transform root, Palette p, List<RigTarget> targets)
        {
            var frame = NewChild(root, "String_Frame");
            float x = k_SideFrameX;
            const float bottom = 0.76f, front = 0.8f, back = -0.01f, frontTop = 1.7f, backTop = 1.4f;
            Box(frame, "Post_Front", new Vector3(x, (frontTop + 0.06f) * 0.5f, front), new Vector3(0.04f, frontTop + 0.06f, 0.04f), p.Frame);
            Box(frame, "Post_Back", new Vector3(x, (backTop + 0.06f) * 0.5f, back), new Vector3(0.04f, backTop + 0.06f, 0.04f), p.Frame);
            Slab(frame, "Rail_Top", new Vector3(x, frontTop + 0.03f, front), new Vector3(x, backTop + 0.03f, back), 0.04f, 0.04f, 0f, p.Frame);
            Box(frame, "Rail_Bottom", new Vector3(x, bottom - 0.03f, (back + front) * 0.5f), new Vector3(0.04f, 0.04f, front - back), p.Frame);
            Box(frame, "Foot", new Vector3(x, 0.01f, (back + front) * 0.5f), new Vector3(0.22f, 0.02f, front - back + 0.06f), p.Frame);

            int count = k_StringNotes.Length;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float z = Mathf.Lerp(0.74f, 0.05f, t);
                float top = Mathf.Lerp(frontTop, backTop, Mathf.InverseLerp(front, back, z));
                var target = NewChild(frame, $"String_{RigTarget.NoteName(k_StringNotes[i])}");
                target.localPosition = new Vector3(x, 0f, z);
                var peg = Cyl(target, "Peg", new Vector3(0f, top + 0.06f, 0f), 0.012f, 0.025f, p.PegStrings);
                Cyl(target, "Anchor", new Vector3(0f, bottom - 0.06f, 0f), 0.01f, 0.02f, p.Frame);

                float hit = Mathf.Min(top - 0.12f, 1.16f + 0.02f * i);
                var line = StringLine(target, p.StringStrings, 0.007f);
                var flash = WallFlash(root, $"Flash_String_{i}", CaveScreens.Right, new Vector3(CaveScreens.HalfWidth - 0.006f, 1.2f, z),
                    new Vector2(0.13f, 1.05f), p.FlashStrings, k_Strings * 1.2f);

                targets.Add(Configure(target, new Spec
                {
                    Kind = RigKind.String, Group = RigGroup.Strings, Midi = k_StringNotes[i],
                    HitPoint = new Vector3(0f, hit, 0f), Pipes = new[] { k_StringPipe, k_ChimePipe }, Apex = 1.85f, Hop = 0f,
                    Landing = new Vector3(k_TroughX, R, z), Roll = RightRoll(),
                    BoxCentre = new Vector3(0f, (bottom + top) * 0.5f, 0f), BoxSize = new Vector3(0.09f, top - bottom + 0.04f, 0.085f),
                    Glow = new[] { peg.GetComponent<Renderer>() }, GlowColour = k_Strings * 3f, RestGlow = 0.12f,
                    String = line, StringFrom = new Vector3(0f, bottom, 0f), StringTo = new Vector3(0f, top, 0f),
                    StringSwing = new Vector3(0.014f, 0f, 0f), StringDecay = 0.7f, Flash = flash,
                }));
            }
        }

        // ================================================================== the chimes, beside the people on the right

        /// <summary>
        /// Eight tubular chimes, C5 to C6, hung in a row from a rail beside the people on the right,
        /// carrying the strings' line on toward the open back: the longest at the front. A ball lands
        /// on a tube's cap, where a chime is struck, and the tube swings on its cord toward the wall
        /// and back; the ball hops off toward the wall into the right trough. The wall behind each
        /// tube glows.
        /// </summary>
        static void BuildChimes(Transform root, Palette p, List<RigTarget> targets)
        {
            var frame = NewChild(root, "Chimes");
            int count = k_ChimeNotes.Length;
            float x = k_ChimeX;
            float front = k_ChimeFrontZ + 0.08f;
            float back = k_ChimeFrontZ - k_ChimeStep * (count - 1) - 0.08f;
            Box(frame, "Post_Front", new Vector3(x, k_ChimeRail * 0.5f, front), new Vector3(0.04f, k_ChimeRail, 0.04f), p.Frame);
            Box(frame, "Post_Back", new Vector3(x, k_ChimeRail * 0.5f, back), new Vector3(0.04f, k_ChimeRail, 0.04f), p.Frame);
            Box(frame, "Rail", new Vector3(x, k_ChimeRail + 0.02f, (front + back) * 0.5f), new Vector3(0.04f, 0.04f, front - back + 0.04f), p.Frame);
            Box(frame, "Foot_Front", new Vector3(x, 0.01f, front), new Vector3(0.26f, 0.02f, 0.05f), p.Frame);
            Box(frame, "Foot_Back", new Vector3(x, 0.01f, back), new Vector3(0.26f, 0.02f, 0.05f), p.Frame);

            for (int i = 0; i < count; i++)
            {
                float z = k_ChimeFrontZ - k_ChimeStep * i;
                float length = Mathf.Lerp(0.85f, 0.55f, i / (float)(count - 1));

                var target = NewChild(frame, $"Chime_{RigTarget.NoteName(k_ChimeNotes[i])}");
                target.localPosition = new Vector3(x, 0f, z);

                // It hangs from the rail on a cord; turned so its own x runs along the row, it swings toward the wall and back.
                var pivot = NewChild(target, "Pivot");
                pivot.localPosition = new Vector3(0f, k_ChimeRail, 0f);
                pivot.localRotation = Quaternion.Euler(0f, 90f, 0f);
                float drop = k_ChimeRail - k_ChimeCap;
                Cyl(pivot, "Cord", new Vector3(0f, -drop * 0.5f, 0f), 0.004f, drop, p.Frame);
                var cap = Cyl(pivot, "Cap", new Vector3(0f, -drop - 0.006f, 0f), 0.042f, 0.012f, p.Nozzle);
                var tube = Cyl(pivot, "Tube", new Vector3(0f, -drop - 0.012f - length * 0.5f, 0f), 0.038f, length, p.Chime);

                var flash = WallFlash(root, $"Flash_Chime_{i}", CaveScreens.Right, new Vector3(CaveScreens.HalfWidth - 0.006f, 1.25f, z),
                    new Vector2(0.09f, 1f), p.FlashChimes, k_Chimes * 1.1f);

                targets.Add(Configure(target, new Spec
                {
                    Kind = RigKind.Chime, Group = RigGroup.Chimes, Midi = k_ChimeNotes[i], Gain = 0.8f,
                    HitPoint = new Vector3(0f, k_ChimeCap + 0.002f + R, 0f), Pipes = new[] { k_ChimePipe, k_StringPipe }, Apex = 1.85f, Hop = 0.06f,
                    Landing = new Vector3(k_TroughX, R, z), Roll = RightRoll(),
                    BoxCentre = new Vector3(0f, k_ChimeCap - length * 0.5f, 0f), BoxSize = new Vector3(0.1f, length + 0.04f, k_ChimeStep - 0.005f),
                    Body = pivot, Glow = new[] { tube.GetComponent<Renderer>(), cap.GetComponent<Renderer>() }, GlowColour = k_Chimes * 1.4f,
                    RestGlow = 0.05f, GlowDecay = 1.2f, Flash = flash,
                }));
            }
        }

        // ================================================================== the drums, on the floor

        /// <summary>
        /// On the left of the bank a kick, a rack tom and a tom, and behind them, beside the people, a
        /// floor tom and a gong; on the right a snare and a crash, and a hi-hat beside the people. All
        /// are struck from above but the gong, which is struck on its face. The floor under each drum
        /// glows when it is hit; balls hop off and roll home.
        /// </summary>
        static void BuildDrums(Transform root, Palette p, List<RigTarget> targets)
        {
            var kit = NewChild(root, "Drums");

            targets.Add(Drum(kit, root, p, RigDrum.Kick, new Vector3(-1.1f, 0f, 0.33f), 0.23f, 0.02f, 0.32f, false,
                new Vector3(-1.1f, R, k_LaneZ), new[] { LeftIntake }, new[] { k_LeftDrumPipe, k_BackDrumPipe, k_BassPipe }, 0.15f, 1.35f));

            targets.Add(Drum(kit, root, p, RigDrum.RackTom, new Vector3(-1.05f, 0f, 0.72f), 0.12f, 0.36f, 0.54f, true,
                new Vector3(-0.9f, R, 0.52f), KitRoll(-1f), new[] { k_LeftDrumPipe, k_BackDrumPipe }, 0.1f, 1.35f));

            targets.Add(Drum(kit, root, p, RigDrum.Tom, new Vector3(-0.72f, 0f, 0.66f), 0.15f, 0.19f, 0.4f, true,
                new Vector3(-0.6f, R, 0.44f), KitRoll(-1f), new[] { k_LeftDrumPipe, k_BackDrumPipe }, 0.1f, 1.35f));

            targets.Add(Drum(kit, root, p, RigDrum.FloorTom, new Vector3(-0.95f, 0f, -0.15f), 0.18f, 0.08f, 0.44f, true,
                new Vector3(-k_TroughX, R, -0.15f), LeftRoll(), new[] { k_BackDrumPipe, k_LeftDrumPipe }, 0.1f, 1.4f));

            targets.Add(Gong(kit, root, p, new Vector3(-1.2f, 0f, -0.6f), 1.05f, 0.27f, new[] { k_BackDrumPipe, k_LeftDrumPipe }));

            targets.Add(Drum(kit, root, p, RigDrum.Snare, new Vector3(0.78f, 0f, 0.6f), 0.15f, 0.3f, 0.44f, true,
                new Vector3(0.64f, R, 0.42f), KitRoll(1f), new[] { k_RightDrumPipe, k_HatPipe }, 0.1f, 1.35f));

            targets.Add(Cymbal(kit, root, p, RigDrum.HiHat, new Vector3(0.95f, 0f, -0.3f), 0.16f, 0.6f, Vector3.zero, true,
                new Vector3(k_TroughX, R, -0.3f), RightRoll(), new[] { k_HatPipe, k_RightDrumPipe }, 0.1f, 1.4f));

            targets.Add(Cymbal(kit, root, p, RigDrum.Crash, new Vector3(1.2f, 0f, 0.66f), 0.21f, 0.86f, new Vector3(-12f, 0f, 8f), false,
                new Vector3(1.45f, R, 0.62f), new[] { new Vector3(k_TroughX, R, 0.62f), new Vector3(k_TroughX, R, k_LaneZ), RightIntake },
                new[] { k_RightDrumPipe, k_HatPipe }, 0.1f, 1.5f));
        }

        /// <summary>
        /// A gong of <paramref name="radius"/>, its middle <paramref name="height"/> up, hung in a frame
        /// at <paramref name="at"/> and turned to face the first of its <paramref name="pipes"/>, so the
        /// ball meets it face on. It swings on its cords when struck; the ball drops off its face and
        /// rolls under it to the left trough. The left wall behind it glows.
        /// </summary>
        static RigTarget Gong(Transform kit, Transform root, Palette p, Vector3 at, float height, float radius, int[] pipes)
        {
            var target = NewChild(kit, "Gong");
            target.localPosition = at;
            var facing = new Vector3(PipeX(pipes[0]) - at.x, 0f, k_BankZ - at.z).normalized;
            target.localRotation = Quaternion.LookRotation(facing, Vector3.up);

            // The frame: a post either side on a foot, and a bar across the top.
            float half = radius + 0.09f;
            float top = height + radius + 0.14f;
            foreach (float side in new[] { -1f, 1f })
            {
                Box(target, side < 0f ? "Post_Left" : "Post_Right", new Vector3(side * half, top * 0.5f, 0f), new Vector3(0.04f, top, 0.04f), p.Frame);
                Box(target, side < 0f ? "Foot_Left" : "Foot_Right", new Vector3(side * half, 0.01f, 0f), new Vector3(0.05f, 0.02f, 0.24f), p.Frame);
            }

            Box(target, "Bar", new Vector3(0f, top + 0.02f, 0f), new Vector3(half * 2f + 0.04f, 0.04f, 0.04f), p.Frame);

            // The gong hangs from the bar on two cords and swings about it: a disc with a turned-back rim and a boss in the middle.
            var pivot = NewChild(target, "Pivot");
            pivot.localPosition = new Vector3(0f, top, 0f);
            float centre = height - top;
            foreach (float side in new[] { -1f, 1f })
            {
                var from = new Vector3(side * radius * 0.45f, 0f, 0f);
                var to = new Vector3(side * radius * 0.5f, centre + radius * 0.86f, 0f);
                Slab(pivot, side < 0f ? "Cord_Left" : "Cord_Right", from, to, 0.006f, 0.006f, 0f, p.Frame);
            }

            var disc = Cyl(pivot, "Disc", new Vector3(0f, centre, 0f), radius, 0.012f, p.GongMetal);
            disc.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var rim = Cyl(pivot, "Rim", new Vector3(0f, centre, -0.02f), radius + 0.008f, 0.04f, p.GongMetal);
            rim.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var boss = Cyl(pivot, "Boss", new Vector3(0f, centre, 0.012f), radius * 0.22f, 0.02f, p.GongMetal);
            boss.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var landing = at + facing * 0.16f;
            var flash = WallFlash(root, "Flash_Gong", CaveScreens.Left, new Vector3(-CaveScreens.HalfWidth + 0.006f, height, at.z),
                new Vector2(1.1f, 1.1f), p.FlashDrums, k_Gong * 1.2f, 0.9f);
            return Configure(target, new Spec
            {
                Kind = RigKind.Gong, Group = RigGroup.Drums, Drum = RigDrum.Gong, Gain = 1f,
                HitPoint = new Vector3(0f, height, 0.022f + R), Pipes = pipes, Apex = 1.85f, Hop = 0.02f,
                Landing = new Vector3(landing.x, R, landing.z),
                Roll = new[] { new Vector3(-k_TroughX, R, landing.z), new Vector3(-k_TroughX, R, k_LaneZ), LeftIntake },
                BoxCentre = new Vector3(0f, height, 0f), BoxSize = new Vector3(radius * 2f + 0.06f, radius * 2f + 0.06f, 0.12f),
                Body = pivot, Glow = new[] { disc.GetComponent<Renderer>(), boss.GetComponent<Renderer>() }, GlowColour = k_Gong * 1.3f,
                RestGlow = 0.03f, GlowDecay = 1.5f, Flash = flash,
            });
        }

        /// <summary>A drum standing on the floor, head up: a shell from <paramref name="low"/> to <paramref name="high"/>, on legs or straight on the floor.</summary>
        static RigTarget Drum(Transform kit, Transform root, Palette p, RigDrum drum, Vector3 at, float radius, float low, float high, bool legs,
            Vector3 landing, Vector3[] roll, int[] pipes, float hop, float apex)
        {
            var target = NewChild(kit, drum.ToString());
            target.localPosition = at;

            Cyl(target, "Shell", new Vector3(0f, (low + high) * 0.5f, 0f), radius, high - low, p.Shell);
            Cyl(target, "Hoop", new Vector3(0f, high, 0f), radius + 0.008f, 0.025f, p.Nozzle);
            Cyl(target, "Hoop_Low", new Vector3(0f, low + 0.012f, 0f), radius + 0.008f, 0.025f, p.Nozzle);
            var head = Cyl(target, "Head", new Vector3(0f, high + 0.008f, 0f), radius - 0.006f, 0.008f, p.Head);

            if (legs)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * 120f * Mathf.Deg2Rad + 0.5f;
                    var foot = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius + 0.05f);
                    var hip = new Vector3(Mathf.Cos(angle) * radius * 0.9f, low + 0.03f, Mathf.Sin(angle) * radius * 0.9f);
                    Slab(target, $"Leg_{i}", foot, hip, 0.016f, 0.016f, 0f, p.Nozzle);
                }
            }

            var flash = FloorFlash(root, $"Flash_{drum}", at, radius * 2.6f, p.FlashDrums, k_Drums);
            return Configure(target, new Spec
            {
                Kind = RigKind.Drum, Group = RigGroup.Drums, Drum = drum, Gain = drum == RigDrum.Kick ? 1.1f : 0.9f,
                HitPoint = new Vector3(0f, high + 0.012f + R, 0f), Pipes = pipes, Apex = apex, Hop = hop, Landing = landing, Roll = roll,
                BoxCentre = new Vector3(0f, (low + high) * 0.5f + 0.02f, 0f), BoxSize = new Vector3(radius * 2.1f, high - low + 0.1f, radius * 2.1f),
                Body = head, Glow = new[] { head.GetComponent<Renderer>() }, GlowColour = k_Drums * 1.4f, RestGlow = 0.03f, GlowDecay = 0.25f,
                Flash = flash,
            });
        }

        /// <summary>A cymbal on a stand at <paramref name="height"/>, tipped by <paramref name="tilt"/>; doubled for a hi-hat.</summary>
        static RigTarget Cymbal(Transform kit, Transform root, Palette p, RigDrum drum, Vector3 at, float radius, float height, Vector3 tilt, bool pair,
            Vector3 landing, Vector3[] roll, int[] pipes, float hop, float apex)
        {
            var target = NewChild(kit, drum.ToString());
            target.localPosition = at;

            Cyl(target, "Stand", new Vector3(0f, height * 0.5f, 0f), 0.011f, height, p.Nozzle);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad + 0.3f;
                Slab(target, $"Leg_{i}", new Vector3(Mathf.Cos(angle) * 0.16f, 0f, Mathf.Sin(angle) * 0.16f), new Vector3(0f, 0.2f, 0f),
                    0.012f, 0.012f, 0f, p.Nozzle);
            }

            if (pair)
                Cyl(target, "Bottom", new Vector3(0f, height - 0.016f, 0f), radius, 0.006f, p.Cymbal);

            var pivot = NewChild(target, "Pivot");
            pivot.localPosition = new Vector3(0f, height, 0f);
            pivot.localRotation = Quaternion.Euler(tilt);
            var disc = Cyl(pivot, "Disc", Vector3.zero, radius, 0.006f, p.Cymbal);
            Cyl(pivot, "Bell", new Vector3(0f, 0.012f, 0f), radius * 0.28f, 0.018f, p.Cymbal);

            // The ball lands part way out toward the bank, on the disc's upper face.
            var hit = pivot.localPosition + pivot.localRotation * new Vector3(-radius * 0.4f, 0.006f + R, 0f);
            var flash = FloorFlash(root, $"Flash_{drum}", at, radius * 2.8f, p.FlashDrums, k_Drums * 0.9f);
            return Configure(target, new Spec
            {
                Kind = RigKind.Cymbal, Group = RigGroup.Drums, Drum = drum, Gain = drum == RigDrum.HiHat ? 0.55f : 0.75f,
                HitPoint = hit, Pipes = pipes, Apex = apex, Hop = hop, Landing = landing, Roll = roll,
                BoxCentre = new Vector3(0f, height, 0f), BoxSize = new Vector3(radius * 2.1f, 0.12f, radius * 2.1f),
                Body = pivot, Glow = new[] { disc.GetComponent<Renderer>() }, GlowColour = new Color(1f, 0.8f, 0.45f) * 1.2f, RestGlow = 0.02f,
                GlowDecay = drum == RigDrum.Crash ? 0.8f : 0.2f, Flash = flash,
            });
        }

        // ================================================================== balls, pointer, sign

        /// <summary>The pool's prototypes: a glossy ball with a short trail, and its soft shadow. Hidden; the rig copies them at load.</summary>
        static RigBalls BuildBalls(Transform root, Palette p)
        {
            var holder = NewChild(root, "Balls");
            var ball = Primitive(holder, PrimitiveType.Sphere, "Ball_Prototype", p.Balls[0]);
            ball.localScale = Vector3.one * (k_Radius * 2f);
            NoShadows(ball);

            var trail = Undo.AddComponent<TrailRenderer>(ball.gameObject);
            trail.time = 0.22f;
            trail.widthMultiplier = k_Radius * 0.8f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.01f;
            trail.numCapVertices = 0;
            trail.sharedMaterial = p.Trail;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            var shadow = Primitive(holder, PrimitiveType.Quad, "Shadow_Prototype", p.Shadow);
            shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.localScale = new Vector3(0.1f, 0.1f, 1f);
            NoShadows(shadow);

            ball.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);

            var balls = Undo.AddComponent<RigBalls>(holder.gameObject);
            var so = new SerializedObject(balls);
            SetRef(k_Tag, so, "m_BallPrototype", ball.gameObject);
            SetRef(k_Tag, so, "m_ShadowPrototype", shadow.gameObject);
            SetArray(k_Tag, so, "m_GroupMaterials", p.Balls);
            SetColors(so, "m_TrailColours", new[] { k_Bass, k_Mallets, k_Strings, k_Drums, k_Guitar, k_Chimes, k_Glock });
            so.ApplyModifiedProperties();
            return balls;
        }

        /// <summary>The wand's ray in play mode and a bead where it meets an instrument.</summary>
        static (LineRenderer, Transform) BuildPointer(Transform root, Palette p)
        {
            var holder = NewChild(root, "Pointer");
            var ray = Undo.AddComponent<LineRenderer>(holder.gameObject);
            ray.useWorldSpace = true;
            ray.positionCount = 2;
            ray.widthMultiplier = 0.006f;
            ray.numCapVertices = 2;
            ray.sharedMaterial = p.Ray;
            ray.shadowCastingMode = ShadowCastingMode.Off;
            ray.receiveShadows = false;
            ray.enabled = false;

            var cursor = Sphere(holder, "Cursor", Vector3.zero, 0.028f, p.Cursor);
            cursor.gameObject.SetActive(false);
            return (ray, cursor);
        }

        /// <summary>A dim status line on the floor at the open back, readable by whoever runs the demo from the doorway.</summary>
        static TMPro.TextMeshPro BuildSign(Transform root)
        {
            var sign = Text(k_Tag, root, "Status_Sign", new Vector3(0f, 0.009f, -0.97f), 0.5f, "", k_SignColour, new Vector2(3.2f, 0.2f));
            sign.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return sign;
        }

        /// <summary>A dark room: the template's sun turned down, a cool ambient, a soft lamp over the machine, and a black clear colour.</summary>
        static void Atmosphere(Scene scene, Transform root)
        {
            var sun = FindRoot(scene, "Directional Light");
            if (sun != null && sun.TryGetComponent<Light>(out var light))
            {
                Undo.RecordObject(light, "Dim the sun");
                light.intensity = 0.45f;
                light.color = new Color(0.85f, 0.9f, 1f);
                light.shadows = LightShadows.None;
            }

            var lamp = NewChild(root, "Machine_Light");
            lamp.localPosition = new Vector3(0f, 2f, 0.2f);
            var point = Undo.AddComponent<Light>(lamp.gameObject);
            point.type = LightType.Point;
            point.range = 4f;
            point.intensity = 0.8f;
            point.color = new Color(1f, 0.92f, 0.82f);
            point.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.11f, 0.13f);
            RenderSettings.reflectionIntensity = 0.5f;
            RenderSettings.fog = false;

            var cameraRoot = FindRoot(scene, "MainCamera");
            if (cameraRoot != null && cameraRoot.TryGetComponent<Camera>(out var camera))
            {
                Undo.RecordObject(camera, "Darken camera");
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
        }

        // ================================================================== targets

        /// <summary>Everything a <see cref="RigTarget"/> is given by the builder.</summary>
        struct Spec
        {
            public RigKind Kind;
            public RigGroup Group;
            public int Midi;
            public RigDrum Drum;
            public float Gain;
            public Vector3 HitPoint;
            public int[] Pipes;
            public float Apex, Hop;
            public Vector3 Landing;
            public Vector3[] Roll;
            public Vector3 BoxCentre, BoxSize;
            public Transform Body;
            public Renderer[] Glow;
            public Color GlowColour;
            public float RestGlow, GlowDecay;
            public LineRenderer String;
            public Vector3 StringFrom, StringTo, StringSwing;
            public float StringDecay;
            public RigFlash Flash;
        }

        static RigTarget Configure(Transform holder, Spec spec)
        {
            var target = Undo.AddComponent<RigTarget>(holder.gameObject);
            var so = new SerializedObject(target);
            SetEnum(k_Tag, so, "m_Kind", (int)spec.Kind);
            SetEnum(k_Tag, so, "m_Group", (int)spec.Group);
            SetInt(k_Tag, so, "m_Midi", spec.Midi);
            SetEnum(k_Tag, so, "m_Drum", (int)spec.Drum);
            SetFloat(k_Tag, so, "m_Gain", spec.Gain > 0f ? spec.Gain : 1f);
            SetVector3(k_Tag, so, "m_HitPoint", spec.HitPoint);
            SetInts(so, "m_Pipes", spec.Pipes ?? new int[0]);
            SetFloat(k_Tag, so, "m_Apex", spec.Apex);
            SetFloat(k_Tag, so, "m_Hop", spec.Hop);
            SetVector3(k_Tag, so, "m_Landing", spec.Landing);
            SetVectors(so, "m_Roll", spec.Roll ?? new Vector3[0]);
            SetVector3(k_Tag, so, "m_BoxCentre", spec.BoxCentre);
            SetVector3(k_Tag, so, "m_BoxSize", spec.BoxSize);
            SetRef(k_Tag, so, "m_Body", spec.Body);
            SetArray(k_Tag, so, "m_Glow", spec.Glow ?? new Renderer[0]);
            SetColor(k_Tag, so, "m_GlowColour", spec.GlowColour);
            SetFloat(k_Tag, so, "m_RestGlow", spec.RestGlow);
            if (spec.GlowDecay > 0f)
                SetFloat(k_Tag, so, "m_GlowDecay", spec.GlowDecay);
            SetRef(k_Tag, so, "m_String", spec.String);
            SetVector3(k_Tag, so, "m_StringFrom", spec.StringFrom);
            SetVector3(k_Tag, so, "m_StringTo", spec.StringTo);
            SetVector3(k_Tag, so, "m_StringSwing", spec.StringSwing);
            if (spec.StringDecay > 0f)
                SetFloat(k_Tag, so, "m_StringDecay", spec.StringDecay);
            SetRef(k_Tag, so, "m_Flash", spec.Flash);
            so.ApplyModifiedProperties();
            return target;
        }

        /// <summary>A string drawn as a glowing line on <paramref name="holder"/>, in its own space; the target sets its points.</summary>
        static LineRenderer StringLine(Transform holder, Material material, float width)
        {
            var line = Undo.AddComponent<LineRenderer>(holder.gameObject);
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }

        /// <summary>A soft glow on a wall screen at <paramref name="centre"/>, for <see cref="RigFlash"/> to light.</summary>
        static RigFlash WallFlash(Transform root, string name, CaveScreen screen, Vector3 centre, Vector2 size, Material material, Color colour,
            float decay = 0.22f)
        {
            var quad = Quad(FlashHolder(root), name, centre, size, material, screen.FacingRotation);
            return Flash(quad, colour, decay);
        }

        static RigFlash FloorFlash(Transform root, string name, Vector3 at, float diameter, Material material, Color colour)
        {
            var quad = Quad(FlashHolder(root), name, new Vector3(at.x, 0.01f, at.z), new Vector2(diameter, diameter), material,
                CaveScreens.Floor.FacingRotation);
            return Flash(quad, colour, 0.2f);
        }

        static Transform FlashHolder(Transform root)
        {
            var holder = root.Find("Flashes");
            return holder != null ? holder : NewChild(root, "Flashes");
        }

        static RigFlash Flash(Transform quad, Color colour, float decay)
        {
            var flash = Undo.AddComponent<RigFlash>(quad.gameObject);
            var so = new SerializedObject(flash);
            SetRef(k_Tag, so, "m_Renderer", quad.GetComponent<Renderer>());
            SetColor(k_Tag, so, "m_Colour", colour);
            SetFloat(k_Tag, so, "m_Decay", decay);
            so.ApplyModifiedProperties();
            return flash;
        }

        // ================================================================== pieces

        static Transform Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            return Box(parent, name, centre, size, material, Quaternion.identity);
        }

        static Transform Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material, Quaternion rotation)
        {
            var box = Primitive(parent, PrimitiveType.Cube, name, material);
            box.localPosition = centre;
            box.localRotation = rotation;
            box.localScale = size;
            return box;
        }

        /// <summary>A cylinder <paramref name="height"/> long on its own Y and <paramref name="radius"/> round.</summary>
        static Transform Cyl(Transform parent, string name, Vector3 centre, float radius, float height, Material material)
        {
            var cylinder = Primitive(parent, PrimitiveType.Cylinder, name, material);
            cylinder.localPosition = centre;
            cylinder.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            return cylinder;
        }

        static Transform Sphere(Transform parent, string name, Vector3 centre, float diameter, Material material)
        {
            var sphere = Primitive(parent, PrimitiveType.Sphere, name, material);
            sphere.localPosition = centre;
            sphere.localScale = Vector3.one * diameter;
            return sphere;
        }

        static Transform Quad(Transform parent, string name, Vector3 centre, Vector2 size, Material material, Quaternion rotation)
        {
            var quad = Primitive(parent, PrimitiveType.Quad, name, material);
            quad.localPosition = centre;
            quad.localRotation = rotation;
            quad.localScale = new Vector3(size.x, size.y, 1f);
            NoShadows(quad);
            return quad;
        }

        /// <summary>A flat glowing strip on the floor from <paramref name="from"/> to <paramref name="to"/>, <paramref name="lift"/> above it.</summary>
        static void Strip(Transform parent, string name, Vector3 from, Vector3 to, float width, float lift, Material material)
        {
            var along = to - from;
            along.y = 0f;
            float length = along.magnitude;
            if (length < 1e-4f)
                return;

            along /= length;
            var middle = (from + to) * 0.5f;
            middle.y = lift;

            // Face up, with the quad's width (its V, which the glow's profile runs across) across the strip.
            Quad(parent, name, middle, new Vector2(length, width), material, Quaternion.LookRotation(Vector3.down, Vector3.Cross(Vector3.down, along)));
        }

        static void NoShadows(Transform t)
        {
            var renderer = t.GetComponent<Renderer>();
            if (renderer == null)
                return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void SetInts(SerializedObject so, string path, int[] values)
        {
            var property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).intValue = values[i];
        }

        static void SetVectors(SerializedObject so, string path, Vector3[] values)
        {
            var property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).vector3Value = values[i];
        }

        static void SetColors(SerializedObject so, string path, Color[] values)
        {
            var property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).colorValue = values[i];
        }

        // ================================================================== palette

        class Palette
        {
            public Material Wall, Floor, Grid, Lane, Trough, Intake;
            public Material Cabinet, Pipe, Collar, Nozzle, Lamp, RecordLamp;
            public Material Frame, PegBass, PegStrings, StringBass, StringStrings;
            public Material Bar, Resonator, Rail;
            public Material Shell, Head, Cymbal;
            public Material GuitarTop, GuitarSides, Neck, SoundHole, PegGuitar, StringGuitar;
            public Material GlockBar, Chime, GongMetal;
            public Material FlashBass, FlashMallets, FlashStrings, FlashDrums, FlashGuitar, FlashGlock, FlashChimes;
            public Material Trail, Ray, Cursor, Shadow;
            public Material[] Balls;
        }

        static string MaterialPath(string name) => $"{k_Art}/Materials/M_Rig_{name}.mat";

        static Palette MakePalette(Shader beam, Shader shadow)
        {
            var p = new Palette
            {
                Wall = Unlit("Wall", new Color(0.012f, 0.016f, 0.028f)),
                Floor = Unlit("Floor", new Color(0.12f, 0.13f, 0.155f)),
                Grid = Glow("Grid", beam, new Color(0.05f, 0.08f, 0.14f), 2f, 0f, false),
                Lane = Glow("Lane", beam, new Color(0.08f, 0.35f, 0.5f), 2f, 0.2f, false),

                Trough = Lit("Trough", new Color(0.09f, 0.1f, 0.12f), 0.5f),
                Intake = Lit("Intake", new Color(0.02f, 0.02f, 0.025f), 0.3f),
                Cabinet = Lit("Cabinet", new Color(0.1f, 0.11f, 0.14f), 0.55f),
                Pipe = Metal("Pipe", new Color(0.78f, 0.52f, 0.26f), 0.8f, 0.6f),
                Collar = Lit("Collar", new Color(0.2f, 0.2f, 0.22f), 0.6f, new Color(0.2f, 0.2f, 0.2f)),
                Nozzle = Metal("Chrome", new Color(0.72f, 0.75f, 0.78f), 0.85f, 0.6f),
                Lamp = Lit("Lamp", new Color(0.3f, 0.3f, 0.28f), 0.7f, new Color(0.05f, 0.05f, 0.04f)),
                RecordLamp = Lit("Record_Lamp", new Color(0.3f, 0.06f, 0.05f), 0.7f, new Color(0.05f, 0.01f, 0.01f)),

                Frame = Lit("Frame", new Color(0.13f, 0.14f, 0.17f), 0.5f),
                PegBass = Lit("Peg_Bass", new Color(0.4f, 0.25f, 0.1f), 0.6f, k_Bass * 0.3f),
                PegStrings = Lit("Peg_Strings", new Color(0.4f, 0.12f, 0.3f), 0.6f, k_Strings * 0.3f),
                StringBass = Glow("String_Bass", beam, k_Bass * 2.2f, 2f, 0.5f, false),
                StringStrings = Glow("String_Strings", beam, k_Strings * 2.2f, 2f, 0.5f, false),

                Bar = Lit("Bar", new Color(0.36f, 0.15f, 0.07f), 0.55f, new Color(0.02f, 0.06f, 0.05f)),
                Resonator = Metal("Resonator", new Color(0.62f, 0.48f, 0.22f), 0.75f, 0.5f),
                Rail = Lit("Rail", new Color(0.08f, 0.08f, 0.09f), 0.4f),

                Shell = Lit("Shell", new Color(0.07f, 0.11f, 0.3f), 0.8f),
                Head = Lit("Head", new Color(0.82f, 0.8f, 0.74f), 0.3f, new Color(0.03f, 0.03f, 0.04f)),
                Cymbal = Metal("Cymbal", new Color(0.72f, 0.54f, 0.22f), 0.85f, 0.55f),

                GuitarTop = Lit("Guitar_Top", new Color(0.62f, 0.44f, 0.24f), 0.6f),
                GuitarSides = Lit("Guitar_Sides", new Color(0.2f, 0.09f, 0.04f), 0.65f),
                Neck = Lit("Neck", new Color(0.09f, 0.05f, 0.03f), 0.5f),
                SoundHole = Lit("Sound_Hole", new Color(0.01f, 0.01f, 0.01f), 0.1f),
                PegGuitar = Lit("Peg_Guitar", new Color(0.2f, 0.36f, 0.14f), 0.6f, k_Guitar * 0.3f),
                StringGuitar = Glow("String_Guitar", beam, k_Guitar * 2.2f, 2f, 0.5f, false),

                GlockBar = Metal("Glock_Bar", new Color(0.72f, 0.74f, 0.8f), 0.85f, 0.8f, new Color(0.03f, 0.02f, 0.05f)),
                Chime = Metal("Chime", new Color(0.8f, 0.62f, 0.3f), 0.85f, 0.7f, new Color(0.04f, 0.03f, 0.01f)),
                GongMetal = Metal("Gong", new Color(0.55f, 0.36f, 0.16f), 0.75f, 0.75f, new Color(0.03f, 0.015f, 0.005f)),

                FlashBass = Glow("Flash_Bass", beam, k_Bass, 1.6f, 0.15f, true),
                FlashMallets = Glow("Flash_Mallets", beam, k_Mallets, 1.6f, 0.15f, true),
                FlashStrings = Glow("Flash_Strings", beam, k_Strings, 1.6f, 0.15f, true),
                FlashDrums = Glow("Flash_Drums", beam, k_Drums, 1.4f, 0.1f, true),
                FlashGuitar = Glow("Flash_Guitar", beam, k_Guitar, 1.6f, 0.15f, true),
                FlashGlock = Glow("Flash_Glock", beam, k_Glock, 1.4f, 0.1f, true),
                FlashChimes = Glow("Flash_Chimes", beam, k_Chimes, 1.6f, 0.15f, true),

                Trail = Glow("Trail", beam, new Color(1.6f, 1.6f, 1.6f), 1.5f, 0.3f, false),
                Ray = Glow("Ray", beam, new Color(0.7f, 0.95f, 1.2f), 2f, 0.6f, false),
                Cursor = Lit("Cursor", new Color(0.9f, 0.95f, 1f), 0.5f, new Color(1.5f, 1.7f, 2f)),
                Shadow = ShadowMaterial("Shadow", shadow),
            };

            // In RigGroup order.
            p.Balls = new[]
            {
                Ball("Ball_Bass", k_Bass),
                Ball("Ball_Mallets", k_Mallets),
                Ball("Ball_Strings", k_Strings),
                Ball("Ball_Drums", k_Drums),
                Ball("Ball_Guitar", k_Guitar),
                Ball("Ball_Chimes", k_Chimes),
                Ball("Ball_Glockenspiel", k_Glock),
            };

            foreach (var field in typeof(Palette).GetFields())
            {
                if (field.FieldType == typeof(Material) && field.GetValue(p) == null)
                {
                    Debug.LogError($"[{k_Tag}] Could not make the {field.Name} material; the rig would render pink.");
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

        static Material Metal(string name, Color colour, float smoothness, float metallic, Color emission = default)
        {
            var material = Lit(name, colour, smoothness, emission);
            if (material != null && material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>A glossy ball, glowing faintly in its group's colour so it reads against the dark at any distance.</summary>
        static Material Ball(string name, Color colour)
        {
            var material = Lit(name, Color.Lerp(colour, Color.white, 0.25f), 0.9f, colour * 0.6f);
            if (material != null && material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.3f);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>URP Unlit, opaque: the walls and floor, which are a backdrop, not lit surfaces.</summary>
        static Material Unlit(string name, Color colour)
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

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The visualizer's additive glow: along a line's or quad's V for the strings, lanes and trails, or round for the flashes.</summary>
        static Material Glow(string name, Shader shader, Color colour, float softness, float core, bool radial)
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

            material.SetColor("_Color", colour);
            material.SetFloat("_Softness", softness);
            material.SetFloat("_Core", core);
            material.SetFloat("_Radial", radial ? 1f : 0f);
            if (radial)
                material.EnableKeyword("_RADIAL");
            else
                material.DisableKeyword("_RADIAL");

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        static Material ShadowMaterial(string name, Shader shader)
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

            material.SetColor("_Color", new Color(0f, 0f, 0f, 0.8f));
            material.SetFloat("_Softness", 1.6f);

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }
    }
}
