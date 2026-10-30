#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using MiddleVR;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VRShootingGallery
{
    /// <summary>
    /// Stands in for MiddleVR 2.4's FPS overlay, which breaks on Unity 2022.2+. MVRManagerScript.Start
    /// builds a "{camera}_Canvas" child under every camera, overlay on or not, with its label in the
    /// built-in "Arial.ttf"; these Unity versions refuse that font with an ArgumentException, which
    /// aborts the rest of the manager's Start. MiddleVR leaves alone any camera that already has the
    /// child, so this builds it first, in LegacyRuntime.ttf, and feeds the label itself (MiddleVR only
    /// feeds labels it made).
    /// </summary>
    /// <remarks>
    /// Builds from sceneLoaded, which lands between the manager's Awake, where its cameras are made,
    /// and its Start. MiddleVR still shows and hides the canvas, from its ShowFPS setting and Shift+D.
    /// </remarks>
    public class MiddleVRFpsOverlay : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        Canvas m_Canvas;
        Text m_Text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (MVR.Kernel == null)
                return;

            // Every camera, not just the enabled ones: MiddleVR builds for Camera.allCameras as it
            // stands at its Start, and one switched on before then must already have its canvas.
            foreach (var cam in FindObjectsOfType<Camera>(true))
            {
                if (cam.transform.Find(cam.name + "_Canvas") == null)
                    Build(cam);
            }
        }

        static void Build(Camera cam)
        {
            // The same canvas and label MiddleVR builds, bar the font.
            var go = new GameObject(cam.name + "_Canvas");
            go.transform.SetParent(cam.transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.enabled = false;
            go.AddComponent<CanvasScaler>();

            var textGO = new GameObject("FPS Text");
            textGO.transform.SetParent(go.transform, false);

            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.LowerLeft;
            var rect = text.rectTransform;
            rect.pivot = Vector2.zero;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;

            var overlay = go.AddComponent<MiddleVRFpsOverlay>();
            overlay.m_Canvas = canvas;
            overlay.m_Text = text;
        }

        void Update()
        {
            // The canvas is on exactly when MiddleVR's ShowFPS is.
            if (m_Canvas.enabled && MVR.Kernel != null)
                m_Text.text = MVR.Kernel.GetFPS().ToString("f2");
        }
#endif
    }
}
