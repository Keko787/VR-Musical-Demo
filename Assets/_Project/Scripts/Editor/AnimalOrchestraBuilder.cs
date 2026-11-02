using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRShootingGallery.AnimalOrchestra;
using VRShootingGallery.TrackingDemo;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the Animal Orchestra: a hands-off, self-playing performance in which every part of a song
    /// is played by an animal that moves to that part's notes and stem. Six animals on the CAVE's
    /// surfaces, readable without glasses; five in a bounded volume within arm's reach of the tracked
    /// viewer; a dusk sky behind and water underfoot. Follows "Animal Orchestra: CAVE Build and Plan".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The layout is the plan's. Large or numerous animals go on surfaces, each big one anchored to the
    /// middle of its wall and never across a corner: the elephant (kick) on the front wall with the
    /// starlings (arpeggio) in the band above it, the gorilla (snare) on the left wall, the wolves (choir)
    /// on a ridge along the right wall, the whale (sub bass) in the water on the floor, and fireflies
    /// (texture) everywhere. Small and medium ones are stereo geometry in front of the tracked viewer's
    /// default spot: the parrot (vocal) at eye level nearest the front wall, the bullfrog (bass) left and
    /// the woodpecker (hats) right at waist height, jellyfish (pads) overhead, and the octopus (keys) low
    /// in the centre on a pedestal. Nothing is placed beyond the walls; the room is the stage, and the
    /// ensemble faces the front wall with the audience at the open back.
    /// </para>
    /// <para>
    /// The scene is a copy of the physics scene for the reason every demo here is: it already carries the
    /// whole MiddleVR side of the setup. Each animal builds its own body (<see cref="Performer.Build"/>); this
    /// places them, wires them to the conductor, and checks them against the room — inside it, clear of
    /// the corners, and visible from the tracked viewer's eyes — as the plan's risk table asks.
    /// </para>
    /// </remarks>
    static class AnimalOrchestraBuilder
    {
        const string k_CreateMenu = "VR-Music-Demo/Animal Orchestra/Create Animal Orchestra Scene";
        const string k_BuildMenu = "VR-Music-Demo/Animal Orchestra/Rebuild Animal Orchestra";

        const string k_TemplateScene = "Assets/_Project/Scenes/Demos/Physics MiddleVR.unity";
        const string k_ScenePath = "Assets/_Project/Scenes/Demos/AnimalOrchestra.unity";

        const string k_RoomRoot = "CAVE_User_Room";
        const string k_DemoRoot = "Animal_Orchestra";
        const string k_GroundName = "Ground";
        const string k_LogTag = "[AnimalOrchestra]";

        const string k_Materials = "Assets/_Project/Art/Materials/AnimalOrchestra/";
        const string k_Meshes = "Assets/_Project/Art/Meshes/AnimalOrchestra/";

        static readonly Color k_AmbientSky = new(0.42f, 0.36f, 0.54f);
        static readonly Color k_AmbientEquator = new(0.38f, 0.27f, 0.31f);
        static readonly Color k_AmbientGround = new(0.10f, 0.10f, 0.17f);

        /// <summary>
        /// The key light: cool moonlight from behind the viewer, high and to the right, so every animal's
        /// face is lit as the audience sees it. The warm side of the picture is the shader's rim light —
        /// the sunset glow behind the animals, on the front wall.
        /// </summary>
        static readonly Vector3 k_LightEuler = new(37f, -26f, 0f);
        static readonly Color k_LightColour = new(0.82f, 0.86f, 1.00f);
        const float k_LightIntensity = 1.05f;

        /// <summary>Where the tracked viewer stands by default, room frame: a little behind centre, so the inner volume is in front.</summary>
        const float k_ViewerZ = -0.35f;
        const float k_ViewerEyeHeight = 1.6f;

        /// <summary>Water cells: the resolution of VR-Demo's head tracking demo, where the water comes from.</summary>
        const float k_WaterCell = 0.025f;

        // Only used when a wall is missing from the room — the CAVE as this project ships it.
        const float k_FallbackLeftX = -1.778f;
        const float k_FallbackRightX = 1.778f;
        const float k_FallbackFrontZ = 1.1113f;
        const float k_FallbackBackZ = -1.1113f;
        const float k_FallbackHeight = 2.2225f;

        /// <summary>Where each animal stands and which way it faces. Positions are in the room frame.</summary>
        enum Facing
        {
            /// <summary>Identity: the performer works in the room frame itself.</summary>
            Room,

            /// <summary>Local +Z toward the tracked viewer's default spot.</summary>
            Viewer,

            /// <summary>Local +Z across the viewer's line of sight, so the animal is seen in profile.</summary>
            Profile,

            /// <summary>An explicit yaw, degrees.</summary>
            Yaw,
        }

        readonly struct Seat
        {
            public readonly string Name;
            public readonly System.Type Type;
            public readonly Part Part;
            public readonly bool Inner;
            public readonly System.Func<RoomBox, Vector3> Position;
            public readonly Facing Facing;
            public readonly float Yaw;

            public Seat(string name, System.Type type, Part part, bool inner, System.Func<RoomBox, Vector3> position,
                Facing facing, float yaw = 0f)
            {
                Name = name;
                Type = type;
                Part = part;
                Inner = inner;
                Position = position;
                Facing = facing;
                Yaw = yaw;
            }
        }

        /// <summary>The cast, in the plan's order: six on the surfaces, five in the volume.</summary>
        static readonly Seat[] k_Cast =
        {
            new("Elephant", typeof(ElephantPerformer), Part.Kick, false, b => new Vector3(b.CentreX, 0f, b.FrontZ - 0.35f), Facing.Yaw, 180f),
            new("Whale", typeof(WhalePerformer), Part.SubBass, false, b => Vector3.zero, Facing.Room),
            new("Gorilla", typeof(GorillaPerformer), Part.Snare, false, b => new Vector3(b.LeftX + 0.378f, 0f, 0f), Facing.Yaw, 90f),
            new("Wolf_Pack", typeof(WolfPackPerformer), Part.Choir, false, b => new Vector3(b.RightX, 0f, 0f), Facing.Room),
            new("Starling_Flock", typeof(StarlingFlock), Part.Arp, false, b => Vector3.zero, Facing.Room),
            new("Fireflies", typeof(FireflySwarm), Part.Texture, false, b => Vector3.zero, Facing.Room),

            new("Parrot", typeof(ParrotPerformer), Part.Vocal, true, b => new Vector3(-0.30f, 0f, 0.50f), Facing.Viewer),
            new("Bullfrog", typeof(BullfrogPerformer), Part.Bass, true, b => new Vector3(-0.62f, 0f, 0.05f), Facing.Viewer),
            new("Woodpecker", typeof(WoodpeckerPerformer), Part.HiHat, true, b => new Vector3(0.66f, 0f, 0.12f), Facing.Profile),
            new("Jellyfish", typeof(JellyfishPerformer), Part.Pads, true, b => Vector3.zero, Facing.Room),
            new("Octopus", typeof(OctopusPerformer), Part.Keys, true, b => new Vector3(0.10f, 0f, 0.15f), Facing.Viewer),
        };

        // ================================================================== menus

        [MenuItem(k_CreateMenu, false, 900)]
        static void CreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_TemplateScene) == null)
            {
                Debug.LogError($"{k_LogTag} Missing the template scene '{k_TemplateScene}'.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_ScenePath) != null &&
                !EditorUtility.DisplayDialog("Rebuild the Animal Orchestra scene?",
                    $"'{k_ScenePath}' already exists and will be overwritten with a fresh copy of the " +
                    "physics scene plus a newly built orchestra.\n\nAnything you added to it by hand is lost.",
                    "Overwrite", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(k_TemplateScene, OpenSceneMode.Single);

            // Saved to the new path before anything is touched, so the template on disk is never at
            // risk from a build that goes wrong half way through.
            if (!EditorSceneManager.SaveScene(scene, k_ScenePath))
            {
                Debug.LogError($"{k_LogTag} Could not save the new scene to '{k_ScenePath}'.");
                return;
            }

            scene = SceneManager.GetActiveScene();
            StripTemplate(scene);

            if (!Build(scene))
                return;

            EditorSceneManager.SaveScene(scene);
            RegisterInBuildSettings();

            Debug.Log($"{k_LogTag} Built '{scene.name}'. Enter play mode: fireflies fill the room while the first song " +
                      "renders, then the orchestra plays. Wand 3 (N) skips a song, wand 2 (L) is low intensity.");
        }

        [MenuItem(k_BuildMenu, false, 910)]
        static void Rebuild()
        {
            var scene = SceneManager.GetActiveScene();
            if (Build(scene))
                EditorSceneManager.MarkSceneDirty(scene);
        }

        // ================================================================== build

        static bool Build(Scene scene)
        {
            var room = FindRoot(scene, k_RoomRoot);
            if (room == null)
            {
                Debug.LogError($"{k_LogTag} No '{k_RoomRoot}' root in '{scene.name}'. Open the Animal Orchestra scene " +
                               "(or any scene with the CAVE room) and try again.");
                return false;
            }

            var kit = Kit(room);
            var waterMaterial = Material("M_OrchestraWater", "VRMusic/Ripple Floor", WaterColours);
            var skyMaterial = Material("M_DuskSky", "VRMusic/Dusk Skybox");
            if (kit == null || waterMaterial == null || skyMaterial == null)
                return false;

            Undo.SetCurrentGroupName("Build Animal Orchestra");
            int group = Undo.GetCurrentGroup();

            var root = FindOrCreate(room, k_DemoRoot);
            Clear(root);

            var conductor = root.GetComponent<OrchestraConductor>();
            if (conductor == null)
                conductor = Undo.AddComponent<OrchestraConductor>(root.gameObject);

            kit.Floor = BuildWater(root, kit.Box, waterMaterial);

            var performers = new List<(Seat seat, Performer performer)>();
            foreach (var seat in k_Cast)
                performers.Add((seat, Place(root, seat, kit, conductor)));

            HideRoomShell(room, root);
            HideGround(scene);
            BuildSky(scene, skyMaterial);
            Moonlight(scene);

            var box = kit.Box;

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root.gameObject;

            Check(room, kit, performers);

            Debug.Log($"{k_LogTag} Room {box.Width:0.###} × {box.Height:0.###} × {box.Depth:0.###} m; {performers.Count} performers, " +
                      $"the viewer's default eye at ({kit.ViewerEye.x:0.##}, {kit.ViewerEye.y:0.##}, {kit.ViewerEye.z:0.##}).", root);
            return true;
        }

        /// <summary>One animal: an empty object in its seat, the performer on it, its body built by the performer itself.</summary>
        static Performer Place(Transform root, Seat seat, CreatureKit kit, OrchestraConductor conductor)
        {
            var go = new GameObject(seat.Name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = seat.Position(kit.Box);
            go.transform.localRotation = Quaternion.Euler(0f, Yaw(seat, kit, go.transform.localPosition), 0f);

            var performer = (Performer)go.AddComponent(seat.Type);
            performer.Configure(conductor, seat.Part);
            performer.Build(kit);
            EditorUtility.SetDirty(performer);

            Undo.RegisterCreatedObjectUndo(go, "Build performer");
            return performer;
        }

        static float Yaw(Seat seat, CreatureKit kit, Vector3 position)
        {
            var toViewer = kit.ViewerEye - position;
            toViewer.y = 0f;

            switch (seat.Facing)
            {
                case Facing.Viewer:
                    return Mathf.Atan2(toViewer.x, toViewer.z) * Mathf.Rad2Deg;

                case Facing.Profile:
                {
                    // Across the line of sight, turned so the animal sits on the room-centre side of its
                    // perch (the woodpecker clings to the −Z face of its trunk).
                    var across = new Vector3(-toViewer.z, 0f, toViewer.x);
                    if (across.x < 0f)
                        across = -across;
                    return Mathf.Atan2(across.x, across.z) * Mathf.Rad2Deg;
                }

                case Facing.Yaw:
                    return seat.Yaw;

                default:
                    return 0f;
            }
        }

        // ================================================================== kit

        static CreatureKit Kit(Transform room)
        {
            var toon = Material("M_ToonCreature", "VRMusic/Toon Creature", m => m.enableInstancing = true);
            var glow = Material("M_GlowSurface", "VRMusic/Glow Surface", m => m.enableInstancing = true);
            var whale = Material("M_WhaleGlow", "VRMusic/Glow Surface", m =>
            {
                m.enableInstancing = true;
                m.SetFloat("_VertexColor", 1f);
                m.SetFloat("_RimStrength", 0.4f);
            });
            var firefly = Material("M_Firefly", "VRMusic/Firefly");
            var starling = Material("M_Starling", "VRMusic/Starling");
            if (toon == null || glow == null || whale == null || firefly == null || starling == null)
                return null;

            var box = ReadRoom(room);
            return new CreatureKit
            {
                Toon = toon,
                Glow = glow,
                Whale = whale,
                Firefly = firefly,
                Starling = starling,
                Sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"),
                Capsule = Resources.GetBuiltinResource<Mesh>("Capsule.fbx"),
                Cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"),
                Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"),
                Cone = SaveMesh(BuildCone(24), "Cone"),
                Bell = SaveMesh(BuildBell(24, 10), "Bell"),
                Room = room,
                Box = box,
                ViewerEye = new Vector3(box.CentreX, k_ViewerEyeHeight, k_ViewerZ),
            };
        }

        /// <summary>A cone along +Y: base diameter 1 at y = 0, apex at y = 1, smooth sides and a capped base.</summary>
        static Mesh BuildCone(int segments)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Side: one ring at the base and one apex vertex per segment, so each face gets its own apex normal.
            float slope = 0.5f;   // base radius over height
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var around = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var normal = (around + Vector3.up * slope).normalized;

                positions.Add(around * 0.5f);
                normals.Add(normal);
                positions.Add(Vector3.up);
                normals.Add(normal);
            }

            for (int i = 0; i < segments; i++)
            {
                int b0 = 2 * i, a0 = b0 + 1, b1 = b0 + 2;
                triangles.Add(b0); triangles.Add(a0); triangles.Add(b1);
            }

            int centre = positions.Count;
            positions.Add(Vector3.zero);
            normals.Add(Vector3.down);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                positions.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.5f);
                normals.Add(Vector3.down);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(centre); triangles.Add(centre + 2 + i); triangles.Add(centre + 1 + i);
            }

            var mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A jellyfish bell: a quarter-ellipse revolved — crown at y = 0.4, rim of diameter 1 at y = 0 — with
        /// the rim flaring out a little, open underneath. One-sided; the glow shader draws both faces.
        /// </summary>
        static Mesh BuildBell(int segments, int rings)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            const float height = 0.4f, radius = 0.5f;

            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings;
                float angle = u * Mathf.PI * 0.5f;
                float flare = u > 0.85f ? (u - 0.85f) / 0.15f : 0f;
                float ringRadius = radius * Mathf.Sin(angle) + 0.02f * flare;
                float y = height * Mathf.Cos(angle) - 0.02f * flare;

                // Normal of the ellipse (x/a)² + (y/b)² = 1 is (x/a², y/b²).
                var profileNormal = new Vector2(Mathf.Sin(angle) / radius, Mathf.Cos(angle) / height).normalized;

                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    var around = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    positions.Add(around * ringRadius + Vector3.up * y);
                    normals.Add((around * profileNormal.x + Vector3.up * profileNormal.y).normalized);
                }
            }

            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int v00 = r * stride + i, v01 = v00 + 1, v10 = v00 + stride, v11 = v10 + 1;
                    triangles.Add(v00); triangles.Add(v01); triangles.Add(v10);
                    triangles.Add(v01); triangles.Add(v11); triangles.Add(v10);
                }
            }

            var mesh = new Mesh { name = "Bell" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ================================================================== water

        /// <summary>
        /// The floor: VR-Demo's head tracking water sheet, the exact size of the floor screen, in dusk
        /// colours. The elephant's stomps and the whale's tail splash it; the ripples run toward the viewer.
        /// </summary>
        static RippleFloor BuildWater(Transform root, RoomBox box, Material material)
        {
            var water = new GameObject("Water").transform;
            water.SetParent(root, false);
            water.localPosition = new Vector3(box.CentreX, 0f, box.CentreZ);

            var filter = water.gameObject.AddComponent<MeshFilter>();
            var renderer = water.gameObject.AddComponent<MeshRenderer>();
            var ripple = water.gameObject.AddComponent<RippleFloor>();

            filter.sharedMesh = SaveMesh(RippleFloor.BuildMesh(box.Width, box.Depth, k_WaterCell, out _, out _), "Water");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var so = new SerializedObject(ripple);
            so.FindProperty("m_Width").floatValue = box.Width;
            so.FindProperty("m_Depth").floatValue = box.Depth;
            so.FindProperty("m_CellSize").floatValue = k_WaterCell;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(water.gameObject, "Build water");
            return ripple;
        }

        static void WaterColours(Material material)
        {
            material.SetColor("_DeepColor", new Color(0.010f, 0.015f, 0.045f));
            material.SetColor("_RestColor", new Color(0.035f, 0.050f, 0.120f));
            material.SetColor("_CrestColor", new Color(0.550f, 0.450f, 0.850f));
            material.SetColor("_FoamColor", new Color(1.000f, 0.860f, 0.760f));
            material.SetFloat("_CrestGlow", 0.5f);
            material.SetFloat("_FresnelStrength", 0.35f);
        }

        // ================================================================== stage

        /// <summary>
        /// Switches off the room's own walls and ceiling — opaque quads in the planes of the screens that
        /// would hide the sky — and logs what it hid. Switched off rather than deleted: they are the template's.
        /// </summary>
        static void HideRoomShell(Transform room, Transform demoRoot)
        {
            var hidden = new List<string>();
            foreach (Transform child in room)
            {
                if (child == demoRoot || !child.gameObject.activeSelf || !child.TryGetComponent(out Renderer _))
                    continue;

                Undo.RecordObject(child.gameObject, "Hide room shell");
                child.gameObject.SetActive(false);
                hidden.Add(child.name);
            }

            if (hidden.Count > 0)
                Debug.Log($"{k_LogTag} Switched off {string.Join(", ", hidden)} — the screens open onto the dusk sky now.", room);
        }

        /// <summary>The template's ground plane sits exactly at the water's rest height and would poke through every trough.</summary>
        static void HideGround(Scene scene)
        {
            var ground = FindRoot(scene, k_GroundName);
            if (ground == null || !ground.gameObject.activeSelf)
                return;

            Undo.RecordObject(ground.gameObject, "Hide ground");
            ground.gameObject.SetActive(false);
            Debug.Log($"{k_LogTag} Switched off '{k_GroundName}' — the water takes its place.", ground);
        }

        /// <summary>
        /// The dusk sky, an ambient light to match, and every camera set to draw it. MiddleVR instantiates
        /// the template camera for every CAVE eye, clear settings and all; every camera is set rather than
        /// the MainCamera-tagged one, because the template's camera is named MainCamera but untagged.
        /// </summary>
        static void BuildSky(Scene scene, Material skyMaterial)
        {
            // Scene settings, not objects, so outside Undo; the scene is marked dirty with the rest.
            RenderSettings.skybox = skyMaterial;

            // A fixed ambient rather than one taken from the skybox: skybox ambient only reaches a build
            // once lighting is generated, and these colours keep the animals' shaded sides readable.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = k_AmbientSky;
            RenderSettings.ambientEquatorColor = k_AmbientEquator;
            RenderSettings.ambientGroundColor = k_AmbientGround;
            DynamicGI.UpdateEnvironment();

            foreach (var go in scene.GetRootGameObjects())
            foreach (var camera in go.GetComponentsInChildren<Camera>(true))
            {
                Undo.RecordObject(camera, "Use skybox");
                camera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        static void Moonlight(Scene scene)
        {
            var light = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(l => l.type == LightType.Directional);
            if (light == null)
            {
                Debug.LogWarning($"{k_LogTag} No directional light in the scene; the animals are lit by ambient only.");
                return;
            }

            Undo.RecordObject(light, "Moonlight");
            Undo.RecordObject(light.transform, "Moonlight");
            light.color = k_LightColour;
            light.intensity = k_LightIntensity;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(k_LightEuler);
        }

        // ================================================================== room check

        /// <summary>
        /// The plan's room rules, checked against what was built: every animal inside the room; the big wall
        /// animals clear of the corners; the inner animals within the plan's 1.5 m and visible from the
        /// tracked viewer's eyes — a point is seen only if the line from the eye through it lands on one of
        /// the four screens, and the CAVE has no ceiling and no back wall. Warnings name the animal and the rule.
        /// </summary>
        static void Check(Transform room, CreatureKit kit, List<(Seat seat, Performer performer)> performers)
        {
            var box = kit.Box;
            var eyes = new List<Vector3>();
            foreach (float dx in new[] { -0.2f, 0f, 0.2f })
            foreach (float dz in new[] { -0.2f, 0f, 0.2f })
            foreach (float height in new[] { 1.5f, 1.6f, 1.75f })
                eyes.Add(new Vector3(box.CentreX + dx, height, k_ViewerZ + dz));

            int warnings = 0;
            foreach (var (seat, performer) in performers)
            {
                if (!TryRoomBounds(room, performer.transform, out var bounds))
                    continue;

                var min = bounds.min;
                var max = bounds.max;
                string extent = $"x {min.x:0.00}…{max.x:0.00}, y {min.y:0.00}…{max.y:0.00}, z {min.z:0.00}…{max.z:0.00}";

                if (min.x < box.LeftX - 0.01f || max.x > box.RightX + 0.01f || max.y > box.Height + 0.01f ||
                    max.z > box.FrontZ + 0.01f || min.y < -0.1f)
                {
                    Debug.LogWarning($"{k_LogTag} {seat.Name} pokes out of the room ({extent}).", performer);
                    warnings++;
                }

                if (!seat.Inner && seat.Facing != Facing.Room)
                {
                    bool frontWall = seat.Part == Part.Kick;
                    bool nearCorner = frontWall
                        ? min.x < box.LeftX + 0.3f || max.x > box.RightX - 0.3f
                        : max.z > box.FrontZ - 0.3f;
                    if (nearCorner)
                    {
                        Debug.LogWarning($"{k_LogTag} {seat.Name} reaches within 30 cm of a corner ({extent}); the plan " +
                                         "anchors big animals to the middle of one wall.", performer);
                        warnings++;
                    }
                }

                if (!seat.Inner)
                    continue;

                float size = Mathf.Max(bounds.size.x, bounds.size.z);
                if (size > 1.5f)
                {
                    Debug.LogWarning($"{k_LogTag} {seat.Name} spans {size:0.00} m; the plan keeps inner animals under 1.5 m.", performer);
                    warnings++;
                }

                int hidden = 0, samples = 0;
                foreach (var point in Samples(bounds))
                foreach (var eye in eyes)
                {
                    samples++;
                    if (!Visible(eye, point, box))
                        hidden++;
                }

                if (hidden > 0)
                {
                    Debug.LogWarning($"{k_LogTag} {seat.Name}: {hidden} of {samples} eye-to-animal sight lines miss every screen " +
                                     $"(they leave through the open top or back), so part of it can vanish for the tracked viewer ({extent}).",
                        performer);
                    warnings++;
                }
            }

            if (warnings == 0)
                Debug.Log($"{k_LogTag} Room check passed: every animal inside the room, the wall animals clear of the corners, " +
                          $"and the inner ones visible from all {eyes.Count} test eyes.");
        }

        /// <summary>The union of the performer's renderers' bounds, in the room frame. Particles and runtime-built meshes have none yet.</summary>
        static bool TryRoomBounds(Transform room, Transform performer, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var renderer in performer.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var local = filter.sharedMesh.bounds;
                var matrix = room.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = matrix.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return any;
        }

        /// <summary>The bounds' centre, its top centre and its four upper corners — where an animal is most likely to leave the screens.</summary>
        static IEnumerable<Vector3> Samples(Bounds b)
        {
            yield return b.center;
            yield return new Vector3(b.center.x, b.max.y, b.center.z);
            yield return new Vector3(b.min.x, b.max.y, b.min.z);
            yield return new Vector3(b.max.x, b.max.y, b.min.z);
            yield return new Vector3(b.min.x, b.max.y, b.max.z);
            yield return new Vector3(b.max.x, b.max.y, b.max.z);
        }

        /// <summary>
        /// True if the line from <paramref name="eye"/> through <paramref name="point"/> reaches a screen — the
        /// front wall, a side wall or the floor — before it leaves the room through the open top or back.
        /// </summary>
        static bool Visible(Vector3 eye, Vector3 point, RoomBox box)
        {
            var d = point - eye;
            if (d.sqrMagnitude < 1e-6f)
                return true;

            float exit = float.MaxValue;
            bool screen = false;

            void Plane(float origin, float direction, float at, bool isScreen)
            {
                if (Mathf.Abs(direction) < 1e-6f)
                    return;
                float t = (at - origin) / direction;
                if (t > 1e-4f && t < exit)
                {
                    exit = t;
                    screen = isScreen;
                }
            }

            if (d.x < 0f) Plane(eye.x, d.x, box.LeftX, true);
            if (d.x > 0f) Plane(eye.x, d.x, box.RightX, true);
            if (d.z > 0f) Plane(eye.z, d.z, box.FrontZ, true);
            if (d.z < 0f) Plane(eye.z, d.z, box.BackZ, false);
            if (d.y < 0f) Plane(eye.y, d.y, 0f, true);
            if (d.y > 0f) Plane(eye.y, d.y, box.Height, false);

            return screen;
        }

        static RoomBox ReadRoom(Transform room)
        {
            float height = k_FallbackHeight;
            var side = room.Find("Left_Wall");
            if (side != null)
                height = side.localPosition.y + 0.5f * side.localScale.y;

            return new RoomBox
            {
                LeftX = WallCoord(room, "Left_Wall", true, k_FallbackLeftX),
                RightX = WallCoord(room, "Right_Wall", true, k_FallbackRightX),
                FrontZ = WallCoord(room, "Front_Wall", false, k_FallbackFrontZ),
                BackZ = WallCoord(room, "Back_Wall", false, k_FallbackBackZ),
                Height = height,
            };
        }

        static float WallCoord(Transform room, string child, bool readX, float fallback)
        {
            // Transform.Find sees inactive children too, so this works on a rebuild, after the walls are off.
            var wall = room.Find(child);
            if (wall != null)
                return readX ? wall.localPosition.x : wall.localPosition.z;

            Debug.LogWarning($"{k_LogTag} '{k_RoomRoot}' has no child called '{child}'. Using the stock CAVE dimension " +
                             $"({fallback:0.###} m) for that side instead.", room);
            return fallback;
        }

        // ================================================================== scene template

        /// <summary>Takes the other demos back out of the copy; anything left would stand among the animals.</summary>
        static void StripTemplate(Scene scene)
        {
            var room = FindRoot(scene, k_RoomRoot);
            if (room != null)
            {
                for (int i = room.childCount - 1; i >= 0; i--)
                {
                    var child = room.GetChild(i);
                    if (child.name.StartsWith("Tether_Rig") || child.name.StartsWith("Sphere") ||
                        child.name.StartsWith("Marble_Run") || child.name.StartsWith("Hot_Wheels"))
                        Object.DestroyImmediate(child.gameObject);
                }
            }

            foreach (var go in scene.GetRootGameObjects())
                if (go.name.StartsWith("Gun"))
                    Object.DestroyImmediate(go);
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(entry => entry.path == k_ScenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(k_ScenePath, false));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"{k_LogTag} Added '{k_ScenePath}' to the build list, disabled. Tick it in File ▸ Build Settings " +
                      "to build the orchestra for the CAVE.");
        }

        // ================================================================== assets

        /// <summary>
        /// A material on one of the demo's shaders. Created the first time, with <paramref name="setup"/>
        /// applied, and left alone after that, so colours tuned in the Inspector survive a rebuild — the one
        /// thing a rebuild does put right is a material that has lost its shader.
        /// </summary>
        static Material Material(string name, string shaderName, System.Action<Material> setup = null)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"{k_LogTag} Shader '{shaderName}' not found. Is Assets/_Project/Art/Shaders present and compiling?");
                return null;
            }

            string path = $"{k_Materials}{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (material.shader != shader)
                {
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                }

                return material;
            }

            EnsureFolder(path);
            material = new Material(shader) { name = name };
            setup?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Writes a generated mesh to its asset, in place if it exists so the scene's references follow the rebuild.</summary>
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{k_Meshes}{name}.asset";
            EnsureFolder(path);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, path);
                AssetDatabase.SaveAssets();
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ================================================================== helpers

        static Transform FindOrCreate(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                existing.localPosition = Vector3.zero;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                return existing;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create orchestra root");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name)
                    return go.transform;

            return null;
        }

        static void EnsureFolder(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            string built = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{built}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(built, parts[i]);
                built = next;
            }
        }
    }
}
