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
│  ├─ World_SkyPool        SimWorld + open sea, Pool (SimPool) and its Water, Springboard, Splash, cumulus, gulls
│  ├─ World_Beach          SimWorld + sand, sea (SimOcean), deck, palms, pier and its lamps, headlands, islands, fireworks
│  ├─ World_Aurora         SimWorld + lake, mountains, island, curtains (and reflections), snow
│  ├─ World_Orbit          SimWorld (3 variants) + meadow, grass, leaves, boulders, tubes, asteroids, planets, star, satellites, meteors, dust
│  └─ World_Lustrafjord …  the nine Norway views: SimWorld + Land (SimSurface, Fjordland) and its Tarns, Water, villages,
│                          fish farms, ship, waterfalls, spruces, trees, boulders, grass, birds, lightning — whichever the view has
├─ Floorboards, Rug
├─ Frame                   iron posts, rails, sills, corner braces
├─ Desk                    glass top, legs, glass of water, Tablet (shows the front view), Teddy_Bear
├─ Cabinet                 drawers, books, ink bottles
├─ Bed                     brass head and foot, mattress, blanket, pillow
├─ Pendants                three PendantLamps, each with a point light
├─ Glass_Walls             Glass_Left / Glass_Front / Glass_Right: WindowView each
├─ Status_Sign             dim line on the floor at the open back
└─ World_Menu              WorldMenu, on the front wall just inside the glass: Content (panel, highlight, title, rows,
                           cursor) and Wand_Ray; hidden until the world button opens it
```

A rebuild clears the children and reuses the `Simulation` on the root, so its Inspector settings
survive a rebuild.

## Running it

| Wand button | Key | Does |
| --- | --- | --- |
| 1 | **W** | Open or close the world menu (**Shift+W** goes straight back a world) |
| 2 | **L** | Toggle low intensity |
| 3 | **N** | Next track |
| 4 | **Space** | Pause / resume |
| 6 | **V** | Next variant of the world outside, if it has variants (**Shift+V** goes back) |
| — | **F** | Show or hide the furniture (no wand button by default; set `m_FurnitureButton`) |

As in the other demos, the number-row keys stand in for the wand buttons with the same digit, and
the keyboard stays on in builds.

### The world menu

The world button opens a menu on the front wall, a dark glass panel over the window at a little
below eye height, above the desk. It lists the worlds by group first: **Shelter** (Red Rock, Above
the Clouds, Sky Pool, Sunset Beach, Aurora), **Space** (Orbit) and **Norway** (nine views). Picking a
group lists its worlds, with a way back at the bottom. Picking a world closes the menu and rebuilds
the outside into it with the usual tile change. The world outside now, and its group, are marked in
amber.

| With the wand | At the desk | Does |
| --- | --- | --- |
| Point at a row | — | Selects it; a thin ray shows where the wand points, and a dot where it meets the panel |
| Joystick up / down | **↑ / ↓** | Moves the selection (holding repeats) |
| Trigger (button 0) | **Enter** | Picks the row |
| Joystick right | **→** | Opens the selected group |
| Joystick left | **← / Backspace / Esc** | Back to the groups, or closes the menu from there |
| Button 1 | **W** | Closes the menu |

The groups come from each world's **Group** on its `SimWorld`, in the order the worlds are listed,
so a new world shows up in the menu by setting that field. Without a `WorldMenu` on the
`Simulation`, the world button goes back to stepping to the next world. The other buttons and keys
keep working while the menu is open. The pointer looks for MiddleVR's `HandNode`; at a desk with no
wand, the keys work alone.

**A change of track leaves the world alone.** The world outside stays until someone picks another
from the menu. Three hands-off options are all off by default: `m_WorldPerTrack` rebuilds the
outside into the next world whenever a new track starts, `m_AutoCycleSeconds` changes worlds on a
timer, and `m_VariantEverySeconds` changes a world's variant on a timer.

## Changing worlds

The change between worlds is the simulation rebuilding itself, in the spirit of the film's
dissolving cliff. The view breaks into 11 cm tiles. Each tile shrinks away into a pale blank page,
showing a bright rim as it goes. The tiles go in a wave around the room, from the back of the left
wall, across the front, to the back of the right wall. While only the blank page shows, the old
world goes to sleep and the next one wakes. The new view then builds back out of tiles behind the
wave. The whole change takes 3.2 s (`m_TransitionSeconds`). The room's light eases to the new
world's light over 1.5 s.

## The worlds

The worlds come in three groups, which the world menu lists them by: **Shelter**, the worlds after
the film and its mood (Red Rock, Above the Clouds, Sky Pool, Sunset Beach, Aurora); **Space**
(Orbit); and **Norway**, nine views after photographs.

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

### Sky Pool

After a surreal collage: a long swimming pool standing in the open sea under one enormous cumulus,
and the water in the pool is sky. Look into it and you look down into a blue sky with white clouds
drifting in it, as if the pool were a hole through to the sky on the other side. The collage's
swimmers are left out.

The room stands on the white deck at the pool's near end. A springboard reaches out over the water
from just past the front glass. The pool is 10 m wide and 50 m long, with a grey coping along its
edge, a waterline frieze of blue tiles with a white Greek key, and steel ladders near both ends. The
far end is an infinity edge: the pool runs out to a thin dark line, and the sea carries on to the
horizon. Gulls wheel overhead, and fair-weather cumulus sit round the rest of the horizon.

- **The music pours into the pool and drifts away down it as cloud.** The spectrogram is laid along
  the pool. Each moment enters at the near end, under the board, and floats toward the far end at
  2.5 m/s, so a loud passage thickens the clouds where it is and then drifts off with them. Pitch runs
  across the pool: the bass down the middle and the treble along the walls. The music fades out
  over the pool's last third, before the infinity edge. A fainter, slower deck of cloud lies deeper down, which
  gives the sky beneath some depth.
- **Each beat is a dive.** The springboard dips and rebounds on the beat, dying away, as if somebody
  has just gone off it. Rings of ripples spread across the water from a little beyond its tip. They
  bend the sky beneath as they pass and catch the light. These are the world's beat rings, at
  4.5 m/s, so strong beats make bigger ripples.
- **The cumulus billows with the music.** It is built from seven turrets, and each one swells with
  its own band: the bass in the great central tower, rising through the shoulders either side, up to
  the treble in the small turrets on top. Each puff swells by a share of its own radius (up to 14% at
  full level), so the cloud boils rather than inflates.
- **The bass swells the sea.** The sea is the beach's ocean shader with its shore put far behind the
  room. Only the swells are left, rolling in from the horizon, bigger with the bass.
- **The mids set the gulls flapping.** They glide while the middle of the music is quiet. As the mids
  come up they beat their wings and climb a little.

The water is one quad drawn by its own shader (`VRMusic/SimSkyPool`). Everything in it (the sky
beneath, the clouds, the ripples) is worked out per pixel. `SimPool` builds the platform from the
same numbers it hands to that shader each frame (`_Pool`), so the water's edges, its walls' shade and
the place the music pours in always match the platform.

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

## Norway

Nine views after photographs of Norway, each a world of its own in the **Norway** group. They share
one way of being built, and the music plays on all of them the same few ways.

**The ground** is a `SimSurface` with the `Fjordland` profile, built in layers:

1. A mass of ridged mountains (`m_Base`, `m_Rim`, `m_RimWidth`), cut off level at `m_Plateau` if
   there is one, leaving sheer walls under flat tops.
2. **Valleys and fjords** carved along paths (`m_Valleys`). Each has a flat floor and walls that are
   steep at the foot and round off into the mountains at the top, the U a glacier leaves. Each point
   on a path carries the floor's height, so one valley can be a fjord at its mouth, a lake further up
   and fields at its head.
3. **A coast and a bay** (`m_Coast`, `m_Bay`) dropping the land into the sea.
4. **The view limit**: within `m_ViewRadius`, nothing rises above a line falling at `m_ViewSlope` from
   the edge of the room's summit, so the summit is a mountain top whatever the mountains round it do.
   It never digs below the mountains' foot, so it leaves no moat.
5. **Horn peaks** (`m_Peaks`), raised after the limit, so a sea stack can stand out of the water and
   a spire can stand beside the room. Their foot reaches further in some directions than others, so
   ridges run off them.
6. **The summit** (`m_Summit`): a broad dome round the room.
7. **Tarns** (`m_Tarns`): each fills to just below the lowest point of its own rim, worked out when
   the ground generates, so a tarn's water can never stand above the ground beside it. The water is
   drawn on the `Tarns` child.

The land shader paints it by height above the water and slope: forest up to a ragged tree line,
heath above it, a patchwork of fields on the level ground low down (one strip in six cut for hay),
bare rock on the cliffs and showing through the heath, shingle or sand along the water, and snow
above the snow line.

**The room floats over most of them**, as the drone the photographs were taken from did
(`Hover`, 30 to 120 m). From eye height on a broad summit, the summit's own shoulder hides everything
below it: you see something only if the ground in between falls away faster than your line of sight
to it. From above, the fjord opens up under the glass. The two views taken by someone standing (the
bird cliffs and the storm gorge) keep the room on the ground. In Bird Cliffs the room stands right at
the cliff edge, because from even a few metres back you cannot see down a cliff.

**The music:**

- **The clouds open with the loudness.** A deck of heaped cloud covers part of the sky. As the music
  swells it thins and the sun comes through, and the clouds' shadows slide across the land as the
  deck drifts. Quiet passages close it again. This is the main way the music reaches these worlds,
  and it is slow on purpose, like weather.
- **Beat rings ripple across the water** from a point on the fjord or bay, lighting it and tilting
  its surface as they pass. The tarns carry them too.
- **Waterfalls run with the bass**: faster and whiter, and a little wider as it swells.
- **The grass sways with the mids**, and gulls (Arctic Bay, Bird Cliffs) flap and climb with them.
- **Fish farms and the cruise ship** carry lamps along the spectrum, one band each: an equaliser on
  the water that shows when the music is loud.
- **Snow glints with the treble** on Glacier Peak. **Surf** runs higher up the shore on each beat at
  Arctic Bay and Bird Cliffs. **Mist** along Storm Gorge's cliff tops thickens with the bass, and
  **lightning** lights its clouds from inside on the strongest beats.

| View | After | What it shows |
| --- | --- | --- |
| **Lustrafjord** | Molden, over the Lustrafjord | A bare summit with tarns at the room's feet, 120 m below the floating room. The turquoise fjord is far below, one arm curling away behind the mountain on the left and the other running off past farms on a green shelf to the right. Haze, and snow on the farthest ridges |
| **Hilltop Fjord** | A grassy knoll above a fjord | Grass falling away under the room, two birches, the fjord running out from the left past a pyramid of a mountain, an arm reaching into the blue distance, heaped cloud |
| **Arctic Bay** | Andøya | High on a green mountainside: a valley leads down to a bay with a white beach, cliffs on the headland to the left, the open ocean to the right with the sun on it, gulls below |
| **Glacier Peak** | The mountains of the north | The snowfield right below the room between a rock wall and a grey fin, a long blue lake in a green valley far beneath, ranges of grey peaks streaked with snow beyond |
| **Olden** | The head of the fjord at Olden | A cruise ship at anchor, the village where the fjord ends, a valley of fields and farms running on between sheer green walls to a lake, waterfalls down the cliffs, snow on the peaks |
| **Bird Cliffs** | Bleik | The grassy edge of a sea cliff: the sea straight down with surf along the rocks, a ridge of jagged peaks running out into it, a stack offshore, gulls wheeling below the edge |
| **Storm Gorge** | A cliff-walled valley under a storm | A meadow and a row of trees, black cliffs split by a narrow gorge, snow in the gullies, cloud pouring over the cliff tops, warm light through the gap, lightning in the cloud. The room's lamps burn full |
| **Dronningruta** | The Queen's Route, Vesterålen | Down into a green bowl with two dark tarns among slabs of rock, black jagged peaks behind on the left, the sea far off on the right, a heavy sky with the light low |
| **Senja** | A fjord on Senja | Straight down a fjord from grey slabs: steep walls either side, a fish farm's rings on the still water, a village on the right shore, a crown of jagged peaks where the fjord opens to the sea |

Each view is one `NorwayView` in `GlassRoomBuilder.Norway.cs`: the ground's numbers, its paint, the
sky, the room's light, the ring origin and what stands in it. A new view is another method there and
a line in `BuildNorway`.

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
- **Lightning** in Storm Gorge strikes only on beats above 0.65 strength, never two within 1.6 s,
  and stays a glow inside the cloud with a faint lift of the land rather than a white-out. Low
  intensity halves it again on top of the usual dimming.
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
| `SimSurface` | Polar-grid ground generated around the room: desert floor, mountain ring, beach, Norway's fjordland (valleys, fjords, peaks, tarns and their water, coast, the view limit), flat water or cloud (water can curve up toward the horizon far out, so from a summit the sea runs all the way out) |
| `SimButte` | Tiered rock formations: buttes, mesas, spires, the room's pedestal |
| `SimPuffs` | Trees, spruces, boulders and cumulus built from balls, optionally lit by a band |
| `SkyDome`, `AuroraCurtain` | The sky's sphere; a curtain's strip |
| `SimOcean` | Hands the sea its beach's waterline |
| `SimPalms` | Palm trees, curved trunks and folded fronds, with sway weights for the shader |
| `SimPier` | The pier's mesh, and its lamps and their reflections as an equaliser |
| `SimFireworks` | Bursts on strong beats, coloured by the leading part of the music, mirrored in the sea |
| `SimPool` | The sky pool's platform (deck, coping, frieze, ladders, infinity edge) and its water quad; hands the water's shader the pool's shape |
| `SimSpringboard` | The springboard and its stand; the board dips and rebounds on the beat |
| `SimGulls` | Gulls circling over the sea, flapping and climbing with the mids |
| `SimProps` | Norway's houses (on level ground above the water), fish farms (rings with a band lamp each) and a cruise ship (windows along the spectrum) |
| `SimWaterfall` | A waterfall traced down the steepest way from its source until the ground levels out or meets the water, as a ribbon on the rock |
| `SimLightning` | Lightning in the clouds on the strongest beats (`_GR_Flash`) |
| `WorldMenu` | The world menu on the front wall: groups, then worlds; wand pointer, joystick, trigger and keys |
| `WorldVariant` (in `SimWorld`) | One look of a world: sky, nebula, palette, tints, room light |
| `SimCelestial` | A planet or star: a quad that `SimPlanet` turns to face the room |
| `SimAsteroids` | Faceted rocks round the room, lifted and spun by their bands |
| `SimFlora` | Grass tufts with treble-lit tips, broad leaves, spiky blades; palette-painted, swaying |
| `SimTubes` | Ribbed arches out of the ground |
| `SimSatellite` | A tumbling probe whose beacon blinks on the beat |
| `SimMeteors` | Shooting stars on strong beats, as velocity-stretched glows |
| `LightPillar`, `VoxelMesa`, `SimMotes` | The pillar, the cubes, the motes |
| `SimMeshes` | Noise and mesh helpers |
| `GlassRoomBuilder`, `GlassRoomTextures` | Editor: builds everything, paints the floorboards. The Norway views and the world menu are in `GlassRoomBuilder.Norway.cs` |

| Shader | Draws |
| --- | --- |
| `VRMusic/GlassWindow` | The glass: the view, tint, haze, streaks, lamp reflections, and the tile dissolve |
| `VRMusic/SimSky` | The dome: gradient, sun, cirrus, stars, milky way, moon |
| `VRMusic/SimLand` | Rock, ground, trees, snow, cumulus, voxels: cel-lit with violet shadows, strata, Norway's biome paint, rim light, band glow, beat rings, surf, snow glints, cloud shadows, haze |
| `VRMusic/SimCloudSea` | The cloud sea lifted by the spectrogram |
| `VRMusic/SimWater` | Sky-reflecting water that marks the stencil buffer; can carry the beat rings as light and ripples |
| `VRMusic/SimFall` | A waterfall: white streaks running down a ribbon, faster, whiter and wider with its band |
| `VRMusic/SimOcean` | The beach's sea: swells, run-up, the surge, glitter, foam; marks the stencil buffer. The sky pool's open sea is the same shader with its shore out of sight |
| `VRMusic/SimSkyPool` | The sky pool's water: the sky beneath, its clouds thickened by the spectrogram and drifting down the pool, wind and beat ripples |
| `VRMusic/SimGlint` | Additive glows (lamps, sparks) that can also be drawn as stencil-clipped reflections |
| `VRMusic/SimPlanet` | Planets (banded, lit, atmosphere, a ring that is the spectrum) and stars (corona, flares) |
| `VRMusic/SimAurora` | Curtains and their stencil-clipped reflections |
| `VRMusic/SimPillar` | The camera-facing light pillar |

`SimCommon.hlsl` holds the shared globals (`_GR_*`), the noise functions and the sky function. The
water reflects the same sky function that the dome draws. The sky also has a deck of heaped cloud
(`_GR_Clouds`, `_GR_CloudMotion`) that the land reads back for its cloud shadows, along the sun, so a
shadow lies under its cloud. The fog adds mist in a band of height (`_GR_Mist`), as much as the line
of sight runs through the band. Lightning (`_GR_Flash`) lights the cloud deck and lifts the land's
light. A world without clouds, mist or lightning leaves them all at zero.

**Land shader vertex colour:** below full alpha, a vertex's colour takes over and glows. A voxel
cube that breaks free has particle alpha 0. It is this way round because a mesh with no colour
channel reads as opaque white, and it must still be painted. For the same reason, any mesh the
land shader draws must fill its second UV channel (the band glow). A missing channel is not read as
zero, and the first canyon floor had one sector glowing white. The third UV channel holds sway
(weight, phase). It is read only by a material with `_Sway` above 0 (the palms), so other meshes can
leave it empty. A cumulus puff puts its radius there instead, for a material with `_Billow` above 0
(the sky pool's cumulus), which swells each puff with its band by that share of its radius. `_WetDarken` darkens a ragged band of sand just above `_WetLevel`, the waterline.

## Tuning

| Component | Field | Default | Effect |
| --- | --- | --- | --- |
| `Simulation` | `m_TransitionSeconds` | 3.2 | Dissolve out + build in |
| | `m_WorldPerTrack` / `m_AutoCycleSeconds` | off / 0 | Hands-off world changes: with each new track, or on a timer |
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
| `SimPool` | `m_Width` / `m_Length` / `m_Near` | 10 / 50 / 2.4 m | The water's size, and how far in front of the room it starts |
| `M_Glass_Sky_Pool` | `_Cover` / `_CoverByMusic` | 0.4 / 0.5 | Cloud in the pool at rest, and what the music adds |
| | `_FlowSpeed` / `_CloudScale` | 2.5 m/s / 7 m | How fast the clouds and the music drift down the pool; cloud size |
| | `_RingRipple` / `_RingWavelength` / `_Sheen` | 4 cm / 0.8 m / 0.35 | The beat's ripples, and the light on them |
| `SimWorld` (Sky Pool) | `m_RingSpeed` / `m_RingDelay` | 4.5 m/s / 0.12 s | How fast the ripples spread, and how long after the beat they start |
| `SimSpringboard` | `m_Dip` / `m_Frequency` / `m_Damping` | 2.2° / 2.4 Hz / 3.2 | The board's spring on the beat |
| `M_Glass_Pool_Cumulus` | `_Billow` | 0.14 | How far a puff swells at full level, as a share of its radius |
| `SimGulls` | `m_Count` / `m_Span` / `m_Climb` | 6 / 1.5 m / 4 m | Gulls, their wingspan, how far the mids lift them |
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
| `SimWorld` (Norway) | `m_CloudCover` / `m_CloudDarkness` / `m_CloudSize` | per view | How much heaped cloud, how dark its thick (a storm), its size and height |
| | `m_CloudsOpen` / `m_CloudShadows` / `m_CloudWind` | 0.35–0.5 / 0.55–0.8 / per view | How far the loudness opens the clouds; their shadows; their drift |
| | `m_MistBand` / `m_MistDensity` / `m_MistByBass` | Storm Gorge: 520–820 m / 6 per km / 0.5 | The mist along the cliff tops |
| | `m_Group` | Shelter, Space, Norway | Which group the world menu lists the world under |
| `SimSurface` (Fjordland) | `m_Valleys` / `m_Peaks` / `m_Tarns` / `m_Coast` / `m_Bay` | per view | The landscape (see Norway above) |
| | `m_Pad` / `m_Summit` / `m_ViewRadius` / `m_ViewSlope` | per view | The room's height over the ground and the summit it floats over; how the ground falls away |
| `M_Glass_Norway_*_Land` | `_TreeLine` / `_FieldLine` / `_SnowLine` | per view | Heights above the water of the forest's edge, the fields, the snow |
| | `_RockPatches` / `_Surf` / `_SnowGlint` | per view | Rock through the heath; the surf's beat; the snow's glints |
| `M_Glass_Norway_*_Water` | `_DeepColor` / `_Reflectivity` / `_Rings` / `_RingRipple` | per view / 0.6 / 1 | Glacial turquoise or sea blue; the beat rings on it |
| `M_Glass_Norway_Waterfall` | `_Speed` / `_SpeedByBand` / `_Band` / `_Swell` | 14 / 18 m/s / 0.08 / 0.35 | How fast the falls run at rest and with the bass, and how far they swell |
| `SimLightning` | `m_Threshold` / `m_Gap` / `m_Brightness` | 0.65 / 1.6 s / 1 | Which beats strike, how often at most, how bright |
| `WorldMenu` | `m_WandNode` / `m_SelectButton` | HandNode / 0 | The node the pointer reads; the button that picks |

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
- **Sky Pool** compiles, and its shader (and the land shader's new billow) compile with `fxc`
  against URP's headers. It was written while the editor was open, so it has not been built into
  the scene or seen in renders yet: the composition (the cumulus' size and turrets, the pool's
  width against the front wall, the gulls' circles) is set from the collage and the walls' angles,
  not from a look.
- **Norway** and the world menu compile (runtime and editor, with MSBuild), and every glass room
  shader compiles with `fxc` against URP's headers. The nine views' layouts were checked against the
  photographs with a rough height-field render of the three walls from the fixed eye, outside Unity.
  That is why the room hovers and why Bird Cliffs stands at the edge. They have not been built into
  the scene or seen in Unity yet: **Rebuild Glass Room** and save. The colours, clouds, mist and the
  props' sizes are set by eye from the photographs, not from a look in the engine. The menu's
  pointer has not been tried with the wand in the CAVE.
- **More worlds** are a `SimWorld` and some children away. The film's flower field and city would
  both suit: a skyline whose lit windows are an equaliser is an obvious next one.
