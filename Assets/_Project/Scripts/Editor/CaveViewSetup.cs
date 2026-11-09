using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEngine;
using VRShootingGallery.CaveView;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Adds the CAVE's view modes to a scene: 3D, 2D and 2D-Fixed (<see cref="CaveViewMode"/>, <see cref="FixedEyeView"/>),
    /// picked with the wand's buttons — Sit-Rep-CAVE's CARE views, for the demos here, with no menu on the screens. A demo's
    /// builder calls <see cref="Build"/>; the bounded volumes demo does.
    /// </summary>
    /// <remarks>
    /// Everything lands on <c>CAVE_User_Room/CAVE_View</c>, cleared and rebuilt each time. Two things outside the scene:
    /// the <c>CaveOverlay</c> layer, added to the project's layers the first time, and the pictures' unlit material.
    /// </remarks>
    static class CaveViewSetup
    {
        const string k_RootName = "CAVE_View";

        /// <summary>The layer the 2D-Fixed view's pictures go on, and anything that must stay live in the room while it shows.</summary>
        public const string OverlayLayerName = "CaveOverlay";

        /// <summary>The CAVE's own description of its screens — the truth, where the room model is an approximation.</summary>
        const string k_ConfigPath = "Assets/_Project/Config/MVR.vrx";

        const string k_PictureMaterialPath = "Assets/_Project/Art/Materials/M_FixedViewPicture.mat";
        const string k_UnlitShader = "Universal Render Pipeline/Unlit";

        /// <summary>
        /// Builds the view modes under <paramref name="room"/>. <paramref name="roomCentre"/> (room frame) tells the
        /// screens' insides from their outsides; <paramref name="fallbackScreens"/> are used if <c>MVR.vrx</c> cannot be
        /// read. Returns false, having logged why, if nothing could be built.
        /// </summary>
        public static bool Build(Transform room, Vector3 roomCentre, FixedEyeView.Screen[] fallbackScreens)
        {
            int layer = EnsureLayer(OverlayLayerName);
            if (layer < 0)
            {
                Debug.LogError($"[CaveView] No free layer for '{OverlayLayerName}': every user layer is taken. Free one " +
                               "in Project Settings ▸ Tags and Layers and build again.");
                return false;
            }

            var pictureMaterial = UnlitMaterial(k_PictureMaterialPath);
            if (pictureMaterial == null)
                return false;

            var screens = ReadScreens(k_ConfigPath, roomCentre);
            if (screens == null || screens.Length == 0)
            {
                Debug.LogWarning($"[CaveView] Could not read the screens from '{k_ConfigPath}'. 2D-Fixed uses the room " +
                                 "model's walls instead, which in this CAVE are shorter than the side screens.");
                screens = fallbackScreens;
            }

            var root = FindOrCreate(room, k_RootName);
            Clear(root);

            var fixedView = Undo.AddComponent<FixedEyeView>(root.gameObject);
            fixedView.Configure(screens, pictureMaterial, layer);
            EditorUtility.SetDirty(fixedView);

            var viewMode = Undo.AddComponent<CaveViewMode>(root.gameObject);
            var so = new SerializedObject(viewMode);
            so.FindProperty("m_Fixed").objectReferenceValue = fixedView;
            so.ApplyModifiedProperties();

            Debug.Log($"[CaveView] View modes on the wand: button 1 is 3D, 2 is 2D, 3 is 2D-Fixed (keys 1, 2, 3 at the " +
                      $"desk). {screens.Length} screens " +
                      $"({string.Join(", ", screens.Select(s => $"{s.Name} {s.Size.x:0.###}×{s.Size.y:0.###} m"))}), " +
                      $"overlay layer {layer} '{OverlayLayerName}'.", root);
            return true;
        }

        /// <summary>
        /// The CAVE's 3D and head-tracked 2D views for a room built on the standard screens (<see cref="CaveScreens"/>)
        /// whose wand buttons are all its own: <see cref="Build"/>, then the views moved onto the thumbstick — up for 2D,
        /// down for 3D — with their wand buttons off, and their desk keys moved from the digits, which in such a room
        /// stand in for its buttons, to F1 (3D) and F2 (2D), as in the harp's scene. 2D-Fixed is built with them but
        /// given no button or key: a room's menu and sign are not on the overlay layer it draws. Returns the view mode,
        /// or null, having logged why. <paramref name="tag"/> heads the room's log lines.
        /// </summary>
        public static CaveViewMode BuildOnThumbstick(Transform room, string tag)
        {
            if (!Build(room, new Vector3(0f, CaveScreens.Height * 0.5f, 0f), ProjectedScreens()))
                return null;

            var view = room.Find(k_RootName);
            if (view == null || !view.TryGetComponent<CaveViewMode>(out var mode))
                return null;

            var so = new SerializedObject(mode);
            StringInstrumentBuilder.SetInt(tag, so, "m_Stereo3DButton", -1);
            StringInstrumentBuilder.SetInt(tag, so, "m_Mono2DButton", -1);
            StringInstrumentBuilder.SetInt(tag, so, "m_Fixed2DButton", -1);
            StringInstrumentBuilder.SetBool(tag, so, "m_StickPicksView", true);
            StringInstrumentBuilder.SetBool(tag, so, "m_KeyboardShortcuts", true);

            var keys = so.FindProperty("m_Keys");
            if (keys != null)
            {
                keys.arraySize = 2;
                keys.GetArrayElementAtIndex(0).intValue = (int)UnityEngine.InputSystem.Key.F1;
                keys.GetArrayElementAtIndex(1).intValue = (int)UnityEngine.InputSystem.Key.F2;
            }
            else
            {
                Debug.LogWarning($"[{tag}] CaveViewMode has no 'm_Keys', so its desk keys stay 1-3, on top of the room's own.", mode);
            }

            so.ApplyModifiedProperties();
            Debug.Log($"[{tag}] CAVE views on the thumbstick: up is 2D (head-tracked), down is 3D; F2 / F1 at the desk. " +
                      "The view modes' wand buttons are off here.", view);
            return mode;
        }

        /// <summary>The projected screens from the room model, for 2D-Fixed if <c>MVR.vrx</c> cannot be read.</summary>
        static FixedEyeView.Screen[] ProjectedScreens() =>
            CaveScreens.Projected.Select(screen => new FixedEyeView.Screen
            {
                Name = screen.Name,
                Centre = screen.Centre,
                Normal = screen.Normal,
                Up = screen.Up,
                Size = new Vector2(screen.Width, screen.Height),
            }).ToArray();

        // ================================================================== screens

        /// <summary>
        /// The CAVE's screens from a MiddleVR <c>.vrx</c>, in the room's frame. The file is Z-up — positions read x,
        /// depth, height — so poses are composed up the parent chain in MiddleVR's frame and only then turned into
        /// Unity's by swapping y and z. A screen is <c>Width</c> along its local x and <c>Height</c> along its local z.
        /// </summary>
        static FixedEyeView.Screen[] ReadScreens(string path, Vector3 roomCentre)
        {
            XmlDocument xml;
            try
            {
                xml = new XmlDocument();
                xml.Load(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CaveView] '{path}': {e.Message}");
                return null;
            }

            var nodes = new Dictionary<string, XmlElement>();
            foreach (XmlElement element in xml.GetElementsByTagName("*"))
                if (element.HasAttribute("Name") && element.HasAttribute("Parent"))
                    nodes[element.GetAttribute("Name")] = element;

            var screens = new List<FixedEyeView.Screen>();
            foreach (XmlElement element in xml.GetElementsByTagName("Screen"))
            {
                Pose(element, nodes, out var position, out var orientation);

                var across = ToUnity(orientation * Vector3.right);
                var up = ToUnity(orientation * Vector3.forward); // MiddleVR's local z: the screen's height
                var centre = ToUnity(position);

                var normal = Vector3.Cross(across, up).normalized;
                if (Vector3.Dot(normal, roomCentre - centre) < 0f)
                    normal = -normal;

                // Up on a wall is up; on the floor, toward the front wall — the way a map is read.
                var reference = Mathf.Abs(Vector3.Dot(up, Vector3.up)) > 0.5f ? Vector3.up : Vector3.forward;
                if (Vector3.Dot(up, reference) < 0f)
                    up = -up;

                screens.Add(new FixedEyeView.Screen
                {
                    Name = element.GetAttribute("Name"),
                    Centre = centre,
                    Normal = normal,
                    Up = up.normalized,
                    Size = new Vector2(Number(element, "Width"), Number(element, "Height")),
                });
            }

            return screens.ToArray();
        }

        /// <summary>The element's pose in MiddleVR's frame, composed up its parent chain.</summary>
        static void Pose(XmlElement element, Dictionary<string, XmlElement> nodes, out Vector3 position, out Quaternion orientation)
        {
            position = Vector3.zero;
            orientation = Quaternion.identity;

            // Walked child to root, each parent applied on the outside; a guard against a cyclic file.
            for (int depth = 0; element != null && depth < 32; depth++)
            {
                var localPosition = Vector(element.GetAttribute("PositionLocal"), Vector3.zero);
                var localOrientation = Rotation(element.GetAttribute("OrientationLocal"));

                position = localPosition + localOrientation * position;
                orientation = localOrientation * orientation;

                nodes.TryGetValue(element.GetAttribute("Parent"), out element);
            }
        }

        static Vector3 ToUnity(Vector3 mvr) => new(mvr.x, mvr.z, mvr.y);

        static Vector3 Vector(string text, Vector3 fallback)
        {
            var parts = Numbers(text);
            return parts.Length == 3 ? new Vector3(parts[0], parts[1], parts[2]) : fallback;
        }

        static Quaternion Rotation(string text)
        {
            var parts = Numbers(text);
            return parts.Length == 4 ? new Quaternion(parts[0], parts[1], parts[2], parts[3]) : Quaternion.identity;
        }

        static float[] Numbers(string text) =>
            string.IsNullOrEmpty(text)
                ? new float[0]
                : text.Split(',').Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();

        static float Number(XmlElement element, string attribute) =>
            float.Parse(element.GetAttribute(attribute), CultureInfo.InvariantCulture);

        // ================================================================== project

        /// <summary>
        /// The named layer's index, adding it to the first free user layer if the project does not have it yet. A
        /// project setting, so it is not undoable; it is logged.
        /// </summary>
        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
                return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return -1;

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");

            // 0–7 are Unity's own.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue))
                    continue;

                slot.stringValue = name;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[CaveView] Added layer {i} '{name}' to the project, for the 2D-Fixed pictures — what every " +
                          "camera but the fixed ones draws in 2D-Fixed.");
                return i;
            }

            return -1;
        }

        /// <summary>An unlit material at the path: created the first time, left alone after, so tuning survives.</summary>
        static Material UnlitMaterial(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find(k_UnlitShader);
            if (shader == null)
            {
                Debug.LogError($"[CaveView] Shader '{k_UnlitShader}' not found — is URP installed?");
                return null;
            }

            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
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
            Undo.RegisterCreatedObjectUndo(go, "Create view root");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// Clears the root for a rebuild: its children — the plaque an earlier version of this built, among them — and
        /// its components, so a rebuild does not stack a second view mode.
        /// </summary>
        static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            foreach (var component in root.GetComponents<MonoBehaviour>())
                if (component != null)
                    Undo.DestroyObjectImmediate(component);
        }
    }
}
