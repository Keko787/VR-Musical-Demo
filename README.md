# VR Musical Demo — CAVE demos for the VisCube M4

Unity demos for the AERI Lab's **VisCube M4 CAVE** at the University of Missouri: three projection
walls plus a floor, driven by **MiddleVR**, with one tracked viewer and one wand.

The project began as a stationary VR shooting gallery for the Quest and was ported to the CAVE; it
is now mostly a set of music demos scoped from `CAVE Music Experience Demo`, each sized so one
developer can build, run and repair it alone.

| | |
| --- | --- |
| **Unity** | 2022.3.9f1 LTS |
| **Render pipeline** | URP 14.0.11 |
| **VR** | MiddleVR 2.4.0 — *not* Unity XR; OpenXR is disabled on purpose |
| **Room** | 3.556 × 2.2225 m screens, 3 walls + floor (`MVR.vrx`) |

## The demos

Each lives in its own scene under `Assets/_Project/Scenes/Demos/`, and each has a build menu item
under **VR Shooting Gallery ▸ …** that generates the scene from a template, plus a document.

| Scene | What it is | Doc |
| --- | --- | --- |
| `Visualizer MiddleVR` | Room audio visualizer — five modes: water tubes on the walls, lasers, point cloud, an astral body, fireflies | [RoomVisualizer_Demo.md](Docs/RoomVisualizer_Demo.md) |
| `HauntedRoom MiddleVR` | Ghost in the machine: a Victorian parlour on the CAVE walls, possessed by the music, and the ghost doing it | [HauntedRoom_Demo.md](Docs/HauntedRoom_Demo.md) |
| `HarpRoom` | Holographic harp the wand plucks | [HolographicHarp_Demo.md](Docs/HolographicHarp_Demo.md) |
| `GuitarRoom` | Holographic guitar the wand strums, with chord shapes on wand buttons | [HolographicGuitar_Demo.md](Docs/HolographicGuitar_Demo.md) |
| `ShootingGallery MiddleVR` | The original shooting gallery round | [ShootingGallery_Game.md](Docs/ShootingGallery_Game.md) |
| `Physics MiddleVR` | Tethered-ball physics; also the template the other scenes are copied from | [TetheredBall_Physics.md](Docs/TetheredBall_Physics.md) |
| `MarbleRun MiddleVR` | Marble run | [MarbleRun_Demo.md](Docs/MarbleRun_Demo.md) |
| `HotWheelsTrack` | Slot-car track | [HotWheels_Track.md](Docs/HotWheels_Track.md) |

## Getting it running

Porting a Unity project to this CAVE has a chain of five things that must all be true before a
single pixel appears — the MiddleVR version guard, the scripting backend, the URP companion
package, disabling OpenXR, and having a plain Main Camera for MiddleVR to clone. All of it, in
order, with the reasoning:

> **[Docs/MiddleVR_CAVE_Setup.md](Docs/MiddleVR_CAVE_Setup.md)**

Pressing Play on a dev PC shows MiddleVR's *preview window*, not the walls. The CAVE itself only
lights up when the app runs on the CAVE system.

Other setup notes: [MiddleVR_Controller_Input.md](Docs/MiddleVR_Controller_Input.md) for the wand's
button indices, [MiddleVR_A6000_GPU_Placement.md](Docs/MiddleVR_A6000_GPU_Placement.md) for the
render nodes.

## Layout

```
Assets/_Project/
├─ Art/            Materials, meshes and the visualizer's shaders; Haunted/ holds the parlour's painted textures
├─ Audio/Music/    Playlist folder — tracks are gitignored, see its README
├─ Scenes/         Boot/ and Demos/
└─ Scripts/
   ├─ Editor/      Scene builders — each demo is generated, not hand-placed
   ├─ Visualizer/  Audio analysis, playback, and the five visualizer modes
   ├─ Haunted/     The haunted room: the director, the possessed furniture, the ghost
   ├─ Guitar/  Harp/  Strings/   The instruments
   ├─ Gun/  Targets/  Core/  UI/ The shooting gallery
   └─ Physics/     Marble run, slot cars, tethered ball
```

Demo scenes are **built by editor scripts**, not assembled by hand: a builder copies the template
scene, strips the other demos out of it and generates the geometry, so layout and tuning live in
constants at the top of one file rather than in a scene nobody can diff. Rebuilding preserves what
you changed in the Inspector.

## Design constraints

The room shapes everything, and these are not negotiable:

- **One correct viewpoint.** Perspective is solved for the tracked user; everyone else sees a
  skewed frame. Content that stays *on* the surfaces has no parallax, so it reads for everyone —
  that is the layer that scales to an audience.
- **One wand.** One-handed interaction only; no two-handed play, no two players.
- **The room is the stage, not a window.** Geometry crossing a screen edge breaks the illusion for
  the whole room, and content past the physical walls has no surface to land on.
- **No virtual locomotion.** The user walks physically. Cybersickness and, in a real room, safety.
- **Cap the flash rate.** An enclosed dark room makes photosensitivity a real risk; the visualizer
  rate-limits every beat-driven flash and ships a low-intensity mode.

## Notes

The shooting-gallery-era planning documents — [DESIGN.md](DESIGN.md),
[IMPLEMENTATION.md](IMPLEMENTATION.md) and the phase checklists — describe the original Quest build
and are kept for provenance; the CAVE demos above superseded that plan.
