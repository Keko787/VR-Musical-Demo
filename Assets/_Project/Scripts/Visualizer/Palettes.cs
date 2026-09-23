using UnityEngine;

namespace VRShootingGallery.Visualizer
{
    /// <summary>Helpers for the colour gradients the visualizers and the haunted room are painted with.</summary>
    public static class Palettes
    {
        /// <summary>
        /// Whether a palette field has been left for the component to fill in. Unity never leaves a
        /// serialized <see cref="Gradient"/> null or empty — an unassigned one is saved as plain
        /// white, two keys — so "left empty" has to be recognised as that. A palette someone has
        /// actually painted has a colour in it somewhere.
        /// </summary>
        public static bool IsUnset(Gradient gradient)
        {
            if (gradient == null)
                return true;

            var keys = gradient.colorKeys;
            if (keys == null || keys.Length == 0)
                return true;

            foreach (var key in keys)
                if (key.color.r < 0.999f || key.color.g < 0.999f || key.color.b < 0.999f)
                    return false;

            return true;
        }
    }
}
