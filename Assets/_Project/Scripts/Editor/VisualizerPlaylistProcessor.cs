using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Fills every <see cref="RoomVisualizer"/>'s playlist from the music folder as its scene goes
    /// into a player build, so the CAVE build carries whatever is in the folder at build time
    /// without anyone having to rebuild the scene first. In the editor the visualizer does the same
    /// scan itself when play starts; this is the equivalent for the asset-database-less player.
    /// </summary>
    class VisualizerPlaylistProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            var clips = RoomVisualizer.ClipsInMusicFolder();
            if (clips.Length == 0)
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var visualizer in root.GetComponentsInChildren<RoomVisualizer>(true))
                {
                    visualizer.Playlist = clips;
                    if (report != null)
                        Debug.Log($"[Visualizer] '{scene.name}': {clips.Length} track(s) from '{RoomVisualizer.MusicFolder}' " +
                                  "written into the build's playlist.");
                }
            }
        }
    }
}
