using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VRShootingGallery.Visualizer;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds a room's front-wall overlays: the <see cref="WallMenu"/> its buttons open, and the
    /// <see cref="TrackIndicator"/> strip that says what is playing and what is up next. The layout
    /// and the look are the glass room's, so every room's menu is the same panel: dark glass, a bar
    /// behind the selected row, eleven rows under a title, the wand's ray and a cursor, and for a
    /// longer list a 'more' strip above and below the rows and a scrollbar beside them.
    /// </summary>
    /// <remarks>
    /// A room's builder adds the menu subclass it wants (<see cref="BuildMenu{T}"/>) and then fills in
    /// that subclass's own fields. The materials are the room's, named under the prefix it passes,
    /// so rebuilding one room never touches another's.
    /// </remarks>
    public static class WallMenuBuilder
    {
        /// <summary>Height of the menu's middle on the front wall: a little below the eye.</summary>
        public const float MenuHeight = 1.5f;

        /// <summary>Height of the strip's middle: above the eye line, clear of the menu.</summary>
        public const float IndicatorHeight = 2.0f;

        /// <summary>
        /// Metres in front of the front screen: in front of anything a room draws flat on the wall. A
        /// room with things standing out from its front wall passes more, so the panel stands clear of them.
        /// </summary>
        public const float Inset = 0.035f;

        /// <summary>Height of the top row and the step down to each next, in the menu's own space.</summary>
        static readonly Vector2 k_Rows = new(0.37f, 0.082f);

        /// <summary>Half the panel: the rows and a margin to their right for the scrollbar, the title, and a 'more' strip above and below.</summary>
        static readonly Vector2 k_HalfSize = new(0.78f, 0.64f);

        const int k_RowCount = 11;

        /// <summary>Half the rows' width; the highlight bar is as wide.</summary>
        const float k_RowHalfWidth = 0.675f;

        /// <summary>Where the scrollbar runs, in the margin right of the rows.</summary>
        const float k_ScrollX = 0.725f;

        /// <summary>
        /// The menu, on the front wall: a dark glass panel, a bar behind the selected row, the title and
        /// the rows, the wand's ray and a cursor where it meets the panel. Closed until a button opens
        /// it, in the editor too. <paramref name="height"/> is its middle above the floor and
        /// <paramref name="inset"/> how far it stands off the front screen.
        /// </summary>
        public static T BuildMenu<T>(string tag, Transform root, string materialPrefix, float height = MenuHeight, float inset = Inset)
            where T : WallMenu
        {
            var front = CaveScreens.Front;
            var holder = NewChild(root, "Menu");
            holder.localPosition = front.Point(0.5f, height / CaveScreens.Height, inset);
            holder.localRotation = front.FacingRotation;

            var content = NewChild(holder, "Content");
            var panel = Quad(content, "Panel", Vector3.zero, k_HalfSize * 2f,
                Material(materialPrefix, "Menu_Panel", new Color(0.04f, 0.06f, 0.1f, 0.78f), 2980));
            Quad(content, "Edge", new Vector3(0f, k_HalfSize.y - 0.004f, -0.001f), new Vector2(k_HalfSize.x * 2f, 0.008f),
                Material(materialPrefix, "Menu_Edge", new Color(0.75f, 0.9f, 1f, 0.9f), 2985));
            var highlight = Quad(content, "Highlight", new Vector3(0f, k_Rows.x, -0.002f), new Vector2(k_RowHalfWidth * 2f, k_Rows.y * 0.92f),
                Material(materialPrefix, "Menu_Highlight", new Color(0.45f, 0.7f, 1f, 0.35f), 2990));

            var title = Text(tag, content, "Title", new Vector3(0f, 0.56f, -0.004f), 0.7f, "", new Color(0.75f, 0.9f, 1f),
                new Vector2(k_RowHalfWidth * 2f, 0.1f));
            var rows = new List<TMP_Text>();
            for (int i = 0; i < k_RowCount; i++)
            {
                var row = Text(tag, content, $"Row_{i}", new Vector3(0f, k_Rows.x - i * k_Rows.y, -0.004f), 0.52f, "",
                    new Color(0.86f, 0.9f, 0.96f), new Vector2(k_RowHalfWidth * 2f, k_Rows.y));

                // A long name ends in an ellipsis at the panel's edge rather than running off it.
                row.overflowMode = TextOverflowModes.Ellipsis;
                rows.Add(row);
            }

            // A list longer than the rows: a strip above and below saying how many more there are that
            // way, which the wand scrolls by resting on, and a bar beside them. The menu shows each
            // only when it applies.
            float rowsTop = k_Rows.x + k_Rows.y * 0.5f;
            float rowsBottom = rowsTop - k_RowCount * k_Rows.y;
            var moreColour = new Color(0.6f, 0.78f, 0.95f, 0.8f);
            var moreAbove = Text(tag, content, "More_Above", new Vector3(0f, rowsTop + 0.045f, -0.004f), 0.4f, "", moreColour,
                new Vector2(k_RowHalfWidth * 2f, 0.07f));
            var moreBelow = Text(tag, content, "More_Below", new Vector3(0f, rowsBottom - 0.045f, -0.004f), 0.4f, "", moreColour,
                new Vector2(k_RowHalfWidth * 2f, 0.07f));

            float trackLength = rowsTop - rowsBottom;
            var scrollTrack = Quad(content, "Scroll_Track", new Vector3(k_ScrollX, (rowsTop + rowsBottom) * 0.5f, -0.001f),
                new Vector2(0.012f, trackLength), Material(materialPrefix, "Menu_ScrollTrack", new Color(0.6f, 0.75f, 0.9f, 0.22f), 2985));
            var scrollThumb = Quad(content, "Scroll_Thumb", new Vector3(k_ScrollX, rowsTop - trackLength * 0.25f, -0.003f),
                new Vector2(0.024f, trackLength * 0.5f), Material(materialPrefix, "Menu_ScrollThumb", new Color(0.75f, 0.9f, 1f, 0.85f), 2992));

            var cursor = Quad(content, "Cursor", new Vector3(0f, 0f, -0.006f), Vector2.one * 0.025f,
                Material(materialPrefix, "Menu_Cursor", new Color(0.9f, 0.97f, 1f, 0.95f), 3010));

            var rayObject = NewChild(holder, "Wand_Ray");
            var ray = Undo.AddComponent<LineRenderer>(rayObject.gameObject);
            ray.useWorldSpace = true;
            ray.positionCount = 2;
            ray.widthMultiplier = 0.005f;
            ray.numCapVertices = 2;
            ray.shadowCastingMode = ShadowCastingMode.Off;
            ray.receiveShadows = false;
            ray.sharedMaterial = Material(materialPrefix, "Menu_Ray", new Color(0.7f, 0.9f, 1f, 0.55f), 3005);
            ray.enabled = false;

            var menu = Undo.AddComponent<T>(holder.gameObject);
            var so = new SerializedObject(menu);
            SetRef(tag, so, "m_Content", content);
            SetRef(tag, so, "m_Highlight", highlight);
            SetRef(tag, so, "m_Title", title);
            SetArray(tag, so, "m_Rows", rows);
            SetVector2(tag, so, "m_RowLayout", k_Rows);
            SetVector2(tag, so, "m_HalfSize", k_HalfSize);
            SetRef(tag, so, "m_Ray", ray);
            SetRef(tag, so, "m_Cursor", cursor);
            SetRef(tag, so, "m_MoreAbove", moreAbove);
            SetRef(tag, so, "m_MoreBelow", moreBelow);
            SetRef(tag, so, "m_ScrollTrack", scrollTrack);
            SetRef(tag, so, "m_ScrollThumb", scrollThumb);
            so.ApplyModifiedProperties();

            content.gameObject.SetActive(false);
            cursor.gameObject.SetActive(false);
            foreach (var hidden in new Component[] { moreAbove, moreBelow, scrollTrack, scrollThumb })
                hidden.gameObject.SetActive(false);
            return menu;
        }

        /// <summary>
        /// The track strip, high on the front wall: a dark band with the track playing on one line, the
        /// one up next below it, and a bar for how far through it the music is. Faded out until a
        /// change of track brings it up; hidden while <paramref name="menu"/> is open.
        /// </summary>
        public static TrackIndicator BuildTrackIndicator(string tag, Transform root, string materialPrefix, MusicDeck deck, WallMenu menu,
            float height = IndicatorHeight, float inset = Inset)
        {
            var front = CaveScreens.Front;
            var holder = NewChild(root, "Track_Indicator");
            holder.localPosition = front.Point(0.5f, height / CaveScreens.Height, inset);
            holder.localRotation = front.FacingRotation;

            const float length = 1.5f;
            var content = NewChild(holder, "Content");
            var panel = Quad(content, "Panel", Vector3.zero, new Vector2(1.7f, 0.15f),
                Material(materialPrefix, "Indicator_Panel", new Color(0.04f, 0.06f, 0.1f, 0.62f), 2980));
            var track = Quad(content, "Bar_Track", new Vector3(0f, -0.055f, -0.001f), new Vector2(length, 0.006f),
                Material(materialPrefix, "Indicator_Track", new Color(0.6f, 0.75f, 0.9f, 0.25f), 2985));
            var bar = Quad(content, "Bar", new Vector3(-length * 0.5f, -0.055f, -0.002f), new Vector2(0.0001f, 0.006f),
                Material(materialPrefix, "Indicator_Bar", new Color(0.75f, 0.9f, 1f, 0.9f), 2990));

            var now = Text(tag, content, "Now", new Vector3(0f, 0.025f, -0.004f), 0.42f, "", new Color(0.92f, 0.95f, 1f),
                new Vector2(1.6f, 0.06f));
            var next = Text(tag, content, "Next", new Vector3(0f, -0.025f, -0.004f), 0.34f, "", new Color(0.6f, 0.88f, 1f),
                new Vector2(1.6f, 0.05f));
            now.overflowMode = TextOverflowModes.Ellipsis;
            next.overflowMode = TextOverflowModes.Ellipsis;

            var indicator = Undo.AddComponent<TrackIndicator>(holder.gameObject);
            var so = new SerializedObject(indicator);
            SetRef(tag, so, "m_Deck", deck);
            SetRef(tag, so, "m_Menu", menu);
            SetRef(tag, so, "m_Content", content);
            SetRef(tag, so, "m_Now", now);
            SetRef(tag, so, "m_Next", next);
            SetRef(tag, so, "m_Bar", bar);
            SetFloat(tag, so, "m_BarLength", length);
            SetArray(tag, so, "m_Fade", new[] { panel.GetComponent<Renderer>(), track.GetComponent<Renderer>(), bar.GetComponent<Renderer>() });
            so.ApplyModifiedProperties();

            content.gameObject.SetActive(false);
            return indicator;
        }

        // ================================================================== pieces

        static Transform Quad(Transform parent, string name, Vector3 centre, Vector2 size, Material material)
        {
            var quad = Primitive(parent, PrimitiveType.Quad, name, material);
            quad.localPosition = centre;
            quad.localScale = new Vector3(size.x, size.y, 1f);

            // Lit by nothing and lighting nothing: it is a sign on the wall.
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return quad;
        }

        /// <summary>URP Unlit, transparent, drawn at <paramref name="queue"/> so the panel, bar, text and cursor stack in order.</summary>
        static Material Material(string prefix, string name, Color colour, int queue)
        {
            string path = $"{prefix}{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
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
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = queue;

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);

            return material;
        }

        static void SetVector2(string tag, SerializedObject so, string path, Vector2 value)
        {
            var property = so.FindProperty(path);
            if (property == null)
                Debug.LogWarning($"[{tag}] {so.targetObject.GetType().Name} has no field '{path}'.");
            else
                property.vector2Value = value;
        }
    }
}
