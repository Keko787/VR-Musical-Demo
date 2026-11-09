using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// A menu on the front wall: a panel of rows that a room's buttons open, picked with the wand or
    /// the keys. This is the part every room's menu shares — showing it, the rows and the highlight,
    /// scrolling a long list, the wand's ray, the joystick and the keys. A subclass says what the
    /// rows are and what picking one does: the glass room's <c>RoomMenu</c> lists worlds and tracks,
    /// the visualizer's <see cref="VisualizerMenu"/> lists modes and tracks, and the two rooms are
    /// worked the same way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With the wand: point at a row and pull the trigger, or step through the rows with the
    /// joystick and pull the trigger (right goes into a row that leads somewhere, left comes back
    /// out). A ray shows where the wand points while the menu is open. At the desk: the arrow keys,
    /// Enter, and Backspace or Escape to go back. A subclass can give a row a second meaning for a
    /// double pick — the trigger or Enter twice in quick succession — as the visualizer's modes do
    /// to layer shows.
    /// </para>
    /// <para>
    /// A list longer than the panel scrolls. Strips above and below the rows say how many more there
    /// are that way; resting the wand on one scrolls the list, and the trigger on one goes a page. A
    /// bar beside the rows shows which part is showing, and the trigger on it jumps there or, held,
    /// drags. Stepping past the last row on the panel scrolls too, and at the desk the mouse wheel,
    /// Page Up and Page Down, and Home and End move through the list.
    /// </para>
    /// <para>
    /// A page can instead be two tables side by side, each under its own heading — the visualizer's
    /// modes are, 2D in one and 3D in the other. The subclass opens it with the split between them
    /// (<see cref="Open(int, int)"/>); up and down then step through a table, right and left go
    /// across to the other, and left from the left-hand table goes back as usual. Tables do not
    /// scroll. A menu built without them shows such a page as one list.
    /// </para>
    /// <para>
    /// A room's builder makes the panel, the highlight bar and the rows; this fills them in. The
    /// press that opens the menu is never also a choice in it, so a subclass should run after the
    /// component that reads the room's buttons.
    /// </para>
    /// </remarks>
    public abstract class WallMenu : MonoBehaviour
    {
        [SerializeField, Tooltip("Everything shown while the menu is open: the panel, the highlight, the text.")]
        Transform m_Content;

        [SerializeField] Transform m_Highlight;
        [SerializeField] TMP_Text m_Title;

        [SerializeField, Tooltip("The rows, top to bottom. A longer list scrolls through them.")]
        TMP_Text[] m_Rows;

        [SerializeField, Tooltip("Height of the top row and the step down to each next one, in the menu's own space.")]
        Vector2 m_RowLayout = new(0.36f, 0.085f);

        [SerializeField, Tooltip("Half the panel's size, in the menu's own space: where the wand's ray counts as on it.")]
        Vector2 m_HalfSize = new(0.75f, 0.58f);

        [Header("Wand")]
        [SerializeField, Tooltip("The MiddleVR node the wand's pose is on. Its forward is where the wand points.")]
        string m_WandNode = "HandNode";

        [SerializeField, Tooltip("Wand button that picks the row: the trigger.")]
        int m_SelectButton;

        [SerializeField, Tooltip("Shows where the wand points while the menu is open.")]
        LineRenderer m_Ray;

        [SerializeField] Transform m_Cursor;

        [SerializeField, Tooltip("Metres the ray reaches when it misses the panel.")]
        float m_RayLength = 2.5f;

        [SerializeField, Tooltip("Seconds within which a second pick of the same row counts as a double pick, on rows where that " +
            "means something. A single pick on those rows waits this long, to be sure it is not the first of two.")]
        float m_DoublePickSeconds = 0.35f;

        [Header("Scrolling")]
        [SerializeField, Tooltip("Above the rows: how many more there are above. Resting the wand on it scrolls up; the trigger " +
            "on it goes up a page.")]
        TMP_Text m_MoreAbove;

        [SerializeField, Tooltip("Below the rows: how many more there are below, and the same for scrolling down.")]
        TMP_Text m_MoreBelow;

        [SerializeField, Tooltip("The scrollbar's track beside the rows: a quad as tall as they are.")]
        Transform m_ScrollTrack;

        [SerializeField, Tooltip("The part of the track that stands for the rows showing. The trigger on the bar jumps there; " +
            "held, it drags.")]
        Transform m_ScrollThumb;

        [SerializeField, Tooltip("Seconds the wand rests on a 'more' strip before the list starts to scroll, then seconds per row.")]
        Vector2 m_HoverScroll = new(0.3f, 0.12f);

        [Header("Tables")]
        [SerializeField, Tooltip("Shown in place of the rows on a page laid out as two tables side by side: their frames, headings and " +
            "rows. Built only for a menu that has such a page; without it, that page is one list.")]
        Transform m_Tables;

        [SerializeField, Tooltip("Each table's heading, left then right.")]
        TMP_Text[] m_TableHeadings;

        [SerializeField, Tooltip("The left-hand table's rows, top to bottom.")]
        TMP_Text[] m_LeftRows;

        [SerializeField, Tooltip("The right-hand table's rows, top to bottom.")]
        TMP_Text[] m_RightRows;

        [SerializeField, Tooltip("Height of the tables' top row and the step down to each next one, in the menu's own space.")]
        Vector2 m_TableRowLayout = new(0.3f, 0.125f);

        [SerializeField, Tooltip("Across the panel, in the menu's own space: the middle of the left-hand table, the middle of the " +
            "right-hand one, and half a table's width.")]
        Vector3 m_TableColumns = new(-0.375f, 0.375f, 0.355f);

        /// <summary>A row's colour: an ordinary one, the one in use now, and the one up next.</summary>
        protected static readonly Color RowColour = new(0.86f, 0.9f, 0.96f);
        protected static readonly Color CurrentColour = new(1f, 0.86f, 0.55f);
        protected static readonly Color NextColour = new(0.6f, 0.88f, 1f);

        /// <summary>A 'more' strip, and one the wand is resting on.</summary>
        static readonly Color k_MoreColour = new(0.6f, 0.78f, 0.95f, 0.8f);
        static readonly Color k_MoreLitColour = new(0.92f, 0.97f, 1f, 1f);

        /// <summary>Metres either side of the scrollbar's track that count as pointing at it.</summary>
        const float k_BarReach = 0.035f;

        /// <summary>Where on the panel the wand points.</summary>
        enum Zone { Off, Row, Above, Below, Bar }

        /// <summary>
        /// What each row on the page stands for. The meaning is the subclass's own — an index into
        /// its list of worlds, modes or tracks, or −1 for the way back.
        /// </summary>
        protected readonly List<int> Entries = new();

        readonly StringBuilder m_Text = new();
        readonly StringBuilder m_TitleText = new();

        bool m_Open;
        int m_OpenedFrame = -1;
        int m_Selected;
        int m_First;                                 // the entry in the top row, when the list scrolls
        int m_Hovered = -1;
        float m_Pop;
        float m_RepeatAt;
        int m_StickDirection;
        Transform m_Wand;
        float m_NextWandSearch;
        int m_Pending = -1;                          // an entry picked once, waiting to see if a second pick follows
        float m_PendingUntil;
        Zone m_Zone;
        Zone m_LitZone;
        float m_PointY;                              // how high the wand's ray meets the panel's plane, in the menu's own space
        int m_HoverDirection;
        float m_HoverAt;
        bool m_Dragging;
        int m_Split = -1;                            // on a page of two tables, the entries before this are the left-hand table's
        bool m_HighlightRead;                        // the highlight's size and place across, as built for the list
        Vector3 m_ListHighlightScale;
        float m_ListHighlightX;

        public bool IsOpen => m_Open;
        protected int VisibleRows => m_Rows != null ? m_Rows.Length : 0;

        /// <summary>A page's worth of rows to scroll: all but one, so the row at the edge is still there to read on.</summary>
        int Page => Mathf.Max(1, VisibleRows - 1);

        bool Scrollable => !InTables && Entries.Count > VisibleRows;

        bool CanScroll(int direction) => !InTables && (direction < 0 ? m_First > 0 : m_First + VisibleRows < Entries.Count);

        /// <summary>Whether this menu was built with the two tables, for a page that wants them.</summary>
        protected bool HasTables => m_Tables != null && m_LeftRows != null && m_LeftRows.Length > 0 && m_RightRows != null && m_RightRows.Length > 0;

        /// <summary>Whether the page showing is laid out as two tables. One asked for on a menu built without them is one list.</summary>
        protected bool InTables => m_Split >= 0 && HasTables;

        protected virtual void Awake() => Show(false);

        // ---------------------------------------------------------------- what a subclass says

        /// <summary>The panel's heading for the page showing.</summary>
        protected abstract string Title();

        /// <summary>Writes the row for <paramref name="entry"/>, and may change its colour from <see cref="RowColour"/>.</summary>
        protected abstract void Describe(int entry, StringBuilder text, ref Color colour);

        /// <summary>The selected row was picked. Close and act, or show another page with <see cref="ShowPage"/>.</summary>
        protected abstract void Choose(int entry);

        /// <summary>Backspace, Escape, the left arrow, or the joystick left. Closes the menu unless a page leads back somewhere.</summary>
        protected virtual void Back() => Close();

        /// <summary>Whether right on the joystick or the arrow keys picks <paramref name="entry"/>: rows that lead into another page.</summary>
        protected virtual bool CanEnter(int entry) => false;

        /// <summary>
        /// Whether picking <paramref name="entry"/> twice in quick succession does something of its
        /// own, <see cref="DoublePick"/>. On such a row a single pick waits a moment before it
        /// counts, so the rows that don't have one stay instant.
        /// </summary>
        protected virtual bool CanDoublePick(int entry) => false;

        /// <summary>The selected row was picked twice in quick succession. Only called where <see cref="CanDoublePick"/> says so.</summary>
        protected virtual void DoublePick(int entry) { }

        /// <summary>False once what the page lists has gone (rebuilt under it in the editor, say), which closes the menu.</summary>
        protected virtual bool Valid => true;

        /// <summary>The heading over a table, 0 the left-hand one, on a page laid out as two.</summary>
        protected virtual string TableHeading(int table) => "";

        // ---------------------------------------------------------------- opening

        /// <summary>
        /// Shows the page in <see cref="Entries"/>, which the subclass has just filled, with
        /// <paramref name="selected"/> selected. Opens the menu if it is closed. With a
        /// <paramref name="split"/>, the page is two tables side by side: the entries before it in
        /// the left-hand one, the rest in the right-hand one.
        /// </summary>
        protected void Open(int selected, int split = -1)
        {
            if (!m_Open)
            {
                m_OpenedFrame = Time.frameCount;
                m_Pop = 0f;

                // A joystick push that brought the menu up is not also a step through it; held, it repeats as usual.
                m_StickDirection = StickDirection(MiddleVRWand.Axis);
                m_RepeatAt = Time.time + 0.45f;
            }

            m_Open = true;
            Show(true);
            ShowPage(selected, false, split);
        }

        /// <summary>
        /// Moves to a page the subclass has just filled into <see cref="Entries"/> while the menu
        /// is open. <paramref name="keepHover"/> keeps the wand's place on the panel, for a page
        /// that is the same list again; <paramref name="split"/> is as for <see cref="Open(int, int)"/>.
        /// </summary>
        protected void ShowPage(int selected, bool keepHover = false, int split = -1)
        {
            m_Split = split >= 0 ? Mathf.Min(split, Entries.Count) : -1;
            m_Selected = Mathf.Clamp(selected, 0, Mathf.Max(0, Entries.Count - 1));
            m_First = 0;
            m_Pending = -1;
            if (!keepHover)
                m_Hovered = -1;

            if (InTables)
            {
                if (TableShown(0) < m_Split || TableShown(1) < Entries.Count - m_Split)
                    Debug.LogWarning($"[{GetType().Name}] A table has more entries than rows; the last ones are left off. " +
                                     "Build the menu with more table rows.", this);
                m_Selected = ClampToShown(m_Selected);
            }

            Refresh();
        }

        public void Close()
        {
            m_Open = false;
            m_Pending = -1;
            m_Dragging = false;
            Show(false);
        }

        void Show(bool show)
        {
            if (m_Content != null)
                m_Content.gameObject.SetActive(show);
            if (m_Ray != null)
                m_Ray.enabled = false;
            if (m_Cursor != null)
                m_Cursor.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!m_Open)
                return;

            if (!Valid)
            {
                Close();
                return;
            }

            m_Pop = Mathf.MoveTowards(m_Pop, 1f, Time.deltaTime / 0.16f);
            if (m_Content != null)
            {
                float eased = 1f - (1f - m_Pop) * (1f - m_Pop) * (1f - m_Pop);
                m_Content.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, eased);
            }

            // The press that opened it is not also a choice in it.
            if (Time.frameCount == m_OpenedFrame)
            {
                Point();
                return;
            }

            Point();
            HoverScroll();
            ReadStick();
            ReadKeys();
            ReadWheel();

            if (m_Open && m_SelectButton >= 0)
            {
                if (MiddleVRWand.PressedThisFrame(m_SelectButton))
                    Pull();
                Drag();
            }

            // No second pick came: it was a single one.
            if (m_Open && m_Pending >= 0 && Time.unscaledTime >= m_PendingUntil)
            {
                int entry = m_Pending;
                m_Pending = -1;
                Choose(entry);
            }
        }

        // ---------------------------------------------------------------- input

        /// <summary>Up or down a row, round from the end to the start; on a page of tables, round within the table.</summary>
        void Move(int step)
        {
            if (Entries.Count == 0)
                return;

            if (InTables)
            {
                int table = TableOf(m_Selected);
                int shown = TableShown(table);
                if (shown == 0)
                    return;

                int row = m_Selected - TableStart(table);
                m_Selected = TableStart(table) + ((row + step) % shown + shown) % shown;
                Refresh();
                return;
            }

            m_Selected = ((m_Selected + step) % Entries.Count + Entries.Count) % Entries.Count;
            Refresh();
        }

        /// <summary>
        /// Selects <paramref name="index"/>, held to the list's ends rather than wrapping round: Page Up
        /// and Down, Home and End. On a page of tables, held to the table's ends.
        /// </summary>
        void MoveTo(int index)
        {
            if (Entries.Count == 0)
                return;

            if (InTables)
            {
                int table = TableOf(m_Selected);
                int shown = TableShown(table);
                if (shown == 0)
                    return;

                m_Selected = Mathf.Clamp(index, TableStart(table), TableStart(table) + shown - 1);
                Refresh();
                return;
            }

            m_Selected = Mathf.Clamp(index, 0, Entries.Count - 1);
            Refresh();
        }

        /// <summary>
        /// Over to the other table, <paramref name="direction"/> 1 for the right-hand one and −1 for the
        /// left, at the same row or the nearest it has. False if there is no table that way, so right
        /// and left can go on meaning what they do on a list.
        /// </summary>
        bool Across(int direction)
        {
            if (!InTables)
                return false;

            int table = TableOf(m_Selected);
            int other = table + direction;
            if (other < 0 || other > 1 || TableShown(other) == 0)
                return false;

            int row = m_Selected - TableStart(table);
            m_Selected = TableStart(other) + Mathf.Min(row, TableShown(other) - 1);
            Refresh();
            return true;
        }

        /// <summary>Scrolls the list <paramref name="rows"/> down (up if negative), taking the selection along if it would leave the panel.</summary>
        void Scroll(int rows) => ScrollTo(m_First + rows);

        void ScrollTo(int first)
        {
            if (InTables)
                return;

            int rows = Mathf.Max(1, VisibleRows);
            first = Mathf.Clamp(first, 0, Mathf.Max(0, Entries.Count - rows));
            if (first == m_First)
                return;

            m_First = first;
            m_Selected = Mathf.Clamp(m_Selected, m_First, m_First + rows - 1);
            m_Hovered = -1;                          // the row under the ray is another entry now
            Refresh();
        }

        /// <summary>The trigger: picks the selected row, or on a 'more' strip goes a page that way, or on the bar takes hold of it.</summary>
        void Pull()
        {
            switch (m_Zone)
            {
                case Zone.Above:
                    Scroll(-Page);
                    break;
                case Zone.Below:
                    Scroll(Page);
                    break;
                case Zone.Bar:
                    m_Dragging = true;
                    DragTo(m_PointY);
                    break;
                default:
                    ChooseSelected();
                    break;
            }
        }

        /// <summary>While the trigger is held after a pull on the bar, the list follows the ray up and down, on the bar or off it.</summary>
        void Drag()
        {
            if (!m_Dragging)
                return;

            if (!MiddleVRWand.Held(m_SelectButton))
            {
                m_Dragging = false;
                return;
            }

            DragTo(m_PointY);
        }

        /// <summary>Scrolls so the bar's thumb is centred on <paramref name="y"/>, as near as the list allows.</summary>
        void DragTo(float y)
        {
            int most = Entries.Count - VisibleRows;
            if (m_ScrollTrack == null || most <= 0 || InTables)
                return;

            float length = m_ScrollTrack.localScale.y;
            float thumb = ThumbLength(length);
            float top = m_ScrollTrack.localPosition.y + length * 0.5f;
            float t = Mathf.InverseLerp(top - thumb * 0.5f, top - length + thumb * 0.5f, y);
            ScrollTo(Mathf.RoundToInt(t * most));
        }

        /// <summary>Resting the wand on a 'more' strip scrolls that way: after a moment, then a row at a time until it moves off.</summary>
        void HoverScroll()
        {
            int direction = m_Zone == Zone.Above ? -1 : m_Zone == Zone.Below ? 1 : 0;
            if (direction == 0 || !CanScroll(direction))
            {
                m_HoverDirection = 0;
                return;
            }

            if (direction != m_HoverDirection)
            {
                m_HoverDirection = direction;
                m_HoverAt = Time.unscaledTime + m_HoverScroll.x;
                return;
            }

            if (Time.unscaledTime < m_HoverAt)
                return;

            m_HoverAt = Time.unscaledTime + Mathf.Max(0.02f, m_HoverScroll.y);
            Scroll(direction);
        }

        void ReadWheel()
        {
            var mouse = Mouse.current;
            if (!m_Open || mouse == null)
                return;

            float y = mouse.scroll.ReadValue().y;
            if (y == 0f)
                return;

            // A notch is 120 on some platforms and 1 on others; two rows a notch either way.
            float size = Mathf.Abs(y);
            int notches = Mathf.Max(1, Mathf.RoundToInt(size >= 60f ? size / 120f : size));
            Scroll(y > 0f ? -2 * notches : 2 * notches);
        }

        /// <summary>
        /// The trigger, Enter, or right into a row. A row that can be double-picked holds its first
        /// pick until the second comes or the moment passes; a pick of another row in that moment
        /// replaces it, since the wand has moved on.
        /// </summary>
        void ChooseSelected()
        {
            if (m_Selected < 0 || m_Selected >= Entries.Count)
                return;

            int entry = Entries[m_Selected];
            if (!CanDoublePick(entry))
            {
                m_Pending = -1;
                Choose(entry);
            }
            else if (entry == m_Pending && Time.unscaledTime < m_PendingUntil)
            {
                m_Pending = -1;
                DoublePick(entry);
            }
            else
            {
                m_Pending = entry;
                m_PendingUntil = Time.unscaledTime + Mathf.Max(0.05f, m_DoublePickSeconds);
            }
        }

        bool SelectedCanEnter => m_Selected >= 0 && m_Selected < Entries.Count && CanEnter(Entries[m_Selected]);

        void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.upArrowKey.wasPressedThisFrame)
                Move(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame)
                Move(1);
            if (keyboard.pageUpKey.wasPressedThisFrame)
                MoveTo(m_Selected - Page);
            if (keyboard.pageDownKey.wasPressedThisFrame)
                MoveTo(m_Selected + Page);
            if (keyboard.homeKey.wasPressedThisFrame)
                MoveTo(0);
            if (keyboard.endKey.wasPressedThisFrame)
                MoveTo(Entries.Count - 1);

            // On a page of tables, right and left go across first; past the last table they mean what they do on a list.
            bool right = keyboard.rightArrowKey.wasPressedThisFrame && !Across(1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || (right && SelectedCanEnter))
                ChooseSelected();

            bool left = keyboard.leftArrowKey.wasPressedThisFrame && !Across(-1);
            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || left)
                Back();
        }

        /// <summary>
        /// The joystick steps through the rows, repeating while held; right goes into a row that leads
        /// somewhere, left back out. On a page of tables, right and left go across to the other table first.
        /// </summary>
        void ReadStick()
        {
            int direction = StickDirection(MiddleVRWand.Axis);
            if (direction == 0)
            {
                m_StickDirection = 0;
                return;
            }

            bool fresh = direction != m_StickDirection;
            if (!fresh && (Mathf.Abs(direction) == 2 || Time.time < m_RepeatAt))
                return;

            m_StickDirection = direction;
            m_RepeatAt = Time.time + (fresh ? 0.45f : 0.16f);

            if (direction == 2)
            {
                if (!Across(1) && SelectedCanEnter)
                    ChooseSelected();
            }
            else if (direction == -2)
            {
                if (!Across(-1))
                    Back();
            }
            else if (Mathf.Abs(direction) == 1)
            {
                Move(direction);
            }
        }

        /// <summary>Where the joystick is pushed: −1 up a row, 1 down, 2 right, −2 left, 0 at rest.</summary>
        static int StickDirection(Vector2 axis)
        {
            if (Mathf.Abs(axis.y) < 0.3f && Mathf.Abs(axis.x) < 0.3f)
                return 0;
            return axis.y > 0.6f ? -1 : axis.y < -0.6f ? 1 : axis.x > 0.7f ? 2 : axis.x < -0.7f ? -2 : 0;
        }

        /// <summary>The wand's ray onto the panel: the row it points at is selected, and a line shows where it goes.</summary>
        void Point()
        {
            var wand = Wand();
            if (wand == null)
            {
                m_Zone = Zone.Off;
                return;
            }

            var origin = wand.position;
            var direction = wand.forward;
            var plane = new Plane(-transform.forward, transform.position);
            var end = origin + direction * m_RayLength;
            bool onPanel = false;
            m_Zone = Zone.Off;

            if (plane.Raycast(new Ray(origin, direction), out float distance) && distance > 0f)
            {
                var hit = origin + direction * distance;
                var local = transform.InverseTransformPoint(hit);
                m_PointY = local.y;
                bool inside = Mathf.Abs(local.x) <= m_HalfSize.x && Mathf.Abs(local.y) <= m_HalfSize.y;
                if (inside && InTables)
                {
                    end = hit;
                    onPanel = true;

                    // A page of tables: the cell under the ray, in either table.
                    int cell = CellAt(local, out int entry);
                    m_Zone = cell >= 0 ? Zone.Row : Zone.Off;
                    if (cell >= 0 && cell != m_Hovered)
                    {
                        m_Hovered = cell;
                        m_Selected = entry;
                        Refresh();
                    }
                }
                else if (inside)
                {
                    end = hit;
                    onPanel = true;
                    m_Zone = ZoneAt(local);

                    // Dragging the bar, the ray can cross the rows without picking them up.
                    int row = Mathf.RoundToInt((m_RowLayout.x - local.y) / Mathf.Max(0.01f, m_RowLayout.y));
                    int entry = m_First + row;
                    if (m_Zone == Zone.Row && !m_Dragging && row >= 0 && row < VisibleRows && entry < Entries.Count && row != m_Hovered)
                    {
                        m_Hovered = row;
                        m_Selected = entry;
                        Refresh();
                    }
                }
            }

            if (m_Zone != Zone.Row)
                m_Hovered = -1;

            LightStrips();

            if (m_Ray != null)
            {
                m_Ray.enabled = true;
                m_Ray.positionCount = 2;
                m_Ray.SetPosition(0, origin);
                m_Ray.SetPosition(1, end);
            }

            if (m_Cursor != null)
            {
                m_Cursor.gameObject.SetActive(onPanel);
                m_Cursor.position = end - transform.forward * 0.004f;
            }
        }

        /// <summary>
        /// What the ray is on, from where it meets the panel: a row, a 'more' strip just above or
        /// below them while there is more that way, or the scrollbar beside them.
        /// </summary>
        Zone ZoneAt(Vector3 local)
        {
            float top = m_RowLayout.x + m_RowLayout.y * 0.5f;
            float bottom = top - VisibleRows * m_RowLayout.y;
            float strip = m_RowLayout.y * 1.25f;

            if (local.y > top)
                return local.y <= top + strip && CanScroll(-1) ? Zone.Above : Zone.Off;
            if (local.y < bottom)
                return local.y >= bottom - strip && CanScroll(1) ? Zone.Below : Zone.Off;
            if (m_ScrollTrack != null && Scrollable && local.x >= m_ScrollTrack.localPosition.x - k_BarReach)
                return Zone.Bar;
            return Zone.Row;
        }

        /// <summary>
        /// On a page of tables, the cell the ray meets the panel in: an id for it, distinct from any
        /// list row's, and the entry it shows; −1 between the tables, above or below their rows.
        /// </summary>
        int CellAt(Vector3 local, out int entry)
        {
            entry = -1;
            float half = m_TableColumns.z;
            int table = Mathf.Abs(local.x - m_TableColumns.x) <= half ? 0 : Mathf.Abs(local.x - m_TableColumns.y) <= half ? 1 : -1;
            if (table < 0)
                return -1;

            float step = Mathf.Max(0.01f, m_TableRowLayout.y);
            float fromTop = (m_TableRowLayout.x + step * 0.5f - local.y) / step;
            int row = Mathf.FloorToInt(fromTop);
            if (fromTop < 0f || row >= TableShown(table))
                return -1;

            entry = TableStart(table) + row;
            return 1000 + table * 100 + row;
        }

        // ---------------------------------------------------------------- tables

        int TableOf(int index) => index < m_Split ? 0 : 1;
        int TableStart(int table) => table == 0 ? 0 : m_Split;
        TMP_Text[] TableRows(int table) => table == 0 ? m_LeftRows : m_RightRows;

        /// <summary>How many of a table's entries have a row to show in.</summary>
        int TableShown(int table)
        {
            var rows = TableRows(table);
            int count = table == 0 ? m_Split : Entries.Count - m_Split;
            return Mathf.Max(0, Mathf.Min(count, rows != null ? rows.Length : 0));
        }

        /// <summary><paramref name="index"/>, or the nearest entry that has a row to show in: the first of the other table if its own has none.</summary>
        int ClampToShown(int index)
        {
            int table = TableOf(index);
            if (TableShown(table) == 0)
            {
                table = 1 - table;
                if (TableShown(table) == 0)
                    return 0;
                return TableStart(table);
            }

            return Mathf.Clamp(index, TableStart(table), TableStart(table) + TableShown(table) - 1);
        }

        /// <summary>The 'more' strip the wand rests on brightens, so it is plain that resting there is doing something.</summary>
        void LightStrips()
        {
            var lit = m_Dragging ? Zone.Bar : m_Zone;
            if (lit == m_LitZone)
                return;

            m_LitZone = lit;
            if (m_MoreAbove != null)
                m_MoreAbove.color = lit == Zone.Above ? k_MoreLitColour : k_MoreColour;
            if (m_MoreBelow != null)
                m_MoreBelow.color = lit == Zone.Below ? k_MoreLitColour : k_MoreColour;
        }

        /// <summary>MiddleVR's wand node, once the kernel has made it. Looked for now and then, not every frame.</summary>
        Transform Wand()
        {
            if (m_Wand != null || !MiddleVRWand.Available || string.IsNullOrEmpty(m_WandNode))
                return m_Wand;
            if (Time.unscaledTime < m_NextWandSearch)
                return null;

            m_NextWandSearch = Time.unscaledTime + 1f;
            var node = GameObject.Find(m_WandNode);
            m_Wand = node != null ? node.transform : null;
            return m_Wand;
        }

        // ---------------------------------------------------------------- display

        /// <summary>Redraws the title, the rows and the highlight. A subclass calls this when what it lists changes under it.</summary>
        protected void Refresh()
        {
            bool tables = InTables;
            if (!tables)
            {
                // Scroll just far enough to keep the selected row on the panel.
                int rows = Mathf.Max(1, VisibleRows);
                if (m_Selected < m_First)
                    m_First = m_Selected;
                else if (m_Selected >= m_First + rows)
                    m_First = m_Selected - rows + 1;
                m_First = Mathf.Clamp(m_First, 0, Mathf.Max(0, Entries.Count - rows));
            }

            if (m_Title != null)
                m_Title.text = Title();

            if (m_Tables != null)
                m_Tables.gameObject.SetActive(tables);

            if (m_Rows != null)
            {
                for (int i = 0; i < m_Rows.Length; i++)
                {
                    int index = m_First + i;
                    Fill(m_Rows[i], !tables && index < Entries.Count ? index : -1);
                }
            }

            if (tables)
            {
                for (int table = 0; table < 2; table++)
                {
                    if (m_TableHeadings != null && table < m_TableHeadings.Length && m_TableHeadings[table] != null)
                        m_TableHeadings[table].text = TableHeading(table);

                    var rows = TableRows(table);
                    int shown = TableShown(table);
                    for (int i = 0; i < rows.Length; i++)
                        Fill(rows[i], i < shown ? TableStart(table) + i : -1);
                }
            }

            PlaceHighlight(tables);
            ShowScroll();
        }

        /// <summary>Writes the entry at <paramref name="index"/> into <paramref name="row"/>, or hides the row for −1.</summary>
        void Fill(TMP_Text row, int index)
        {
            if (row == null)
                return;

            bool used = index >= 0 && index < Entries.Count;
            row.gameObject.SetActive(used);
            if (!used)
                return;

            m_Text.Clear();
            var colour = RowColour;
            Describe(Entries[index], m_Text, ref colour);
            row.text = m_Text.ToString();
            row.color = colour;
        }

        /// <summary>The bar behind the selected row: across the list's rows, or across one table's cell on a page of tables.</summary>
        void PlaceHighlight(bool tables)
        {
            if (m_Highlight == null)
                return;

            if (!m_HighlightRead)
            {
                m_ListHighlightScale = m_Highlight.localScale;
                m_ListHighlightX = m_Highlight.localPosition.x;
                m_HighlightRead = true;
            }

            var at = m_Highlight.localPosition;
            var scale = m_ListHighlightScale;
            bool shown = m_Selected >= 0 && m_Selected < Entries.Count;

            if (tables)
            {
                int table = TableOf(m_Selected);
                int row = m_Selected - TableStart(table);
                at.x = table == 0 ? m_TableColumns.x : m_TableColumns.y;
                at.y = m_TableRowLayout.x - row * m_TableRowLayout.y;
                scale.x = m_TableColumns.z * 2f;
                scale.y = m_TableRowLayout.y * 0.92f;
                shown &= row < TableShown(table);
            }
            else
            {
                at.x = m_ListHighlightX;
                at.y = m_RowLayout.x - (m_Selected - m_First) * m_RowLayout.y;
            }

            m_Highlight.localPosition = at;
            m_Highlight.localScale = scale;
            m_Highlight.gameObject.SetActive(shown);
        }

        /// <summary>
        /// The 'more' strips, each only while there is more that way, and the bar, only while the list
        /// is longer than the panel. Tables do not scroll, so a page of them has neither.
        /// </summary>
        void ShowScroll()
        {
            if (InTables)
            {
                ShowMore(m_MoreAbove, 0, "▲");
                ShowMore(m_MoreBelow, 0, "▼");
                if (m_ScrollTrack != null)
                    m_ScrollTrack.gameObject.SetActive(false);
                if (m_ScrollThumb != null)
                    m_ScrollThumb.gameObject.SetActive(false);
                return;
            }

            int rows = Mathf.Max(1, VisibleRows);
            ShowMore(m_MoreAbove, m_First, "▲");
            ShowMore(m_MoreBelow, Entries.Count - m_First - rows, "▼");

            bool scrollable = Scrollable && m_ScrollTrack != null;
            if (m_ScrollTrack != null)
                m_ScrollTrack.gameObject.SetActive(scrollable);
            if (m_ScrollThumb == null)
                return;

            m_ScrollThumb.gameObject.SetActive(scrollable);
            if (!scrollable)
                return;

            float length = m_ScrollTrack.localScale.y;
            float thumb = ThumbLength(length);
            float top = m_ScrollTrack.localPosition.y + length * 0.5f;
            float t = (float)m_First / (Entries.Count - rows);

            var scale = m_ScrollThumb.localScale;
            scale.y = thumb;
            m_ScrollThumb.localScale = scale;
            var at = m_ScrollThumb.localPosition;
            at.y = top - thumb * 0.5f - t * (length - thumb);
            m_ScrollThumb.localPosition = at;
        }

        static void ShowMore(TMP_Text strip, int count, string arrow)
        {
            if (strip == null)
                return;

            strip.gameObject.SetActive(count > 0);
            if (count > 0)
                strip.text = $"{arrow}   {count} more   {arrow}";
        }

        /// <summary>The thumb's length on a track <paramref name="track"/> long: the share of the list showing, and never too small to point at.</summary>
        float ThumbLength(float track) => track * Mathf.Clamp((float)VisibleRows / Mathf.Max(1, Entries.Count), 0.1f, 1f);

        /// <summary><paramref name="title"/>, and, when the list is longer than the panel, which part of it is showing: "TRACKS   3–13 of 13".</summary>
        protected string Paged(string title)
        {
            int rows = VisibleRows;
            if (Entries.Count <= rows)
                return title;

            m_TitleText.Clear();
            m_TitleText.Append(title).Append("   ").Append(m_First + 1).Append('–').Append(Mathf.Min(Entries.Count, m_First + rows))
                .Append(" of ").Append(Entries.Count);
            return m_TitleText.ToString();
        }

        // ---------------------------------------------------------------- the tracks, which every room's menu has

        /// <summary>The playlist, in order, as the page's rows.</summary>
        protected void FillTracks(MusicDeck deck)
        {
            Entries.Clear();
            for (int i = 0; deck != null && i < deck.TrackCount; i++)
                Entries.Add(i);
        }

        /// <summary>A track's row: the one playing in amber, marked playing or paused, the one up next in blue.</summary>
        protected static void DescribeTrack(MusicDeck deck, int index, StringBuilder text, ref Color colour)
        {
            bool playing = index == deck.Track;
            bool next = !playing && index == deck.NextIndex;
            if (playing)
            {
                colour = CurrentColour;
                text.Append("•  ");
            }
            else if (next)
            {
                colour = NextColour;
            }

            text.Append(TrackIndicator.TitleOf(deck.ClipAt(index)));
            if (playing)
                text.Append(deck.Paused ? "   · paused" : "   · playing");
            else if (next)
                text.Append("   » up next");
        }
    }
}
