using UnityEngine;
using UnityEngine.Rendering;
using VRShootingGallery.TrackingDemo;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>The CAVE's room box in the room's own frame: x across, y up, z toward the front wall.</summary>
    [System.Serializable]
    public struct RoomBox
    {
        public float LeftX;
        public float RightX;
        public float BackZ;
        public float FrontZ;
        public float Height;

        public float Width => RightX - LeftX;
        public float Depth => FrontZ - BackZ;
        public float CentreX => (LeftX + RightX) * 0.5f;
        public float CentreZ => (BackZ + FrontZ) * 0.5f;
    }

    /// <summary>
    /// What a <see cref="Performer"/> builds its body from: the shared meshes and materials, the room, and
    /// a handful of helpers that make a coloured part in one line. Handed to <see cref="Performer.Build"/>
    /// by the scene builder; never used at runtime.
    /// </summary>
    /// <remarks>
    /// <para>Mesh conventions, all unit-sized so a part's scale is its size in metres:</para>
    /// <list type="bullet">
    /// <item><see cref="Sphere"/> — diameter 1, pivot at the centre.</item>
    /// <item><see cref="Capsule"/>, <see cref="Cylinder"/> — Unity's: diameter 1, length 2 along Y, pivot at the
    /// centre. <see cref="Limb"/> hides the factor of two.</item>
    /// <item><see cref="Cone"/> — base diameter 1 at y = 0, apex at y = 1, pivot at the base.</item>
    /// <item><see cref="Bell"/> — an open dome: rim diameter 1 at y = 0, crown at y = 0.4. Drawn from both sides
    /// by the glow material.</item>
    /// </list>
    /// <para>
    /// Every animal is an original, stylised character built from these: low detail reads at CAVE
    /// distances, is cheap to draw for eight eye cameras, and hides small timing faults.
    /// </para>
    /// </remarks>
    public sealed class CreatureKit
    {
        /// <summary>Opaque, toon-lit: fur, skin, feathers, wood, stone.</summary>
        public Material Toon;

        /// <summary>See-through and self-lit, drawn from both sides: jellyfish, the moon, the whale's glow.</summary>
        public Material Glow;

        /// <summary>Additive soft dots, for particle billboards (fireflies).</summary>
        public Material Firefly;

        /// <summary>Bird silhouettes with flapping wings, for particle billboards (starlings).</summary>
        public Material Starling;

        /// <summary>
        /// The glow shader with vertex colour switched on, for the whale's own mesh, which carries its
        /// belly-to-edge shading in vertex colour. (Unity's primitives have no vertex colour, hence two materials.)
        /// </summary>
        public Material Whale;

        public Mesh Sphere;
        public Mesh Capsule;
        public Mesh Cylinder;
        public Mesh Cube;
        public Mesh Cone;
        public Mesh Bell;

        /// <summary>The water sheet on the CAVE floor, for animals that splash it. May be null.</summary>
        public RippleFloor Floor;

        /// <summary>The room's frame. Performers are built under it.</summary>
        public Transform Room;

        public RoomBox Box;

        /// <summary>Where the tracked viewer's eyes are by default, in the room's frame — for animals that face them.</summary>
        public Vector3 ViewerEye;

        // ================================================================== helpers

        /// <summary>An empty transform: a joint or a group to move as one.</summary>
        public Transform Node(Transform parent, string name, Vector3 position, Vector3 euler = default)
        {
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = Vector3.one;
            return t;
        }

        /// <summary>A coloured, toon-lit part: <paramref name="mesh"/> at <paramref name="position"/>, scaled to <paramref name="size"/>.</summary>
        public CreatureTint Solid(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 size, Color color,
            Vector3 euler = default) =>
            Part(parent, name, mesh, Toon, position, size, euler, color, Color.black);

        /// <summary>A toon-lit part that can glow: give it an emission and drive <see cref="CreatureTint.Glow"/>.</summary>
        public CreatureTint Lit(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 size, Color color,
            Color emission, Vector3 euler = default) =>
            Part(parent, name, mesh, Toon, position, size, euler, color, emission);

        /// <summary>A see-through, self-lit part on the glow material. <paramref name="color"/>'s alpha is its body opacity.</summary>
        public CreatureTint Glowing(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 size, Color color,
            Color emission, Vector3 euler = default) =>
            Part(parent, name, mesh, Glow, position, size, euler, color, emission);

        /// <summary>A sphere part; <paramref name="size"/> is its diameter along each axis.</summary>
        public CreatureTint Ball(Transform parent, string name, Vector3 position, Vector3 size, Color color, Vector3 euler = default) =>
            Solid(parent, name, Sphere, position, size, color, euler);

        /// <summary>A cone part pointing along its local +Y; <paramref name="size"/> is (base diameter, length, base diameter).</summary>
        public CreatureTint Spike(Transform parent, string name, Vector3 position, Vector3 size, Color color, Vector3 euler = default) =>
            Solid(parent, name, Cone, position, size, color, euler);

        /// <summary>
        /// A joint at <paramref name="position"/> with a capsule hanging from it along the joint's local −Y,
        /// <paramref name="length"/> long and <paramref name="thickness"/> across. Rotate the returned joint
        /// to swing the limb; chain the next joint at <c>(0, −length, 0)</c> under it.
        /// </summary>
        public Transform Limb(Transform parent, string name, Vector3 position, Vector3 euler, float length, float thickness,
            Color color, Mesh mesh = null)
        {
            var joint = Node(parent, name, position, euler);
            mesh ??= Capsule;

            // Unity's capsule and cylinder are 2 long at scale 1; the sphere is 1.
            float lengthScale = mesh == Sphere ? length : length * 0.5f;
            Solid(joint, name + "_Mesh", mesh, new Vector3(0f, -length * 0.5f, 0f),
                new Vector3(thickness, lengthScale, thickness), color);
            return joint;
        }

        CreatureTint Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 size,
            Vector3 euler, Color color, Color emission)
        {
            var t = Node(parent, name, position, euler);
            t.localScale = size;

            var filter = t.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var tint = t.gameObject.AddComponent<CreatureTint>();
            tint.Set(color, emission);
            return tint;
        }
    }
}
