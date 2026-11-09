# CAVE view modes: 3D, 2D and 2D-Fixed

The three views Sit-Rep-CAVE's CARE offers (`Sit-Rep-CAVE/Assets/_Project/Scripts/CaveBox`), by way of
VR-Demo, where they were first brought to the demos (its bounded volumes scene). Only the tracked viewer sees the CAVE right in 3D. These views are for
everyone else in the room.

| View | Wand button | What the screens show | For |
| --- | --- | --- | --- |
| **3D** | 1 | MiddleVR's stereo as configured, from the tracked head | The person wearing the tracked glasses |
| **2D** | 2 | The same head-tracked view, flat: both eyes get one picture, sharp without glasses | Watching over the tracked viewer's shoulder |
| **2D-Fixed** | 3 | Every screen drawn from one fixed eye in the room, flat and steady whoever moves | An audience; no glasses, no head-follow |

**Picking a view is on the wand only:** one button per view, and nothing on the screens. Sit-Rep picks
from a plaque in the room; here a menu would sit in every audience member's view of the scene, so
there is none. One button per view rather than one that cycles, so a press always lands on the same
view whatever the CAVE was showing. The trigger (button 0) is left out, so squeezing the wand never
changes the view. The buttons are fields on `CaveViewMode`, because wand button indices come from the
`.vrx`. `MiddleVRWandButtonProbe` lists them, as in
[MiddleVR_Controller_Input.md](MiddleVR_Controller_Input.md).

Keys **1**, **2** and **3** do the same for whoever is at the desk, from Unity's keyboard or
MiddleVR's; MiddleVR's works without the Editor having focus. Untick **Keyboard Shortcuts** to make it
wand-only. The view holds until changed; leaving Play hands MiddleVR its stereo back. Each change is
logged: `[CaveView] View: 2D-Fixed`.

Here the Animal Orchestra has it ([AnimalOrchestra_Demo.md](AnimalOrchestra_Demo.md)); its builder
calls `CaveViewSetup.Build`, and any other room's builder can do the same with one line. In the other
music rooms buttons 1–3 do other things (world, low intensity, next track), so a room that takes the
views moves those, as the orchestra does to 4 and 6.

**Or the thumbstick**, for a room that keeps its buttons: tick **Stick Picks View** and set the
buttons to −1 (none — a negative index is never read as the trigger). Then the stick pushed up picks
2D and pushed down picks 3D, one change per push, the stick back in the middle before the next, and
not while a wall menu is open (the stick is the menu's then). The haunted room does this
([HauntedRoom_Demo.md](HauntedRoom_Demo.md)), with its desk keys moved to F1 / F2.

## How each view works

**2D** sets every MiddleVR stereo camera's inter-eye distance to 0, on the kernel's cameras rather
than Unity's, because the kernel computes each eye's projection from it. MiddleVR's own Invert Eyes
sample changes it the same way. Each camera's own distance is kept by name and put back for 3D. Names,
because SWIG hands out a new proxy object per call.

**2D-Fixed** leaves MiddleVR's tracking alone. Freezing the tracked head is what
[the fixed-viewpoint experiment](MiddleVR_FixedViewpoint_Experimental.md) struggled with, and moving
Unity's `HeadNode` would not change the projections MiddleVR computes. Instead, `FixedEyeView` puts a
camera at the fixed eye for each screen, with an off-axis frustum cut through that screen's four
edges, and renders it into a **picture lying on the screen**. A picture on the screen plane has no
parallax: every eye MiddleVR draws it for sees it in the same place. So the head-tracked cameras show
it unmoved however the tracked viewer moves. While 2D-Fixed is on, every camera but the fixed ones
draws only the `CaveOverlay` layer, where the pictures are. MiddleVR rewrites its cameras' projection
every frame but never their culling mask, so the swap holds until it is undone. Sit-Rep does this for
its front wall; here it's every screen.

The fixed eye defaults to **(0, 1.6, −0.9)** in the room's frame: a standing viewer's eyes, a little
behind the room's centre. Change **Eye** on `CAVE_View`'s `FixedEyeView` (live in Play mode); its gizmo
draws the eye and its lines of sight to every screen's corners.

The screens are the CAVE's real ones, read from `Assets/_Project/Config/MVR.vrx` by the builder, not
the room model. In this CAVE the side screens are 2.3876 m deep, 16.5 cm more than the room's wall
quads, and a picture sized from the room would leave that strip uncovered. The `.vrx` is Z-up
(positions read x, depth, height), so poses are composed up the parent chain in MiddleVR's frame and
only then turned into Unity's. Checked numerically: every screen's corners land exactly on its
picture's corners. If the file can't be read, the builder falls back to the room model and says so.

Each picture is 1600 px tall, the CAVE's own height, so nothing is resampled. Four fixed cameras
render the full scene, and MiddleVR's eight eye cameras render only four quads, so 2D-Fixed costs less
than 3D.

## What the builder makes

`CaveViewSetup.Build` puts `CaveViewMode` and `FixedEyeView` (screens from `MVR.vrx`) on
`CAVE_User_Room/CAVE_View`, cleared and rebuilt each time. At runtime `FixedEyeView` adds a camera and
a picture per screen the first time 2D-Fixed is picked.

Outside the scene it makes the pictures' material, `M_FixedViewPicture` (URP Unlit, created once and
then left alone). It also adds the **`CaveOverlay` layer** to the project's first free user layer; here
that is layer 8. That is a project setting, so it is not undoable, and the Console says which layer it
took. When Unity saves `TagManager.asset` it writes it in 2022.3's own format, which drops two blocks
only newer Unity versions use (`m_RenderingLayers` and `m_MigratedRenderPipelines`); they do nothing in
2022.3.9.

| Component | Field | Default |
| --- | --- | --- |
| `CaveViewMode` | Start View | 3D |
| | Stereo 3D / Mono 2D / Fixed 2D Button | 1 / 2 / 3 |
| | Keyboard Shortcuts | on (1 / 2 / 3) |
| `FixedEyeView` | Eye | (0, 1.6, −0.9) |
| | Picture Height | 1600 px |

## Checking it

Press Play and press **2**: the Console logs `[CaveView] View: 2D` and the stereo should collapse to
one picture. Press **3**: every screen shows the scene from the fixed eye, and moving the head (or the
simulator's) no longer moves the scene. Press **1** to get the stereo back. In the CAVE, do the same
with wand buttons 1, 2 and 3.

Not tested on the CAVE yet. Two things to look at there:
- **The seams in 2D-Fixed.** A line in the scene should run straight across where two screens meet,
  seen from the fixed eye's spot.
- **Which physical buttons 1–3 are.** They're the ones the gun demos use for their toggles. If one is
  awkward to reach, change the index on `CAVE_View`.
