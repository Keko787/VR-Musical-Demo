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
        /// A page of two tables: the middle of the left-hand one, the right-hand one, and half a table's
        /// width; the height of each heading, and of the top row and the step down. Taller rows than
        /// the list's, so each has room for a line of its own under its name.
        /// </summary>
        static readonly Vector3 k_TableColumns = new(-0.375f, 0.375f, 0.355f);
        const float k_TableHeadingY = 0.43f;
        static readonly Vector2 k_TableRows = new(0.3f, 0.125f);
        const int k_TableRowCount = 7;

        /// <summary>
        /// The menu, on the front wall: a dark glass panel, a bar behind the selected row, the title and
        /// the rows, the wand's ray and a cursor where it meets the panel. Closed until a button opens
        /// it, in the editor too. <paramref name="height"/> is its middle above the floor and
        /// <paramref name="inset"/> how far it stands off the front screen. With
        /// <paramref name="tables"/>, it also has the two tables a page can be laid out as instead of
        /// the list: each a pale frame with a heading, a rule under it and seven rows.
        /// </summary>
        public static T BuildMenu<T>(string tag, Transform root, string materialPrefix, float height = MenuHeight, float inset = Inset,
            bool tables = false)
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
            if (tables)
                BuildTables(tag, content, materialPrefix, so);
            so.ApplyModifiedProperties();
            Stack(holder);

            content.gameObject.SetActive(false);
            cursor.gameObject.SetActive(false);
            foreach (var hidden in new Component[] { moreAbove, moreBelow, scrollTrack, scrollThumb })
                hidden.gameObject.SetActive(false);
            return menu;
        }

        /// <summary>
        /// The two tables, under the title where the list's rows would be: each a pale frame on the
        /// panel with its heading at the top, a rule under the heading and its rows below. The menu
        /// shows them, and hides the list, on a page laid out as tables.
        /// </summary>
        static void BuildTables(string tag, Transform content, string materialPrefix, SerializedObject so)
        {
            var holder = NewChild(content, "Tables");
            var frame = Material(materialPrefix, "Menu_Table", new Color(0.35f, 0.55f, 0.85f, 0.1f), 2982);
            var rule = Material(materialPrefix, "Menu_Edge", new Color(0.75f, 0.9f, 1f, 0.9f), 2985);

            float width = k_TableColumns.z * 2f;
            float top = k_TableHeadingY + 0.05f;
            float bottom = k_TableRows.x - (k_TableRowCount - 0.5f) * k_TableRows.y - 0.025f;

            var headings = new List<TMP_Text>();
            var sides = new[] { new List<TMP_Text>(), new List<TMP_Text>() };
            for (int table = 0; table < 2; table++)
            {
                float x = table == 0 ? k_TableColumns.x : k_TableColumns.y;
                string side = table == 0 ? "Left" : "Right";

                Quad(holder, $"Table_{side}", new Vector3(x, (top + bottom) * 0.5f, -0.0005f), new Vector2(width, top - bottom), frame);
                Quad(holder, $"Table_{side}_Rule", new Vector3(x, k_TableHeadingY - 0.045f, -0.001f), new Vector2(width - 0.06f, 0.004f), rule);

                var heading = Text(tag, holder, $"Table_{side}_Heading", new Vector3(x, k_TableHeadingY, -0.004f), 0.5f, "",
                    new Color(0.75f, 0.9f, 1f), new Vector2(width, 0.08f));
                heading.overflowMode = TextOverflowModes.Ellipsis;
                headings.Add(heading);

                for (int i = 0; i < k_TableRowCount; i++)
                {
                    var row = Text(tag, holder, $"Table_{side}_Row_{i}", new Vector3(x, k_TableRows.x - i * k_TableRows.y, -0.004f), 0.46f,
                        "", new Color(0.86f, 0.9f, 0.96f), new Vector2(width - 0.03f, k_TableRows.y));
                    row.overflowMode = TextOverflowModes.Ellipsis;
                    row.lineSpacing = -12f;
                    sides[table].Add(row);
                }
            }

            SetRef(tag, so, "m_Tables", holder);
            SetArray(tag, so, "m_TableHeadings", headings);
            SetArray(tag, so, "m_LeftRows", sides[0]);
            SetArray(tag, so, "m_RightRows", sides[1]);
            SetVector2(tag, so, "m_TableRowLayout", k_TableRows);
            var columns = so.FindProperty("m_TableColumns");
            if (columns == null)
                Debug.LogWarning($"[{tag}] {so.targetObject.GetType().Name} has no field 'm_TableColumns'.");
            else
                columns.vector3Value = k_TableColumns;

            holder.gameObject.SetActive(false);
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
            Stack(holder);

            content.gameObject.SetActive(false);
            return indicator;
        }

        /// <summary>
        /// Draws an overlay's parts back to front in the order they are stacked: the panel, then the
        /// frames, rules and tracks on it, the highlight, the bars, the text, and the cursor and ray.
        /// They are all see-through, write no depth and sit in one queue (Unity puts URP's
        /// transparents back at 3000 whatever <see cref="Material"/> asks), so without this they sort
        /// by distance from the eye, and a row of text farther off than the panel's middle — the top
        /// and bottom rows, seen from eye height — drew first and was dimmed by the panel.
        /// </summary>
        static void Stack(Transform overlay)
        {
            foreach (var renderer in overlay.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name;
                int order;
                if (renderer is LineRenderer || name == "Cursor")
                    order = 6;
                else if (renderer.GetComponent<TMP_Text>() != null)
                    order = 5;
                else if (name == "Scroll_Thumb" || name == "Bar")
                    order = 4;
                else if (name == "Highlight")
                    order = 3;
                else if (name == "Panel")
                    order = 1;
                else
                    order = 2;                       // the edge, the tables' frames and rules, the tracks the bars run in

                renderer.sortingOrder = order;
            }
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
