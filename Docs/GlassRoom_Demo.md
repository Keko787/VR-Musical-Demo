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
│  ├─ World_Beach          SimWorld + sand, sea (SimOcean), deck, palms, pier and its lamps, headlands, islands, fireworks
│  ├─ World_Aurora         SimWorld + lake, mountains, island, curtains (and reflections), snow
│  └─ World_Orbit          SimWorld (3 variants) + meadow, grass, leaves, boulders, tubes, asteroids, planets, star, satellites, meteors, dust
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
| 6 | **V** | Next variant of the world outside, if it has variants (**Shift+V** goes back) |
| — | **F** | Show or hide the furniture (no wand button by default; set `m_FurnitureButton`) |

As in the other demos, the number-row keys stand in for the wand buttons with the same digit, and
the keyboard stays on in builds.

**Hands off:** `m_NewWorldEachTrack` (on by default) rebuilds the outside into the next world
whenever a new track starts. `m_AutoCycleSeconds` changes worlds on a timer, and
`m_VariantEverySeconds` changes a world's variant on a timer (both off by default).

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

### Sunset Beach

Sunset on a sandy bay. The room stands on a low wooden deck at the top of the beach, with the sea
7 m in front and the sun going down over it, slightly right of centre. Palms line the shore both
ways, a pier runs out to sea on the right, there are rocky headlands at the two ends of the bay,
and islands sit on the horizon. The stars come out overhead, but not over the sunset.

- **The bass swells the sea.** Swells roll in parallel to the curved shore, grow toward the sand,
  break in the last few metres, and run up the beach as each one arrives. The more bass, the bigger
  the sea.
- **The beat breaks on the shore.** On every beat the surf surges up the whole beach at once, with
  foam at its leading edge. It happens right on the beat, not a wave-travel time later, so it
  stays legible.
- **The pier's lamps are an equaliser.** There are 24 pairs of lamps, one band each, with the bass
  at the shore end and the treble far out. They brighten and dim like a meter laid out in
  perspective, and each lamp's reflection quivers on the water beneath it.
- **The mids wave the palms.** The fronds sway in the sea wind, harder with the mids.
- **The treble sets the sun's glitter path dancing** across the water toward the sun.
- **Strong beats burst fireworks over the sea.** A shell bursts on the beat itself, with no launch
  first, so the bang and the light land together. Its colour shows which part of the music made
  it: reds and golds when the bass leads, greens when the mids do, blues and violets for the
  treble. The bursts are mirrored in the water. No two bursts come within 0.6 s of each other
  (1.4 s in low intensity), and each one is a small patch of sky, not a flash of the whole wall.

The sea's shader decides its own height, and it knows where the sand is from the beach's own
formula. That formula is a plain slope near the waterline, with the beach's `SimSurface` as the one
source, handed over by `SimOcean`. That is how the water can run up the sand and foam exactly at the
waterline. Like the lake, the sea marks itself in the stencil buffer, so the lamps' and the
fireworks' mirrored glows are drawn on the water and nowhere else.

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

### Orbit

Space, after Good Kid's "Orbit" video. The room sits on an alien meadow: cyan grass, big tropical
leaves, spiky dark plants, teal boulders, and ribbed tubes arching out of the ground. Above it hang
a ringed planet on the left, a burning star on the right, a smaller planet further round, a field of
asteroids, four drifting satellites and a nebula across the stars.

- **The ring is the spectrum.** Going round the planet's ring, each stretch is a band, with the bass
  at the front and the treble at the back. It brightens like a circular meter. The ring's far half
  hides behind the planet and its near half crosses in front.
- **The star breathes with the bass.** Its corona's flames reach further with the bass, and a beat
  throws a ring of flare out from its edge.
- **The asteroids dance in layers.** The lower a rock hangs, the lower its band: rocks near the
  horizon lift and spin with the bass, and the high ones with the treble. The whole field drifts
  round the room as if in orbit.
- **The meadow plays too.** The grass waves with the mids and its tips glow with the treble; the big
  leaves sway with the bass.
- **Beats** blink the satellites' beacons. **Strong beats** send a shooting star across the sky
  (at most one every 0.8 s, or 2 s in low intensity). **Loudness** brightens the nebula.

The planets and the star are billboards that always face the room's fixed eye, not any one camera,
so their discs, rings and lighting match at the corners between walls.

#### Variants, and the glitch between them

Orbit has three looks: **Orbit** (the video's blue night), **Pink Nebula** (the pastel pink and cyan
of its glitch) and **Green Signal** (teal and green). Each look repaints the sky, the nebula's
colours and swirls, the planets' and the star's tints, the room's light, and the flora. The flora's
materials take their colour from one of four palette slots in the active variant: ground and grass,
light leaves, dark leaves, and rocks and tubes.

**V** (or wand 6) switches looks with a glitch on the glass rather than the tile rebuild used
between worlds. For 1.4 s rows of the picture shear and smear sideways, blocks pixelate, and the
colour channels split. The look swaps underneath at the glitch's worst point. Any world can have
variants: give its `SimWorld` some in **Variants**. A world without them ignores V.

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
- **The furniture can go.** Untick **Furniture** on `Glass_Room`'s `Simulation` component, or press
  **F** in play mode, and the room is a bare glass box: the iron frame, the glass and the floor,
  with nothing between the visitors and the world outside. This hides the desk (with its tablet,
  glass and teddy bear), the cabinet, the bed, the rug and the pendant lamps. The lamps' light and
  their reflections in the glass go with them, so at night the room is lit only by what is
  outside. The setting is kept through a rebuild. A room whose furniture was switched off by hand
  before this option existed stays empty through its next rebuild, with the box unticked.

## Safety

- Every flash outside rides the analyser's beat, which is capped at 3 per second (2 in low
  intensity).
- **The glitch between variants** shifts and smears the picture but keeps its brightness. Its
  pattern jumps six times a second for the 1.4 s it lasts. In low intensity the pattern holds still
  and the glitch is gentler.
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
| `SimOcean` | Hands the sea its beach's waterline |
| `SimPalms` | Palm trees, curved trunks and folded fronds, with sway weights for the shader |
| `SimPier` | The pier's mesh, and its lamps and their reflections as an equaliser |
| `SimFireworks` | Bursts on strong beats, coloured by the leading part of the music, mirrored in the sea |
| `WorldVariant` (in `SimWorld`) | One look of a world: sky, nebula, palette, tints, room light |
| `SimCelestial` | A planet or star: a quad that `SimPlanet` turns to face the room |
| `SimAsteroids` | Faceted rocks round the room, lifted and spun by their bands |
| `SimFlora` | Grass tufts with treble-lit tips, broad leaves, spiky blades; palette-painted, swaying |
| `SimTubes` | Ribbed arches out of the ground |
| `SimSatellite` | A tumbling probe whose beacon blinks on the beat |
| `SimMeteors` | Shooting stars on strong beats, as velocity-stretched glows |
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
| `VRMusic/SimOcean` | The beach's sea: swells, run-up, the surge, glitter, foam; marks the stencil buffer |
| `VRMusic/SimGlint` | Additive glows (lamps, sparks) that can also be drawn as stencil-clipped reflections |
| `VRMusic/SimPlanet` | Planets (banded, lit, atmosphere, a ring that is the spectrum) and stars (corona, flares) |
| `VRMusic/SimAurora` | Curtains and their stencil-clipped reflections |
| `VRMusic/SimPillar` | The camera-facing light pillar |

`SimCommon.hlsl` holds the shared globals (`_GR_*`), the noise functions and the sky function. The
water reflects the same sky function that the dome draws.

**Land shader vertex colour:** below full alpha, a vertex's colour takes over and glows. A voxel
cube that breaks free has particle alpha 0. It is this way round because a mesh with no colour
channel reads as opaque white, and it must still be painted. For the same reason, any mesh the
land shader draws must fill its second UV channel (the band glow). A missing channel is not read as
zero, and the first canyon floor had one sector glowing white. The third UV channel holds sway
(weight, phase). It is read only by a material with `_Sway` above 0 (the palms), so other meshes can
leave it empty. `_WetDarken` darkens a ragged band of sand just above `_WetLevel`, the waterline.

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
| `SimSurface` (beach) | `m_ShoreDistance` / `m_BeachSlope` / `m_BayRadius` | 7 m / 0.05 / 900 m | Where the waterline is, how steep the beach, how the bay curves |
| `M_Glass_Ocean` | `_Swell` / `_SwellByBass` | 0.3 / 0.9 m | The sea at rest, and what the bass adds |
| | `_Surge` / `_SurgeReach` | 0.3 m / 30 m | How far a beat lifts the surf, and how far out |
| | `_Glitter` / `_Wavelength` / `_Speed` | 1.5 / 26 m / 6 m/s | Sun path sparkle; the swells |
| `SimPier` | `m_LampPairs` / `m_Rest` | 24 / 0.12 | Lamps (one band a pair); brightness with nothing in a band |
| `SimFireworks` | `m_MinStrength` / `m_MinInterval` | 0.4 / 0.6 s | What beat sets a shell off, and how often at most |
| | `m_Sparks` / `m_Life` / `m_Speed` | 140 / 2.4 s / 38 m/s | A burst's size and length |
| `M_Glass_Palm` | `_Sway` | 0.6 m | Frond tip sway at full mids |
| `Simulation` | `m_GlitchSeconds` / `m_VariantEverySeconds` | 1.4 s / 0 (off) | The glitch between variants; hands-off variant changes |
| | `m_Furniture` | on | The room's furniture; off for the bare glass box. Kept through a rebuild |
| `SimWorld` (Orbit) | **Variants** | Orbit, Pink Nebula, Green Signal | Each look's sky, nebula, palette, tints and room light |
| | `m_Nebula` / `m_NebulaScale` | 1 / 1.3 | Nebula brightness and swirl size |
| `SimAsteroids` | `m_Count` / `m_Lift` / `m_Spin` | 420 / 0.05 / 6 + 60°/s | Rocks; how far they lift and how fast they spin with their bands |
| `SimMeteors` | `m_MinStrength` / `m_MinInterval` | 0.5 / 0.8 s | What beat sends a shooting star, and how often at most |
| `M_Glass_Grass` | `_BandGlow` / `_Sway` | 1.4 / 0.18 m | Treble glow at the tips; sway with the mids |
| `M_Glass_Leaf_*` | `_Sway` | 0.35 / 0.3 m | Leaf sway with the bass |
| `M_Glass_Planet_Ringed` | `_RingInner` / `_RingOuter` / `_RingTilt` / `_RingAngle` | 1.45 / 2.3 / 0.2 / −16° | The ring's shape |
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
- **Sunset Beach** has been compiled and its shaders imported cleanly, but it has not yet been
  checked in renders. It was written while the editor was open, so no batch preview ran; the scene
  was then rebuilt with it in the editor.
- **Orbit** and its variants compile, and every glass room shader compiles with `fxc` against URP's
  headers. The scene has been rebuilt with Orbit in the editor, but nobody has checked it in
  renders yet.
- **More worlds** are a `SimWorld` and some children away. The film's flower field and city would
  both suit: a skyline whose lit windows are an equaliser is an obvious next one.
