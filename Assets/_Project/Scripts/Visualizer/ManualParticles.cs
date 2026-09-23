using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>
    /// Sets a <see cref="ParticleSystem"/> up to be written by hand: nothing emits, nothing
    /// simulates, and the particles live until they are overwritten. The point clouds, the ghost
    /// and the fire all use a particle system for the one thing it does well here — thousands of
    /// camera-facing sprites that render correctly through every one of MiddleVR's cameras.
    /// </summary>
    public static class ManualParticles
    {
        /// <summary>Lifetime given to every particle, and reset every frame, so the system's own ageing never removes one.</summary>
        public const float Lifetime = 1e6f;

        public static void Configure(ParticleSystem system, ParticleSystemRenderer renderer, bool size3D = false)
        {
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startSize3D = size3D;
            main.maxParticles = Mathf.Max(main.maxParticles, 1);

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = false;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 1f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Grows the system's particle budget to at least <paramref name="count"/>.</summary>
        public static void Reserve(ParticleSystem system, int count)
        {
            var main = system.main;
            if (main.maxParticles < count)
                main.maxParticles = count;
        }
    }
}
