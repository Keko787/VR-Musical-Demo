# Room audio visualizer (Visualizer MiddleVR)

The room reacts to whatever is playing. Nothing is tracked, nothing is held: this is the hands-off
demo from the deck — *3D and Room Audio Visualization* and *Rave Room Visualizer* as one build,
the piece that runs first because it tests the room config, alignment and audio routing without
depending on the wand, and the idle state the room can sit in between visitors.

It has five modes, cross-faded rather than cut — one on the surfaces, four in the volume:

- **2D — water walls.** Every projected screen carries a row of glass tubes with water in them,
  drawn flat on the surface and mapped 1:1 to the CAVE's screens. Each tube is one frequency band;
  the water rises and sloshes with it. Content on the surface has no parallax, so everyone in the
  room sees the same thing wherever they stand, glasses or not. This is the layer that scales to a
  crowd.
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

## Build it

| Menu item | What it does |
| --- | --- |
| **VR Shooting Gallery ▸ Visualizer ▸ Create Visualizer Scene** | Copies the physics scene to `Visualizer MiddleVR.unity`, strips the tether demo and the gun out of it, and builds the visualizer |
| **VR Shooting Gallery ▸ Visualizer ▸ Rebuild Visualizer** | Rebuilds it in whatever scene is open, as long as it has a `CAVE_User_Room` |

Same flow as the [guitar](HolographicGuitar_Demo.md) and the [harp](HolographicHarp_Demo.md): the
scene starts as a copy of `Physics MiddleVR.unity` because that scene already carries the whole
MiddleVR setup, the copy is saved before anything is deleted from it, and Create adds it to
**File ▸ Build Settings** switched off.

**Put a track in.** The playlist is every audio clip in `Assets/_Project/Audio/Music`, sorted by
name. The folder starts empty; drop an MP3/WAV/OGG in and press Play. The folder is read three
times, so the playlist is never stale: by the builder, **again each time play starts in the
editor** (`RoomVisualizer.Start`), and **at player build time** (`VisualizerPlaylistProcessor`,
an `IProcessSceneWithReport`) so a CAVE build carries whatever is in the folder without anyone
rebuilding the scene. A player has no asset database, which is why the list is serialized at all.
With no clip the scene still runs — the water idles at a low breathing level and the status line
on the front wall says so — and a clip can be dragged onto `Room_Visualizer ▸ Playlist` by hand,
which holds only while the folder is empty.

Everything lives under `CAVE_User_Room/Room_Visualizer`, all registered with Undo:

```
Room_Visualizer            RoomVisualizer — modes, fade, intensity, playlist, input
├─ Analyser                AudioAnalyser
│  └─ Music                AudioSource, 2D, the playlist plays through it
├─ Surfaces_2D
│  ├─ Panel_Front          Quad on the front screen, WaterTubePanel, 24 tubes, bass in the middle
│  ├─ Panel_Left           Quad on the left screen, 16 tubes, bass at the back
│  ├─ Panel_Right          Quad on the right screen, 16 tubes, bass at the back
│  └─ Panel_Floor          Quad on the floor, 24 tubes, water rising toward the front wall
├─ Lasers_3D               LaserShow — builds its emitters and beams at run time
├─ Waveforms_3D
│  ├─ Ribbon_Across        LineRenderer + WaveformRibbon, wall to wall at chest height
│  ├─ Ribbon_Deep          back to front, a little higher
│  └─ Ribbon_Diagonal      floor back-left up to front-right
├─ PointCloud_3D           ParticleSystem + AudioPointCloud, SpectrumField
├─ AstralBody_3D           ParticleSystem + AudioPointCloud, AstralBody
├─ Fireflies_3D            ParticleSystem + AudioPointCloud, Fireflies
└─ Status_Sign             TMP line along the bottom of the front wall: mode, track
```

A rebuild clears the children; the `RoomVisualizer` on the root is reused, so **the mode, the
buttons, the auto-cycle and everything else in its Inspector survive a rebuild**. Layout constants — tubes per
screen, where the ribbons hang, the panel inset — are at the top of `RoomVisualizerBuilder.cs`; the
look of each layer is on its own component.

The builder also sets the main camera's clear colour to black. The panels cover all four screens,
so the only place the background could show is a seam, and a seam should be dark. MiddleVR clones
the base camera's settings onto its CAVE cameras.

## Running it

| Wand button | Key | Does |
| --- | --- | --- |
| 1 | **M** | Next mode — water → lasers → point cloud → astral body → fireflies → water — with a 1.5 s cross-fade (**Shift+M** goes back) |
| 2 | **L** | Toggle low intensity |
| 3 | **N** | Next track |
| 4 | **Space** | Pause / resume |

The number-row keys stand in for wand buttons with the same digit, the way the other demos do it,
and the keyboard stays on in builds (`m_KeyboardControl`) so the room's audio routing can be
checked from the server node without the wand. Button indices are whatever the `.vrx` says; the
VisBox wand has 0–4, 6–8, 11 and 15, and 0 is the trigger.

**Hands off.** Set `m_AutoCycleSeconds` (say 90) and the room walks the five modes on its own; with
`m_LoopPlaylist` on it plays the folder round and round. When the music stops the analyser reports
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
The palette (`m_Palette`, a Gradient) runs navy at the bass end to aqua-white at the treble; leave
it empty for that default or paint your own.

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
- **Untracked viewers.** 2D mode is right for everyone. 3D mode is right for the tracked user; the
  hit-spots on the walls and the wash are what carry the rest of the room, which is why they exist.
  For a large group, run 2D and leave 3D for when someone has the glasses on.

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
| `RoomVisualizer` | `m_FadeSeconds` | 1.5 | Cross-fade between modes |
| | `m_AutoCycleSeconds` | 0 (off) | Hands-off mode switching |
| | `m_Intensity` | 1 | Master brightness |
| | `m_MaxFlashesPerSecond` / `m_LowIntensityFlashesPerSecond` | 3 / 2 | Beat rate cap |
| `AudioAnalyser` | `m_BandCount` | 24 | Bands; the tubes interpolate, so more tubes than bands is fine |
| | `m_RangeDb` | 40 | Dynamic range of a tube, bottom to top |
| | `m_GainHalfLife` | 3 s | How fast the auto-gain lets go after a loud passage |
| | `m_Attack` / `m_Release` | 15 / 200 ms | Band envelope |
| | `m_BeatSensitivity` | 1.4 σ | Lower finds more beats; raise it if a busy track fires on everything |
| `WaterTubePanel` | `m_TubeCount` | 24 / 16 | Tubes on that screen |
| | `m_Stiffness` / `m_Damping` | 80 / 0.55 | Level spring: snap and overshoot |
| | `m_SloshHz` / `m_SloshDamping` / `m_SloshDrive` | 1.4 / 0.12 / 0.012 | Surface wave: pitch, ring time, how hard a rise kicks it |
| | `m_WashBass` / `m_BeatFlash` | 0.35 / 0.3 | Wall wash response |
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
