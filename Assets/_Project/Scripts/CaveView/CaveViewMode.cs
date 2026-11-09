using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using MiddleVR;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.CaveView
{
    /// <summary>
    /// How the CAVE shows the scene, one of three — the same three Sit-Rep-CAVE's CARE offers:
    /// <list type="bullet">
    /// <item><b>3D</b> — MiddleVR's stereo as configured, head-tracked, for whoever wears the tracked glasses;</item>
    /// <item><b>2D</b> — every MiddleVR stereo camera's inter-eye distance at 0, so both eyes get the same picture and it
    /// is sharp without glasses. Still head-tracked: the tracked viewer's view, flat;</item>
    /// <item><b>2D-Fixed</b> — 2D, and every screen shows the scene from one fixed eye in the room
    /// (<see cref="FixedEyeView"/>): the same for everyone and steady whoever moves, for an audience.</item>
    /// </list>
    /// Picked with the wand's buttons, one per view, so there is nothing on the screens to pick from: an audience watches
    /// the scene, not a menu. 1, 2 and 3 on the keyboard do the same for whoever is at the desk. A scene whose buttons
    /// are all taken can use the thumbstick instead (<see cref="m_StickPicksView"/>): up for 2D, down for 3D. The
    /// stereo part does nothing off the MiddleVR platform.
    /// </summary>
    /// <remarks>
    /// The inter-eye distance is set on MiddleVR's kernel cameras, not Unity's: the kernel computes each eye's projection
    /// from it, and its own Invert Eyes sample changes it the same way. Each camera's own distance is kept by name while
    /// 2D has it at 0 — SWIG hands out a new proxy per call, so the name is the stable key — and put back for 3D, and
    /// when the scene ends, so MiddleVR never keeps a flat CAVE after Play.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CaveViewMode : MonoBehaviour
    {
        /// <summary>The three views.</summary>
        public enum View
        {
            Stereo3D,
            Mono2D,
            Fixed2D,
        }

        [Tooltip("The view the scene starts in.")]
        [SerializeField] View m_StartView = View.Stereo3D;

        [Tooltip("Draws 2D-Fixed. Without one, 2D-Fixed is plain 2D.")]
        [SerializeField] FixedEyeView m_Fixed;

        // One button per view rather than one that cycles, so a press always lands on the same view whatever the CAVE
        // was showing. Not the trigger (0): squeezing the wand should never change the view.
        [Header("Wand buttons")]
        [Tooltip("The wand button that picks 3D. Indices are the .vrx's; MiddleVRWandButtonProbe lists them. −1 for none.")]
        [SerializeField, Min(-1)] int m_Stereo3DButton = 1;

        [Tooltip("The wand button that picks 2D. −1 for none.")]
        [SerializeField, Min(-1)] int m_Mono2DButton = 2;

        [Tooltip("The wand button that picks 2D-Fixed. −1 for none.")]
        [SerializeField, Min(-1)] int m_Fixed2DButton = 3;

        [Header("Thumbstick")]
        [Tooltip("Push the wand's thumbstick up for 2D, down for 3D — for a scene whose buttons are all spoken for. One " +
                 "view per push: the stick goes back to the middle before the next. While a wall menu is open the stick " +
                 "is the menu's.")]
        [SerializeField] bool m_StickPicksView;

        [Tooltip("1, 2 and 3 pick 3D, 2D and 2D-Fixed too, for whoever is at the desk — from Unity's keyboard or " +
                 "MiddleVR's, which works while the Editor does not have focus, as in the CAVE it seldom does.")]
        [SerializeField] bool m_KeyboardShortcuts = true;

        [Tooltip("The desk keys for 3D, 2D and 2D-Fixed, in that order. Digits 1-3 everywhere except a scene whose " +
                 "instruments already play those keys — the harp's strings do — where the function row keeps the " +
                 "views reachable without sounding a note.")]
        [SerializeField] Key[] m_Keys = { Key.Digit1, Key.Digit2, Key.Digit3 };

        // MiddleVR builds its cameras in MVRManager's Awake, not necessarily before ours.
        const float k_StartTimeout = 10f;

        // How far the stick must go to count as a push, and how near the middle it must come back.
        const float k_StickPush = 0.7f, k_StickRest = 0.3f;

        int m_StickHeld;        // the push being held: 1 up, −1 down, 0 none
        WallMenu[] m_Menus;

        readonly Dictionary<string, float> m_SavedEyeDistance = new();
        readonly bool[] m_KeyWasHeld = new bool[3];

        View m_View = View.Stereo3D;

        /// <summary>The view now.</summary>
        public View Current => m_View;

        /// <summary>True while both eyes get the same picture: 2D or 2D-Fixed.</summary>
        public bool Mono => m_View != View.Stereo3D;

        /// <summary>Raised after the view changes.</summary>
        public event Action Changed;

        /// <summary>What a chip reads: "3D", "2D", "2D-Fixed".</summary>
        public static string Label(View view) => view switch
        {
            View.Mono2D => "2D",
            View.Fixed2D => "2D-Fixed",
            _ => "3D",
        };

        IEnumerator Start()
        {
            if (m_StartView == View.Stereo3D)
                yield break;

            float timeout = k_StartTimeout;
            while (timeout > 0f && !StereoReady())
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            // One more frame, so the eye cameras MiddleVR makes from the kernel's exist for 2D-Fixed to find.
            yield return null;
            SetView(m_StartView);
        }

        void Update()
        {
            // A negative index is no button; MiddleVRWand would read it as the trigger.
            if (m_Stereo3DButton >= 0 && MiddleVRWand.PressedThisFrame(m_Stereo3DButton))
                SetView(View.Stereo3D);
            else if (m_Mono2DButton >= 0 && MiddleVRWand.PressedThisFrame(m_Mono2DButton))
                SetView(View.Mono2D);
            else if (m_Fixed2DButton >= 0 && MiddleVRWand.PressedThisFrame(m_Fixed2DButton))
                SetView(View.Fixed2D);

            if (m_StickPicksView)
                ReadStick();

            if (!m_KeyboardShortcuts)
                return;

            var keyboard = Keyboard.current;
            int keys = m_Keys != null ? Mathf.Min(m_Keys.Length, m_KeyWasHeld.Length) : 0;
            for (int i = 0; i < keys; i++)
            {
                // MiddleVR's keyboard has no pressed-this-frame, so its edge is found here.
                bool held = MiddleVRKeyboard.Held(m_Keys[i]);
                bool pressed = (held && !m_KeyWasHeld[i]) ||
                               (keyboard != null && keyboard[m_Keys[i]].wasPressedThisFrame);
                m_KeyWasHeld[i] = held;

                if (pressed)
                    SetView((View)i);
            }
        }

        /// <summary>
        /// Up picks 2D, down picks 3D: a push past <see cref="k_StickPush"/>, mostly up or down, and the next only after
        /// the stick has come back within <see cref="k_StickRest"/> of the middle. A push while a wall menu is open is
        /// the menu's, and still has to come back before it counts here, so closing a menu with the stick held does not
        /// change the view.
        /// </summary>
        void ReadStick()
        {
            var axis = MiddleVRWand.Axis;
            if (Mathf.Abs(axis.x) < k_StickRest && Mathf.Abs(axis.y) < k_StickRest)
            {
                m_StickHeld = 0;
                return;
            }

            if (m_StickHeld != 0 || Mathf.Abs(axis.y) <= Mathf.Abs(axis.x))
                return;

            int push = axis.y >= k_StickPush ? 1 : axis.y <= -k_StickPush ? -1 : 0;
            if (push == 0)
                return;

            m_StickHeld = push;
            if (!MenuOpen())
                SetView(push > 0 ? View.Mono2D : View.Stereo3D);
        }

        bool MenuOpen()
        {
            m_Menus ??= FindObjectsOfType<WallMenu>(true);
            foreach (var menu in m_Menus)
                if (menu != null && menu.IsOpen)
                    return true;

            return false;
        }

        /// <summary>3D, 2D or 2D-Fixed.</summary>
        public void SetView(View view)
        {
            if (view == m_View)
                return;

            bool mono = view != View.Stereo3D;
            if (mono != Mono)
                ApplyMono(mono);

            m_View = view;

            if (m_Fixed != null)
                m_Fixed.Show(view == View.Fixed2D);

            Debug.Log($"[CaveView] View: {Label(view)}.", this);
            Changed?.Invoke();
        }

        void ApplyMono(bool mono)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            var display = MVR.DisplayMgr;
            if (display == null)
                return;

            foreach (var camera in display.Cameras)
            {
                if (!(camera is vrCameraStereo stereo))
                    continue;

                string key = stereo.GetName();
                if (mono)
                {
                    if (!m_SavedEyeDistance.ContainsKey(key))
                        m_SavedEyeDistance[key] = stereo.GetInterEyeDistance();

                    stereo.SetInterEyeDistance(0f);
                }
                else if (m_SavedEyeDistance.TryGetValue(key, out float distance))
                {
                    stereo.SetInterEyeDistance(distance);
                }
            }

            if (!mono)
                m_SavedEyeDistance.Clear();
#endif
        }

        static bool StereoReady()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            var display = MVR.DisplayMgr;
            if (display == null)
                return false;

            foreach (var camera in display.Cameras)
                if (camera is vrCameraStereo)
                    return true;
#endif
            return false;
        }

        // Leaving Play — or the scene — in 2D hands MiddleVR its stereo back.
        void OnDestroy()
        {
            if (Mono)
                ApplyMono(false);
        }
    }
}
