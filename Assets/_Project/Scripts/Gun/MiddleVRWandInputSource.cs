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

        public bool FireHeld => MiddleVRWand.Held(m_TriggerButton);
    }
}
