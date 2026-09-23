using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.Visualizer;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the room audio visualizer: the analyser and its music source, a water-tube panel on
    /// each of the four projected screens for the 2D mode, the laser rig and three waveform
    /// ribbons, the three point clouds (the spectrum field, the astral body, the fireflies), a status line on
    /// the front wall, and the <see cref="RoomVisualizer"/> that switches between them. Everything lives under <c>CAVE_User_Room/Room_Visualizer</c> and
    /// is registered with Undo; the <see cref="RoomVisualizer"/> on the root is reused, so its
    /// Inspector settings survive a rebuild.
    /// </summary>
    /// <remarks>
    /// The panels are placed from <see cref="CaveScreens"/>, which is the CAVE's <c>.vrx</c> in
    /// Unity's frame, not from the room model's wall quads — the side screens are wider than the
    /// floor is deep, and a panel sized to the quads would leave a stripe of the side walls dark.
    /// </remarks>
    static class RoomVisualizerBuilder
    {
        const string k_Tag = "Visualizer";
        const string k_CreateMenu = "VR Shooting Gallery/Visualizer/Create Visualizer Scene";
        const string k_BuildMenu = "VR Shooting Gallery/Visualizer/Rebuild Visualizer";
        const string k_ScenePath = "Assets/_Project/Scenes/Demos/Visualizer MiddleVR.unity";

        const string k_Root = "Room_Visualizer";
        const string k_AnalyserName = "Analyser";
        const string k_MusicName = "Music";
        const string k_SurfacesName = "Surfaces_2D";
        const string k_LasersName = "Lasers_3D";
        const string k_WaveformsName = "Waveforms_3D";
        const string k_CloudName = "PointCloud_3D";
        const string k_BodyName = "AstralBody_3D";
        const string k_FirefliesName = "Fireflies_3D";
        const string k_SignName = "Status_Sign";

        const string k_MusicFolder = RoomVisualizer.MusicFolder;
        const string k_WaterShaderPath = "Assets/_Project/Art/Shaders/WaterTubes.shader";
        const string k_BeamShaderPath = "Assets/_Project/Art/Shaders/LaserBeam.shader";
        const string k_WaterMaterialPath = "Assets/_Project/Art/Materials/M_Visualizer_Water.mat";
        const string k_BeamMaterialPath = "Assets/_Project/Art/Materials/M_Visualizer_Beam.mat";
        const string k_SpotMaterialPath = "Assets/_Project/Art/Materials/M_Visualizer_Spot.mat";
        const string k_RibbonMaterialPath = "Assets/_Project/Art/Materials/M_Visualizer_Ribbon.mat";
        const string k_PointMaterialPath = "Assets/_Project/Art/Materials/M_Visualizer_Point.mat";

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Metres the panels sit inside the screen planes. Enough to draw over the room model's
        /// wall quads, far too little to read as depth from anywhere in the room.
        /// </summary>
        const float k_PanelInset = 0.005f;

        /// <summary>Tubes per screen. The front and floor are wider than the sides; the pitch comes out near 15 cm on all four.</summary>
        const int k_FrontTubes = 24;
        const int k_SideTubes = 16;
        const int k_FloorTubes = 24;

        /// <summary>
        /// Where the waveform ribbons hang. Across the room at chest height, front to back a little
        /// higher, and one diagonal from the floor's back-left corner up toward the front-right, so
        /// there is a trace in the volume whichever wall a viewer faces.
        /// </summary>
        static readonly (string name, Vector3 from, Vector3 to, Vector3 axis, Vector3 second, Color colour)[] k_Ribbons =
        {
            ("Ribbon_Across", new Vector3(-1.7f, 1.3f, 0.2f), new Vector3(1.7f, 1.3f, 0.2f),
                Vector3.up, Vector3.forward * 0.5f, new Color(0.4f, 0.9f, 1f)),
            ("Ribbon_Deep", new Vector3(0f, 1.65f, -1.05f), new Vector3(0f, 1.65f, 1.05f),
                Vector3.right, Vector3.up * 0.5f, new Color(1f, 0.45f, 0.9f)),
            ("Ribbon_Diagonal", new Vector3(-1.5f, 0.35f, -1.0f), new Vector3(1.5f, 1.9f, 1.0f),
                new Vector3(0.6f, -0.35f, -0.4f), Vector3.zero, new Color(0.5f, 1f, 0.6f)),
        };

        static readonly Color k_SignColour = new(0.5f, 0.56f, 0.64f);

        // ================================================================== menus

        [MenuItem(k_CreateMenu, false, 600)]
        static void CreateVisualizerScene()
        {
            CreateScene(k_Tag, k_ScenePath, "visualizer", Build,
                "Drop a track into Assets/_Project/Audio/Music and enter play mode: M steps through the " +
                "water walls, the lasers, the point cloud, the astral body and the fireflies; L toggles low " +
                "intensity, N skips, " +
                "Space pauses.");
        }

        [MenuItem(k_BuildMenu, false, 610)]
        static void Rebuild() => RebuildInOpenScene(Build);

        // ================================================================== build

        static bool Build(Scene scene)
        {
            var room = RequireRoom(scene, k_Tag, "visualizer");
            if (room == null)
                return false;

            var waterShader = AssetDatabase.LoadAssetAtPath<Shader>(k_WaterShaderPath);
            var beamShader = AssetDatabase.LoadAssetAtPath<Shader>(k_BeamShaderPath);
            if (waterShader == null || beamShader == null)
            {
                Debug.LogError($"[{k_Tag}] Missing '{k_WaterShaderPath}' or '{k_BeamShaderPath}'.");
                return false;
            }

            Undo.SetCurrentGroupName("Build Room Visualizer");
            int group = Undo.GetCurrentGroup();

            var root = FindOrCreateInstrument(room, k_Root, Vector3.zero, k_Tag);
            Clear(root);

            var water = ShaderMaterial(k_WaterMaterialPath, waterShader, m => { });
            var beam = ShaderMaterial(k_BeamMaterialPath, beamShader, m =>
            {
                m.SetFloat("_Softness", 2.5f);
                m.SetFloat("_Core", 0.6f);
            });
            var spot = ShaderMaterial(k_SpotMaterialPath, beamShader, m =>
            {
                m.SetFloat("_Softness", 3f);
                m.SetFloat("_Core", 0.5f);
                m.SetFloat("_Radial", 1f);
                m.EnableKeyword("_RADIAL");
            });
            var ribbon = ShaderMaterial(k_RibbonMaterialPath, beamShader, m =>
            {
                m.SetFloat("_Softness", 1.8f);
                m.SetFloat("_Core", 0.7f);
            });
            var point = ShaderMaterial(k_PointMaterialPath, beamShader, m =>
            {
                m.SetFloat("_Softness", 2.2f);
                m.SetFloat("_Core", 0.35f);
                m.SetFloat("_Radial", 1f);
                m.EnableKeyword("_RADIAL");
            });

            var playlist = Playlist();
            var (analyser, music) = BuildAnalyser(root, playlist);
            var panels = BuildPanels(NewChild(root, k_SurfacesName), analyser, water);
            var lasers = BuildLasers(NewChild(root, k_LasersName), analyser, beam, spot);
            var ribbons = BuildRibbons(NewChild(root, k_WaveformsName), analyser, ribbon);
            var cloud = BuildCloud(root, k_CloudName, CloudShape.SpectrumField, analyser, point);
            var body = BuildCloud(root, k_BodyName, CloudShape.AstralBody, analyser, point);
            var flies = BuildCloud(root, k_FirefliesName, CloudShape.Fireflies, analyser, point);
            var sign = BuildSign(root);

            Wire(root, analyser, music, playlist, panels, lasers, ribbons, cloud, body, flies, sign);
            DarkenCamera(scene);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;

            Debug.Log($"[{k_Tag}] Built the visualizer: {panels.Count} water panels, {ribbons.Count} ribbons, " +
                      $"three point clouds, {playlist.Count} track(s) in the playlist.", root);

            if (playlist.Count == 0)
                Debug.LogWarning($"[{k_Tag}] No audio clips in '{k_MusicFolder}'. Drop a track there and press Play — " +
                                 "the folder is read again each time play starts — or drag one onto Room_Visualizer's " +
                                 "Playlist.", root);

            return true;
        }

        // ---------------------------------------------------------------- audio

        static (AudioAnalyser, AudioSource) BuildAnalyser(Transform root, List<AudioClip> playlist)
        {
            var analyserRoot = NewChild(root, k_AnalyserName);
            var analyser = Undo.AddComponent<AudioAnalyser>(analyserRoot.gameObject);

            // Straight to the speakers: the room's mix is the track as mastered, and the deck's
            // idea of a spatial source that follows the tracked user is a later experiment.
            var musicRoot = NewChild(analyserRoot, k_MusicName);
            var music = Undo.AddComponent<AudioSource>(musicRoot.gameObject);
            music.playOnAwake = false;
            music.loop = false;
            music.spatialBlend = 0f;
            music.dopplerLevel = 0f;
            music.priority = 0;
            music.clip = playlist.Count > 0 ? playlist[0] : null;

            var so = new SerializedObject(analyser);
            SetRef(k_Tag, so, "m_Source", music);
            so.ApplyModifiedProperties();

            return (analyser, music);
        }

        /// <summary>Every clip in the music folder, by name — the same scan the visualizer runs when play starts.</summary>
        static List<AudioClip> Playlist() => new(RoomVisualizer.ClipsInMusicFolder());

        // ---------------------------------------------------------------- 2D

        /// <summary>
        /// One quad per projected screen, sized and placed from the CAVE config. The front and the
        /// floor mirror the spectrum about their centre so the bass sits in the middle of the room;
        /// the sides run it back to front, the right wall reversed, so the treble on all four meets
        /// at the front corners.
        /// </summary>
        static List<WaterTubePanel> BuildPanels(Transform parent, AudioAnalyser analyser, Material material)
        {
            var panels = new List<WaterTubePanel>
            {
                Panel(parent, CaveScreens.Front, k_FrontTubes, TubeLayout.Mirrored, analyser, material),
                Panel(parent, CaveScreens.Left, k_SideTubes, TubeLayout.Ascending, analyser, material),
                Panel(parent, CaveScreens.Right, k_SideTubes, TubeLayout.Descending, analyser, material),
                Panel(parent, CaveScreens.Floor, k_FloorTubes, TubeLayout.Mirrored, analyser, material),
            };

            return panels;
        }

        static WaterTubePanel Panel(Transform parent, CaveScreen screen, int tubes, TubeLayout layout,
            AudioAnalyser analyser, Material material)
        {
            var quad = Primitive(parent, PrimitiveType.Quad, $"Panel_{screen.Name}", material);
            quad.localPosition = screen.Point(0.5f, 0.5f, k_PanelInset);
            quad.localRotation = screen.FacingRotation;
            quad.localScale = new Vector3(screen.Width, screen.Height, 1f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var panel = Undo.AddComponent<WaterTubePanel>(quad.gameObject);
            var so = new SerializedObject(panel);
            SetRef(k_Tag, so, "m_Analyser", analyser);
            SetInt(k_Tag, so, "m_TubeCount", tubes);
            SetEnum(so, "m_Layout", (int)layout);
            SetVector2(so, "m_PanelSize", new Vector2(screen.Width, screen.Height));
            so.ApplyModifiedProperties();

            return panel;
        }

        // ---------------------------------------------------------------- 3D

        static LaserShow BuildLasers(Transform parent, AudioAnalyser analyser, Material beam, Material spot)
        {
            var lasers = Undo.AddComponent<LaserShow>(parent.gameObject);
            var so = new SerializedObject(lasers);
            SetRef(k_Tag, so, "m_Analyser", analyser);
            SetRef(k_Tag, so, "m_BeamMaterial", beam);
            SetRef(k_Tag, so, "m_SpotMaterial", spot);
            so.ApplyModifiedProperties();
            return lasers;
        }

        static List<WaveformRibbon> BuildRibbons(Transform parent, AudioAnalyser analyser, Material material)
        {
            var ribbons = new List<WaveformRibbon>(k_Ribbons.Length);

            foreach (var (name, from, to, axis, second, colour) in k_Ribbons)
            {
                var go = new GameObject(name, typeof(LineRenderer));
                Undo.RegisterCreatedObjectUndo(go, "Create waveform ribbon");
                go.transform.SetParent(parent, false);

                var line = go.GetComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.numCapVertices = 4;
                line.numCornerVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = material;
                line.startColor = line.endColor = Color.white;
                line.positionCount = 2;
                line.SetPositions(new[] { from, to });

                // Tapered ends, so a trace fades into the wall rather than stopping dead on it.
                line.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(0.88f, 1f), new Keyframe(1f, 0f));

                var component = Undo.AddComponent<WaveformRibbon>(go);
                var so = new SerializedObject(component);
                SetRef(k_Tag, so, "m_Analyser", analyser);
                SetVector3(so, "m_From", from);
                SetVector3(so, "m_To", to);
                SetVector3(so, "m_Axis", axis);
                SetVector3(so, "m_SecondAxis", second);
                SetColor(k_Tag, so, "m_Color", colour);
                so.ApplyModifiedProperties();

                ribbons.Add(component);
            }

            return ribbons;
        }

        /// <summary>
        /// A particle system the cloud drives by hand, so it is built quiet: nothing emits, nothing
        /// plays on awake, and the scene view does not fill with default particles. The cloud sets
        /// the rest of the system up at run time from its own settings.
        /// </summary>
        static AudioPointCloud BuildCloud(Transform parent, string name, CloudShape shape, AudioAnalyser analyser,
            Material material)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            Undo.RegisterCreatedObjectUndo(go, "Create point cloud");
            go.transform.SetParent(parent, false);

            var system = go.GetComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = 16384;

            var emission = system.emission;
            emission.enabled = false;

            var systemShape = system.shape;
            systemShape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var cloud = Undo.AddComponent<AudioPointCloud>(go);
            var so = new SerializedObject(cloud);
            SetRef(k_Tag, so, "m_Analyser", analyser);
            SetEnum(so, "m_Shape", (int)shape);
            so.ApplyModifiedProperties();

            return cloud;
        }

        // ---------------------------------------------------------------- sign

        /// <summary>
        /// A status line along the bottom of the front wall. Text reads correctly when its forward
        /// points away from the viewer, and the viewer is on the −Z side of the front wall, so the
        /// default rotation is already right.
        /// </summary>
        static TMPro.TextMeshPro BuildSign(Transform root)
        {
            var position = CaveScreens.Front.Point(0.5f, 0.03f, k_PanelInset * 2f);
            return Text(k_Tag, root, k_SignName, position, 0.7f, "", k_SignColour,
                new Vector2(CaveScreens.FrontWidth - 0.2f, 0.2f));
        }

        // ---------------------------------------------------------------- wiring

        static void Wire(Transform root, AudioAnalyser analyser, AudioSource music, List<AudioClip> playlist,
            List<WaterTubePanel> panels, LaserShow lasers, List<WaveformRibbon> ribbons, AudioPointCloud cloud,
            AudioPointCloud body, AudioPointCloud flies, TMPro.TextMeshPro sign)
        {
            // Reused rather than re-added, so the mode, the buttons, the auto-cycle and anything else
            // tuned in the Inspector survive a rebuild.
            var visualizer = root.GetComponent<RoomVisualizer>();
            if (visualizer == null)
                visualizer = Undo.AddComponent<RoomVisualizer>(root.gameObject);

            var so = new SerializedObject(visualizer);
            SetRef(k_Tag, so, "m_Analyser", analyser);
            SetRef(k_Tag, so, "m_Music", music);
            SetArray(k_Tag, so, "m_Panels", panels);
            SetRef(k_Tag, so, "m_Lasers", lasers);
            SetArray(k_Tag, so, "m_Ribbons", ribbons);
            SetRef(k_Tag, so, "m_PointCloud", cloud);
            SetRef(k_Tag, so, "m_AstralBody", body);
            SetRef(k_Tag, so, "m_Fireflies", flies);
            SetRef(k_Tag, so, "m_Sign", sign);

            // The music folder is the playlist's source of truth when it has anything in it; an
            // empty folder leaves whatever was dragged onto the component by hand.
            if (playlist.Count > 0)
                SetArray(k_Tag, so, "m_Playlist", playlist);

            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// A black clear colour. The panels cover every projected screen, so the only place the
        /// camera's background can show is a seam, and a seam should be dark. MiddleVR clones this
        /// camera's settings onto its CAVE cameras.
        /// </summary>
        static void DarkenCamera(Scene scene)
        {
            var cameraRoot = FindRoot(scene, "MainCamera");
            var camera = cameraRoot != null ? cameraRoot.GetComponent<Camera>() : null;
            if (camera == null)
                return;

            Undo.RecordObject(camera, "Darken visualizer camera");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            EditorUtility.SetDirty(camera);
        }

        // ================================================================== materials

        /// <summary>
        /// A material on one of the visualizer's own shaders, re-authored on every build like the
        /// instrument builders' materials: the look lives in the shader's defaults and the constants
        /// here, and a material left over from an earlier build is a bug, not a tweak.
        /// </summary>
        static Material ShaderMaterial(string path, Shader shader, System.Action<Material> configure)
        {
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

            AssetDatabase.SaveAssets();
            return material;
        }

        // ================================================================== serialized fields

        static void SetEnum(SerializedObject so, string path, int value)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
            else
                property.enumValueIndex = value;
        }

        static void SetVector2(SerializedObject so, string path, Vector2 value)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
            else
                property.vector2Value = value;
        }

        static void SetVector3(SerializedObject so, string path, Vector3 value)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[{k_Tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
            else
                property.vector3Value = value;
        }
    }
}
