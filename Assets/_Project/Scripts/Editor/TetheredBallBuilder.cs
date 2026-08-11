using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.PhysicsDemo;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the tethered-ball rig for the Physics MiddleVR scene: a weight on a cord hung from the
    /// room's ceiling, the way a tennis ball on a string marks the stopping point in a garage.
    ///
    /// The rig is torn down and rebuilt each run — the chain length is baked into the joints, so
    /// patching an existing chain in place would be more fragile than making a new one.
    /// </summary>
    static class TetheredBallBuilder
    {
        const string k_Menu = "VR Shooting Gallery/Physics/Build Tethered Ball";

        const string k_RoomRoot = "CAVE_User_Room";
        const string k_CeilingName = "Ceiling";
        const string k_RigName = "Tether_Rig";

        const string k_BallMaterialPath = "Assets/_Project/Art/Materials/M_TetherBall.mat";
        const string k_CordMaterialPath = "Assets/_Project/Art/Materials/M_TetherCord.mat";

        // A dozen links is enough for the cord to read as slack rope rather than a stick, without
        // asking the solver for more than it can hold steady at a 50 Hz step.
        const int k_Segments = 12;
        const float k_BallRadius = 0.07f;
        const float k_BallMass = 0.06f;      // a real tennis ball
        const float k_SegmentMass = 0.02f;
        const float k_CordThickness = 0.015f;
        const float k_FallbackBallHeight = 1.05f;

        [MenuItem(k_Menu, false, 200)]
        static void Build()
        {
            var scene = SceneManager.GetActiveScene();

            var room = FindRoot(scene, k_RoomRoot);
            if (room == null)
            {
                Debug.LogError($"[TetherBuilder] No '{k_RoomRoot}' root in '{scene.name}'. Open the " +
                               "Physics MiddleVR scene and try again.");
                return;
            }

            var ceiling = room.Find(k_CeilingName);
            if (ceiling == null)
            {
                Debug.LogError($"[TetherBuilder] '{k_RoomRoot}' has no child called '{k_CeilingName}' to hang from.");
                return;
            }

            Undo.SetCurrentGroupName("Build Tethered Ball");
            int group = Undo.GetCurrentGroup();

            var placeholder = FindPlaceholderBall(room);
            var anchorPoint = AnchorPoint(ceiling, placeholder);
            float ropeLength = RopeLength(anchorPoint.y, placeholder);

            var rig = ResetRig(room);
            BuildChain(rig, anchorPoint, ropeLength);
            RetirePlaceholder(placeholder);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = rig.gameObject;

            Debug.Log($"[TetherBuilder] Hung a {k_BallRadius * 2f:0.##} m ball on {ropeLength:0.##} m of cord " +
                      $"from '{ceiling.name}' at {anchorPoint}.", rig);
        }

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Hangs from wherever the scene already says the ball should be. Any inert sphere parented
        /// to the room is treated as the author's placeholder for it.
        /// </summary>
        static Transform FindPlaceholderBall(Transform room)
        {
            foreach (Transform child in room)
            {
                if (child.name.StartsWith("Sphere") && child.gameObject.activeSelf &&
                    child.GetComponent<SphereCollider>() != null && child.GetComponent<Rigidbody>() == null)
                    return child;
            }

            return null;
        }

        static Vector3 AnchorPoint(Transform ceiling, Transform placeholder)
        {
            // Straight up from the placeholder so the ball ends up where it was already marked.
            var over = placeholder != null ? placeholder.position : ceiling.position;
            return new Vector3(over.x, ceiling.position.y, over.z);
        }

        static float RopeLength(float ceilingY, Transform placeholder)
        {
            float ballCentre = placeholder != null ? placeholder.position.y : k_FallbackBallHeight;
            return Mathf.Max(0.2f, ceilingY - ballCentre - k_BallRadius);
        }

        static void RetirePlaceholder(Transform placeholder)
        {
            if (placeholder == null)
                return;

            // Switched off rather than deleted — it is the author's marker, and the new ball now
            // occupies the same space.
            Undo.RecordObject(placeholder.gameObject, "Retire placeholder ball");
            placeholder.gameObject.SetActive(false);
            Debug.Log($"[TetherBuilder] Switched off placeholder '{placeholder.name}' — the tethered ball " +
                      "now hangs in its place.", placeholder);
        }

        // ------------------------------------------------------------------ rig

        static Transform ResetRig(Transform room)
        {
            var existing = room.Find(k_RigName);
            if (existing != null)
            {
                for (int i = existing.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(existing.GetChild(i).gameObject);

                existing.localPosition = Vector3.zero;
                existing.localRotation = Quaternion.identity;
                existing.localScale = Vector3.one;
                return existing;
            }

            var go = new GameObject(k_RigName);
            Undo.RegisterCreatedObjectUndo(go, "Create tether rig");
            Undo.SetTransformParent(go.transform, room, "Parent tether rig");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static void BuildChain(Transform rig, Vector3 anchorPoint, float ropeLength)
        {
            float segmentLength = ropeLength / k_Segments;
            var nodes = new List<Transform>(k_Segments + 2);

            // Kinematic, so the whole chain has something immovable to hang from.
            var anchor = new GameObject("Anchor");
            Undo.RegisterCreatedObjectUndo(anchor, "Create anchor");
            anchor.transform.SetParent(rig, false);
            anchor.transform.position = anchorPoint;
            var anchorBody = anchor.AddComponent<Rigidbody>();
            anchorBody.isKinematic = true;
            anchorBody.useGravity = false;
            nodes.Add(anchor.transform);

            Rigidbody previous = anchorBody;
            float previousHalf = 0f;

            for (int i = 0; i < k_Segments; i++)
            {
                var segment = new GameObject($"Cord_{i:00}");
                Undo.RegisterCreatedObjectUndo(segment, "Create cord segment");
                segment.transform.SetParent(rig, false);
                segment.transform.position = anchorPoint + Vector3.down * ((i + 0.5f) * segmentLength);

                var body = segment.AddComponent<Rigidbody>();
                body.mass = k_SegmentMass;
                body.drag = 0.05f;
                body.angularDrag = 0.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                // These bodies carry no collider, so PhysX has no shape to derive an inertia tensor
                // from. Left to itself it picks one that makes a light link feel oddly resistant to
                // twisting; a small explicit tensor keeps the cord limp.
                body.inertiaTensor = Vector3.one * 0.0005f;
                body.inertiaTensorRotation = Quaternion.identity;

                Link(segment, body, previous, segmentLength * 0.5f, previousHalf);

                previous = body;
                previousHalf = segmentLength * 0.5f;
                nodes.Add(segment.transform);
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Tethered_Ball";
            Undo.RegisterCreatedObjectUndo(ball, "Create tethered ball");
            ball.transform.SetParent(rig, false);
            ball.transform.position = anchorPoint + Vector3.down * (ropeLength + k_BallRadius);
            ball.transform.localScale = Vector3.one * (k_BallRadius * 2f);

            var ballBody = ball.AddComponent<Rigidbody>();
            ballBody.mass = k_BallMass;
            ballBody.drag = 0.05f;
            ballBody.angularDrag = 0.2f;
            ballBody.interpolation = RigidbodyInterpolation.Interpolate;
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Paint(ball, k_BallMaterialPath);
            Link(ball, ballBody, previous, k_BallRadius, previousHalf);
            nodes.Add(ball.transform);

            var cords = BuildCords(rig, nodes.Count - 1);

            var tether = Undo.AddComponent<TetheredBall>(rig.gameObject);
            var so = new SerializedObject(tether);
            SetArray(so, "m_Nodes", nodes);
            SetArray(so, "m_Cords", cords);
            var thickness = so.FindProperty("m_Thickness");
            if (thickness != null)
                thickness.floatValue = k_CordThickness;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Pins the top of <paramref name="go"/> to the bottom of the body above it. Linear motion is
        /// locked and angular motion left free, which is what makes the link behave like cord rather
        /// than a spring.
        /// </summary>
        static void Link(GameObject go, Rigidbody body, Rigidbody connectTo, float halfHeight, float connectedHalfHeight)
        {
            var joint = go.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectTo;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = new Vector3(0f, halfHeight, 0f);
            joint.connectedAnchor = new Vector3(0f, -connectedHalfHeight, 0f);

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            // Preprocessing lets PhysX "help" badly-conditioned joints, which on a chain shows up as
            // links snapping to odd poses. Projection pulls any drift back instead.
            joint.enablePreprocessing = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.01f;
            joint.projectionAngle = 5f;

            body.WakeUp();
        }

        static List<Transform> BuildCords(Transform rig, int count)
        {
            var cords = new List<Transform>(count);
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_CordMaterialPath);

            for (int i = 0; i < count; i++)
            {
                var cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cord.name = $"Cord_Visual_{i:00}";

                // Visual only. A collider here would let the cord catch on the ball and on itself.
                Object.DestroyImmediate(cord.GetComponent<CapsuleCollider>());
                Undo.RegisterCreatedObjectUndo(cord, "Create cord visual");
                cord.transform.SetParent(rig, false);

                if (material != null)
                    cord.GetComponent<MeshRenderer>().sharedMaterial = material;

                cords.Add(cord.transform);
            }

            return cords;
        }

        // ------------------------------------------------------------------ helpers

        static void Paint(GameObject go, string materialPath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                return;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        static void SetArray(SerializedObject so, string path, List<Transform> values)
        {
            var property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"[TetherBuilder] TetheredBall has no field '{path}'.");
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name)
                    return go.transform;

            return null;
        }
    }
}
