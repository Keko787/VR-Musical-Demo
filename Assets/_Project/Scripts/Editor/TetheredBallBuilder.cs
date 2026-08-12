using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.PhysicsDemo;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Builds the tethered-ball rigs for the Physics MiddleVR scene: a weight on a cord hung from the
    /// room's ceiling, the way a tennis ball on a string marks the stopping point in a garage.
    ///
    /// Both menu items act on <em>every</em> rig in the room, so a grid of duplicates stays
    /// consistent. Each rig's own position and rotation are left alone — only its contents are
    /// rebuilt, because the chain length is baked into the joint anchors and patching one in place
    /// would be more fragile than making a new one.
    /// </summary>
    static class TetheredBallBuilder
    {
        const string k_BuildMenu = "VR Shooting Gallery/Physics/Build Tethered Ball";
        const string k_RederiveMenu = "VR Shooting Gallery/Physics/Rebuild Tethered Balls From Placeholder";

        const string k_RoomRoot = "CAVE_User_Room";
        const string k_CeilingName = "Ceiling";
        const string k_RigName = "Tether_Rig";
        const string k_AnchorName = "Anchor";
        const string k_BallName = "Tethered_Ball";

        const string k_BallMaterialPath = "Assets/_Project/Art/Materials/M_TetherBall.mat";
        const string k_CordMaterialPath = "Assets/_Project/Art/Materials/M_TetherCord.mat";
        const string k_BouncePath = "Assets/_Project/Art/Materials/PM_TetherBall.physicMaterial";

        // A dozen links is enough for the cord to read as slack rope rather than a stick, without
        // asking the solver for more than it can hold steady at a 50 Hz step.
        const int k_Segments = 12;
        const float k_BallRadius = 0.07f;
        const float k_BallMass = 0.06f;      // a real tennis ball
        const float k_SegmentMass = 0.02f;
        const float k_CordThickness = 0.015f;
        const float k_FallbackBallHeight = 1.05f;

        // The walls carry no physic material of their own, so bounceCombine has to be Maximum for
        // the ball's own restitution to survive the contact. Friction is combined the other way, so
        // the ball glances off a wall instead of gripping it and killing the swing.
        const float k_Bounciness = 0.65f;
        const float k_Friction = 0.2f;

        /// <summary>Where a rig hangs from, in the rig's own space, and how much cord it pays out.</summary>
        readonly struct Geometry
        {
            public readonly Vector3 AnchorLocal;
            public readonly float CordLength;

            public Geometry(Vector3 anchorLocal, float cordLength)
            {
                AnchorLocal = anchorLocal;
                CordLength = cordLength;
            }
        }

        [MenuItem(k_BuildMenu, false, 200)]
        static void Build() => Run(false);

        [MenuItem(k_RederiveMenu, false, 210)]
        static void RebuildFromPlaceholder() => Run(true);

        static void Run(bool rederive)
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

            Undo.SetCurrentGroupName(rederive ? "Rebuild Tethered Balls" : "Build Tethered Ball");
            int group = Undo.GetCurrentGroup();

            var rigs = FindRigs(room);
            bool createdFirstRig = rigs.Count == 0;
            if (createdFirstRig)
                rigs.Add(CreateRig(room));

            // One shared geometry when it comes from the placeholder — that is the whole point of
            // re-deriving, so a grid of duplicates ends up hanging alike.
            bool haveShared = false;
            var shared = default(Geometry);

            if (rederive || createdFirstRig)
            {
                haveShared = TryDeriveFromPlaceholder(room, ceiling, out shared);
                if (!haveShared && rederive)
                {
                    Debug.LogError($"[TetherBuilder] No placeholder sphere found under '{k_RoomRoot}'. Add an " +
                                   "inactive sphere with a SphereCollider and no Rigidbody where you want the " +
                                   "ball to hang, then run this again.");
                    return;
                }
            }

            foreach (var rig in rigs)
            {
                var geometry = shared;
                if (!haveShared && !TryReuseGeometry(rig, out geometry))
                    geometry = Fallback(ceiling);

                Rebuild(rig, geometry);
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = rigs[0].gameObject;

            Debug.Log($"[TetherBuilder] Rebuilt {rigs.Count} rig(s) with {(haveShared ? "placeholder" : "existing")} " +
                      $"geometry: anchor {(haveShared ? shared.AnchorLocal.ToString() : "per rig")}, " +
                      $"cord {(haveShared ? $"{shared.CordLength:0.###} m" : "per rig")}.", rigs[0]);
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>
        /// Reads the ball's intended spot off the placeholder sphere: the anchor goes straight up
        /// from it to the ceiling, and the cord is sized so the ball lands exactly where it sat.
        /// </summary>
        static bool TryDeriveFromPlaceholder(Transform room, Transform ceiling, out Geometry geometry)
        {
            geometry = default;

            var placeholder = FindPlaceholderBall(room, ceiling);
            if (placeholder == null)
                return false;

            var local = placeholder.localPosition;
            float ceilingY = ceiling.localPosition.y;

            geometry = new Geometry(
                new Vector3(local.x, ceilingY, local.z),
                Mathf.Max(0.2f, ceilingY - local.y - k_BallRadius));

            RetirePlaceholder(placeholder);
            return true;
        }

        /// <summary>
        /// Any child sphere with a collider and no Rigidbody counts. An active one wins outright;
        /// otherwise the nearest to being directly under the ceiling's centre does, which picks the
        /// marker out from stray spheres left elsewhere in the room — including the marker this
        /// builder itself switched off on a previous run.
        /// </summary>
        static Transform FindPlaceholderBall(Transform room, Transform ceiling)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;

            foreach (Transform child in room)
            {
                if (!child.name.StartsWith("Sphere") ||
                    child.GetComponent<SphereCollider>() == null ||
                    child.GetComponent<Rigidbody>() != null)
                    continue;

                if (child.gameObject.activeSelf)
                    return child;

                var offset = new Vector2(child.localPosition.x - ceiling.localPosition.x,
                    child.localPosition.z - ceiling.localPosition.z);

                if (offset.sqrMagnitude < bestDistance)
                {
                    bestDistance = offset.sqrMagnitude;
                    best = child;
                }
            }

            return best;
        }

        /// <summary>Recovers what a rig already uses: the anchor from its transform, the cord from its record.</summary>
        static bool TryReuseGeometry(Transform rig, out Geometry geometry)
        {
            geometry = default;

            var anchor = rig.Find(k_AnchorName);
            if (anchor == null)
                return false;

            // Every copy is consulted, not just the first: an earlier builder stacked components, and
            // the one GetComponent happens to return may be a stale copy that recorded nothing.
            float cordLength = 0f;
            foreach (var tether in rig.GetComponents<TetheredBall>())
                cordLength = Mathf.Max(cordLength, tether.CordLength);

            if (cordLength <= 0f)
                return false;

            geometry = new Geometry(anchor.localPosition, cordLength);
            return true;
        }

        static Geometry Fallback(Transform ceiling)
        {
            float ceilingY = ceiling.localPosition.y;
            return new Geometry(new Vector3(0f, ceilingY, ceiling.localPosition.z),
                Mathf.Max(0.2f, ceilingY - k_FallbackBallHeight - k_BallRadius));
        }

        static void RetirePlaceholder(Transform placeholder)
        {
            if (placeholder == null || !placeholder.gameObject.activeSelf)
                return;

            // Switched off rather than deleted — it is the author's marker, and the ball now
            // occupies the same space.
            Undo.RecordObject(placeholder.gameObject, "Retire placeholder ball");
            placeholder.gameObject.SetActive(false);
            Debug.Log($"[TetherBuilder] Switched off placeholder '{placeholder.name}' — the tethered ball " +
                      "now hangs in its place.", placeholder);
        }

        // ------------------------------------------------------------------ rigs

        static List<Transform> FindRigs(Transform room)
        {
            var rigs = new List<Transform>();
            foreach (Transform child in room)
                if (child.name.StartsWith(k_RigName))
                    rigs.Add(child);

            return rigs;
        }

        static Transform CreateRig(Transform room)
        {
            var go = new GameObject(k_RigName);
            Undo.RegisterCreatedObjectUndo(go, "Create tether rig");
            Undo.SetTransformParent(go.transform, room, "Parent tether rig");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static void Rebuild(Transform rig, Geometry geometry)
        {
            for (int i = rig.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(rig.GetChild(i).gameObject);

            StripDuplicateTethers(rig);

            // Position and rotation are the author's — a duplicated rig is placed by hand and must
            // stay put. Scale is not negotiable: the cord visuals are sized in the rig's own space,
            // so anything other than one would stretch them.
            if (rig.localScale != Vector3.one)
            {
                Debug.LogWarning($"[TetherBuilder] '{rig.name}' had scale {rig.localScale}; reset to one so the " +
                                 "cord draws at the right thickness.", rig);
                Undo.RecordObject(rig, "Reset rig scale");
                rig.localScale = Vector3.one;
            }

            BuildChain(rig, geometry);
        }

        /// <summary>
        /// Clears up after an earlier version of this builder, which added a component per run rather
        /// than reusing one. Stacked copies each drive the same cord and each answer the reset button,
        /// and worse, <c>GetComponent</c> can hand back one whose recorded cord length is zero — which
        /// silently loses a rig's geometry. Keeps whichever copy actually recorded a length.
        /// </summary>
        static void StripDuplicateTethers(Transform rig)
        {
            var tethers = rig.GetComponents<TetheredBall>();
            if (tethers.Length < 2)
                return;

            int keep = 0;
            for (int i = 0; i < tethers.Length; i++)
            {
                if (tethers[i].CordLength > 0f)
                {
                    keep = i;
                    break;
                }
            }

            for (int i = tethers.Length - 1; i >= 0; i--)
                if (i != keep)
                    Undo.DestroyObjectImmediate(tethers[i]);

            Debug.LogWarning($"[TetherBuilder] '{rig.name}' carried {tethers.Length} TetheredBall components " +
                             "from earlier rebuilds; kept the one holding the cord length.", rig);
        }

        static void BuildChain(Transform rig, Geometry geometry)
        {
            float segmentLength = geometry.CordLength / k_Segments;
            var nodes = new List<Transform>(k_Segments + 2);

            // Kinematic, so the whole chain has something immovable to hang from.
            var anchor = new GameObject(k_AnchorName);
            Undo.RegisterCreatedObjectUndo(anchor, "Create anchor");
            anchor.transform.SetParent(rig, false);
            anchor.transform.localPosition = geometry.AnchorLocal;
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
                segment.transform.localPosition = geometry.AnchorLocal + Vector3.down * ((i + 0.5f) * segmentLength);

                var body = segment.AddComponent<Rigidbody>();
                body.mass = k_SegmentMass;

                // Barely damped. Every link rotates as the pendulum swings, so angular drag here is
                // multiplied twelve times over and is what quietly kills the arc.
                body.drag = 0f;
                body.angularDrag = 0.05f;
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
            ball.name = k_BallName;
            Undo.RegisterCreatedObjectUndo(ball, "Create tethered ball");
            ball.transform.SetParent(rig, false);
            ball.transform.localPosition = geometry.AnchorLocal + Vector3.down * (geometry.CordLength + k_BallRadius);
            ball.transform.localScale = Vector3.one * (k_BallRadius * 2f);

            var ballBody = ball.AddComponent<Rigidbody>();
            ballBody.mass = k_BallMass;

            // Just enough drag to settle eventually. The bounces shed most of the energy anyway:
            // restitution acts on velocity, so 0.65 costs a little over half the kinetic energy per
            // wall hit.
            ballBody.drag = 0.01f;
            ballBody.angularDrag = 0.05f;
            ballBody.interpolation = RigidbodyInterpolation.Interpolate;

            // The room walls are zero-thickness boxes. Speculative contacts are what stop a fast
            // swing passing straight through one of them.
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Paint(ball, k_BallMaterialPath);
            ball.GetComponent<SphereCollider>().sharedMaterial = EnsureBounceMaterial();
            Link(ball, ballBody, previous, k_BallRadius, previousHalf);
            nodes.Add(ball.transform);

            var cords = BuildCords(rig, nodes.Count - 1);

            // Reused rather than re-added: Rebuild only clears children, so a component added here
            // unconditionally would stack up one copy per rebuild.
            var tether = rig.GetComponent<TetheredBall>();
            if (tether == null)
                tether = Undo.AddComponent<TetheredBall>(rig.gameObject);

            var so = new SerializedObject(tether);
            SetArray(so, "m_Nodes", nodes);
            SetArray(so, "m_Cords", cords);
            SetFloat(so, "m_Thickness", k_CordThickness);
            SetFloat(so, "m_CordLength", geometry.CordLength);
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

        /// <summary>
        /// Bounce lives on the ball, not the walls. Nothing in the room carries a physic material, so
        /// the ball's own restitution only survives a contact if it is combined with Maximum.
        /// </summary>
        static PhysicMaterial EnsureBounceMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(k_BouncePath);
            if (material != null)
                return material;

            material = new PhysicMaterial("PM_TetherBall")
            {
                bounciness = k_Bounciness,
                dynamicFriction = k_Friction,
                staticFriction = k_Friction,
                bounceCombine = PhysicMaterialCombine.Maximum,
                frictionCombine = PhysicMaterialCombine.Minimum,
            };

            AssetDatabase.CreateAsset(material, k_BouncePath);
            AssetDatabase.SaveAssets();
            return material;
        }

        static void Paint(GameObject go, string materialPath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                return;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        static void SetFloat(SerializedObject so, string path, float value)
        {
            var property = so.FindProperty(path);
            if (property != null)
                property.floatValue = value;
            else
                Debug.LogWarning($"[TetherBuilder] TetheredBall has no field '{path}'.");
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
