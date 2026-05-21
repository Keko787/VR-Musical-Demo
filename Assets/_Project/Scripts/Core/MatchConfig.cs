using UnityEngine;

namespace VRShootingGallery.Core
{
    /// <summary>
    /// Match modes. Only <see cref="MatchMode.TimeAttack"/> is wired for v1; the others are
    /// reserved so adding them later is additive, not a rewrite (see DESIGN scope notes).
    /// </summary>
    public enum MatchMode
    {
        TimeAttack,
        TargetCount,
        Endless,
    }

    [CreateAssetMenu(menuName = "VR Shooting Gallery/Match Config", fileName = "MatchConfig")]
    public class MatchConfig : ScriptableObject
    {
        [Tooltip("Only TimeAttack is implemented in v1.")]
        public MatchMode Mode = MatchMode.TimeAttack;

        [Tooltip("Round length in seconds (TimeAttack).")]
        public float Duration = 60f;

        [Tooltip("Targets to clear (reserved for TargetCount mode).")]
        public int TargetGoal = 30;

        [Tooltip("Shown on the results screen / leaderboard.")]
        public string DisplayName = "Time Attack 60s";
    }
}
