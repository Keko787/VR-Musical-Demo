using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.Strings;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// What the holographic harp and guitar builders have in common: a scene copied from the physics
    /// template, a wand tip that attaches to MiddleVR's HandNode, a shared tube mesh for strings,
    /// emissive materials, TMP signs, and the SerializedObject plumbing that fills in a component's
    /// private fields. Each instrument's builder owns its own layout, tuning and palette.
    /// </summary>
    static class StringInstrumentBuilder
    {
        // The instrument scenes start as copies of the physics scene, which already carries the whole
        // MiddleVR side of the setup: MVRManager pointed at the CAVE .vrx, the MVR_URP settings
        // prefab, the base camera MiddleVR clones onto its viewports, and the room itself. Rebuilding
        // any of that from scratch would be five chances to get it subtly wrong.
        public const string k_TemplateScene = "Assets/_Project/Scenes/Demos/Physics MiddleVR.unity";
        public const string k_RoomRoot = "CAVE_User_Room";

        /// <summary>
        /// The wand tip, shared by every string instrument. A root object that <c>MVRAttachToNode</c>
        /// reparents under MiddleVR's HandNode once the kernel is up, exactly as the gun prefab does.
        /// </summary>
        public const string k_PlectrumName = "Wand_Plectrum";
        const string k_TipName = "Tip";
        const string k_WandNode = "HandNode";
        const string k_PlectrumMaterialPath = "Assets/_Project/Art/Materials/M_Wand_Plectrum.mat";
        const string k_LegacyPlectrumName = "Harp_Plectrum";
        const string k_LegacyPlectrumMaterialPath = "Assets/_Project/Art/Materials/M_Harp_Plectrum.mat";
        const string k_ShaderReferencePath = "Assets/_Project/Art/Materials/M_CAVE_Room.mat";
        const float k_TipRadius = 0.012f;
        static readonly Color k_TipGlow = new(2.5f, 3f, 3f);

        /// <summary>Everything a <see cref="PluckedString"/> is given by its builder.</summary>
        public struct StringSettings
        {
            public int Midi;
            public string NoteName;
            public float RingSeconds;
            public float PluckPosition;
            public float Tone;
            public float Radius;
            public Color Glow;
            public float SwingDecay;
            public float SwingRate;
            public float RestGlow;
        }

        // ================================================================== scene flow

        /// <summary>
        /// Copies the template to <paramref name="scenePath"/>, strips the other demos out of it, runs
        /// <paramref name="build"/>, saves, and registers the scene in the build list.
        /// </summary>
        public static void CreateScene(string tag, string scenePath, string what, System.Func<Scene, bool> build,
            string afterwards)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_TemplateScene) == null)
            {
                Debug.LogError($"[{tag}] Missing the template scene '{k_TemplateScene}'.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null &&
                !EditorUtility.DisplayDialog($"Rebuild the {what} scene?",
                    $"'{scenePath}' already exists and will be overwritten with a fresh copy of the " +
                    $"physics scene plus a newly built {what}.\n\nAnything you added to it by hand is lost.",
                    "Overwrite", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(k_TemplateScene, OpenSceneMode.Single);

            // Saved to the new path before anything is touched, so the template on disk is never at
            // risk from a build that goes wrong half way through.
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                Debug.LogError($"[{tag}] Could not save the new scene to '{scenePath}'.");
                return;
            }

            scene = SceneManager.GetActiveScene();
            StripTemplate(scene);

            if (!build(scene))
                return;

            EditorSceneManager.SaveScene(scene);
            RegisterInBuildSettings(tag, scenePath);

            Debug.Log($"[{tag}] Built '{scene.name}'. {afterwards}");
        }

        public static void RebuildInOpenScene(System.Func<Scene, bool> build)
        {
            var scene = SceneManager.GetActiveScene();
            if (build(scene))
                EditorSceneManager.MarkSceneDirty(scene);
        }

        public static Transform RequireRoom(Scene scene, string tag, string what)
        {
            var room = FindRoot(scene, k_RoomRoot);
            if (room == null)
                Debug.LogError($"[{tag}] No '{k_RoomRoot}' root in '{scene.name}'. Open the {what} scene (or any " +
                               "scene with the CAVE room) and try again.");

            return room;
        }

        /// <summary>
        /// Removes the parts of the physics scene that are a different demo: the tethered balls and
        /// their placeholder spheres, which would hang through the instrument, and the gun, whose
        /// place on the wand the plectrum takes.
        /// </summary>
        static void StripTemplate(Scene scene)
        {
            var room = FindRoot(scene, k_RoomRoot);
            if (room != null)
            {
                for (int i = room.childCount - 1; i >= 0; i--)
                {
                    var child = room.GetChild(i);
                    if (child.name.StartsWith("Tether_Rig") || child.name.StartsWith("Sphere"))
                        Object.DestroyImmediate(child.gameObject);
                }
            }

            foreach (var go in scene.GetRootGameObjects())
                if (go.name.StartsWith("Gun"))
                    Object.DestroyImmediate(go);
        }

        /// <summary>Adds the scene to the build list, switched off, so it is one tick away for a CAVE build.</summary>
        static void RegisterInBuildSettings(string tag, string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var entry in scenes)
                if (entry.path == scenePath)
                    return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, false));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[{tag}] Added '{scenePath}' to the build list, disabled. Tick it in File ▸ Build " +
                      "Settings to build it for the CAVE.");
        }

        /// <summary>
        /// The instrument's own root under the room. Position and rotation are the author's — move it
        /// to suit the player and rebuild, and it stays put. Scale is not negotiable: string lengths
        /// and swing amplitudes are metres in the instrument's own space.
        /// </summary>
        public static Transform FindOrCreateInstrument(Transform room, string name, Vector3 defaultLocalPosition,
            string tag)
        {
            var existing = room.Find(name);
            if (existing != null)
            {
                if (existing.localScale != Vector3.one)
                {
                    Debug.LogWarning($"[{tag}] '{name}' had scale {existing.localScale}; reset to one.", existing);
                    Undo.RecordObject(existing, "Reset instrument scale");
                    existing.localScale = Vector3.one;
                }

                return existing;
            }

            var root = NewChild(room, name);
            root.localPosition = defaultLocalPosition;
            return root;
        }

        // ================================================================== wand

        /// <summary>
        /// The tip's position at the root is the offset it keeps from the wand's tracked origin, so it
        /// is authored at zero — the tracked point itself plucks. Set it forward along the wand if a
        /// tip feels more natural, once the wand's axes have been checked in the room.
        /// </summary>
        public static Transform EnsurePlectrum(Scene scene)
        {
            var plectrum = FindRoot(scene, k_PlectrumName);

            // The harp built before the guitar existed named these after itself. Renamed in place
            // rather than duplicated, so a scene from then does not end up with two tips on the wand.
            if (plectrum == null)
            {
                plectrum = FindRoot(scene, k_LegacyPlectrumName);
                if (plectrum != null)
                {
                    Undo.RecordObject(plectrum.gameObject, "Rename plectrum");
                    plectrum.gameObject.name = k_PlectrumName;
                }
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(k_PlectrumMaterialPath) == null &&
                AssetDatabase.LoadAssetAtPath<Material>(k_LegacyPlectrumMaterialPath) != null)
                AssetDatabase.MoveAsset(k_LegacyPlectrumMaterialPath, k_PlectrumMaterialPath);

            if (plectrum == null)
            {
                var go = new GameObject(k_PlectrumName);
                Undo.RegisterCreatedObjectUndo(go, "Create wand plectrum");
                plectrum = go.transform;
                plectrum.localPosition = Vector3.zero;
                plectrum.localRotation = Quaternion.identity;
            }

            var attach = plectrum.GetComponent<MVRAttachToNode>();
            if (attach == null)
                attach = Undo.AddComponent<MVRAttachToNode>(plectrum.gameObject);

            Undo.RecordObject(attach, "Configure plectrum attach");
            attach.MVRParentNode = k_WandNode;
            attach.DisableGameObjectIfParentNotFound = false;
            attach.KeepLocalPosition = true;
            attach.KeepLocalRotation = true;
            attach.KeepLocalScale = true;

            var material = EnsureMaterial("Plectrum", k_PlectrumMaterialPath, Color.white, k_TipGlow, 1f, 0.8f);

            var tip = plectrum.Find(k_TipName);
            if (tip == null)
                tip = Primitive(plectrum, PrimitiveType.Sphere, k_TipName, material);
            else if (material != null && tip.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.sharedMaterial = material;

            tip.localPosition = Vector3.zero;
            tip.localScale = Vector3.one * (k_TipRadius * 2f);

            return plectrum;
        }

        // ================================================================== strings

        /// <summary>
        /// A string: tube mesh, emissive material, its own AudioSource, and a <see cref="PluckedString"/>
        /// filled in from <paramref name="settings"/>. The caller places it — the mesh is one unit long
        /// on Y and the length goes in the transform's scale.
        /// </summary>
        public static PluckedString CreateString(string tag, Transform parent, string name, Mesh mesh, Material material,
            StringSettings settings)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(AudioSource));
            Undo.RegisterCreatedObjectUndo(go, "Create string");
            go.transform.SetParent(parent, false);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 8f;

            var pluckedString = Undo.AddComponent<PluckedString>(go);
            var so = new SerializedObject(pluckedString);
            SetInt(tag, so, "m_Midi", settings.Midi);
            SetString(tag, so, "m_NoteName", settings.NoteName);
            SetFloat(tag, so, "m_RingSeconds", settings.RingSeconds);
            SetFloat(tag, so, "m_PluckPosition", settings.PluckPosition);
            SetFloat(tag, so, "m_Tone", settings.Tone);
            SetRef(tag, so, "m_Source", source);
            SetFloat(tag, so, "m_Radius", settings.Radius);
            SetColor(tag, so, "m_Glow", settings.Glow);
            SetFloat(tag, so, "m_SwingDecay", settings.SwingDecay);
            SetFloat(tag, so, "m_SwingRate", settings.SwingRate);
            SetFloat(tag, so, "m_RestGlow", settings.RestGlow);
            so.ApplyModifiedProperties();

            return pluckedString;
        }

        /// <summary>
        /// An open tube one unit long on Y, saved as an asset so the strings referencing it survive a
        /// scene reload. Rings along it give a bend its smoothness; sides round a few millimetres of
        /// tube are more than the eye can tell apart past six or so.
        /// </summary>
        public static Mesh EnsureStringMesh(string path, float radius, int rings, int sides)
        {
            var vertices = new List<Vector3>(rings * (sides + 1));
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>((rings - 1) * sides * 6);

            for (int j = 0; j < rings; j++)
            {
                float v = (float)j / (rings - 1);
                for (int k = 0; k <= sides; k++)
                {
                    float angle = (float)k / sides * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices.Add(radial * radius + Vector3.up * v);
                    normals.Add(radial);
                    uvs.Add(new Vector2((float)k / sides, v));
                }
            }

            int stride = sides + 1;
            for (int j = 0; j < rings - 1; j++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = j * stride + k;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return SaveMesh(mesh, path);
        }

        /// <summary>
        /// Writes the mesh to disk and hands back <em>the asset</em>. On a rebuild that is not the mesh
        /// passed in — the existing asset is rewritten in place so every string already pointing at it
        /// follows, rather than being left holding a loose mesh that gets baked into the scene.
        /// </summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            EnsureFolder(path);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                AssetDatabase.SaveAssets();
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        // ================================================================== materials

        /// <summary>
        /// A material a builder owns, re-authored on every build: an instrument's look is defined by
        /// its builder's constants, and a material left over from an earlier palette is a bug, not a
        /// tweak. Emission is what makes it a hologram, and it is switched on here because the runtime
        /// pulse only changes the emission <em>colour</em> — with the keyword off the string never glows.
        /// </summary>
        public static Material EnsureMaterial(string tag, string path, Color color, Color emission, float alpha,
            float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;

            if (created)
            {
                EnsureFolder(path);

                // The shader is taken from a material already in the project rather than looked up
                // by name, so the instrument cannot end up on the wrong render pipeline's Lit shader.
                var reference = AssetDatabase.LoadAssetAtPath<Material>(k_ShaderReferencePath);
                var shader = reference != null ? reference.shader : Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogWarning($"[{tag}] No shader to build '{path}' from; the piece will render pink.");
                    return null;
                }

                material = new Material(shader);
            }

            color.a = alpha;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            SetBlending(material, alpha < 1f);

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);

            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Switches a material between opaque and alpha-blended. There is no API for this — the
        /// Material inspector writes these properties, keywords and the render queue by hand, and a
        /// material that has only had its alpha lowered stays stubbornly, invisibly opaque.
        /// </summary>
        static void SetBlending(Material material, bool transparent)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", transparent ? 1f : 0f);
                if (material.HasProperty("_Blend"))
                    material.SetFloat("_Blend", 0f);            // straight alpha, not premultiplied
            }
            else if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", transparent ? 3f : 0f);
            }

            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)(transparent
                    ? UnityEngine.Rendering.BlendMode.SrcAlpha
                    : UnityEngine.Rendering.BlendMode.One));

            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)(transparent
                    ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                    : UnityEngine.Rendering.BlendMode.Zero));

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", transparent ? 0f : 1f);

            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);

            Keyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            Keyword(material, "_ALPHABLEND_ON", transparent);
            Keyword(material, "_ALPHATEST_ON", false);
            Keyword(material, "_ALPHAPREMULTIPLY_ON", false);

            // A see-through frame throwing a solid shadow across the floor would undo the hologram.
            material.SetShaderPassEnabled("ShadowCaster", !transparent);

            material.renderQueue = (int)(transparent
                ? UnityEngine.Rendering.RenderQueue.Transparent
                : UnityEngine.Rendering.RenderQueue.Geometry);
        }

        static void Keyword(Material material, string keyword, bool on)
        {
            if (on)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        // ================================================================== scene objects

        /// <summary>A primitive with its collider stripped — the instruments have no physics; the wand test is geometric.</summary>
        public static Transform Primitive(Transform parent, PrimitiveType type, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            Undo.RegisterCreatedObjectUndo(go, "Create instrument piece");
            go.transform.SetParent(parent, false);

            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go.transform;
        }

        /// <summary>A box running from <paramref name="from"/> to <paramref name="to"/>, lifted along local Y by <paramref name="lift"/>.</summary>
        public static Transform Slab(Transform parent, string name, Vector3 from, Vector3 to, float thickness, float depth,
            float lift, Material material)
        {
            var delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f)
                return null;

            var slab = Primitive(parent, PrimitiveType.Cube, name, material);
            slab.localPosition = (from + to) * 0.5f + Vector3.up * lift;
            slab.localRotation = Quaternion.FromToRotation(Vector3.right, delta / length);

            // A hair over length so neighbouring pieces overlap instead of showing a seam.
            slab.localScale = new Vector3(length + 0.01f, thickness, depth);
            return slab;
        }

        public static Light PulseLight(Transform parent, string name, Vector3 localPosition, float range)
        {
            var go = NewChild(parent, name);
            go.localPosition = localPosition;

            var light = Undo.AddComponent<Light>(go.gameObject);
            light.type = LightType.Point;
            light.range = range;
            light.intensity = 0f;
            light.color = Color.white;
            light.shadows = LightShadows.None;
            return light;
        }

        public static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create instrument object");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        public static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }

        public static Transform FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name)
                    return go.transform;

            return null;
        }

        public static void EnsureFolder(string assetPath)
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

        // ================================================================== serialized fields

        static SerializedProperty Property(string tag, SerializedObject so, string path)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[{tag}] {so.targetObject.GetType().Name} has no field '{path}'.");

            return property;
        }

        public static void SetInt(string tag, SerializedObject so, string path, int value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.intValue = value;
        }

        public static void SetFloat(string tag, SerializedObject so, string path, float value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.floatValue = value;
        }

        public static void SetString(string tag, SerializedObject so, string path, string value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.stringValue = value;
        }

        public static void SetColor(string tag, SerializedObject so, string path, Color value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.colorValue = value;
        }

        public static void SetBool(string tag, SerializedObject so, string path, bool value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.boolValue = value;
        }

        public static void SetEnum(string tag, SerializedObject so, string path, int value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.enumValueIndex = value;
        }

        public static void SetVector3(string tag, SerializedObject so, string path, Vector3 value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.vector3Value = value;
        }

        public static void SetRef(string tag, SerializedObject so, string path, Object value)
        {
            var property = Property(tag, so, path);
            if (property != null)
                property.objectReferenceValue = value;
        }

        public static void SetArray<T>(string tag, SerializedObject so, string path, IReadOnlyList<T> values) where T : Object
        {
            var property = Property(tag, so, path);
            if (property == null)
                return;

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ================================================================== text

        public static TextMeshPro Text(string tag, Transform parent, string name, Vector3 localPosition, float size,
            string content, Color color, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create sign text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var text = Undo.AddComponent<TextMeshPro>(go);

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
                Debug.LogWarning($"[{tag}] No TMP font asset found. Run Window ▸ TextMeshPro ▸ Import TMP " +
                                 "Essential Resources, then build again.");
            }

            text.enabled = true;
            if (go.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.enabled = true;

            text.text = content;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.rectTransform.sizeDelta = box;
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
            catch (System.Exception)
            {
                s_Font = null;
            }

            if (s_Font == null)
                s_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            return s_Font;
        }
    }
}
