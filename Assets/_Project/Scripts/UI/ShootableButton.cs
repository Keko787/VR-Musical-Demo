using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using VRShootingGallery.Core;
using VRShootingGallery.Targets;

namespace VRShootingGallery.UI
{
    /// <summary>What a <see cref="ShootableButton"/> does to the round when it is shot.</summary>
    public enum MatchButtonAction
    {
        Start,
        Restart,
        Quit,
        None,
    }

    /// <summary>
    /// A physical button you press by shooting it. The CAVE has no pointer-and-click, so the
    /// control panel reuses the projectile pipeline: the button is just another
    /// <see cref="IShootable"/>. Destructive actions can ask for a second shot to confirm.
    /// </summary>
    public class ShootableButton : MonoBehaviour, IShootable
    {
        [Header("Behaviour")]
        [SerializeField] MatchButtonAction m_Action = MatchButtonAction.Start;

        [SerializeField, Tooltip("Raised alongside the built-in action, for anything extra this button should do.")]
        UnityEvent m_Pressed;

        [SerializeField, Tooltip("Seconds before the button accepts another shot.")]
        float m_Cooldown = 0.6f;

        [SerializeField, Tooltip("Require a second shot to confirm. Keeps a stray round from ending the session.")]
        bool m_ConfirmRequired;

        [SerializeField, Tooltip("Seconds the confirm shot has to arrive in.")]
        float m_ConfirmWindow = 3f;

        [Header("Look")]
        [SerializeField, Tooltip("Renderer tinted to show the button's state. Auto-found in children if empty.")]
        Renderer m_Face;

        [SerializeField] TMP_Text m_Label;

        [SerializeField, Tooltip("Caption shown when the button is ready.")]
        string m_Caption = "START";

        [SerializeField, Tooltip("Caption shown while waiting for the confirming shot.")]
        string m_ConfirmCaption = "SHOOT AGAIN";

        [SerializeField] Color m_ReadyColor = new(0.16f, 0.62f, 0.28f);
        [SerializeField] Color m_PressedColor = Color.white;
        [SerializeField] Color m_ConfirmColor = new(0.95f, 0.66f, 0.15f);
        [SerializeField] Color m_DisabledColor = new(0.22f, 0.22f, 0.24f);

        [SerializeField, Tooltip("How far the button sinks when pressed (metres).")]
        float m_PressDepth = 0.025f;

        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock m_Block;
        MatchService m_Match;
        Vector3 m_HomeLocalPos;
        float m_NextAcceptTime;
        float m_ConfirmDeadline;
        bool m_Pressing;

        /// <summary>False when shooting this button would do nothing, e.g. START mid-round.</summary>
        public bool Interactable
        {
            get
            {
                if (m_Match == null)
                    return m_Action == MatchButtonAction.None;

                return m_Action switch
                {
                    MatchButtonAction.Start => m_Match.State != MatchState.Running,
                    MatchButtonAction.Restart => true,
                    MatchButtonAction.Quit => true,
                    _ => true,
                };
            }
        }

        bool AwaitingConfirm => m_ConfirmRequired && Time.time < m_ConfirmDeadline;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_HomeLocalPos = transform.localPosition;

            if (m_Face == null)
                m_Face = GetComponentInChildren<Renderer>();
        }

        void Start()
        {
            m_Match = MatchService.Instance;
            if (m_Match != null)
                m_Match.StateChanged += OnStateChanged;

            Refresh();
        }

        void OnDestroy()
        {
            if (m_Match != null)
                m_Match.StateChanged -= OnStateChanged;
        }

        void Update()
        {
            // Drop back out of confirm mode once the window lapses.
            if (m_ConfirmDeadline > 0f && Time.time >= m_ConfirmDeadline)
            {
                m_ConfirmDeadline = 0f;
                Refresh();
            }
        }

        public void OnShot(in ShotInfo info)
        {
            if (Time.time < m_NextAcceptTime)
                return;

            m_NextAcceptTime = Time.time + m_Cooldown;

            if (!Interactable)
                return;

            if (m_ConfirmRequired && !AwaitingConfirm)
            {
                m_ConfirmDeadline = Time.time + m_ConfirmWindow;
                Refresh();

                // Say so out loud. Silently swallowing the first shot reads as a dead button.
                Debug.Log($"[ShootableButton] '{name}' armed — shoot again within {m_ConfirmWindow:0.#}s " +
                          $"to confirm {m_Action}.", this);
                return;
            }

            m_ConfirmDeadline = 0f;
            RunAction();

            if (isActiveAndEnabled)
                StartCoroutine(PressRoutine());
        }

        void RunAction()
        {
            // Cheap to leave in: presses are rate-limited by m_Cooldown, and when a button "does
            // nothing" this is the line that says whether the shot ever reached it.
            Debug.Log($"[ShootableButton] '{name}' pressed — {m_Action}.", this);

            switch (m_Action)
            {
                case MatchButtonAction.Start:
                    if (m_Match != null)
                        m_Match.StartMatch();
                    break;
                case MatchButtonAction.Restart:
                    if (m_Match != null)
                        m_Match.RestartMatch();
                    break;
                case MatchButtonAction.Quit:
                    if (m_Match != null)
                        m_Match.QuitGame();
                    break;
            }

            m_Pressed?.Invoke();
        }

        void OnStateChanged(MatchState state) => Refresh();

        IEnumerator PressRoutine()
        {
            m_Pressing = true;
            Refresh();

            // The button's forward points away from the player and into the panel, so pressing it
            // travels along +forward. Then it pops back out.
            transform.localPosition = m_HomeLocalPos + transform.localRotation * Vector3.forward * m_PressDepth;
            yield return new WaitForSeconds(0.12f);
            transform.localPosition = m_HomeLocalPos;

            m_Pressing = false;
            Refresh();
        }

        void Refresh()
        {
            if (m_Label != null)
                m_Label.text = AwaitingConfirm ? m_ConfirmCaption : m_Caption;

            if (m_Face == null)
                return;

            Color color;
            if (m_Pressing)
                color = m_PressedColor;
            else if (AwaitingConfirm)
                color = m_ConfirmColor;
            else if (!Interactable)
                color = m_DisabledColor;
            else
                color = m_ReadyColor;

            m_Face.GetPropertyBlock(m_Block);
            m_Block.SetColor(s_BaseColor, color);
            m_Face.SetPropertyBlock(m_Block);
        }

        /// <summary>Used by the scene builder to author a button without touching private fields by hand.</summary>
        public void Author(MatchButtonAction action, string caption, Color readyColor, Renderer face, TMP_Text label,
            bool confirmRequired = false)
        {
            m_Action = action;
            m_Caption = caption;
            m_ReadyColor = readyColor;
            m_Face = face;
            m_Label = label;
            m_ConfirmRequired = confirmRequired;

            if (m_Label != null)
                m_Label.text = caption;
        }
    }
}
