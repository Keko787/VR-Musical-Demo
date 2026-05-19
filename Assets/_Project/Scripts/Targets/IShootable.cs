using UnityEngine;

namespace VRShootingGallery.Targets
{
    /// <summary>
    /// Data passed to something that gets hit by a projectile. Defined now so the
    /// projectile's collision path is final; targets implement <see cref="IShootable"/>
    /// in Phase 2 without any change to <c>Projectile</c>.
    /// </summary>
    public readonly struct ShotInfo
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public ShotInfo(Vector3 point, Vector3 normal, Vector3 direction, GameObject source)
        {
            Point = point;
            Normal = normal;
            Direction = direction;
            Source = source;
        }
    }

    public interface IShootable
    {
        void OnShot(in ShotInfo info);
    }
}
