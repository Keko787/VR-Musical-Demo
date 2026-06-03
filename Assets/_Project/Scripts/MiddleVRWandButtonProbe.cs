#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using MiddleVR;
#endif
using UnityEngine;

namespace VRShootingGallery
{
    /// <summary>
    /// TEMP diagnostic — logs MiddleVR wand button activity so you can discover which physical
    /// button maps to which index in your .vrx config. Drop it on any GameObject in the
    /// MiddleVR scene, press each wand button, and read the Console; then set the button
    /// indices on the gun's input components to match. Remove (or disable) when done.
    /// </summary>
    public class MiddleVRWandButtonProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("Highest wand button index to scan.")]
        int m_MaxButton = 15;

        bool _announced;

        void Update()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            var devices = MVR.DeviceMgr;
            if (devices == null)
                return;

            if (!_announced)
            {
                _announced = true;
                Debug.Log($"[WandProbe] Wand ready. Scanning buttons 0..{m_MaxButton}. " +
                          "Press each physical wand button to see its index.");
            }

            int max = Mathf.Max(0, m_MaxButton);
            for (uint i = 0; i <= (uint)max; i++)
            {
                // IsWandButtonToggled fires on the frame the state changes (press or release).
                if (devices.IsWandButtonToggled(i))
                    Debug.Log($"[WandProbe] button {i} {(devices.IsWandButtonPressed(i) ? "PRESSED" : "released")}");
            }
#endif
        }
    }
}
