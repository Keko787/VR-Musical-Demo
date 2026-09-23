# Haunted room (HauntedRoom MiddleVR)

A ghost-in-the-machine music visualizer. An ordinary Victorian parlour, built at one to one inside
the CAVE so its walls *are* the screens, comes alive with the music: the lamps flicker, the rocking
chair rocks, the piano plays itself, the television turns on, the books slide out, the pictures
bang against the wall, the coffee table hops — and somewhere in the room is the thing doing it.

It is the hands-off pattern from the deck, like the [visualizer](RoomVisualizer_Demo.md): nothing
tracked, nothing held. It reads with or without glasses — the room is mostly on the walls and against
them — and it is built from the same analyser.

## Build it

| Menu item | What it does |
| --- | --- |
| **VR Shooting Gallery ▸ Haunted Room ▸ Create Haunted Room Scene** | Copies the physics scene to `HauntedRoom MiddleVR.unity`, strips the tether demo and the gun out of it, and builds the room |
| **VR Shooting Gallery ▸ Haunted Room ▸ Rebuild Haunted Room** | Rebuilds it in whatever scene is open, as long as it has a `CAVE_User_Room` |

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
| 1 | **G** | Summon the ghost, or send it away — a manual reveal for when the music will not oblige |
| 2 | **L** | Toggle low intensity |
| 3 | **N** | Next track |
| 4 | **Space** | Pause / resume |

As in the other demos the number-row keys stand in for the wand buttons with the same digit, and
the keyboard stays on in builds. A dim status line lies on the floor at the open back of the room,
for whoever is running it from the doorway: how possessed the room is, the track, and whether the
ghost is summoned or low intensity is on.

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
| 0.10 | Rocking chair | low mids | Rocks on its runners |
| 0.15 | Mantel clock hands | mids / high mids | Spin |
| 0.20 | Piano | its own keys | Plays itself |
| 0.20 | Portrait above the mantel | — | Its eyes follow you |
| 0.25 | Television | loudness | Turns itself on: static |
| 0.30 | Teacups, teapot | treble | Rattle |
| 0.30 | Piano bench | bass | Scoots back |
| 0.35 | Curtains | mids | Stir (they always answer to the ghost) |
| 0.35 | Table lamp | treble | Flickers |
| 0.40–0.48 | Books | one band each | Slide out |
| 0.45 | Wall sconces | high mids | Flicker |
| 0.50 | Paintings | bass | Bang against the wall |
| 0.50 | Chandelier light | loudness | Flickers |
| 0.55 | Sideboard doors | mids | Fly open, slam shut |
| 0.60 | Chandelier | bass | Swings |
| 0.60 | Bookcase | bass | Shudders |
| 0.70 | Coffee table | bass | Hops on the kick |

The ghost's nearness lowers a threshold by up to 0.2, so things near it wake first. And the ghost
goes, by preference, to whatever is **about to wake** — the lowest threshold not yet reached — so the
possession spreads through the room following it. When nothing is next, it visits something already
awake, weighted toward the most possessed.

### Every object has a part of the music

Legibility is the whole trick: every sound has a visible cause, as in Animusic. Low things listen to
low bands, heavy things hop on the kick, small bright things rattle on the treble. Two are worth
singling out:

- **The piano is a spectrum analyser in the shape of an instrument.** A keyboard is already a
  logarithmic frequency axis, so each of its 88 keys looks up its own pitch in the analyser's raw
  spectrum and goes down when that note is loud — measured against a running peak across the whole
  keyboard, and only if it is louder than both neighbours, so one bass note does not smear across
  the keys that share its bins. Pressed keys glow faintly blue, the invisible player's touch. At
  11.7 Hz a bin, keys are resolved individually from about middle C up.
- **The bookcase is an equaliser nobody built.** Each book listens to its own band — bass on the
  bottom shelf, treble at the top — and slides out with it.

### The ghost is mostly unseen

"Ghost in the machine" is an invisible thing operating a mechanism, so for most of a track you know
the ghost only by what it does:

- **Its wake.** Curtains billow as it passes; candle flames lean away from it.
- **Its shadow**, sliding across whichever walls it is near with nothing there to cast it — sharper
  and darker close to a wall, larger and fainter further off. Flat on the screens, so the whole
  audience sees it, glasses or not.
- **Glimpses in the machine.** On a strong beat, once the room is possessed, its face flashes in the
  television's static.
- **The portrait's eyes** follow the tracked viewer's head — the CAVE really knows where it is — and
  turn to watch the ghost when it comes near.
- **A shimmer**, faint, like heat, when the room is deeply possessed.

### The reveal is earned

The ghost shows itself on a loud section that follows a quieter one — the drop after a build — for
at most 16 s, and not again for 12 s after. Because the envelope is read ahead, **the lamps start
going down 1.5 s before the drop**, dimming to 40% as the ghost fades in at the drop itself. While
it is out, the fire burns blue, the candles turn blue, the portrait watches it, and the shadows are
gone because the thing casting them is there to see.

**The spectre** is made of the music: a sheet of 2,000 points over nothing, treble in the head and
bass in the billowing hem, the live waveform running round the hem's edge, two eye holes where there
are no points at all, and a trail of ectoplasm behind it. It travels on a critically damped spring,
dances a figure-eight around where it is that widens with the music, bobs and hops on the beat,
leans into its travel, twirls now and then while it is out, and always keeps 0.8 m from the tracked
viewer's head — any closer and stereo breaks down.

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
  in the middle; the floor between is left for the people in it. The ghost keeps to the front of the
  room, above head height, clear of the open back where there is no screen to draw it on.
- **Lamps are real point lights.** They need the pipeline's additional lights, which the Standalone
  build's quality level (Ultra, `Quality URP Config`) has, per pixel, four per object. The Quest-era
  `Performance URP Config` has them off — that is why the editor's quality level is set to Ultra
  too, so play mode at a desk shows what the CAVE shows.
- **No screen-space effects.** `MVR_URP` strips bloom and friends because they seam across walls;
  the fire, candles and ghost glow through their own additive material instead.
- **Single node.** As with the visualizer, one director drives all four viewports. On the 4-node
  config the director's state (possession, reveal, the ghost's position) would need sharing across
  the cluster.

## How it is put together

| Script | Role |
| --- | --- |
| `Haunting` | The director: possession arc, reveal, glimpses, the ghost's targets, the last gasp, input, status line |
| `Possessable` | Base for everything the haunting takes over: threshold, what it listens to, whether the ghost visits |
| `PossessedObject` | Rigid motions: Hop, Roll, Swing, Tilt, Flap, Slide, Rattle, Spin — springs, pendulums and gravity driven by the music |
| `PossessedLight` | Lamps: swell, dip on the beat, sag when deeply possessed, dim for the reveal |
| `PossessedPiano` | 88 keys on the raw spectrum |
| `PortraitEyes` | Eyes that follow the tracked head, or the ghost |
| `HauntedTV` | CRT static drawn on the CPU, with the ghost's face in it |
| `HauntedCurtain` | Cloth billowed by the ghost's wake; grid mesh deformed on the CPU |
| `HauntedFireplace` | Flame tongues as the spectrum, embers on the beat, ghost-blue while it is out |
| `CandleFlame` | Leans away from the ghost |
| `GhostShadows` | The ghost's shadow on the walls |
| `SpectreGhost` | The ghost: travel, dance, and a body of points made of the music |
| `GhostSilhouette` | The ghost's 2D outline, shared by the shadow texture and the TV face |
| `MusicDeck`, `TrackEnvelope`, `ManualParticles` | Shared with the visualizer: playback, whole-track loudness, hand-written particles |
| `HauntedRoomBuilder`, `HauntedTextures` | Editor: builds the room, paints the textures |

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
| | `m_GhostMin` / `m_GhostMax` / `m_Stage` | front of room | Where the ghost may go, and where it appears |
| each `Possessable` | `m_Threshold` | see table | When it wakes |
| | `m_Listen` / `m_Band` | | What it answers to |
| `PossessedObject` | `m_Amount`, `m_Frequency`, `m_Damping`, `m_BeatKick` | per object | How far, how fast, how springy, how hard the beat hits |
| `PossessedLight` | `m_FlickerDepth` / `m_FlickerChance` | 0.65 / 0.6 | Dip depth, and how often a beat dips it |
| `PossessedPiano` | `m_PressFrom` / `m_PressFull` / `m_RangeDb` | 0.6 / 0.88 / 24 | How loud a note must be to press its key |
| `SpectreGhost` | `m_KeepAway` | 0.8 m | Closest it comes to the tracked head |
| | `m_Dance` / `m_TwirlChance` | 0.22 m / 0.25 | How much it dances |
| | `m_Shimmer` | 0.07 | How visible it is before a reveal |
| `GhostShadows` | `m_Darkness` / `m_From` | 0.55 / 0.18 | Shadow strength, and the possession it first appears at |
