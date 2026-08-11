using UnityEngine;

namespace VRShootingGallery.Gun
{
    /// <summary>
    /// Fire source backed by the <b>MiddleVR wand</b>, the CAVE equivalent of
    /// <see cref="ControllerTriggerInputSource"/>. The CAVE has no Unity-XR controller, so
    /// instead of reading an <c>InputAction</c> we poll the wand via <see cref="MiddleVRWand"/>.
    ///
    /// Drop this on the gun in place of <see cref="ControllerTriggerInputSource"/> for the
    /// MiddleVR build; <see cref="GunController"/> auto-finds any <see cref="IGunInputSource"/>
    /// on its GameObject, so no other gun code changes.
    /// </summary>
    /// <remarks>
    /// The wand button index maps to whatever the active controller's trigger is bound to in
    /// your <c>.vrx</c> configuration (in MiddleVR's config tool). On most wand mappings the
    /// trigger is button <b>0</b>; adjust <see cref="m_TriggerButton"/> if your config differs.
    /// </remarks>
    public class MiddleVRWandInputSource : MonoBehaviour, IGunInputSource
    {
        [SerializeField, Tooltip("MiddleVR wand button index that fires. Maps to the button " +
            "bound to the trigger in your .vrx config (usually 0).")]
        int m_TriggerButton = 0;

        int m_PolledFrame = -1;
        bool m_Held;
        bool m_WasHeld;
        bool m_Pressed;
        bool m_Primed;

        public bool FireHeld
        {
            get
            {
                Poll();
                return m_Held;
            }
        }

        public bool FirePressedThisFrame
        {
            get
            {
                Poll();
                return m_Pressed;
            }
        }

        /// <summary>
        /// Latches the press edge here rather than using MiddleVR's own "toggled" flag. The wand is
        /// sampled on the cluster's cadence, not Unity's, so that flag can stay set across more than
        /// one frame and let a single pull fire twice. Polling lazily on first read (instead of in
        /// Update) also keeps the edge frame-accurate whatever script execution order the gun ends
        /// up with — reading a stale value would show up as a round arriving a frame late.
        /// </summary>
        void Poll()
        {
            if (m_PolledFrame == Time.frameCount)
                return;

            m_PolledFrame = Time.frameCount;
            m_WasHeld = m_Held;
            m_Held = MiddleVRWand.Held(m_TriggerButton);

            // m_Primed swallows the very first sample, so a trigger already held when the scene
            // loads does not count as a pull and let a round off before the player touches anything.
            m_Pressed = m_Primed && m_Held && !m_WasHeld;
            m_Primed = true;
        }
    }
}
