# Music

Drop audio files here — MP3, WAV or OGG. Every clip in this folder, sorted by name, is the
visualizer's playlist.

**The tracks themselves are not in the repository.** They are commercial recordings, and twenty-odd
megabytes of audio would sit in every clone forever, so `.gitignore` keeps this folder's contents
out. Only this README is tracked. Your local files are untouched by any of that — they simply do
not travel with the project.

## How the playlist is read

The folder is scanned three times over, so the playlist is never stale:

| When | What reads it |
| --- | --- |
| When the visualizer scene is built | `RoomVisualizerBuilder` |
| Every time play starts, in the editor | `RoomVisualizer.Start` |
| At player build time | `VisualizerPlaylistProcessor` |

So a track dropped in here plays the next time you press Play — no rebuild. A player build carries
whatever is in the folder at the moment it is built, because a player has no asset database to
scan at run time.

With this folder empty the visualizer still runs: the water idles at a low breathing level, and the
status line on the front wall says there is no track.

See [Docs/RoomVisualizer_Demo.md](../../../../Docs/RoomVisualizer_Demo.md).
