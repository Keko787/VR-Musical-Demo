using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRShootingGallery.GlassRoom
{
    /// <summary>
    /// The world menu: a panel on the front wall, over the window, listing the worlds outside by
    /// group — Shelter, Space, Norway — and then the worlds in the group picked. The world button
    /// opens and closes it; the world picked builds outside as the menu closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With the wand: point at a row and pull the trigger, or step through the rows with the
    /// joystick (right goes into a group, left comes back out) and pull the trigger. A ray shows
    /// where the wand points while the menu is open. At the desk: the arrow keys, Enter, and
    /// Backspace or Escape to go back.
    /// </para>
    /// <para>
    /// The builder makes the panel, the highlight bar and the rows; this fills them in. The groups
    /// come from each <see cref="SimWorld"/>'s group, in the order the worlds are listed.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(-70)] // after the Simulation, which opens it, has read its buttons
    public class WorldMenu : MonoBehaviour
    {
        [SerializeField] Simulation m_Simulation;

        [SerializeField, Tooltip("Everything shown while the menu is open: the panel, the highlight, the text.")]
        Transform m_Content;

        [SerializeField] Transform m_Highlight;
        [SerializeField] TMP_Text m_Title;

        [SerializeField, Tooltip("The rows, top to bottom. Enough for the biggest group and a way back.")]
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

        static readonly Color k_Row = new(0.86f, 0.9f, 0.96f);
        static readonly Color k_Current = new(1f, 0.86f, 0.55f);

        readonly List<string> m_Groups = new();
        readonly List<int> m_Entries = new();      // world indices on the worlds page; -1 is the way back
        readonly StringBuilder m_Text = new();

        bool m_Open;
        int m_OpenedFrame = -1;
        int m_Group = -1;                            // -1: the groups page
        int m_Selected;
        int m_Hovered = -1;
        float m_Pop;
        float m_RepeatAt;
        int m_StickDirection;
        Transform m_Wand;
        float m_NextWandSearch;

        public bool IsOpen => m_Open;

        void Awake() => Show(false);

        /// <summary>Opens the menu on the page of the world outside, or closes it.</summary>
        public void Toggle()
        {
            if (m_Open)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (m_Simulation == null || m_Simulation.WorldCount == 0)
                return;

            CollectGroups();
            var world = m_Simulation.World;
            m_Group = world != null ? m_Groups.IndexOf(world.Group) : -1;
            m_Selected = 0;
            Fill();
            m_Selected = Mathf.Max(0, m_Group >= 0 ? m_Entries.IndexOf(m_Simulation.WorldIndex) : 0);

            m_Open = true;
            m_OpenedFrame = Time.frameCount;
            m_Pop = 0f;
            m_Hovered = -1;
            Show(true);
            Refresh();
        }

        public void Close()
        {
            m_Open = false;
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

        void CollectGroups()
        {
            m_Groups.Clear();
            for (int i = 0; i < m_Simulation.WorldCount; i++)
            {
                var world = m_Simulation.WorldAt(i);
                if (world != null && !m_Groups.Contains(world.Group))
                    m_Groups.Add(world.Group);
            }
        }

        /// <summary>The rows of the page being shown: the groups, or the worlds of one group and the way back.</summary>
        void Fill()
        {
            m_Entries.Clear();
            if (m_Group < 0)
            {
                for (int i = 0; i < m_Groups.Count; i++)
                    m_Entries.Add(i);
                return;
            }

            for (int i = 0; i < m_Simulation.WorldCount; i++)
            {
                var world = m_Simulation.WorldAt(i);
                if (world != null && world.Group == m_Groups[m_Group])
                    m_Entries.Add(i);
            }

            // One group only: the groups page has nothing to offer, so there is no way back to it.
            if (m_Groups.Count > 1)
                m_Entries.Add(-1);
        }

        void Update()
        {
            if (!m_Open)
                return;

            // A group can't be emptied while open, but the worlds can be rebuilt under it in the editor.
            if (m_Simulation == null)
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
            ReadStick();
            ReadKeys();

            if (m_SelectButton >= 0 && MiddleVRWand.PressedThisFrame(m_SelectButton))
                Choose();
        }

        // ---------------------------------------------------------------- input

        void Move(int step)
        {
            if (m_Entries.Count == 0)
                return;

            m_Selected = ((m_Selected + step) % m_Entries.Count + m_Entries.Count) % m_Entries.Count;
            Refresh();
        }

        void Choose()
        {
            if (m_Selected < 0 || m_Selected >= m_Entries.Count)
                return;

            int entry = m_Entries[m_Selected];
            if (m_Group < 0)
            {
                Enter(entry);
            }
            else if (entry < 0)
            {
                Back();
            }
            else
            {
                Close();
                m_Simulation.ShowWorld(entry);
            }
        }

        void Enter(int group)
        {
            int from = m_Group;
            m_Group = group;
            Fill();
            m_Selected = Mathf.Max(0, m_Entries.IndexOf(m_Simulation.WorldIndex));
            if (from != group)
                m_Hovered = -1;
            Refresh();
        }

        void Back()
        {
            if (m_Group < 0)
            {
                Close();
                return;
            }

            int was = m_Group;
            m_Group = -1;
            Fill();
            m_Selected = was;
            m_Hovered = -1;
            Refresh();
        }

        void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.upArrowKey.wasPressedThisFrame)
                Move(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame)
                Move(1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                (keyboard.rightArrowKey.wasPressedThisFrame && m_Group < 0))
                Choose();
            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                Back();
        }

        /// <summary>The joystick steps through the rows, repeating while held; right goes into a group, left back out.</summary>
        void ReadStick()
        {
            var axis = MiddleVRWand.Axis;
            int direction = axis.y > 0.6f ? -1 : axis.y < -0.6f ? 1 : axis.x > 0.7f ? 2 : axis.x < -0.7f ? -2 : 0;
            if (Mathf.Abs(axis.y) < 0.3f && Mathf.Abs(axis.x) < 0.3f)
                direction = 0;

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

            if (direction == 2 && m_Group < 0)
                Choose();
            else if (direction == -2)
                Back();
            else if (Mathf.Abs(direction) == 1)
                Move(direction);
        }

        /// <summary>The wand's ray onto the panel: the row it points at is selected, and a line shows where it goes.</summary>
        void Point()
        {
            var wand = Wand();
            if (wand == null)
                return;

            var origin = wand.position;
            var direction = wand.forward;
            var plane = new Plane(-transform.forward, transform.position);
            var end = origin + direction * m_RayLength;
            bool onPanel = false;

            if (plane.Raycast(new Ray(origin, direction), out float distance) && distance > 0f)
            {
                var hit = origin + direction * distance;
                var local = transform.InverseTransformPoint(hit);
                if (Mathf.Abs(local.x) <= m_HalfSize.x && Mathf.Abs(local.y) <= m_HalfSize.y)
                {
                    end = hit;
                    onPanel = true;
                    int row = Mathf.RoundToInt((m_RowLayout.x - local.y) / Mathf.Max(0.01f, m_RowLayout.y));
                    if (row >= 0 && row < m_Entries.Count && row != m_Hovered)
                    {
                        m_Hovered = row;
                        m_Selected = row;
                        Refresh();
                    }
                }
            }

            if (!onPanel)
                m_Hovered = -1;

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

        void Refresh()
        {
            var current = m_Simulation.World;

            if (m_Title != null)
                m_Title.text = m_Group < 0 ? "WORLDS" : m_Groups[m_Group].ToUpperInvariant();

            if (m_Rows == null)
                return;

            for (int i = 0; i < m_Rows.Length; i++)
            {
                var row = m_Rows[i];
                if (row == null)
                    continue;

                bool used = i < m_Entries.Count;
                row.gameObject.SetActive(used);
                if (!used)
                    continue;

                int entry = m_Entries[i];
                m_Text.Clear();
                bool here;
                if (m_Group < 0)
                {
                    string group = m_Groups[entry];
                    here = current != null && current.Group == group;
                    m_Text.Append(group).Append("   ").Append(CountIn(group)).Append("  ›");
                }
                else if (entry < 0)
                {
                    here = false;
                    m_Text.Append("‹  Back");
                }
                else
                {
                    var world = m_Simulation.WorldAt(entry);
                    here = world == current;
                    m_Text.Append(world != null ? world.Title : "?");
                }

                row.text = m_Text.ToString();
                row.color = here ? k_Current : k_Row;
            }

            if (m_Highlight != null)
            {
                var at = m_Highlight.localPosition;
                at.y = m_RowLayout.x - m_Selected * m_RowLayout.y;
                m_Highlight.localPosition = at;
                m_Highlight.gameObject.SetActive(m_Selected >= 0 && m_Selected < m_Entries.Count);
            }
        }

        int CountIn(string group)
        {
            int count = 0;
            for (int i = 0; i < m_Simulation.WorldCount; i++)
            {
                var world = m_Simulation.WorldAt(i);
                if (world != null && world.Group == group)
                    count++;
            }

            return count;
        }
    }
}
