# Room audio visualizer (Visualizer MiddleVR)

The room reacts to whatever is playing. Nothing is tracked, nothing is held: this is the hands-off
demo from the deck — *3D and Room Audio Visualization* and *Rave Room Visualizer* as one build,
the piece that runs first because it tests the room config, alignment and audio routing without
depending on the wand, and the idle state the room can sit in between visitors.

It has eight modes, cross-faded rather than cut — three on the surfaces, five in the volume:

- **2D — water walls.** Every projected screen carries a row of glass tubes with water in them,
  drawn flat on the surface and mapped 1:1 to the CAVE's screens. Each tube is one frequency band;
  the water rises and sloshes with it. Content on the surface has no parallax, so everyone in the
  room sees the same thing wherever they stand, glasses or not. This is the layer that scales to a
  crowd.
- **2D — rainbow ripples.** The four screens as one sheet of rainbow liquid the music drops things
  into. A kick lands a big drop, mostly on the floor; a hit in the mids lands a smaller one on a
  wall; hats and cymbals patter small quick ones everywhere. Each drop's rings are a rainbow, crest
  by crest, over a gradient that lies across the whole room and turns slowly; they stand up off the
  surface in relief (the 2.5D part), and they run round the corners unbroken — a ring dropped on
  the floor climbs the wall. On the surface, so right for everyone, like the water.
- **2D — lava lamp.** The room as a lava lamp: the floor is the heater, and the bass turns it up.
  Wax warms on the floor, slides out to a wall, climbs it, cools near the top and sinks back down;
  blobs merge as they meet and neck apart as they part. The beat swells them, the mids churn their
  outlines, the highs make glitter twinkle in the liquid, and the lamp's colouring drifts through
  five classic ones.
- **3D — lasers and waveforms.** Emitters on the walls fan beams across the volume to other walls,
  re-aiming on the beat, and three oscilloscope traces hang in the air at arm's reach. The beams
  are only right from the tracked viewpoint; where each one lands on a screen there is a hit-spot
  drawn flat, so the untracked room still gets a light show of moving dots, and the colour wash
  behind the tubes stays on in every volume mode. The walls never go black.
- **3D — point cloud.** The spectrogram as a field of points: frequency across the room, time
  flowing from the front wall toward the back, level as a column of points rising from the floor.
  The music arrives from the front and flows through whoever is standing in the room.
- **3D — astral body.** *Musica universalis.* A sun at the room's centre wears the spectrogram on
  its surface, ringed by celestial spheres at harmonic radii, each sphere one band, orbiting at
  Kepler's rates — the inner spheres quick and high-pitched, the outer slow and bass, which is the
  assignment Kepler made in *Harmonices Mundi*. A small planet rides each sphere.
- **3D — fireflies.** A swarm in a box that never changes size. The music is how many fireflies
  are lit, how fast they dart and how they blink, and a beat flashes the whole swarm at once. The
  fixed scale is the point: a change in the audio is a change in the swarm, never in its extent,
  so it reads more clearly than a cloud that grows and shrinks.
- **3D — holographic tree.** A tree of points, two metres tall, standing toward the front of the
  room. It sways with the song — the bass sets how far, each beat sends a gust through it from the
  trunk out to the twigs — and the beat shakes leaves loose, which flutter down turning autumn
  colours, lie on the floor a while, fade, and grow back on their twig. Each leaf is lit by one band,
  bass low in the crown and treble at the top.

## Build it

| Menu item | What it does |
| --- | --- |
| **VR-Music-Demo ▸ Visualizer ▸ Create Visualizer Scene** | Copies the physics scene to `Visualizer MiddleVR.unity`, strips the tether demo and the gun out of it, and builds the visualizer |
| **VR-Music-Demo ▸ Visualizer ▸ Rebuild Visualizer** | Rebuilds it in whatever scene is open, as long as it has a `CAVE_User_Room` |

Same flow as the [guitar](HolographicGuitar_Demo.md) and the [harp](HolographicHarp_Demo.md): the
scene starts as a copy of `Physics MiddleVR.unity` because that scene already carries the whole
MiddleVR setup, the copy is saved before anything is deleted from it, and Create adds it to
**File ▸ Build Settings** switched off.

**Put a track in.** The playlist is every audio clip in `Assets/_Project/Audio/Music`, sorted by
name. The folder starts empty; drop an MP3/WAV/OGG in and press Play — the room opens on the first
track, paused, and **Space** or wand button 4 starts it (see *It opens paused*). The folder is read three
times, so the playlist is never stale: by the builder, **again each time play starts in the
editor** (`MusicDeck.Start`), and **at player build time** (`VisualizerPlaylistProcessor`,
an `IProcessSceneWithReport`) so a CAVE build carries whatever is in the folder without anyone
rebuilding the scene. A player has no asset database, which is why the list is serialized at all.
With no clip the scene still runs — the water idles at a low breathing level and the status line
on the front wall says so — and a clip can be dragged onto the `MusicDeck`'s **Playlist** (under
`Analyser/Music`) by hand, which holds only while the folder is empty. The deck is the same one the
haunted room and the glass room play through.

Everything lives under `CAVE_User_Room/Room_Visualizer`, all registered with Undo:

```
Room_Visualizer            RoomVisualizer — modes, fade, intensity, input
├─ Analyser                AudioAnalyser
│  └─ Music                AudioSource (2D) + MusicDeck — the playlist, play, pause, the track up next
├─ Surfaces_2D
│  ├─ Panel_Front          Quad on the front screen, WaterTubePanel, 24 tubes, bass in the middle
│  ├─ Panel_Left           Quad on the left screen, 16 tubes, bass at the back
│  ├─ Panel_Right          Quad on the right screen, 16 tubes, bass at the back
│  └─ Panel_Floor          Quad on the floor, 24 tubes, water rising toward the front wall
├─ Ripples_2D              RippleSurface — the drops; switched off until its mode is on
│  ├─ Ripples_Front        See-through quad 2.5 mm in front of each water panel, VRMusic/RippleSurface
│  ├─ Ripples_Floor
│  ├─ Ripples_Left
│  └─ Ripples_Right
├─ LavaLamp_2D             LavaLamp — the wax; switched off until its mode is on
│  └─ Lava_Front … Lava_Right  Quads 1.2 mm in front of the water panels (under the ripples), VRMusic/LavaLamp
├─ Lasers_3D               LaserShow — builds its emitters and beams at run time
├─ Waveforms_3D
│  ├─ Ribbon_Across        LineRenderer + WaveformRibbon, wall to wall at chest height
│  ├─ Ribbon_Deep          back to front, a little higher
│  └─ Ribbon_Diagonal      floor back-left up to front-right
├─ PointCloud_3D           ParticleSystem + AudioPointCloud, SpectrumField
├─ AstralBody_3D           ParticleSystem + AudioPointCloud, AstralBody
├─ Fireflies_3D            ParticleSystem + AudioPointCloud, Fireflies
├─ Tree_3D                 ParticleSystem + AudioTree — grows the tree at run time; switched off until its mode is on
├─ Status_Sign             TMP line along the bottom of the front wall: mode, track, next track
├─ Menu                    VisualizerMenu on the front wall: Content (panel, highlight, title, rows,
│                          'more' strips, scrollbar, cursor, and Tables — the 2D and 3D tables the
│                          modes page is laid out as) and Wand_Ray; hidden until the mode or track
│                          button opens it
└─ Track_Indicator         TrackIndicator, high on the front wall: now playing, up next, and a bar
```

A rebuild clears the children; the `RoomVisualizer` on the root is reused, so **the mode, the
buttons, the auto-cycle and everything else in its Inspector survive a rebuild**. Layout constants — tubes per
screen, where the ribbons hang, the panel inset — are at the top of `RoomVisualizerBuilder.cs`; the
look of each layer is on its own component.

The builder also puts the CAVE's view modes on the thumbstick (`CaveViewSetup.BuildOnThumbstick`,
on `CAVE_User_Room/CAVE_View`; see *Running it*), and sets the main camera's clear colour to black. The panels cover all four screens,
so the only place the background could show is a seam, and a seam should be dark. MiddleVR clones
the base camera's settings onto its CAVE cameras.

## Running it

| Wand button | Key | Does |
| --- | --- | --- |
| 1 | **M** | Open or close the **modes** on the front wall (**Shift+M** steps straight to the next mode) |
| 2 | **L** | Toggle low intensity |
| 3 | **N** | Open or close the **tracks** on the front wall (**Shift+N** skips straight to the next track) |
| 4 | **Space** | Play / pause. The room opens paused |
| 0 (trigger) | **Enter** | In the menu: pick the row the wand points at, or the one selected. On a mode, twice quickly layers it with the others |
| Stick up | **F2** | The CAVE's **2D** view: still head-tracked, one picture for both eyes, so no glasses needed |
| Stick down | **F1** | The CAVE's **3D** (stereo) view, the default |

The view is the CAVE's own (`CaveViewMode` on `CAVE_User_Room/CAVE_View`, the same as the haunted
room and the pipe rig — see [CAVE_View_Modes.md](CAVE_View_Modes.md)): one view per push, the stick
back to the middle before the next, and the stick is the menu's while the menu is open, so it changes
the view only with the menu closed. The view's own wand buttons are off here, since the visualizer's
use 1–4. 2D-Fixed is built but has no button or key in this room.

The number-row keys stand in for wand buttons with the same digit, the way the other demos do it,
and the keyboard stays on in builds (`m_KeyboardControl`) so the room's audio routing can be
checked from the server node without the wand. Button indices are whatever the `.vrx` says; the
VisBox wand has 0–4, 6–8, 11 and 15, and 0 is the trigger.

**It opens paused.** The room starts on the first track, cued at its top but not playing: the
strip on the front wall says PAUSED with the track's name, the status line says "(paused)", and the
show waits at its slow idle breathing. Play (button 4 / **Space**) starts the track; picking a track
from the menu plays that one instead. `m_StartPaused` on the `RoomVisualizer` turns this off, and the
music starts with the scene as it used to. It is on the visualizer rather than the `MusicDeck`, so
the haunted room and the glass room, which share the deck, still start playing.

### The menu: modes and tracks

The mode and track buttons open a menu on the front wall, a dark glass panel at a little below eye
height — the same menu as the glass room's, worked the same way. Both are a `WallMenu`, which owns
the panel, the pointer, the joystick and the keys; this room's pages are `VisualizerMenu`, and
`WallMenuBuilder` builds the panel and the strip below.

**Modes** (button 1 / **M**). The eight shows as two tables side by side. On the left, **2D /
SURFACE**: Water Tubes, Rainbow Ripples and Lava Lamp, drawn on the screens and right for everyone
in the room. On the right, **3D / HOLOGRAMS**: Lasers & Waveforms, Point Cloud, Musica Universalis,
Fireflies and Holographic Tree, standing in the volume and right from the tracked viewpoint. Each row has the show's name
and, in small type under it, a few words on what it is. The ones on are marked in amber. Point at a
row in either table; with the joystick or the arrows, up and down step through a table (round from
the bottom to the top), right goes across to the 3D table and left back to the 2D one, and left from
the 2D table closes the menu. A mode can be picked two ways:

- **Pick it once** and the room cross-fades (1.5 s) to that mode alone, and the menu closes.
- **Pick it twice in quick succession** (the trigger or **Enter**, twice within 0.35 s) and it is
  layered with whatever is on: it fades in beside them, or, if it was on, fades out and leaves the
  rest. The menu stays open, so a few can be added in a row, from either table. Water on the walls
  with fireflies in the room, the ripples running over the tubes, or the astral body inside the
  point cloud, are combinations worth trying. The last mode on stays on — the room always has a
  show — so a double pick on it does nothing.

A single pick waits those 0.35 s before it acts, to be sure it is not the first of two; the track
page has no double pick, so a track plays the moment it is picked. The status line names a mix by
its parts: "WATER + LASERS + FIREFLIES".

**Tracks** (button 3 / **N**). The playlist, in order, opened at the track playing. That track is
marked in amber with "playing" (or "paused"), and the one that plays when it ends is marked in blue
with "up next". Picking a track plays it from the start; the mode stays as it is. Track names are
shown without the uploader's tags, so "Good Kid - Orbit (Official Audio)" reads "Good Kid - Orbit".

**Scrolling.** The panel has eleven rows; a longer playlist scrolls, and the title says which part
is showing ("TRACKS 3–13 of 25"). While there is more above or below, a strip just above or below
the rows says so ("▲ 3 more ▲", "▼ 11 more ▼"). **Rest the wand on a strip** and, after a moment,
the list scrolls that way a row at a time until the ray moves off it; **pull the trigger on a strip**
to go a page at once. A **scrollbar** in the margin right of the rows shows how long the list is and
which part is showing: pull the trigger on it to jump there, or hold the trigger and move the wand
up and down to drag. Stepping past the top or bottom row with the joystick scrolls too. At the desk,
the **mouse wheel** scrolls, **Page Up** / **Page Down** go a page, and **Home** / **End** go to the
first and last track.

| Wand | Desk | In the menu |
| --- | --- | --- |
| Point at a row | — | Selects it; a ray shows where the wand points, a cursor where it meets the panel |
| Joystick up / down | ↑ / ↓ | Step through the rows, repeating while held, scrolling past the edge |
| Rest on a ▲ / ▼ strip | Mouse wheel | Scroll the list |
| Trigger on a ▲ / ▼ strip | **Page Up** / **Page Down** | A page up or down |
| Trigger on the scrollbar, held to drag | **Home** / **End** | Jump through the list; to the first or last row |
| Trigger (button 0) | **Enter** | Pick the selected row |
| Trigger twice | **Enter** twice | On a mode: layer it with the others, or take it out; the menu stays open |
| Joystick right / left | → / ← | On the modes: across to the 3D table, back to the 2D one |
| Joystick left | **Backspace**, **Esc**, ← | Close (from the 2D table, on the modes) |
| Button 1 / button 3 | **M** / **N** | Close, or switch to the other list |

The tables are the shared `WallMenu`'s, so any room's menu can have a page of two: its builder
passes `tables: true` to `WallMenuBuilder.BuildMenu`, and the subclass opens the page with the split
between them (`Open(selected, split)`) and names the headings (`TableHeading`). Each table has seven
rows and does not scroll. A visualizer scene built before the tables shows the modes as one list,
2D first, and says so in the console until it is rebuilt.

The panel's see-through parts all sit in one render queue, so `WallMenuBuilder` gives each a sorting
order in the order they are stacked — panel, frames, highlight, bars, text, cursor — rather than
leaving them to sort by distance. Before, rows of text farther from the eye than the panel's middle
(the top and bottom rows, seen from eye height) drew first and were dimmed by the panel. Every
room's menu gets this when its scene is rebuilt, except the animal orchestra's: its builder then sets
one sorting order on the whole menu (to draw it after the starlings), which flattens the stack again.

While the menu is open the shows in the room's volume — lasers, ribbons, the point clouds — dim to a
quarter (`m_MenuDim`), so the panel can be read through them; the water and the ripples, on the
walls behind the panel, stay. The auto-cycle waits while it is open. The other buttons and keys keep working. The
pointer looks for MiddleVR's `HandNode`; at a desk with no wand, the keys work alone. Without a
`VisualizerMenu` on the `RoomVisualizer`, the buttons go back to stepping to the next mode and
skipping to the next track.

### Now playing, up next

A slim strip high on the front wall says what is playing, what plays next, and has a bar for how
far through the track the music is. It fades in for 8 s when a track starts, again for the last
20 s of a track (counting down: "UP NEXT IN 0:12"), and stays while the music is paused. In between
it fades away, so the show has the wall to itself. It steps aside while the menu is open, because
the track list marks the same two tracks. **Always** on the `TrackIndicator` keeps it up all the
time. The status line at the foot of the front wall also names the next track.

**Hands off.** Set `m_AutoCycleSeconds` (say 90) and the room walks the eight modes on its own, one
at a time; a mix someone layered from the menu is left alone until a single pick or **Shift+M**
goes back to one mode. With `m_LoopPlaylist` on the `MusicDeck` it plays the folder round and
round, and with `m_StartPaused` off it starts without anyone pressing play. When the music stops the analyser reports
idle and every layer eases down to a slow, dim breathing rather than going dead, so an open house
can leave it running.

### Low intensity

The deck's safety note: fast flashing is a photosensitivity risk, more so in an enclosed dark room.
Two things guard against it, one always on and one switchable.

- **Always:** beats are rate-limited in the analyser. However busy the track, nothing driven by
  the beat can flash faster than `m_MaxFlashesPerSecond` (3 Hz, the usual guidance).
- **Low intensity** (wand 2 / **L**): everything dims to `m_LowIntensityScale` (45%), the beat
  flash on the walls is off (the wash still breathes with the bass), and the beat cap drops to
  `m_LowIntensityFlashesPerSecond` (2 Hz). The status line says LOW INTENSITY. Turn it on for a
  school group before they walk in.

## How the music becomes numbers

`AudioAnalyser` reads the music source every frame (or the `AudioListener` — everything the room
is playing — if no source is set, which is the right choice once a microphone source exists) and
publishes:

- **`Bands[]`** — 24 log-spaced bands from 40 Hz to 14 kHz, each in 0–1. A band is the mean FFT
  magnitude across its bins, tilted up toward the highs (music falls off up there), then measured
  in decibels against **its own running peak** — a peak that rises at once and halves every 3 s,
  and never drops below a noise floor. So a quiet acoustic track fills the tubes as well as a
  mastered club mix, a band with nothing in it sits at zero instead of dancing on hiss, and a
  passage that is merely quieter reads as quieter. Attack 15 ms, release 200 ms.
- **`Bass`, `Mid`, `High`, `Loudness`** — the same treatment over 40–150 Hz, 150–2000 Hz,
  2–14 kHz, and the RMS of the output.
- **`Waveform[]`** — the last 2048 output samples, normalised by a slow peak so the ribbons stay
  legible at any level.
- **`Beat`, `BeatPulse`, `BeatStrength`** — spectral flux in the kick range (35–160 Hz): how much
  louder each bin got since the last frame, summed, against the mean and spread of the last 1.2 s
  of the same measure. A frame that stands 1.4 standard deviations clear of that history is a
  beat, provided it is not too soon after the last one. `BeatPulse` goes to 1 and decays in
  180 ms; that is what a flash reads. `BeatStrength` says how far past the threshold it was, so a
  soft hit and a drop are not the same beat.
- **`Idle`** — 0 while music plays, ramping to 1 over 2 s once the room has been silent for 1.5 s.
- **`HistoryAt(slice, band)`** — a spectrogram: the bands copied into a ring 24 times a second,
  72 slices deep (three seconds), newest first, on a fixed clock so the time axis is regular
  whatever the frame rate. `HistoryPhase` says how far the clock is toward the next sample, so a
  layer can slide between slices instead of stepping. The point clouds read this.

The FFT is 2048 bins (a 4096-sample window, 85 ms at 48 kHz, 11.7 Hz per bin) — enough to tell a
kick from a bass note without lagging the beat. 4096 doubles both the resolution and the lag.

## The water

Each panel is one quad, sized and placed from the CAVE's `.vrx` (see `CaveScreens`), sitting 5 mm
inside the screen plane — enough to draw over the room model's wall quads, far too little to read
as depth from anywhere in the room. The whole look is the `VRMusic/WaterTubes` shader; the C# side
(`WaterTubePanel`) owns what a shader cannot, the water's motion, and hands over per-tube arrays
through a `MaterialPropertyBlock`, so the four panels share one material.

**Motion.** A tube's level is a mass on a spring chasing its band (`m_Stiffness` 80,
`m_Damping` 0.55 — under 1, so it overshoots a little, which reads as weight). The surface is a
second, lighter oscillator (`m_SloshHz` 1.4, `m_SloshDamping` 0.12) that the level's acceleration
kicks: a sudden rise sloshes, a held note goes still, and a drop rings on for a second after.
Rising water glows at the meniscus, and every tube catches the beat.

**Look.** In the shader, per pixel: which tube, then a capsule distance in metres so the tubes come
out round on a 3.5 × 2.2 m quad. Under the surface the water is the band's colour, darker with
depth, shaded round across the tube, with caustic shimmer that brightens as the level rises and a
few bubbles looping up the glass. The meniscus is a bright line that flashes on a rise and on the
beat. Above the surface the glass is a faint tint over the wash. A rim catches the wash at the
edges and a specular streak runs down one side. Outside the tubes the water's glow leaks through
the glass onto the wash.

**Wash.** The colour behind the tubes drifts between two dark colours over 45 s, is lifted by the
bass, and flashes on the beat (not in low intensity). It is time-based, not stateful, so every
panel computes the same wash from the same analyser and the four screens agree. It stays on in 3D
mode after the tubes fade out — the rave part of the show is on the walls in both modes.

**Layout.** The front and the floor mirror the spectrum about their centre, so the bass sits in the
middle of the room; the sides run it back to front, the right wall reversed, so the treble on all
four screens meets at the front corners. `m_Layout` per panel: Ascending, Descending, Mirrored.
The palette (`m_Palette`, a Gradient) runs navy at the bass end to aqua-white at the treble. Unity
saves an unassigned Gradient as plain white, so plain white means "use the default"; paint any
colour into it and yours is used instead (`Palettes.IsUnset`). The same holds for every palette
in the project.

## The rainbow ripples

`RippleSurface` owns the drops; the look is the `VRMusic/RippleSurface` shader, on one quad per
screen 7.5 mm inside the screen plane — 2.5 mm in front of the water panels and behind the status
line. The quads are see-through (premultiplied: a crest adds light, a trough's shade darkens what
is under it), so alone the rings run over the water panels' dark wash, which stays on in every mode,
and layered with the tubes they run over the tubes.

**Drops.** Four kinds (`RippleKind`: wavelength, speed, life, height, hue, share on the floor):

| Kind | When | Rings | Lands |
| --- | --- | --- | --- |
| `m_Kick` | every beat the analyser fires, strength from the beat's | 0.42 m apart, 1.2 m/s, 3.2 s | half on the floor |
| `m_Mid` | a jump in the mids (150 Hz–2 kHz) well over their recent level | 0.24 m, 0.85 m/s, 2.4 s | mostly the walls |
| `m_High` | the same in the highs (2–14 kHz) | 0.13 m, 0.6 m/s, 1.4 s | anywhere |
| `m_Drizzle` | nothing playing, or nothing landed for 2 s | 0.3 m, 0.55 m/s, 3.6 s, soft | anywhere |

A hit in the mids or highs has to fall back toward the band's level before it can fire again, and
not sooner than 0.2 s / 0.12 s after the last, so a held chord is one drop. Sixteen drops run at
once; a new one takes a free slot or the one that has died down the most. Of three random spots on
the chosen screen, a drop lands on the one farthest from the drops still running, so they spread.
Each drop is a packet of rings: a sharp leading crest and a train behind it that grows longer as it
spreads, falling away with time and with the ring's growing circumference.

**One surface, not four.** When a drop lands, its centre is worked out once for every panel,
*unfolded* into that panel's plane — the screen it landed on folded flat about the edge the two
share — so a distance on a panel is the distance along the walls and floor, and a ring crosses a
corner at the speed it crossed the floor, unbroken at the seam. The side walls face each other with
no shared edge, so a drop on one reaches the other two ways round, over the floor and over the
front wall, and the shader takes the nearer. The panels' places are read back from the quads at run
time, so the drops follow the screens if the layout changes.

**Colour.** A rainbow gradient lies across the whole room (one full rainbow per `m_GradientSpan`,
6 m of surface, along a direction that leans upward and comes round in 140 s), and the colour wheel
turns once every `m_HueCycleSeconds` (90 s), a little faster on a busy track (`m_HuePerKick`). Each
kind starts at its own place on the wheel — kicks warm, mids yellow-green, highs blue, drizzle
violet — and each crest behind a drop's front is a step further round (`_HueSpread`), so a drop's
rings are concentric rainbows. Where rings cross, their colours mix on the colour wheel, passing
through the hues between; where the surface is quiet, the gradient shows through faintly
(`_Sheen`), so the room is a dim rainbow pool between drops.

**Relief (the 2.5D part).** The rings and a slow four-wave swell (higher with the bass) make a
height field; its slope tilts the surface, a lamp inside the room (`m_Lamp`, above and behind the
middle) lights it, and the flanks that face the lamp catch a glint seen from a fixed eye (`m_Eye`).
Lamp and eye are fixed, not the tracked head, so every eye and every viewer sees the same picture:
it is surface content, like the tubes. A beat brightens the whole surface a little (`m_BeatLift`),
not in low intensity, and low intensity dims it like everything else.

## The lava lamp

`LavaLamp` moves the wax; the look is the `VRMusic/LavaLamp` shader, on one quad per screen
6.2 mm inside the screen plane — over the water panels and under the ripples, so the ripples can
run over the wax when the two are layered. The liquid is mostly opaque (`_LiquidAlpha` 0.86): it
hides the wash, and over the tubes the tubes show faintly through.

**The wax.** Eighteen blobs (`m_Blobs`, radius 0.13–0.3 m) on the screens as one surface, the same
`SurfaceMap` the ripples use. Each has a heat: the heater warms what is on the floor or low on a wall
(`m_HeaterReach` 0.45 m), slowly (`m_HeatRate`), so the wax pools on the floor a while; it cools a
little on the way up and most near the top (`m_CoolRate`). A blob warmed past 0.7 sets off up and
keeps rising until it has cooled below 0.3, then sinks all the way down until it has warmed again —
without that gap it would hover at the height where warming and cooling balance, which is what the
first renders showed. It moves at `m_RiseSpeed` (0.12 m/s, slower across the floor), faster with
the music's loudness (`m_MusicPace`), and takes a moment to turn round. On a wall, rising is up; on the floor it is out toward the nearest wall (front,
left or right — the back is open), so a hot blob slides off the floor and climbs the wall it
reached, and a cold one sinks off the bottom of a wall and drifts back into the middle of the
floor to warm again. A blob that drifts round a corner goes on along the next wall; one at the top
of a wall can climb no further and cools there. Blobs on the same screen ease apart when they
crowd, so the wax does not all end up in one lump.

**The music.** The bass turns the heater up (`m_BassHeat`) — quick to warm, slow to cool, like a
real element — so a heavy track keeps the wax moving and a quiet one lets it settle. The beat swells
the blobs for a moment (`m_BeatSwell`, off in low intensity), the mids churn their outlines, and
the highs set how many glitter flecks twinkle in the liquid.

**The look.** The blobs are metaballs: each a soft bump that reaches twice its radius, summed, and
wax where the sum crosses a threshold, so two that touch flow into one and one pulling away necks
and parts. The wax is lit as a rounded lump by the same fixed lamp as the ripples, with a gloss
where it faces it, and glows from inside, brighter deeper in and when hot. The liquid is lit from
below by the heater (the floor is the brightest part of the lamp) and glows round the wax. The
colouring — hot wax, cooled wax, the liquid low and high — drifts through five every
`m_PaletteSeconds` (70 s): classic orange in purple, aqua in navy, magenta in teal, yellow in
crimson, lime in violet.

## The holographic tree

`AudioTree`, a particle system written by hand like the point clouds, on the same additive point
sprite. The tree is grown once from `m_Seed`: a trunk that splits two or three ways `m_Depth` (5)
times, each branch 62–80% of its parent's length, tilted 25–42° from it and reaching a little
upward, one of every three coming off part way along; then scaled so the top is at `m_Height`
(2.0 m) and the crown reaches no more than `m_Spread` (0.8 m) from the trunk, which stands at
`m_Base` (z = 0.2 m), so the whole tree is inside the room. The trunk and branches are about 3,200
points (`m_BranchPoints`), in rings round the thick ones so they read as a shell, with scanlines
running up them; each twig carries 14 leaves (`m_LeavesPerTwig`). The whole tree turns slowly
(`m_Turn`, a turn in about two minutes) so its depth shows to someone standing still.

**Sway.** The wind's strength is a calm `m_CalmSway`, plus `m_BassSway` at full bass, plus a gust
on each beat (`m_Gust`, by the beat's strength) that dies away over a second; it swings at
`m_SwayHz` (0.3 Hz), a little faster with loudness, and its direction comes slowly round. Every
branch bends on its parent toward the wind, by `m_Flex` for its depth — thin branches more — and a
little later than its parent, so a gust runs from the trunk out to the twigs. Leaves flutter on
their twigs, more with the mids.

**Leaves.** Each is lit by one band, bass low in the crown and treble at the top, green through
aqua to a pale violet (`m_LeafPalette`), brighter and bigger with its band. Each beat shakes
`m_LeavesPerBeat` loose (more on a strong one) while more than `m_KeepLeaves` (45%) are still on
the tree; a loose leaf falls rocking side to side, carried a little by the wind and held inside
the room, turning autumn amber as it goes, lies on the floor `m_LieSeconds` (4 s), fades, and grows
back on its twig over `m_RegrowSeconds` (3 s). Like the other shows in the volume, it dims while
the menu is open.

**With Musica Universalis.** The two, layered, would run into each other: the astral body's spheres
fill the middle of the room at the crown's height. So while both are on, each moves aside as it
fades in — the astral body up 0.3 m, centred as before (`m_BodyBesideTree` on the `RoomVisualizer`;
its top ring then reaches about 2.08 m, under the top of the walls), and the tree 0.85 m to the left
and 0.45 m back from the front wall (`m_TreeBesideBody`; its crown stays about 10 cm off the left
wall as it sways) — and both glide home when either is turned off. Leaves already falling or lying
on the floor stay where they are. For symmetry the tree is then two: a second, its mirror image
across the room's middle, stands as far to the right and sways and drops leaves as its reflection
(`AudioTree.Twin`). It grows in where the first stands as the pair forms, so the tree splits in two
that part to either side of the rising astral body, and they merge back when the pair is undone. Measured in batch with a track playing: with the tree moved left
but the astral body not raised, about a hundred of their points came within 5 cm of each other every
frame; with both moves and both trees, none come within about 13 cm, and none leave the room. Raised, the outermost
(most tilted) sphere's top arc passes above the front wall's top edge as seen from the back of the
room, where the CAVE has no ceiling to draw it, so that arc goes out of sight until it turns down
again. A rise of 0.2 m also keeps the two apart (about 9 cm between them) with less of that.

## The lasers and the ribbons

**Lasers** (`LaserShow`): 3 emitters, 5 beams each, built at run time from `CaveScreens`. An
emitter sits on a projected screen; each of its beams ends on a *different* screen, so every beam
crosses the volume, and both ends are on a surface — the deck's design rule, treat the room as the
stage, not a window. A beam is one band: its width (`m_CoreWidth` + `m_EnergyWidth` × band) and
brightness follow that band, and the fan opens from `m_FanClosed` to `m_FanOpen` of the target
screen with it. Between beats the endpoints drift on Perlin noise. On a beat, with probability
`m_HopOnBeat` scaled by the beat's strength, one emitter acts: a strong beat moves it to another
wall and re-aims every beam (a cut, the way a real rig does it), a soft one only re-aims the fan
(a 0.35 s sweep). Where a beam meets a screen there is a radial hit-spot drawn flat on it, sized
by the band. `m_OpenSurfaces` lets beams end on the back wall and ceiling too — off by default,
since the projectors do not cover those and a beam ending there has no visible landing.

The beams are `LineRenderer`s on `VRMusic/LaserBeam`: additive, no depth write, a hot white core
falling off to the colour at the edges (`_Softness`, `_Core`). Colour is HDR and per beam through
a property block. The palette runs red at the bass end through magenta and blue to a green-white
treble.

**Ribbons** (`WaveformRibbon`): a line strung between two room points, displaced along an axis by
the live waveform, one sample per point, averaged over the samples between points so a dense
waveform does not alias into a jitter. A second axis fed a copy of the waveform 32 samples behind
twists the flat trace into a ribbon. The drawn trace follows the live one with a 30 ms lag — enough
to read as a wave rather than a flicker, not enough to smear the transients. Width follows
loudness; brightness follows loudness and the beat. The ends taper so a trace fades into the wall
rather than stopping dead on it. Three are built: across the room at chest height, back to front a
little higher, and one diagonal from the floor's back-left corner up toward the front-right, so
there is a trace in the volume whichever wall a viewer faces.

3D content near the tracked user holds up best (deck, slide 6) — the ribbons pass through the
middle of the room at 1.3–1.65 m, within arm's reach of someone standing in the centre.

## The point clouds

All three are one component, `AudioPointCloud`, on a `ParticleSystem` whose particles are written by
hand every frame — the system emits and simulates nothing, it is used for what it is good at, a
few thousand camera-facing sprites that render correctly through every one of MiddleVR's
cameras. The layout is plain C# on the CPU (a few thousand points, a few dozen operations each)
so every number is in the Inspector. Per-point colour carries hue and level; the material's HDR
colour, through a property block, carries brightness, opacity and the beat, so a fade costs
nothing per point. The sprite is `M_Visualizer_Point`, the laser shader in radial mode.

**Spectrum field** (`CloudShape.SpectrumField`): one column of points per band per history slice,
24 × 72 × 6 = 10,368 points. Frequency runs across the room (`m_FieldLayout`, Mirrored by default
so the bass runs down the middle like the walls), time runs from the front wall back, and a
column's points spread up its height in proportion to the level, so a loud band is a tall column
and a quiet one a bright dot on the floor line. Between history samples the whole field slides
back by `HistoryPhase`, so it flows rather than steps; older slices dim toward the back wall.
Points shimmer sideways with their level and the field bounces on the beat.

**Astral body** (`CloudShape.AstralBody`): musica universalis — the ancient idea that the
proportions of the heavens are a music, and Kepler's version of it, where each planet sings a
range set by its orbit. Built as:

- **The sun** — 3,200 points on a Fibonacci sphere (even everywhere, no crowding at the poles),
  wearing the spectrogram: latitude is the band, bass at the equator and treble at the poles;
  longitude is time, newest at the front meridian; the surface rises by `m_CoreBulge` (40% of the
  radius) with the level and colours from ember through orange to white-hot.
- **The celestial spheres** — six rings of 480 points at radii that are just-intonation intervals
  of the core radius (`m_SphereRatios`: 5/4, 3/2, 5/3, 2, 5/2, 3 — major third, fifth, major
  sixth, octave, tenth, twelfth). Each is one band, innermost the highest. Each orbits at a rate
  that follows **Kepler's third law**, period ∝ radius^1.5 from `m_OrbitRate`, hurried a little
  by its band, so the inner treble spheres spin fast and the outer bass ones turn slowly. Each
  plane is tilted by its own inclination (`m_Inclinations`) with its line of nodes turned on from
  the last, so the rig reads as an armillary sphere. A sphere thickens and brightens with its
  band and breathes out on the beat.
- **The planets** — a ball of 64 points riding each sphere, swelling with its band.

The whole body precesses about the vertical (`m_Precession`, one turn in ~80 s) so it moves for a
viewer standing still. It sits forward of the room's middle (`m_BodyCentre` z = 0.3 m) so someone
standing in the middle is not inside the sun; the outer sphere reaches 0.72 m, inside the room's
volume with margin, per the deck's rule.

**Fireflies** (`CloudShape.Fireflies`): 2,400 fireflies with homes in a 3.0 × 1.7 × 2.0 m box
(`m_SwarmSize`, less the wander margin, so the swarm's full extent is the box and no more). The
box is fixed — the spectrum field's columns and the astral body's spheres grow with the level,
which is expressive but makes a quiet passage and a loud one different *sizes*; here they are
the same size with different life in them, so a change in the audio reads as a change in the
swarm. Bass fireflies live low in the box and treble ones high (`m_BandsByHeight`), spread enough
to overlap. Each fly is dealt to a band in equal blocks and:

- **is lit when its band's level is above its own random threshold**, so the lit fraction of a
  band *is* its level — at half level, half that band's fireflies are lit. Unlit ones stay at
  `m_Unlit` (5%) so the box always shows;
- **wanders** around its home on a sum of sines whose clock runs at `m_RestSpeed` + `m_DartSpeed`
  × level, so loud bands dart and quiet ones drift; the clock is integrated, so a speed change is
  continuous rather than a jump;
- **blinks**, if lit, at a rate that rises with the level (`m_BlinkRate` at full, a fifth of that
  at silence), shaped into a flash with a dark gap between;
- **flashes with every other fly on a beat** (`m_Synchrony`), the way synchronous fireflies do.

Colour by band, amber for the bass through the classic yellow-green to a cool white treble.

## In the CAVE

- **Single node.** `MVR.vrx` renders all four viewports on one node, so one analyser feeds all four
  walls and they agree by construction. On the 4-node config the analysis would have to be shared
  across the cluster (the bands, energies and beat as MiddleVR shared data, ~30 floats a frame);
  that is the one thing in this demo that is not cluster-safe as written.
- **No post-processing.** `MVR_URP` disables bloom, so the lasers do not bloom. Their hot core is
  done in the shader instead. HDR is off in the CAVE's `DisplayManager`, so overbright colours clip
  to white, which is what a laser's core should do anyway.
- **Everything is unlit.** The panels, beams, spots and ribbons ignore the scene's lights, so the
  four screens cannot disagree about shading, and the directional light in the template scene is
  irrelevant to the show.
- **Audio.** The music source is 2D — the room's speakers carry the mix as mastered. The deck's
  idea of a spatial mix that follows the tracked user is a later experiment: set
  `spatialBlend` on the music source and move it, and the analyser is unaffected because it reads
  the source, not the listener.
- **Untracked viewers.** The 2D table's modes are right for everyone. The 3D table's are right for
  the tracked user; the hit-spots on the walls and the wash are what carry the rest of the room,
  which is why they exist. For a large group, run 2D and leave 3D for when someone has the glasses on.
  The CAVE's 2D view (stick up) is a different thing from the 2D table: it draws the whole scene, 3D
  shows included, the same for both eyes, still from the tracked head.

## Where it stands

- **Microphone input.** The analyser can already take any `AudioSource`; a `Microphone.Start`
  source looped into it (muted at the output if the room has feedback) would make the room react
  to a live performer. Not built.
- **Side-screen overhang.** The side screens run 16.5 cm past the floor's back edge; the panels
  cover it, the room model's wall quads do not, so in the preview window the extra stripe is the
  panel alone. In the CAVE that stripe is projected and correct.
- **Beat tracking, not beat detection.** Beats are found as they happen; there is no tempo estimate
  and nothing anticipates the next one. A phase-locked tempo would let the lasers land *on* the
  beat instead of a frame after it. Not built.

## Tuning

Everything is in the Inspector, per component. The ones worth knowing:

| Component | Field | Default | Effect |
| --- | --- | --- | --- |
| `RoomVisualizer` | `m_StartPaused` | on | Open on the first track, paused, until someone presses play |
| | `m_FadeSeconds` | 1.5 | Cross-fade between modes, and a layered mode's fade in or out |
| | `m_AutoCycleSeconds` | 0 (off) | Hands-off mode switching; waits while the menu is open or a mix is layered |
| | `m_MenuDim` | 0.75 | How far the volume's shows dim while the menu is open |
| | `m_BodyBesideTree` / `m_TreeBesideBody` | (0, 0.3, 0) / (−0.85, 0, −0.45) m | Where Musica Universalis and the tree move to while both are on |
| | `m_Intensity` | 1 | Master brightness |
| | `m_MaxFlashesPerSecond` / `m_LowIntensityFlashesPerSecond` | 3 / 2 | Beat rate cap |
| `VisualizerMenu` | `m_DoublePickSeconds` | 0.35 | How quick a second pick must be to layer a mode; also how long a single pick waits |
| | `m_HoverScroll` | 0.3 s / 0.12 s | How long the wand rests on a 'more' strip before the list scrolls, then the time per row |
| `TrackIndicator` | `m_AfterStart` / `m_BeforeEnd` / `m_Always` | 8 s / 20 s / off | When the strip shows: after a track starts, before it ends, or all the time |
| `AudioAnalyser` | `m_BandCount` | 24 | Bands; the tubes interpolate, so more tubes than bands is fine |
| | `m_RangeDb` | 40 | Dynamic range of a tube, bottom to top |
| | `m_GainHalfLife` | 3 s | How fast the auto-gain lets go after a loud passage |
| | `m_Attack` / `m_Release` | 15 / 200 ms | Band envelope |
| | `m_BeatSensitivity` | 1.4 σ | Lower finds more beats; raise it if a busy track fires on everything |
| `WaterTubePanel` | `m_TubeCount` | 24 / 16 | Tubes on that screen |
| | `m_Stiffness` / `m_Damping` | 80 / 0.55 | Level spring: snap and overshoot |
| | `m_SloshHz` / `m_SloshDamping` / `m_SloshDrive` | 1.4 / 0.12 / 0.012 | Surface wave: pitch, ring time, how hard a rise kicks it |
| | `m_WashBass` / `m_BeatFlash` | 0.35 / 0.3 | Wall wash response |
| `RippleSurface` | `m_Kick` / `m_Mid` / `m_High` / `m_Drizzle` | see *The rainbow ripples* | Each kind's ring spacing, speed, life, height, hue and floor share |
| | `m_OnsetJump` / `m_OnsetGap` | 0.16 / 0.2 s, 0.12 s | How big a jump in the mids or highs drops a ring, and how often |
| | `m_HueCycleSeconds` / `m_GradientSpan` | 90 s / 6 m | How fast the colours turn; how much surface one rainbow spans |
| | `m_IdleSwell` / `m_BassSwell` | 0.28 / 0.45 | The swell between drops, and how much the bass raises it |
| | `m_Lamp` / `m_Eye` | room points | Where the relief is lit from and the glints seen from |
| `LavaLamp` | `m_Blobs` / `m_Radius` | 18 / 0.13–0.3 m | How much wax, and how big |
| | `m_RiseSpeed` / `m_MusicPace` | 0.12 m/s / 1.4 | How fast the wax moves, and how much faster with loudness |
| | `m_HeatRate` / `m_CoolRate` / `m_HeaterReach` | 0.12 / 0.2 / 0.45 m | How long the wax pools on the floor; how fast it cools at the top; how far up the heater reaches |
| | `m_BassHeat` / `m_BeatSwell` | 1.2 / 0.12 | How much the bass turns the heater up; the swell on a beat |
| | `m_PaletteSeconds` | 70 s | How long each colouring lasts |
| `VRMusic/LavaLamp` defaults¹ | `_Threshold` / `_Bulge` | 0.42 / 0.07 | Where the wax's edge is; how rounded it looks |
| | `_Glow` / `_Gloss` / `_Glitter` | 0.7 / 0.6 / 0.8 | Inner glow, gloss, glitter |
| | `_LiquidAlpha` / `_HeaterGlow` | 0.86 / 0.7 | How much of the wash the liquid hides; the floor's glow |
| `AudioTree` | `m_Height` / `m_Spread` / `m_Base` | 2 m / 0.8 m / z 0.2 m | The tree's size and where it stands |
| | `m_Depth` / `m_LeavesPerTwig` / `m_BranchPoints` | 5 / 14 / 3200 | How bushy, how leafy, how dense the bark |
| | `m_CalmSway` / `m_BassSway` / `m_Gust` | 0.2 / 0.9 / 0.6 | Sway at rest, with the bass, and on a beat |
| | `m_SwayHz` / `m_Flex` | 0.3 Hz / 0.02…0.14 rad | Swing rate; how far each depth of branch bends |
| | `m_LeavesPerBeat` / `m_KeepLeaves` | 14 / 0.45 | Leaves a beat drops; the share always kept on the tree |
| | `m_LieSeconds` / `m_RegrowSeconds` | 4 s / 3 s | How long a fallen leaf lies, and grows back |
| `VRMusic/RippleSurface` defaults¹ | `_Relief` | 0.05 | How far the rings stand up off the surface |
| | `_Sheen` / `_RingGlow` / `_Glint` | 0.06 / 1 / 0.9 | The quiet pool's glow, the rings' glow, the glints |
| | `_HueSpread` / `_HueByHeight` / `_Saturation` | 0.18 / 0.08 / 0.9 | Hue step per crest, crest-to-trough hue shift, how vivid |
| `LaserShow` | `m_Emitters` × `m_BeamsPerEmitter` | 3 × 5 | Beam count |
| | `m_HopOnBeat` | 0.55 | How often a beat moves an emitter |
| | `m_FanClosed` / `m_FanOpen` | 0.08 / 0.45 | Fan spread at silence / full band |
| | `m_Brightness` / `m_BeatBrightness` | 1.4 / 1.2 | Beam level and its beat lift |
| `WaveformRibbon` | `m_Amplitude` | 0.3 m | Trace displacement at full scale |
| | `m_Smoothing` | 30 ms | Trace lag |
| `AudioPointCloud` | `m_PointSize` / `m_SizeByLevel` | 16 / 28 mm | Sprite size at zero and full level |
| | `m_Brightness` / `m_BeatBrightness` | 1.2 / 0.7 | Cloud level and its beat lift |
| | `m_FieldSize` / `m_Stack` | 3 × 1.8 × 2 m / 6 | Field extent and points per column |
| | `m_CoreRadius` / `m_CoreBulge` | 0.24 m / 0.4 | The sun's size and how far it swells |
| | `m_SphereRatios` / `m_Inclinations` | just intonation / 0…34° | The spheres' radii and tilts |
| | `m_OrbitRate` / `m_Precession` | 0.09 / 0.012 rev/s | Orbit speed at ratio 1; the whole body's turn |
| | `m_SwarmSize` / `m_Fireflies` | 3 × 1.7 × 2 m / 2400 | The swarm's fixed box and how many live in it |
| | `m_RestSpeed` / `m_DartSpeed` | 0.12 / 1.0 | Wander speed at silence and the extra at full level |
| | `m_BlinkRate` / `m_Synchrony` | 0.7 Hz / 0.8 | Blink rate at full level; how hard a beat flashes the swarm |
| | `m_Unlit` | 0.05 | Brightness of an unlit firefly |
| `AudioAnalyser` | `m_HistoryRate` / `m_HistorySlices` | 24 Hz / 72 | Spectrogram depth the clouds read |
| Shader `M_Visualizer_Water` | `_TubeFill` | 0.7 | Tube diameter as a fraction of its cell |
| | `_Bubbles` / `_Ripple` / `_Caustics` | 0.75 / 0.6 / 0.5 | The water's decoration |
| | `_GlassStrength` | 0.35 | Rim and streak |

¹ Tune these in the shader's defaults, not on the material: each Rebuild writes the shader's defaults into
`M_Visualizer_Ripples` and `M_Visualizer_LavaLamp`, so a material made by an earlier build never keeps an old look.
