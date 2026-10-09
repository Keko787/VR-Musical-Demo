# Glass room (GlassRoom MiddleVR)

A glass box with an iron frame, built 1:1 inside the CAVE so that its walls are the screens. It is
furnished like the room in Porter Robinson & Madeon's *Shelter*: a glass desk with a tablet and a
teddy bear, a small drawer cabinet with books on it, a brass bed, and three glass pendant lamps.
Outside the glass is a world that plays to the music. The room stays the same while the world
outside changes, much as Rin's room stays put while she builds worlds on her tablet.

Like the [visualizer](RoomVisualizer_Demo.md) and the [haunted room](HauntedRoom_Demo.md), it is a
hands-off demo: nothing is tracked and nothing has to be held. It reuses their analyser and music
deck.

## The glass walls are camera feeds, not windows

Each glass wall shows a camera's picture, drawn flat on the CAVE screen. The camera stands outside,
at the room's position in the world. The worlds are not 3D content seen through the walls.

- **All three cameras use one fixed eye**: the middle of the room at standing eye height,
  `(0, 1.6, −0.15)`. Each camera's frustum is cut to its wall's corners as seen from that eye (an
  off-axis projection). So each picture is exactly what a real window in that wall would show
  someone standing at the eye. Because all three come from the same eye, they meet at the
  corners as one continuous panorama.
- **The eye does not move when people move.** Because the picture lies flat on the screen
  surface, the outside looks the same from anywhere in the room. Untracked visitors never see the
  landscape skew or swim, which is what 3D content does for them. This is the "fix the camera to one
  place" item from `ToDo.txt`. The room itself is built 1:1 with the CAVE, which keeps the 3D parts
  comfortable for everyone.
- For a demo with one person wearing glasses, `WindowView ▸ m_Parallax` moves the eye part of the
  way, or all the way, toward the tracked head. At 1 the walls become true windows for that person,
  and the outside swims for everyone else.

The worlds sit 20 km from the room in the scene. That is well beyond the CAVE cameras' 1 km far
plane, and the room is well beyond the window cameras' 10 km far plane, so neither set of cameras
can draw the other's content. No layers or culling masks are involved.

## Build it

| Menu item | What it does |
| --- | --- |
| **VR Shooting Gallery ▸ Glass Room ▸ Create Glass Room Scene** | Copies the physics scene to `GlassRoom MiddleVR.unity`, strips the tether demo and the gun out, and builds the room and its worlds |
| **VR Shooting Gallery ▸ Glass Room ▸ Rebuild Glass Room** | Rebuilds the room in whatever scene is open, as long as that scene has a `CAVE_User_Room` |

It also builds without opening the editor:

```
Unity -batchmode -projectPath . -executeMethod VRShootingGallery.EditorTools.GlassRoomBuilder.CreateFromCommandLine -quit
```

**Tracks** come from `Assets/_Project/Audio/Music`, shared with the other demos.

**Nothing is bought or downloaded.** The furniture is built from primitives, the floorboards are
painted in code (`GlassRoomTextures`), and the land, rocks, trees, clouds and aurora are generated
**at run time** from the numbers on their components. A canyon floor has tens of thousands of
vertices; saved as a mesh asset, it would put megabytes of text into version control. The same
numbers always generate the same land. As a result, the outside looks empty in the editor's
Scene view until you press Play.

Everything lives under `CAVE_User_Room/Glass_Room`:

```
Glass_Room                 Simulation — worlds, transitions, music globals, room light, input, status
├─ Audio                   AudioSource + MusicDeck + AudioAnalyser
├─ Outside                 20 km away: the room's position in every world
│  ├─ Sky                  SkyDome: one dome, painted by whichever world is awake
│  ├─ World_RedRock        SimWorld + its land, pillar, voxels, motes
│  ├─ World_CloudSea       SimWorld + cloud sea, cumulus towers, pillars, sparks
│  └─ World_Aurora         SimWorld + lake, mountains, island, curtains (and reflections), snow
├─ Floorboards, Rug
├─ Frame                   iron posts, rails, sills, corner braces
├─ Desk                    glass top, legs, glass of water, Tablet (shows the front view), Teddy_Bear
├─ Cabinet                 drawers, books, ink bottles
├─ Bed                     brass head and foot, mattress, blanket, pillow
├─ Pendants                three PendantLamps, each with a point light
├─ Glass_Walls             Glass_Left / Glass_Front / Glass_Right: WindowView each
└─ Status_Sign             dim line on the floor at the open back
```

A rebuild clears the children and reuses the `Simulation` on the root, so its Inspector settings
survive a rebuild.

## Running it

| Wand button | Key | Does |
| --- | --- | --- |
| 1 | **W** | Next world (**Shift+W** goes back) |
| 2 | **L** | Toggle low intensity |
| 3 | **N** | Next track |
| 4 | **Space** | Pause / resume |

As in the other demos, the number-row keys stand in for the wand buttons with the same digit, and
the keyboard stays on in builds.

**Hands off:** `m_NewWorldEachTrack` (on by default) rebuilds the outside into the next world
whenever a new track starts. `m_AutoCycleSeconds` changes worlds on a timer.

## Changing worlds

The change between worlds is the simulation rebuilding itself, in the spirit of the film's
dissolving cliff. The view breaks into 11 cm tiles. Each tile shrinks away into a pale blank page,
showing a bright rim as it goes. The tiles go in a wave around the room, from the back of the left
wall, across the front, to the back of the right wall. While only the blank page shows, the old
world goes to sleep and the next one wakes. The new view then builds back out of tiles behind the
wave. The whole change takes 3.2 s (`m_TransitionSeconds`). The room's light eases to the new
world's light over 1.5 s.

## The worlds

### Red Rock

The canyon from the film's opening. The room is perched on a rock 25 m above a desert floor.
Around it stand red buttes, mesas and spires cut in strata, with green trees on their ledges and
across the floor. A light pillar falls into a pool in front of the middle butte.

- **The light pillar** gets wider with the bass and brighter with the loudness. Light streams
  down it. On a beat, a bright band falls from the sky to its foot.
- **Beat rings:** when that band reaches the ground, a shockwave of cyan light runs out across the
  canyon floor and the rock bases (240 m/s). Up to four rings can be in flight at once.
- **The voxel mesa** on the left is the film's dissolving cliff turned into an equaliser. Each of
  its 24 columns of cubes is one band, bass on the left. As a band rises, cubes break off the top of
  its column, turn pale and glowing, and float up and turn: the louder the band, the higher they go.
  When the band falls, they settle back into the rock. A beat throws the free cubes higher.
- **Strata seams:** a thin line of light runs along the top of each rock stratum. Each stratum
  follows one band, bass at the foot of the cliffs and treble at the top, so the cliffs light up
  like meters on a loud passage.
- **Motes** rise around the pillar and twinkle with the treble.

### Above the Clouds

The outside the room has in the film: a pale sky, the sun low ahead, and the room floating 60 m
above a sea of cloud.

- **The music rolls out from the room as swells across the cloud sea.** The sea is lifted by a
  20-second spectrogram. Each swell leaves the room the moment it is heard and rolls outward at
  45 m/s, so older music is further away. Direction is pitch: the bass rises straight ahead of the
  front wall, and the treble comes round toward the sides and back. Swell crests glow faintly with
  the level that raised them.
- **Cumulus towers** on the horizon each glow from inside with their own part of the spectrum.
- **Three distant pillars of light**, one each for bass, mids and treble.
- **Sparks** lift off the sea with the treble.

### Aurora

Night on a lake ringed by snowy mountains, with a low moon on the left and snow drifting past the
glass. The room's lamps burn at full brightness.

- **Three aurora curtains.** Each vertical ray in a curtain is one band, with the bass in the
  middle of the curtain and the treble toward its ends. A ray reaches higher and burns brighter
  with its band's level. The folds sway on their own and ripple harder with the mids. A beat sends
  a surge of light out from the middle along the lower edge. The rays are green low down and violet
  where they reach high.
- **The reflection** in the lake is a second copy of each curtain, flipped about the waterline. It
  is drawn only where the water is (the water marks itself in the stencil buffer) and wavers with
  the bass.
- **Stars** twinkle harder with the treble, and so do their reflections in the lake.

## The room

- **Its light comes from outside.** Each world sets the scene's directional light (colour,
  strength and direction, matched to its sun or moon) and the ambient light. The iron frame casts
  real shadows across the floor. The builder turns off shadow casting on the template's room
  model; otherwise its wall quads would shade the room from the sun.
- **The pendant lamps** are the calm inside the show. They burn low by day and full at night,
  breathe a little warmer with the loudness, and swing gently on their cords, a touch further with
  the bass. They never flicker. Their reflections show in the glass where the fixed eye would see
  them.
- **The tablet** on the desk shows the front wall's view: the device the worlds are made on.

## Safety

- Every flash outside rides the analyser's beat, which is capped at 3 per second (2 in low
  intensity).
- **Low intensity** (wand 2 / **L**) dims every glow outside to 55%, halves the beat rings, and dims
  the blank page between worlds. The blank page is the one large change in brightness, and it fades
  over 1.6 s, never cuts.

## In the CAVE

- **Cost:** three extra cameras, each rendering one mono picture per frame (2048 × 1280 for the
  front, 1375 × 1280 for the sides, 4× MSAA). The worlds use custom unlit-style shaders, cast no
  shadows and have nothing to light, so they are cheap to draw. `m_PixelsPerMetre` on each
  `WindowView` lowers the resolution if needed.
- **Single node:** as with the other demos, one director drives all four viewports. On the
  4-node config, each node would render its own window pictures. They would match as long as the
  analysis and the world index are shared across the cluster.
- **Untracked viewers:** every wall's picture is flat and the same for everyone. Only the room's
  furniture, frame and lamps are 3D. Those are near the walls and at the room's own scale, which is
  where 3D holds up best for people without glasses.
- **The floor** is the room's floor. Nothing outside is drawn on it.

## How it is put together

| Script | Role |
| --- | --- |
| `Simulation` | Director: worlds, the transition, the music as shader globals (bands, beat, rings, spectrogram), the room's light, input, status line |
| `SimWorld` | One world's sky, how it lights the room, and where its beat rings start |
| `WindowView` | One glass wall: the outside camera, its off-axis frustum from the fixed eye, the render texture, lamp reflections |
| `PendantLamp` | A lamp's glow, light and gentle swing |
| `SimSurface` | Polar-grid ground generated around the room: desert floor, mountain ring, flat water or cloud |
| `SimButte` | Tiered rock formations: buttes, mesas, spires, the room's pedestal |
| `SimPuffs` | Trees and cumulus built from balls, optionally lit by a band |
| `SkyDome`, `AuroraCurtain` | The sky's sphere; a curtain's strip |
| `LightPillar`, `VoxelMesa`, `SimMotes` | The pillar, the cubes, the motes |
| `SimMeshes` | Noise and mesh helpers |
| `GlassRoomBuilder`, `GlassRoomTextures` | Editor: builds everything, paints the floorboards |

| Shader | Draws |
| --- | --- |
| `VRMusic/GlassWindow` | The glass: the view, tint, haze, streaks, lamp reflections, and the tile dissolve |
| `VRMusic/SimSky` | The dome: gradient, sun, cirrus, stars, milky way, moon |
| `VRMusic/SimLand` | Rock, ground, trees, snow, cumulus, voxels: cel-lit with violet shadows, strata, rim light, band glow, beat rings, haze |
| `VRMusic/SimCloudSea` | The cloud sea lifted by the spectrogram |
| `VRMusic/SimWater` | Sky-reflecting water that marks the stencil buffer |
| `VRMusic/SimAurora` | Curtains and their stencil-clipped reflections |
| `VRMusic/SimPillar` | The camera-facing light pillar |

`SimCommon.hlsl` holds the shared globals (`_GR_*`), the noise functions and the sky function. The
water reflects the same sky function that the dome draws.

**Land shader vertex colour:** below full alpha, a vertex's colour takes over and glows. A voxel
cube that breaks free has particle alpha 0. It is this way round because a mesh with no colour
channel reads as opaque white, and it must still be painted. For the same reason, any mesh the
land shader draws must fill its second UV channel (the band glow). A missing channel is not read as
zero, and the first canyon floor had one sector glowing white.

## Tuning

| Component | Field | Default | Effect |
| --- | --- | --- | --- |
| `Simulation` | `m_TransitionSeconds` | 3.2 | Dissolve out + build in |
| | `m_NewWorldEachTrack` / `m_AutoCycleSeconds` | on / 0 | Hands-off world changes |
| | `m_Attack` / `m_Release` | 0.05 / 0.4 s | How the outside eases with the bands, on top of the analyser |
| | `m_SpectrogramRate` / `m_SpectrogramSlices` | 12 / 240 | 20 s of music on the cloud sea |
| | `m_LowIntensityScale` | 0.55 | Glow level in low intensity |
| `WindowView` | `m_Eye` | (0, 1.6, −0.15) | The fixed eye all three walls are taken from |
| | `m_Parallax` | 0 | How far the eye follows the tracked head |
| | `m_PixelsPerMetre` / `m_Msaa` | 576 / 4 | Picture resolution and antialiasing |
| `SimWorld` | sky colours, sun, clouds, stars, moon, fog | per world | The look |
| | `m_RingSpeed` / `m_RingDelay` | 240 m/s / 0.35 s | Beat rings |
| | `m_WaveSpeed` | 45 m/s | Cloud-sea swells |
| | room ambient / light / `m_LampLevel` | per world | How each world lights the room |
| `VoxelMesa` | `m_Columns` × `m_Rows` × `m_Layers`, `m_Cube` | 24 × 4 × 4, 8.5 m | The cube crown |
| | `m_Lift` / `m_BeatKick` | 55 m / 14 m/s | How far cubes float, how hard a beat throws them |
| `LightPillar` | `m_Width` / `m_Swell` / `m_Listen` | per pillar | Size, bass swell, which band it follows |
| `PendantLamp` | `m_SwayDegrees` / `m_LightIntensity` | 2.5° / 0.9 | Swing and brightness |
| `M_Glass_Canyon` | `_StrataGlow` / `_Rings` | 1.4 / 1.2 | Strata seams and beat rings on the rock |
| `M_Glass_Window` | `_TintAmount` / `_Haze` / `_Streaks` / `_CellSize` | 0.12 / 0.06 / 0.05 / 0.11 m | The glass, and the dissolve's tile size |

## Where it stands

- **Checked:** the scene was built and run in batch mode with a track playing, and stills were
  rendered of each world, each wall and a transition, both from inside the room and as the raw wall
  pictures. The beat rings and the voxel lift come from code paths the stills could not show
  moving; they need a look at a desk in play mode.
- **Not tried in the CAVE yet:** in particular, whether MiddleVR leaves the three extra
  render-texture cameras alone (`WindowView` switches its camera back on if anything turns it off),
  and the frame rate with them.
- **More worlds** are a `SimWorld` and some children away. The film's flower field and city would
  both suit: a skyline whose lit windows are an equaliser is an obvious next one.
