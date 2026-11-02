using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using MiddleVR;
#endif
using UnityEngine;
using UnityEngine.InputSystem;

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
    /// the scene, not a menu. 1, 2 and 3 on the keyboard do the same for whoever is at the desk. The stereo part does
    /// nothing off the MiddleVR platform.
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
        [Tooltip("The wand button that picks 3D. Indices are the .vrx's; MiddleVRWandButtonProbe lists them.")]
        [SerializeField, Min(0)] int m_Stereo3DButton = 1;

        [Tooltip("The wand button that picks 2D.")]
        [SerializeField, Min(0)] int m_Mono2DButton = 2;

        [Tooltip("The wand button that picks 2D-Fixed.")]
        [SerializeField, Min(0)] int m_Fixed2DButton = 3;

        [Tooltip("1, 2 and 3 pick 3D, 2D and 2D-Fixed too, for whoever is at the desk — from Unity's keyboard or " +
                 "MiddleVR's, which works while the Editor does not have focus, as in the CAVE it seldom does.")]
        [SerializeField] bool m_KeyboardShortcuts = true;

        // MiddleVR builds its cameras in MVRManager's Awake, not necessarily before ours.
        const float k_StartTimeout = 10f;

        static readonly Key[] s_Keys = { Key.Digit1, Key.Digit2, Key.Digit3 };

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
            if (MiddleVRWand.PressedThisFrame(m_Stereo3DButton))
                SetView(View.Stereo3D);
            else if (MiddleVRWand.PressedThisFrame(m_Mono2DButton))
                SetView(View.Mono2D);
            else if (MiddleVRWand.PressedThisFrame(m_Fixed2DButton))
                SetView(View.Fixed2D);

            if (!m_KeyboardShortcuts)
                return;

            var keyboard = Keyboard.current;
            for (int i = 0; i < s_Keys.Length; i++)
            {
                // MiddleVR's keyboard has no pressed-this-frame, so its edge is found here.
                bool held = MiddleVRKeyboard.Held(s_Keys[i]);
                bool pressed = (held && !m_KeyWasHeld[i]) ||
                               (keyboard != null && keyboard[s_Keys[i]].wasPressedThisFrame);
                m_KeyWasHeld[i] = held;

                if (pressed)
                    SetView((View)i);
            }
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
