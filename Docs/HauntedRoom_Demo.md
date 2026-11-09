# Haunted room (HauntedRoom MiddleVR)

A ghost-in-the-machine music visualizer. An ordinary Victorian parlour, built at one to one inside
the CAVE so its walls *are* the screens, comes alive with the music: the lamps flicker, the rocking
chair rocks, the piano plays itself, the television turns on, the books slide out, the pictures
bang against the wall, the coffee table hops — and somewhere in the room are the two things doing it.

It is the hands-off pattern from the deck, like the [visualizer](RoomVisualizer_Demo.md): nothing
tracked, nothing held. It reads with or without glasses — the room is mostly on the walls and against
them — and it is built from the same analyser.

## Build it

| Menu item | What it does |
| --- | --- |
| **VR-Music-Demo ▸ Haunted Room ▸ Create Haunted Room Scene** | Copies the physics scene to `HauntedRoom MiddleVR.unity`, strips the tether demo and the gun out of it, and builds the room |
| **VR-Music-Demo ▸ Haunted Room ▸ Rebuild Haunted Room** | Rebuilds it in whatever scene is open, as long as it has a `CAVE_User_Room` |

Same flow as the other demos. It can also be built without opening the editor:

```
Unity -batchmode -projectPath . -executeMethod VRShootingGallery.EditorTools.HauntedRoomBuilder.CreateFromCommandLine -quit
```

**Tracks** come from `Assets/_Project/Audio/Music`, shared with the visualizer and read the same
way — by the builder, again every time play starts, and at player build time. See the folder's
README; the tracks themselves are not in the repository.

**Nothing is bought or downloaded.** The furniture is built from primitives by the builder, and
every texture — the wallpaper, the floorboards, the rug, the portrait, the landscapes, the night
outside the window, the ghost's shadow — is painted in code by `HauntedTextures` and saved under
`Assets/_Project/Art/Haunted/`. Each possessed thing is a pivot with its looks as children, so a
bought model can replace the looks later without touching the behaviour.

## Running it

| Wand button | Key | Does |
| --- | --- | --- |
| 1 | **G** | Summon the ghosts, or send them away — a manual reveal for when the music will not oblige |
| 2 | **L** | Toggle low intensity |
| 3 | **N** | Open or close the **tracks** on the front wall (**Shift+N** skips straight to the next track) |
| 4 | **Space** | Pause / resume |
| Thumbstick up | **F2** | **2D**: the head-tracked view, flat — both eyes get one picture, sharp without glasses, for everyone watching over the tracked viewer's shoulder |
| Thumbstick down | **F1** | **3D**: MiddleVR's stereo, for whoever wears the tracked glasses (the default) |
| 0 (trigger) | **Enter** | In the menu: pick the row the wand points at, or the one selected |

As in the other demos the number-row keys stand in for the wand buttons with the same digit, and
the keyboard stays on in builds. The 2D / 3D views are the CAVE's own (`CaveViewMode`, see
[CAVE_View_Modes.md](CAVE_View_Modes.md)); here they are on the thumbstick because buttons 1–3 are the
room's, so their usual wand buttons are off and their desk keys are F1 / F2 as in the harp's scene.
Each push of the stick is one change, and it has to come back to the middle before the next; while
the tracks menu is open the stick moves through the menu instead. The floor sign says **2D** while
the room is flat. 2D-Fixed is built but not offered: the menu and sign are not drawn in it. A dim status line lies on the floor at the open back of the room,
for whoever is running it from the doorway: how possessed the room is, the track and the one up
next, and whether the ghosts are summoned or low intensity is on.

### The tracks menu

The track button opens the playlist on a dark glass panel in front of the chimney breast — the same
menu as the visualizer's and the glass room's (`TrackMenu`, a `WallMenu` with only the tracks page),
worked the same way. It opens at the track playing, marked in amber with "playing" (or "paused");
the one that plays when it ends is marked in blue with "up next". Picking a track plays it from the
start, and the haunting reads it ahead and begins its arc again, as it does for any new track. The
haunting carries on while the panel is up.

| Wand | Desk | In the menu |
| --- | --- | --- |
| Point at a row | — | Selects it; a ray shows where the wand points, a cursor where it meets the panel |
| Joystick up / down | ↑ / ↓ | Step through the rows, repeating while held, scrolling past the edge |
| Trigger (button 0) | **Enter** | Play the selected track |
| Rest on a ▲ / ▼ strip | Mouse wheel | Scroll the list |
| Trigger on a ▲ / ▼ strip | **Page Up** / **Page Down** | A page up or down |
| Trigger on the scrollbar, held to drag | **Home** / **End** | Jump through the list; to the first or last track |
| Joystick left, or button 3 | **Backspace**, **Esc**, ←, **N** | Close |

**Scrolling.** The panel has eleven rows; a longer playlist scrolls, and the title says which part is
showing ("TRACKS 3–13 of 25"). While there is more above or below, a strip just above or below the
rows says so ("▲ 3 more ▲", "▼ 11 more ▼"). **Rest the wand on a strip** and, after a moment, the
list scrolls that way a row at a time until the ray moves off it; **pull the trigger on a strip** to
go a page at once. A **scrollbar** in the margin right of the rows shows how long the list is and
which part is showing: pull the trigger on it to jump there, or hold the trigger and move the wand
up and down to drag. Stepping past the top or bottom row with the joystick scrolls too. At the desk,
the **mouse wheel** scrolls, **Page Up** / **Page Down** go a page, and **Home** / **End** go to the
first and last track.

**It stands off the wall.** The mantel, the clock and candles on it, and the chimney breast stand
up to 0.28 m out from the front screen, and anything in front of the panel draws over it, so the
panel hangs 0.34 m into the room (`k_MenuInset` in the builder) rather than flat on the wall as in
the other rooms. From the tracked viewpoint it reads as a pane floating before the fireplace; an
untracked onlooker sees it skew a little, as they see the furniture.

### Now playing, up next

A slim strip above the portrait says what is playing, what plays next, and has a bar for how far
through the track the music is. It fades in for 8 s when a track starts, again for the last 20 s of
a track (counting down: "UP NEXT IN 0:12"), and stays while the music is paused; in between it
fades away, so the parlour has the wall to itself. It steps aside while the menu is open. It stands
8 cm off the wall, just clear of the portrait's frame, whose top its lower edge crosses. **Always**
on the `TrackIndicator` keeps it up all the time.

A scene built before the menu existed still runs: the track button skips to the next track, as it
used to, and the status line says NO MENU: REBUILD THE SCENE until **Rebuild Haunted Room** is run
and the scene saved.

## How the haunting works

The `Haunting` component on the root is the director. Everything else reads its cues from it, so
the room moves as one story instead of a hundred things twitching on their own.

### The arc comes from the song, read ahead

When a track starts, the whole clip is read in the background — a few seconds of audio a frame —
into a loudness envelope (`TrackEnvelope`): quarter-second windows, smoothed over six seconds into
*sections*, and mapped onto the track's own range so its quietest tenth reads 0 and its loudest
tenth reads 1. A live analyser only knows what has already played; it cannot tell a quiet intro from
a quiet song, and can only react to a drop after the drop. The envelope knows the song's shape.

**Possession** — 0 to 1, how haunted the room is — follows the section loudness 0.6 s ahead, rising
over 2 s and falling over 4 s. While anything plays it never drops below 6%: something is always
faintly wrong.

It needs the clip imported as **Decompress On Load**, which is the default for a dropped-in MP3. For
anything else (a streamed clip, a microphone) the director falls back to live loudness against the
loudest the track has been so far, and loses the ability to see ahead.

### Things wake in order, and the ghost spreads it

Every possessable thing has a **threshold**; it wakes as possession passes it. The order is the
order the room comes alive in:

| Threshold | What | Answers to | How |
| ---: | --- | --- | --- |
| 0.02 | Floor lamp | mids | Flickers — the first thing to go wrong |
| 0.10 | Rocking chair | low mids | Rocks on its runners — and floats while the ghosts are out (below) |
| 0.15 | Mantel clock hands | mids / high mids | Spin |
| 0.20 | Piano | its own keys | Plays itself |
| 0.20 | Portrait above the mantel | — | Its eyes follow you |
| 0.25 | Television | loudness | Turns itself on: static |
| 0.30 | Teacups, teapot | treble | Rattle — and from 0.35 levitate, higher the more possessed (below) |
| 0.30 | Piano bench | bass | Scoots back |
| 0.35 | Curtains | mids | Stir (they always answer to the ghost) |
| 0.35 | Table lamp | treble | Flickers |
| 0.40–0.48 | Books | one band each | Slide out — and from 0.72 about half float out of the shelf (below) |
| 0.45 | Wall sconces | high mids | Flicker |
| 0.50 | Paintings | bass | Bang against the wall |
| 0.50 | Chandelier light | loudness | Flickers |
| 0.55 | Sideboard doors | mids | Fly open, slam shut |
| 0.60 | Chandelier | bass | Swings |
| 0.60 | Bookcase | bass | Shudders — until its books float out at 0.72, then goes still |
| 0.70 | Coffee table | bass | Hops on the kick |

A ghost's nearness lowers a threshold by up to 0.2, so things near either ghost wake first. And each
ghost goes, by preference, to whatever is **about to wake** — the lowest threshold not yet reached —
so the possession spreads through the room following them. When nothing is next, it visits something
already awake, weighted toward the most possessed. The two never go to the same thing, nor to within
a metre of where the other is going, so the room wakes from two places at once.

### Things leave the ground

The higher the possession, the less the room obeys gravity. Levitation goes on top of whatever an
object already does — the motion carries on in the air — rising over 1.5 s and settling over 2.5 s,
with a slow bob and tumble while it is up:

| Cue | What | Floats |
| --- | --- | --- |
| Possession 0.35 → 0.95 | Teapot, teacups | Up off the table, higher the more possessed: the pot to 0.22 m, the cups to about 0.3 m, each to its own height and still trembling |
| Possession 0.72 → 0.87 | About half the books | Out of the shelf one by one, each its own distance into the room (0.15–0.55 m), lifting a little and turning slowly, drifting in toward the middle of the bookcase so they clear the floor lamp and the screen's back edge. The rest stay and keep sliding |
| The reveal | Rocking chair | 0.4 m off the floor while the ghosts are out (or summoned), still rocking |

The books need a loud section to get going (possession passes 0.72 only near the top of a track's
range), so they are the build's last word before the ghosts appear; the chair is the reveal's own.
As the first books lift, the bookcase stops shuddering — fading to still between 0.72 and 0.80 —
as if whatever was shaking it has gone into the books; it starts again when possession falls back.

### Every object has a part of the music

Legibility is the whole trick: every sound has a visible cause, as in Animusic. Low things listen to
low bands, heavy things hop on the kick, small bright things rattle on the treble. Two are worth
singling out:

- **The piano is a spectrum analyser in the shape of an instrument.** A keyboard is already a
  logarithmic frequency axis, so each of its 88 keys looks up its own pitch in the analyser's raw
  spectrum and goes down when that note is loud — measured against a running peak across the whole
  keyboard, and only if it is louder than both neighbours, so one bass note does not smear across
  the keys that share its bins. Pressed keys glow faintly with the invisible players' touch: the
  two ghosts play four hands, the blue one below middle C and the rose one above, blending over a
  few notes where the hands meet. At 11.7 Hz a bin, keys are resolved individually from about
  middle C up.
- **The bookcase is an equaliser nobody built.** Each book listens to its own band — bass on the
  bottom shelf, treble at the top — and slides out with it.

### The ghosts are mostly unseen

"Ghost in the machine" is an invisible thing operating a mechanism, so for most of a track you know
the two ghosts only by what they do:

- **Their wake.** Curtains billow as one passes; candle flames lean away from the nearer one.
- **Their shadows**, one each, sliding across whichever walls they are near with nothing there to
  cast them — sharper and darker close to a wall, larger and fainter further off. Flat on the
  screens, so the whole audience sees them, glasses or not.
- **Glimpses in the machine.** On a strong beat, once the room is possessed, a face flashes in the
  television's static. And the picture **glitches in the ghosts' colours**: on a beat — more often
  the more possessed the room — it tears into a blue copy and a rose copy pulled apart sideways,
  and a few bands of it slip and go wholly blue or rose, settling in a quarter of a second. While
  the ghosts are out the split holds, lightly, so the face in the static is two faces, blue and
  rose. Glitches come only on the beat (capped at 3 a second) and are never brighter than the plain
  picture — the picture breaking up, not a flash; low intensity halves them.
- **The portrait's eyes** follow the tracked viewer's head — the CAVE really knows where it is — and
  turn to watch a ghost when one comes near.
- **A shimmer**, faint, like heat, when the room is deeply possessed.

### The reveal is earned

The ghosts show themselves on a loud section that follows a quieter one — the drop after a build —
for at most 16 s, and not again for 12 s after. Because the envelope is read ahead, **the lamps
start going down 1.5 s before the drop**, dimming to 40% as the ghosts fade in at the drop itself.
While they are out, the fire and the candles burn in the ghosts' colours, the portrait follows the
dance, the rocking chair floats, and the shadows are gone because the things casting them are there
to see. The room is split at the line halfway between the two ghosts, across the floor: everything
on the blue ghost's side burns blue, everything on the rose ghost's side rose, blending over 0.3 m
where they meet. So with the pair side by side the fire is half blue, half rose, and each candle
takes the ghost beside it; as they waltz the colours swap sides with them — and where one passes
behind the other, the whole fire takes the colour of the one nearer it. The embers keep the colour
of the side they flew from, and the fire's light follows the split at the middle of the grate.

**The spectres** are made of the music: each a sheet of 2,000 points over nothing, treble in the head
and bass in the billowing hem, the live waveform running round the hem's edge, two eye holes where
there are no points at all, and a trail of ectoplasm behind it. Each travels on a critically damped
spring, dances a figure-eight around where it is that widens with the music, bobs and hops on the
beat, leans into its travel, and always keeps 0.8 m from the tracked viewer's head — any closer and
stereo breaks down. The first is pale violet to white; its partner is a tenth smaller and rose to
white, so the two read as two from across the room.

**On show they dance as partners.** Both take the stage and waltz slowly round each other — a turn
every 9 s when the music is quiet, every 5 s at full energy — on a circle wider than it is deep
(1 m across, 0.44 m front to back), so they mostly swap sides in front of the viewer rather than
pass in front of each other. The room only has about 0.7 m of depth between the viewer's 0.8 m
keep-away and the mantel, so where they do cross, the waltz's **rise and fall** keeps them two: the
partner going round the back rises 0.15 m and the one coming round the front dips, so the one behind
shows over the one in front. The middle of the dance is held at least 1.05 m across the floor from
the viewer's head (the stage moves back if the viewer walks toward it), so the front partner is not
pushed into the other by the keep-away. The solo figure-eight mostly folds away while they are
partnered; the partner dances the mirror image of what is left, and sways the other way. Each turns
partly toward the other (25°, not all the way: the faces are what read) and banks into the turn.
They hop on the same beats, and when a beat sets them twirling they twirl together, opposite ways.
When the reveal ends the waltz closes up as they part, and each goes back to haunting its own corner
of the room.

A scene built when there was one ghost still runs with one, and says so in the console; rebuild it
(**Rebuild Haunted Room**, then save) for the pair.

### The last gasp

When the music stops after the room was properly possessed, everything settles — and then, two or
three seconds into the silence, one thing that was awake moves once more. The chair rocks once. A
lamp dips. The piano strikes a single chord. The television flicks on for a moment with the face in
it.

## Safety

The room is dark and enclosed, and flicker is the core of the idea, which is exactly the
photosensitivity risk the deck warns about. So:

- **Every flicker is a dip, never a flash.** A lamp darkens briefly and recovers; nothing goes above
  its normal level.
- **Every flicker is on the analyser's beat**, which is hard-capped at 3 a second (2 in low
  intensity). Nothing flickers on a clock of its own.
- **The reveal's dimming is slow** — 1.5 s down, 2.5 s up — never a cut.
- **Low intensity** (wand 2 / **L**) cuts lamp dips to a third and lowers the beat cap.

## In the CAVE

- **The room is the stage.** Its walls land on the screens; the furniture stands against them or low
  in the middle; the floor between is left for the people in it. The ghosts keep to the front of the
  room, above head height, clear of the open back where there is no screen to draw them on.
- **Lamps are real point lights.** They need the pipeline's additional lights, which the Standalone
  build's quality level (Ultra, `Quality URP Config`) has, per pixel, four per object. The Quest-era
  `Performance URP Config` has them off — that is why the editor's quality level is set to Ultra
  too, so play mode at a desk shows what the CAVE shows.
- **No screen-space effects.** `MVR_URP` strips bloom and friends because they seam across walls;
  the fire, candles and ghost glow through their own additive material instead.
- **Single node.** As with the visualizer, one director drives all four viewports. On the 4-node
  config the director's state (possession, reveal, the ghosts' positions) would need sharing across
  the cluster.

## How it is put together

| Script | Role |
| --- | --- |
| `Haunting` | The director: possession arc, reveal, glimpses, each ghost's targets, the pair's waltz and twirls, the last gasp, input, status line |
| `Possessable` | Base for everything the haunting takes over: threshold, what it listens to, whether the ghost visits |
| `PossessedObject` | Rigid motions: Hop, Roll, Swing, Tilt, Flap, Slide, Rattle, Spin — springs, pendulums and gravity driven by the music — and levitation on top, cued by possession or the reveal |
| `PossessedLight` | Lamps: swell, dip on the beat, sag when deeply possessed, dim for the reveal |
| `PossessedPiano` | 88 keys on the raw spectrum, played four hands in the ghosts' colours |
| `PortraitEyes` | Eyes that follow the tracked head, or the ghost |
| `HauntedTV` | CRT static drawn on the CPU, with the ghost's face in it, glitching blue and rose on the beat |
| `HauntedCurtain` | Cloth billowed by the ghost's wake; grid mesh deformed on the CPU |
| `HauntedFireplace` | Flame tongues as the spectrum, embers on the beat, blue and rose after the ghosts while they are out |
| `CandleFlame` | Leans away from the nearer ghost, and burns its colour while they are out |
| `GhostShadows` | One ghost's shadow on the walls; one per ghost |
| `SpectreGhost` | A ghost: travel, dance (the partner's mirrored), and a body of points made of the music |
| `GhostSilhouette` | The ghost's 2D outline, shared by the shadow texture and the TV face |
| `MusicDeck`, `TrackEnvelope`, `ManualParticles` | Shared with the visualizer: playback, whole-track loudness, hand-written particles |
| `TrackMenu`, `TrackIndicator` | Shared with the other rooms: the tracks on the front wall (a `WallMenu`), and the now-playing strip |
| `HauntedRoomBuilder`, `HauntedTextures` | Editor: builds the room, paints the textures; `WallMenuBuilder` builds the menu and the strip |

## Tuning

Everything is in the Inspector. The numbers worth knowing:

| Component | Field | Default | Effect |
| --- | --- | --- | --- |
| `Haunting` | `m_MinPossession` | 0.06 | Possession while anything plays |
| | `m_PossessionRise` / `m_PossessionFall` | 2 / 4 s | How fast the room wakes and settles |
| | `m_GhostSpread` / `m_WakeRadius` | 0.2 / 1.1 m | How strongly, and how far, the ghost stirs things |
| | `m_RevealAt` | 0.8 | Section loudness that brings the ghost out |
| | `m_RevealLead` | 1.5 s | How early the lamps start down before a drop |
| | `m_EarnedBelow` / `m_EarnWindow` | 0.55 / 25 s | What makes a reveal earned |
| | `m_RevealHold` / `m_RevealCooldown` | 16 / 12 s | How long it stays, how soon it can return |
| | `m_RevealDim` | 0.6 | How far the lamps go down for it |
| | `m_GhostMin` / `m_GhostMax` / `m_Stage` | front of room | Where the ghosts may go, and where they dance |
| | `m_GhostSpacing` | 1 m | How far apart the ghosts keep their targets while unseen |
| | `m_WaltzRadius` / `m_WaltzTurn` | 0.5 × 0.22 m / 9–5 s | The waltz's circle (half-width, half-depth), and a turn's length, quiet to loud |
| | `m_WaltzRise` / `m_DanceClearance` | 0.15 m / 1.05 m | Rise and fall round the back and front; least floor distance from the viewer to the middle of the dance |
| | `m_TwirlChance` | 0.25 | How often a beat sets the pair twirling |
| each `Possessable` | `m_Threshold` | see table | When it wakes |
| | `m_Listen` / `m_Band` | | What it answers to |
| `PossessedObject` | `m_Amount`, `m_Frequency`, `m_Damping`, `m_BeatKick` | per object | How far, how fast, how springy, how hard the beat hits |
| | `m_FloatOn` / `m_FloatFrom` / `m_FloatFull` / `m_FloatTo` | per object | What lifts it (possession or the reveal), over what range, and where to |
| | `m_StillFrom` | 1 (never); bookcase 0.72 | Possession past which its motion fades to still |
| `PossessedPiano` | `m_SplitNote` | 60 (middle C) | Where the two ghosts' hands meet on the keyboard |
| `HauntedTV` | `m_GlitchChance` / `m_GlitchDecay` / `m_RevealSplit` | 0.5 / 0.25 s / 0.35 | How often a beat glitches the picture, how fast it settles, how split it stays while the ghosts are out |
| `PossessedLight` | `m_FlickerDepth` / `m_FlickerChance` | 0.65 / 0.6 | Dip depth, and how often a beat dips it |
| `PossessedPiano` | `m_PressFrom` / `m_PressFull` / `m_RangeDb` | 0.6 / 0.88 / 24 | How loud a note must be to press its key |
| `SpectreGhost` | `m_KeepAway` | 0.8 m | Closest it comes to the tracked head |
| | `m_Dance` | 0.22 m | How far its figure-eight reaches |
| | `m_PartnerTurn` / `m_Bank` | 25° / 9° | How far it turns toward its partner, and banks into the waltz |
| | `m_Shimmer` | 0.07 | How visible it is before a reveal |
| `GhostShadows` | `m_Darkness` / `m_From` | 0.55 / 0.18 | Shadow strength, and the possession it first appears at |
