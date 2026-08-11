using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.Core;
using VRShootingGallery.Targets;
using VRShootingGallery.UI;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// One-click setup for the CAVE shooting-gallery round. Builds the match service and its
    /// config asset, the scoreboard on the CAVE room's left wall, the shoot-to-press control
    /// panel on the front kick panel, and a target spawner sized to whatever sphere layout is
    /// already on Shooting_Gallery_Wall.
    ///
    /// Everything it makes is named and reused, so running it again updates the scene rather
    /// than duplicating it. Every step is registered with Undo.
    /// </summary>
    static class ShootingGalleryBuilder
    {
        // Top level rather than under Tools — this is the project's own menu, not a general utility.
        const string k_BuildMenu = "VR Shooting Gallery/Build/Build CAVE Gallery Round";
        const string k_RestoreMenu = "VR Shooting Gallery/Build/Restore Original Static Targets";

        const string k_ConfigPath = "Assets/_Project/Config/MatchConfig_CaveGallery.asset";
        const string k_TargetPrefabPath = "Assets/_Project/Prefabs/Targets/Target_Moving.prefab";
        const string k_TargetMaterialPath = "Assets/_Project/Art/Materials/M_Target.mat";
        const string k_RoomMaterialPath = "Assets/_Project/Art/Materials/M_CAVE_Room.mat";
        const string k_PanelMaterialPath = "Assets/_Project/Art/Materials/M_CAVE_Panel.mat";
        const string k_GalleryMaterialPath = "Assets/_Project/Art/Materials/M_Gallery_Backdrop.mat";
        const string k_FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        /// <summary>Metres a backdrop is parked behind the target plane. Enough depth to read as a room.</summary>
        const float k_BackdropGap = 0.9f;

        // Objects the builder owns. Anything else in the scene is left alone.
        const string k_SystemsName = "Game_Systems";
        const string k_SpawnerName = "Target_Spawner";
        const string k_ScoreboardName = "HUD_Scoreboard";
        const string k_ControlsName = "Match_Controls";

        // Scene objects the builder hangs things off, by name.
        const string k_GalleryRoot = "Shooting_Gallery_Wall";
        const string k_RoomRoot = "CAVE_User_Room";

        static readonly Color k_Ink = new(0.93f, 0.95f, 1f);
        static readonly Color k_Muted = new(0.55f, 0.62f, 0.72f);
        static readonly Color k_Accent = new(0.35f, 0.85f, 0.55f);

        [MenuItem(k_BuildMenu, false, 100)]
        static void Build()
        {
            var scene = SceneManager.GetActiveScene();

            var gallery = FindRoot(scene, k_GalleryRoot);
            var room = FindRoot(scene, k_RoomRoot);
            if (gallery == null || room == null)
            {
                Debug.LogError($"[GalleryBuilder] Open the ShootingGallery MiddleVR scene first — this needs a " +
                               $"'{k_GalleryRoot}' and a '{k_RoomRoot}' root object.");
                return;
            }

            Undo.SetCurrentGroupName("Build CAVE Gallery Round");
            int group = Undo.GetCurrentGroup();

            // Resolve (and if necessary repair) the font before anything adds a TextMeshPro, so
            // TMP's Awake finds a usable material instead of throwing on every single text object.
            DefaultFont();

            var config = EnsureConfig();
            var match = EnsureMatchService(config);
            var prefab = EnsureTargetPrefab();

            BuildSpawner(gallery, prefab);
            BuildScoreboard(room);
            BuildControls(room);
            Recolor(room, gallery);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = match.gameObject;

            Debug.Log($"[GalleryBuilder] Built the round in '{scene.name}'. Enter play mode and shoot START.", match);
        }

        [MenuItem(k_RestoreMenu, false, 120)]
        static void RestoreStaticTargets()
        {
            var gallery = FindRoot(SceneManager.GetActiveScene(), k_GalleryRoot);
            if (gallery == null)
                return;

            int restored = 0;
            foreach (var target in gallery.GetComponentsInChildren<TargetBase>(true))
            {
                if (target is Target_MovingSphere || target.gameObject.activeSelf)
                    continue;

                Undo.RecordObject(target.gameObject, "Restore static targets");
                target.gameObject.SetActive(true);
                restored++;
            }

            Debug.Log($"[GalleryBuilder] Re-enabled {restored} original target(s).");
        }

        // ------------------------------------------------------------------ assets

        static MatchConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(k_ConfigPath);
            if (config != null)
                return config;

            EnsureFolder(k_ConfigPath);
            config = ScriptableObject.CreateInstance<MatchConfig>();
            config.Mode = MatchMode.TimeAttack;
            config.Duration = 60f;
            config.DisplayName = "CAVE Gallery 60s";

            AssetDatabase.CreateAsset(config, k_ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        static Target_MovingSphere EnsureTargetPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(k_TargetPrefabPath);
            if (existing != null)
                return existing.GetComponent<Target_MovingSphere>();

            EnsureFolder(k_TargetPrefabPath);

            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.name = "Target_Moving";

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_TargetMaterialPath);
            if (material != null)
                temp.GetComponent<Renderer>().sharedMaterial = material;

            // Moving colliders belong to a kinematic body; Target_MovingSphere enforces this at
            // runtime too, but authoring it here keeps the prefab honest in the inspector.
            var body = temp.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            temp.AddComponent<Target_MovingSphere>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, k_TargetPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab.GetComponent<Target_MovingSphere>();
        }

        // ------------------------------------------------------------------ scene pieces

        static MatchService EnsureMatchService(MatchConfig config)
        {
            var match = Object.FindObjectOfType<MatchService>();
            if (match == null)
                match = Ensure<MatchService>(NewObject(k_SystemsName, null));

            var so = new SerializedObject(match);
            SetRef(so, "m_DefaultConfig", config);
            so.ApplyModifiedProperties();
            return match;
        }

        static void BuildSpawner(Transform gallery, Target_MovingSphere prefab)
        {
            var go = FindOrCreate(gallery, k_SpawnerName);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var spawner = go.GetComponent<TargetSpawner>();
            bool isNew = spawner == null;
            if (isNew)
                spawner = Undo.AddComponent<TargetSpawner>(go);

            var so = new SerializedObject(spawner);
            SetRef(so, "m_TargetPrefab", prefab);
            SetRef(so, "m_SpawnParent", gallery);

            // Only derive the playfield the first time. Re-running the builder must not stomp on a
            // spawn area that has since been tuned by hand — and by then the spheres it was derived
            // from are switched off anyway.
            if (isNew)
            {
                var area = DeriveSpawnArea(gallery, out var center);
                SetVector3(so, "m_AreaCenter", center);
                SetVector2(so, "m_AreaSize", area);
            }

            so.ApplyModifiedProperties();
            RetireStaticTargets(gallery);

            // Re-check every run against whatever the spawn plane ended up as, tuned or derived.
            var plane = new SerializedObject(spawner).FindProperty("m_AreaCenter");
            if (plane != null)
                PushOccludersBehindTargets(gallery, plane.vector3Value.z);
        }

        /// <summary>
        /// Uses the spheres already placed on the gallery wall as the authored playfield, so
        /// spawned targets land exactly where the hand-placed ones were.
        /// </summary>
        static Vector2 DeriveSpawnArea(Transform gallery, out Vector3 center)
        {
            var placed = gallery.GetComponentsInChildren<TargetBase>(true)
                .Where(t => !(t is Target_MovingSphere) && t.gameObject.activeSelf)
                .Select(t => gallery.InverseTransformPoint(t.transform.position))
                .ToArray();

            if (placed.Length < 2)
            {
                center = new Vector3(0f, 1.1f, -0.09f);
                return new Vector2(2.6f, 1.5f);
            }

            var min = new Vector2(placed.Min(p => p.x), placed.Min(p => p.y));
            var max = new Vector2(placed.Max(p => p.x), placed.Max(p => p.y));

            center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, placed.Average(p => p.z));
            return Vector2.Max(max - min, new Vector2(1.6f, 1.1f));
        }

        /// <summary>Nothing is deleted — the hand-placed spheres are switched off so the spawner owns the wall.</summary>
        static void RetireStaticTargets(Transform gallery)
        {
            foreach (var target in gallery.GetComponentsInChildren<TargetBase>(true))
            {
                if (target is Target_MovingSphere || !target.gameObject.activeSelf)
                    continue;

                Undo.RecordObject(target.gameObject, "Retire static targets");
                target.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The player looks down +Z at the gallery, so any solid backdrop sitting at a smaller local
        /// z than the targets stands in front of them and hides the whole playfield. Push those
        /// behind the target plane so they become a backdrop instead of a screen.
        ///
        /// Only walls are moved — side walls run alongside the targets and are left where they are,
        /// as is anything the builder owns.
        /// </summary>
        static void PushOccludersBehindTargets(Transform gallery, float targetPlaneZ)
        {
            float backdropZ = targetPlaneZ + k_BackdropGap;

            foreach (Transform child in gallery)
            {
                if (child.name == k_SpawnerName || child.GetComponent<TargetBase>() != null)
                    continue;

                if (child.GetComponent<MeshRenderer>() == null)
                    continue;

                var local = child.localPosition;
                if (local.z >= targetPlaneZ - 0.01f)
                    continue;

                Undo.RecordObject(child, "Move backdrop behind targets");
                child.localPosition = new Vector3(local.x, local.y, backdropZ);

                Debug.Log($"[GalleryBuilder] Moved '{child.name}' from local z {local.z:0.###} to {backdropZ:0.###} — " +
                          $"it was in front of the targets at {targetPlaneZ:0.###} and hiding them.", child);
            }
        }

        static void BuildScoreboard(Transform room)
        {
            var board = FindOrCreate(room, k_ScoreboardName);
            board.transform.localPosition = new Vector3(-1.74f, 1.4f, 0f);

            // Text reads correctly when its forward points AWAY from the viewer (a default-rotation
            // TMP object is read by a camera sitting on its -Z side). The player stands at +X of the
            // left wall, so the board faces -X. Getting this backwards renders every label mirrored.
            board.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            // Static captions; nothing binds to these at runtime.
            Text(board.transform, "Label_Hits", new Vector3(-0.62f, 0.42f, 0f), 1.4f, "TARGETS HIT", k_Muted);
            Text(board.transform, "Label_Time", new Vector3(0.62f, 0.42f, 0f), 1.4f, "TIME LEFT", k_Muted);

            var hits = Text(board.transform, "Value_Hits", new Vector3(-0.62f, 0.02f, 0f), 4.2f, "0", k_Accent);
            var time = Text(board.transform, "Value_Time", new Vector3(0.62f, 0.02f, 0f), 4.2f, "1:00", k_Ink);
            var score = Text(board.transform, "Value_Score", new Vector3(0f, -0.42f, 0f), 1.8f, "SCORE  0", k_Ink);
            var status = Text(board.transform, "Value_Status", new Vector3(0f, -0.66f, 0f), 1.5f, "SHOOT  START  TO PLAY", k_Muted);

            var hud = Ensure<HUDController>(board);
            var so = new SerializedObject(hud);
            SetRef(so, "m_HitsText", hits);
            SetRef(so, "m_TimerText", time);
            SetRef(so, "m_ScoreText", score);
            SetRef(so, "m_StatusText", status);
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// The console lies flat on the floor a little in front of the player's feet. On the kick
        /// panel it sat inside the arc of any low shot at the gallery; down here you have to point
        /// the gun at your own feet to reach it, which no stray round does.
        /// </summary>
        static void BuildControls(Transform room)
        {
            var controls = FindOrCreate(room, k_ControlsName);
            controls.transform.localPosition = new Vector3(0f, 0.04f, 0.65f);
            controls.transform.localRotation = Quaternion.identity;

            BuildConsoleBase(controls.transform);

            MakeButton(controls.transform, "Button_Start", new Vector3(-0.55f, 0f, 0f),
                MatchButtonAction.Start, "START", new Color(0.16f, 0.62f, 0.28f), false);

            MakeButton(controls.transform, "Button_Restart", Vector3.zero,
                MatchButtonAction.Restart, "RESTART", new Color(0.18f, 0.45f, 0.75f), false);

            // No confirm shot. It existed to stop a stray round ending the session while the console
            // sat on the kick panel, in the firing line; on the floor, with a semi-auto gun, nothing
            // strays there — and the silent arming step just read as a dead button. Set
            // m_ConfirmRequired on Button_Quit in the Inspector to bring it back for a public demo.
            MakeButton(controls.transform, "Button_Quit", new Vector3(0.55f, 0f, 0f),
                MatchButtonAction.Quit, "QUIT", new Color(0.68f, 0.19f, 0.19f), false);
        }

        /// <summary>A slab under the tiles so the console reads as one object rather than three loose plates.</summary>
        static void BuildConsoleBase(Transform controls)
        {
            var slab = controls.Find("Console_Base");
            if (slab == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Console_Base";
                Object.DestroyImmediate(cube.GetComponent<BoxCollider>());   // must not block shots at the tiles
                Undo.RegisterCreatedObjectUndo(cube, "Create console base");
                Undo.SetTransformParent(cube.transform, controls, "Parent console base");
                slab = cube.transform;
            }

            slab.localPosition = new Vector3(0f, -0.018f, 0f);
            slab.localRotation = Quaternion.identity;
            slab.localScale = new Vector3(1.8f, 0.02f, 0.62f);

            var panel = AssetDatabase.LoadAssetAtPath<Material>(k_PanelMaterialPath);
            var renderer = slab.GetComponent<MeshRenderer>();
            if (panel != null && renderer != null)
            {
                Undo.RecordObject(renderer, "Paint console base");
                renderer.sharedMaterial = panel;
            }
        }

        static void MakeButton(Transform parent, string name, Vector3 localPos, MatchButtonAction action,
            string caption, Color color, bool confirm)
        {
            var button = FindOrCreate(parent, name);
            button.transform.localPosition = localPos;

            // Forward points down into the floor, away from the player: that is what makes the label
            // read the right way round, and it is the direction the tile travels when pressed.
            button.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Far deeper than the visible tile, sunk into the floor. Projectile also sweeps its
            // path for shootables, but a generous volume means a glancing shot still counts.
            var box = Ensure<BoxCollider>(button);
            box.size = new Vector3(0.5f, 0.5f, 0.35f);
            box.center = new Vector3(0f, 0f, 0.12f);

            var face = button.transform.Find("Face");
            if (face == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Face";
                Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
                Undo.RegisterCreatedObjectUndo(cube, "Create button face");
                Undo.SetTransformParent(cube.transform, button.transform, "Parent button face");
                face = cube.transform;
            }

            face.localPosition = Vector3.zero;
            face.localRotation = Quaternion.identity;
            face.localScale = new Vector3(0.45f, 0.45f, 0.05f);

            // Sits just proud of the tile on the player's side, which is local -Z (up, from the floor).
            var label = Text(button.transform, "Label", new Vector3(0f, 0f, -0.04f), 1.4f, caption, Color.white);

            var shootable = Ensure<ShootableButton>(button);
            Undo.RecordObject(shootable, "Author button");
            shootable.Author(action, caption, color, face.GetComponent<Renderer>(), label, confirm);
            EditorUtility.SetDirty(shootable);
        }

        // ------------------------------------------------------------------ colour

        /// <summary>
        /// Splits the two spaces apart visually. The CAVE room goes dark and matte so the bright
        /// scoreboard and the coloured buttons read against it; the gallery goes a lighter cool
        /// blue so the pale targets stand out and the opening is obvious from inside the room.
        /// Matte matters as much as hue here — a glossy wall throws specular glare across the
        /// CAVE screens and washes the text out.
        /// </summary>
        static void Recolor(Transform room, Transform gallery)
        {
            var roomMat = AssetDatabase.LoadAssetAtPath<Material>(k_RoomMaterialPath);
            var panelMat = AssetDatabase.LoadAssetAtPath<Material>(k_PanelMaterialPath);
            var galleryMat = AssetDatabase.LoadAssetAtPath<Material>(k_GalleryMaterialPath);

            if (roomMat == null || galleryMat == null)
            {
                Debug.LogWarning($"[GalleryBuilder] Missing wall materials — expected '{k_RoomMaterialPath}' and " +
                                 $"'{k_GalleryMaterialPath}'. Skipping the recolour.");
                return;
            }

            // The kick panel gets the lighter tone so the control strip reads as its own surface.
            Repaint(room, roomMat, panelMat, "Front_Wall_Pannel_Bottom");
            Repaint(gallery, galleryMat, null, null);
        }

        /// <summary>
        /// Only direct children, and only the ones that are plain geometry — targets keep their own
        /// look, and the scoreboard/controls/spawner this builder made are left alone.
        /// </summary>
        static void Repaint(Transform root, Material wall, Material accent, string accentChild)
        {
            foreach (Transform child in root)
            {
                if (child.name == k_ScoreboardName || child.name == k_ControlsName || child.name == k_SpawnerName)
                    continue;

                if (child.GetComponent<TargetBase>() != null)
                    continue;

                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;

                Undo.RecordObject(renderer, "Recolour walls");
                renderer.sharedMaterial = accent != null && child.name == accentChild ? accent : wall;
                EditorUtility.SetDirty(renderer);
            }
        }

        // ------------------------------------------------------------------ helpers

        static TextMeshPro Text(Transform parent, string name, Vector3 localPos, float size, string content, Color color)
        {
            var existing = parent.Find(name);
            GameObject go;

            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create scoreboard text");
                Undo.SetTransformParent(go.transform, parent, "Parent scoreboard text");
            }

            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var text = Ensure<TextMeshPro>(go);
            Undo.RecordObject(text, "Author scoreboard text");

            // TMP 3.0.9's Awake throws inside LoadFontAsset when the component is added from an
            // editor script, which leaves the text and its renderer disabled with no material —
            // silently invisible. Wire the font, material and enabled flags up by hand.
            var font = DefaultFont();
            if (font != null)
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            else
            {
                Debug.LogWarning("[GalleryBuilder] No TMP font asset found. Run Window ▸ TextMeshPro ▸ " +
                                 "Import TMP Essential Resources, then build again.");
            }

            text.enabled = true;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                Undo.RecordObject(meshRenderer, "Enable scoreboard text");
                meshRenderer.enabled = true;
            }

            text.text = content;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta = new Vector2(2.5f, 0.6f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.ForceMeshUpdate();
            EditorUtility.SetDirty(text);

            return text;
        }

        static TMP_FontAsset s_Font;

        static TMP_FontAsset DefaultFont()
        {
            if (s_Font != null)
                return s_Font;

            try
            {
                s_Font = TMP_Settings.defaultFontAsset;
            }
            catch (System.NullReferenceException)
            {
                // TMP_Settings.instance is not always resolvable from a menu callback.
            }

            if (s_Font == null)
                s_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FallbackFontPath);

            RepairFontAsset(s_Font);
            return s_Font;
        }

        /// <summary>
        /// This project's LiberationSans SDF asset was imported with its <c>material</c> reference
        /// unassigned. TMP dereferences that field in <c>TextMeshPro.Awake</c>, so every text object
        /// throws, ends up disabled with no material, and silently draws nothing — and TMP's own
        /// inspector throws too. The right material is already a sub-asset of the same file, so
        /// point the font back at it.
        /// </summary>
        static void RepairFontAsset(TMP_FontAsset font)
        {
            if (font == null || font.material != null)
                return;

            var path = AssetDatabase.GetAssetPath(font);
            var material = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().FirstOrDefault();

            if (material == null)
            {
                Debug.LogError($"[GalleryBuilder] '{path}' has no material to repair with. Re-import it " +
                               "with Window ▸ TextMeshPro ▸ Import TMP Essential Resources, then build again.");
                return;
            }

            Undo.RecordObject(font, "Repair TMP font asset");
            font.material = material;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GalleryBuilder] '{font.name}' had no material assigned — repaired it with " +
                      $"'{material.name}'. That was why no text was drawing.", font);
        }

        /// <summary>
        /// Gets a component, adding it if it is missing. Never use <c>??</c> for this — GetComponent
        /// can hand back a fake-null wrapper that the null-coalescing operator happily accepts, and
        /// the next property access throws MissingComponentException.
        /// </summary>
        static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component == null ? Undo.AddComponent<T>(go) : component;
        }

        static GameObject FindOrCreate(Transform parent, string name)
        {
            var existing = parent != null ? parent.Find(name) : null;
            return existing != null ? existing.gameObject : NewObject(name, parent);
        }

        static GameObject NewObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            if (parent != null)
                Undo.SetTransformParent(go.transform, parent, $"Parent {name}");

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
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
            var folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var running = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{running}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(running, parts[i]);
                running = next;
            }
        }

        static void SetRef(SerializedObject so, string path, Object value)
        {
            var property = Find(so, path);
            if (property != null)
                property.objectReferenceValue = value;
        }

        static void SetVector2(SerializedObject so, string path, Vector2 value)
        {
            var property = Find(so, path);
            if (property != null)
                property.vector2Value = value;
        }

        static void SetVector3(SerializedObject so, string path, Vector3 value)
        {
            var property = Find(so, path);
            if (property != null)
                property.vector3Value = value;
        }

        static SerializedProperty Find(SerializedObject so, string path)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[GalleryBuilder] '{so.targetObject.GetType().Name}' has no field '{path}'.");

            return property;
        }
    }
}
