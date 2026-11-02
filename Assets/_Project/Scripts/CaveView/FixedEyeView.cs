using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRShootingGallery.CaveView
{
    /// <summary>
    /// The <b>2D-Fixed</b> view (<see cref="CaveViewMode"/>): every CAVE screen shows the scene from one fixed eye in the
    /// room instead of from the tracked head, so everyone standing in the CAVE sees the same steady picture, sharp
    /// without glasses. The way Sit-Rep-CAVE's CARE does its front wall, done here for every screen.
    /// </summary>
    /// <remarks>
    /// For each screen, a camera stands at the fixed eye with an off-axis frustum cut through that screen's four edges,
    /// and renders into a picture that lies on the screen. A picture lying on a screen projects to the same place
    /// whatever eye MiddleVR draws it for — a point on the screen plane has no parallax — so the head-tracked eye cameras
    /// show it unmoved however the head moves, and MiddleVR's tracking is never touched. While the view is shown, every
    /// other camera draws only the overlay layer: the pictures, and anything put there to stay live in the room. MiddleVR
    /// rewrites its cameras' projection every frame but never their culling mask, so the swap holds until it is undone.
    ///
    /// The screens are the CAVE's real ones, read from the <c>.vrx</c> by the builder — not the room model, which in this
    /// CAVE is shorter than the side screens.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FixedEyeView : MonoBehaviour
    {
        /// <summary>One CAVE screen, in this object's frame.</summary>
        [Serializable]
        public struct Screen
        {
            public string Name;
            public Vector3 Centre;

            [Tooltip("Unit normal pointing into the room.")]
            public Vector3 Normal;

            [Tooltip("Unit vector in the screen's plane that reads as up on it: up on a wall, toward the front wall on the floor.")]
            public Vector3 Up;

            [Tooltip("Width across the screen, and height along Up, in metres.")]
            public Vector2 Size;
        }

        /// <summary>The fixed eye by default: a standing viewer's eyes, a little behind the room's centre.</summary>
        public static readonly Vector3 DefaultEye = new(0f, 1.6f, -0.9f);

        /// <summary>
        /// How far into the room the pictures lie, metres: off the screen, so nothing else on the screen plane fights
        /// them, and close enough that their parallax is nil.
        /// </summary>
        const float k_Standoff = 0.003f;

        [Tooltip("The fixed eye, in this object's frame (the room's: x along the front wall, y up from the floor, z toward " +
                 "the front wall).")]
        [SerializeField] Vector3 m_Eye = DefaultEye;

        [SerializeField] Screen[] m_Screens = Array.Empty<Screen>();

        [Tooltip("Copied for each screen's picture, with the picture's texture as its base map. An unlit material, so the " +
                 "picture is shown exactly as the fixed eye rendered it.")]
        [SerializeField] Material m_PictureMaterial;

        [Tooltip("The layer the pictures go on. The fixed cameras draw every layer but this; while the view is shown, " +
                 "every other camera draws only this.")]
        [SerializeField] int m_OverlayLayer;

        [Tooltip("Each picture's height in pixels — the CAVE's own, so the picture is not resampled; the width follows the " +
                 "screen's shape.")]
        [SerializeField, Min(64)] int m_PictureHeight = 1600;

        [SerializeField, Min(0.01f)] float m_Near = 0.05f;
        [SerializeField, Min(1f)] float m_Far = 1000f;

        sealed class Wall
        {
            public Camera Camera;
            public RenderTexture Texture;
            public GameObject Picture;
            public Material Material;
        }

        readonly List<Wall> m_Walls = new();
        readonly Dictionary<Camera, int> m_SavedMasks = new();
        bool m_Shown;

        /// <summary>True while the screens show the fixed eye's view.</summary>
        public bool Shown => m_Shown;

        /// <summary>The fixed eye, in this object's frame.</summary>
        public Vector3 Eye
        {
            get => m_Eye;
            set
            {
                m_Eye = value;
                Aim();
            }
        }

        /// <summary>Sets the screens and the overlay layer; the builder's way in.</summary>
        public void Configure(Screen[] screens, Material pictureMaterial, int overlayLayer)
        {
            m_Screens = screens ?? Array.Empty<Screen>();
            m_PictureMaterial = pictureMaterial;
            m_OverlayLayer = overlayLayer;
        }

        /// <summary>Puts the fixed eye's pictures on the screens, or takes them off.</summary>
        public void Show(bool show)
        {
            if (show == m_Shown)
                return;

            if (show && m_Walls.Count == 0 && !Build())
                return;

            m_Shown = show;

            foreach (var wall in m_Walls)
            {
                wall.Picture.SetActive(show);
                wall.Camera.enabled = show; // no rendering while nobody sees it
            }

            if (show)
                OverlayOnly();
            else
                RestoreMasks();
        }

        bool Build()
        {
            // Layer 0 is Default, where the scene itself lives: the fixed cameras would skip it and draw nothing.
            if (m_Screens.Length == 0 || m_PictureMaterial == null || m_OverlayLayer <= 0)
            {
                Debug.LogWarning("[CaveView] FixedEyeView has no screens, picture material or overlay layer, so " +
                                 "2D-Fixed shows plain 2D. Rebuild the demo to fill them in.", this);
                return false;
            }

            foreach (var screen in m_Screens)
                m_Walls.Add(BuildWall(screen));

            Aim();
            return true;
        }

        Wall BuildWall(in Screen screen)
        {
            int height = m_PictureHeight;
            int width = Mathf.Max(1, Mathf.RoundToInt(height * screen.Size.x / Mathf.Max(0.01f, screen.Size.y)));

            // One sample, and a camera that asks for one: a texture made with more samples than URP renders the camera
            // with fails its render pass every frame and is never cleared (Sit-Rep's lesson).
            var texture = new RenderTexture(width, height, 24) { name = $"FixedEye_{screen.Name}", antiAliasing = 1 };

            var cameraObject = new GameObject($"FixedEyeCamera_{screen.Name}");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = texture;
            camera.allowMSAA = false;
            camera.clearFlags = CameraClearFlags.Skybox; // the whole picture, every frame: nothing left from the last
            camera.backgroundColor = Color.black;
            camera.cullingMask = ~(1 << m_OverlayLayer);
            camera.depth = -100; // before the eye cameras, so they show this frame's picture
            camera.enabled = false;

            // The picture: a quad over the screen, facing into the room. Its +X and +Y are the camera's right and up, so
            // the texture lands the right way round.
            var picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
            picture.name = $"FixedEyePicture_{screen.Name}";
            Destroy(picture.GetComponent<Collider>());
            picture.layer = m_OverlayLayer;
            picture.transform.SetParent(transform, false);
            picture.transform.localPosition = screen.Centre + screen.Normal * k_Standoff;
            picture.transform.localRotation = Facing(screen);
            picture.transform.localScale = new Vector3(screen.Size.x, screen.Size.y, 1f);

            var material = new Material(m_PictureMaterial) { name = picture.name };
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;

            var renderer = picture.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            picture.SetActive(false);

            return new Wall { Camera = camera, Texture = texture, Picture = picture, Material = material };
        }

        /// <summary>Stands each camera at the eye, facing its screen, with its frustum cut through the screen's edges.</summary>
        void Aim()
        {
            for (int i = 0; i < m_Walls.Count && i < m_Screens.Length; i++)
            {
                var camera = m_Walls[i].Camera;
                var rotation = Facing(m_Screens[i]);
                camera.transform.localPosition = m_Eye;
                camera.transform.localRotation = rotation;
                camera.nearClipPlane = m_Near;
                camera.farClipPlane = m_Far;
                camera.projectionMatrix = OffAxis(m_Eye, m_Screens[i], rotation, m_Near, m_Far);
            }
        }

        /// <summary>Looking into the screen, with the screen's up as up.</summary>
        static Quaternion Facing(in Screen screen) => Quaternion.LookRotation(-screen.Normal, screen.Up);

        /// <summary>
        /// The projection of an eye at <paramref name="eye"/> through the screen's rectangle, for a camera turned to
        /// <paramref name="rotation"/>: off-axis, so the screen's corners land on the picture's corners.
        /// </summary>
        public static Matrix4x4 OffAxis(Vector3 eye, in Screen screen, Quaternion rotation, float near, float far)
        {
            var toCentre = screen.Centre - eye;
            float distance = Mathf.Max(1e-3f, Vector3.Dot(toCentre, rotation * Vector3.forward));
            float across = Vector3.Dot(toCentre, rotation * Vector3.right);
            float up = Vector3.Dot(toCentre, rotation * Vector3.up);
            float scale = near / distance;

            return Matrix4x4.Frustum(
                (across - 0.5f * screen.Size.x) * scale,
                (across + 0.5f * screen.Size.x) * scale,
                (up - 0.5f * screen.Size.y) * scale,
                (up + 0.5f * screen.Size.y) * scale,
                near, far);
        }

        /// <summary>Every camera but the fixed ones draws only the overlay layer: the pictures and the live overlays.</summary>
        void OverlayOnly()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (IsFixed(camera) || m_SavedMasks.ContainsKey(camera))
                    continue;

                m_SavedMasks[camera] = camera.cullingMask;
                camera.cullingMask = 1 << m_OverlayLayer;
            }
        }

        void RestoreMasks()
        {
            foreach (var saved in m_SavedMasks)
                if (saved.Key != null)
                    saved.Key.cullingMask = saved.Value;

            m_SavedMasks.Clear();
        }

        bool IsFixed(Camera camera)
        {
            foreach (var wall in m_Walls)
                if (wall.Camera == camera)
                    return true;

            return false;
        }

        void OnValidate()
        {
            if (Application.isPlaying)
                Aim();
        }

        void OnDisable() => Show(false);

        void OnDestroy()
        {
            RestoreMasks();

            foreach (var wall in m_Walls)
            {
                if (wall.Camera != null)
                    Destroy(wall.Camera.gameObject);
                if (wall.Picture != null)
                    Destroy(wall.Picture);
                if (wall.Material != null)
                    Destroy(wall.Material);
                if (wall.Texture != null)
                {
                    wall.Texture.Release();
                    Destroy(wall.Texture);
                }
            }

            m_Walls.Clear();
        }

        void OnDrawGizmosSelected()
        {
            // The fixed eye, and its lines of sight to every screen's corners.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawSphere(m_Eye, 0.05f);

            foreach (var screen in m_Screens)
            {
                var right = Vector3.Cross(screen.Up, -screen.Normal).normalized * (0.5f * screen.Size.x);
                var up = screen.Up * (0.5f * screen.Size.y);
                var a = screen.Centre - right - up;
                var b = screen.Centre + right - up;
                var c = screen.Centre + right + up;
                var d = screen.Centre - right + up;

                Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
                Gizmos.DrawLine(m_Eye, a);
                Gizmos.DrawLine(m_Eye, b);
                Gizmos.DrawLine(m_Eye, c);
                Gizmos.DrawLine(m_Eye, d);

                Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, c);
                Gizmos.DrawLine(c, d);
                Gizmos.DrawLine(d, a);
            }
        }
    }
}
