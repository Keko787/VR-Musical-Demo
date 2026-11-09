# Animal Orchestra demo (AnimalOrchestra)

A hands-off, self-playing performance. Every part of a song is played by an animal, and each animal
moves to its part's notes and its part's sound: a note fires, an animal moves. The three big animals
stand **beyond the walls**, each wall a window onto one of them, with starlings in the sky past the
front wall and a whale in the water underfoot: that outer layer reads from the open back without
glasses. Five more stand in a bounded volume within arm's reach of the tracked viewer, where the
stereo pays off. Two tigers, for Mizzou: a **cub** front and centre sings lead with the parrot, and a
**tiger** on a crag behind the wolves roars on each drop. Between songs, fireflies fill the room, so the demo can run unattended as the idle
state for open houses and school groups. For the audience there are Sit-Rep-CAVE's three views,
**3D**, **2D** and **2D-Fixed**, on wand buttons 1–3 ([CAVE_View_Modes.md](CAVE_View_Modes.md)).
The **visualize mode**, joystick right, has the animals play the room's own music folder instead,
reading each track's parts off its mix ([The music folder](#the-music-folder-the-visualize-mode)).

Built from *Animal Orchestra: CAVE Build and Plan* (Nov 2026). It covers all five of the plan's
phases in code. Each phase gate is still to be passed **in the room**; see [Against the plan](#against-the-plan).

```
seen from above, front wall at the top            hills on the horizon, the sunset behind the front wall
                          starlings in the sky (beyond the front wall)
                            ELEPHANT, wading (1 m beyond the wall)         moon, far out
              ≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈≈ rippling water runs in under the wall ≈≈≈≈≈≈≈≈
 GORILLA    ┌────────────────────────── front wall ───────────────────────┐   ridge,
 on a bank  │ jellyfish ~ (high)        tiger cub    jellyfish ~ (lower)  │   WOLVES
 (beyond    │          parrot          (on a rock)   woodpecker           │   (beyond
 the left   │                       jellyfish ~ (chest height)            │   the right
 wall)      │      bullfrog         ◉ viewer                 octopus      │   wall);
            │                                                             │   TIGER on a
            │                                                             │   crag behind
            │         ~~~~~~~~~~~ WHALE in the water ~~~~~~~~~~~          │
            └──────────────────── open back: audience ────────────────────┘
       still lagoon all round, out to the horizon; fireflies in the room
```

## Build it

| Menu item | What it does |
| --- | --- |
| **VR-Music-Demo ▸ Animal Orchestra ▸ Create Animal Orchestra Scene** | Copies the physics scene to `AnimalOrchestra.unity`, strips the other demos out of it, and builds the orchestra |
| **VR-Music-Demo ▸ Animal Orchestra ▸ Rebuild Animal Orchestra** | Rebuilds the orchestra in whatever scene is open, as long as it has a `CAVE_User_Room` |

Like every demo here, the scene starts as a copy of `Physics MiddleVR.unity`, because that scene
already carries the whole MiddleVR setup. Create also adds the scene to **File ▸ Build Settings**,
switched off.

Everything the builder makes lives under `CAVE_User_Room/Animal_Orchestra`:

```
Animal_Orchestra        OrchestraConductor: the playlist and the one clock
├── Water               RippleFloor (water from VR-Demo's head tracking demo), M_OrchestraWater:
│                       the floor, and on 1.8 m past the front wall under the elephant
├── Lagoon              still water round it all, out to the horizon (Lagoon.asset)
├── Scenery             the gorilla's bank; hills on the horizon
├── Elephant            ElephantPerformer     Kick      1 m beyond the front wall, centre, ×1.4
├── Whale               WhalePerformer        SubBass   floor
├── Gorilla             GorillaPerformer      Snare     0.75 m beyond the left wall, centre, ×1.3
├── Wolf_Pack           WolfPackPerformer     Choir     beyond the right wall: ridge, three wolves, the moon far out, ×1.15
├── Starling_Flock      StarlingFlock         Arp       the sky 1.2 m beyond the front wall
├── Fireflies           FireflySwarm          Texture   every free surface; the idle state
├── Parrot              ParrotPerformer       Vocal     eye level, front left, on a stand
├── Bullfrog            BullfrogPerformer     Bass      knee height, left, beside the viewer's spot, on a stump
├── Woodpecker          WoodpeckerPerformer   HiHat     waist height, front right, on a trunk
├── Jellyfish           JellyfishPerformer    Pads      three: high by the left wall, lower by the right, chest height in front of the viewer
├── Octopus             OctopusPerformer      Keys      low, right, beside the viewer's spot, on a pedestal
├── Tiger               TigerPerformer        Roar      beyond the right wall on a crag, further out than the wolves'
│                                                       ridge, between the back and middle wolves, its head above theirs, ×1.1
├── Tiger_Cub           TigerCubPerformer     Vocal     front and centre on its own rock, facing the viewer (with the parrot)
└── Menu                OrchestraTrackMenu: the music folder's tracks on the front wall, hidden until
                        the joystick brings it up; on the CaveOverlay layer

CAVE_User_Room/CAVE_View    3D / 2D / 2D-Fixed on wand buttons 1–3 (CAVE_View_Modes.md)
```

Each animal builds its own body in `Performer.Build` from a shared kit
(`Scripts/AnimalOrchestra/CreatureKit.cs`): Unity's sphere, capsule and cylinder plus a generated
cone and jellyfish bell, all on two shared materials. The colours sit on each part's `CreatureTint`.
A rebuild clears the subtree, so hand-tuning *inside* it is lost; the numbers live on the
components. The materials in `Art/Materials/AnimalOrchestra/` are created once and then left alone,
so colours tuned there survive.

Outside the subtree the builder switches off the room's wall quads and the `Ground` plane (the sky
and the water replace them), and sets every camera to clear to the skybox. It also makes the
skybox the dusk sky, sets a dusk trilight ambient, and turns the directional light into cool
moonlight from behind the viewer. The skybox and ambient are scene settings, so **not undoable**.
The view modes add the `CaveOverlay` layer to the project the first time (layer 8 here) and the
pictures' material, `M_FixedViewPicture`.

## The stage

**The walls are windows.** The room's wall quads are off, so each screen looks out of the room, and
the three big animals stand outside it: the elephant 1 m beyond the front wall, wading; the gorilla
0.75 m beyond the left wall, on a mossy bank; the wolves' ridge beyond the right wall. Each is
anchored to the middle of its own wall and seen through that wall alone, never across a corner, so a
screen's seam never cuts an animal in two (the builder checks this; see [Room check](#room-check)).
They are scaled up, 1.4, 1.3 and 1.15 times, because a metre past the glass an animal looks smaller
than one against it, and these still have to read from the open back. The room itself holds only the
inner volume. This is a change from the plan, which keeps everything inside the room, at Kevin's ask.

Beyond the glass the image has real depth for the tracked viewer: step sideways and the elephant
moves against the hills behind it, as through a window. For everyone else, **2D-Fixed** turns each
wall into a steady picture of the same scene from one spot in the room.

The starlings fly in the sky beyond the front wall, above the elephant. Seen through the front
window, the flock's band sits between the elephant's head and the window's top edge for eyes from
1.5 to 1.75 m. The wolves' moon is far out over the water, low, ahead of them, seen through the
front half of the right wall just above their heads.

The **sky** (`VRMusic/Dusk Skybox`) is the moment after sunset, made from the view direction alone:
indigo overhead, violet, then a warm horizon with the glow brightest behind the front wall. A few
cloud streaks catch the glow, and stars fade in overhead. The elephant and the starlings stand
against the brightest part of it. Every colour is a material property on `M_DuskSky`.

The **floor** is water: the sheet from VR-Demo's head tracking demo (`Scripts/Tracking/RippleFloor.cs`,
brought over with this demo along with `TrackedHead`), as wide as the floor screen and running on
1.8 m past the front wall, under the elephant. Its stomps splash the water at its foot, out there, and
the rings come in under the wall and across the floor toward the viewer, one sheet seen through the
front wall and then on the floor, as the plan asks: *"tying the front wall to the floor."* Round it,
out to the horizon, is a still **lagoon** on the same material. The whale swims in the room's part
of it, 1.2 cm above the rest surface, so every crest passing over the whale hides a strip of it.
That is how it reads as being under the water.

**Lighting.** The key light is cool moonlight from behind the viewer, so every animal's face is lit
as the audience sees it. The warm half comes from the creature shader's rim light: the sunset behind
the animals edges their silhouettes.

## The songs

The plan leaves the songs open (*"pick two or three demo songs with clean stems and MIDI"*), and the
music folder the other rooms play has neither stems nor MIDI, so the demo brings its own (and its
visualize mode reads the folder's tracks off their mixes, [below](#the-music-folder-the-visualize-mode)).
It has three lab-made songs, written note by note in `DemoSongs.cs` and
synthesised by `SongSynth.cs`, so there is nothing to license and nothing to load:

| Song | Key, tempo | Character | Parts |
| --- | --- | --- | --- |
| Lagoon Lights | A minor, 110 BPM, i–VI–III–VII | intro, verse, build, two choruses, a break | all eleven |
| Night Parade | D major, 96 BPM, I–V–vi–IV | half-time groove led by keys and voice | all eleven |
| Deep Current | E minor, 124 BPM, i–iv–VI–v | electronic, arpeggio-led | no vocal, no keys |

Each song also has a **drop**: a crash cymbal on the downbeat of each chorus the energy jumps into
(twice a song), which is the tiger's roar. It has a part of its own, `Roar`, so it can be that rare.

Deep Current leaves the singers and the octopus out on purpose: *"songs without a part simply leave
that animal idle."* Parts come and go between sections in the other two, so animals take turns
resting.

The notes that make the sound are the notes the animals read, so sync is exact by construction.
Each part is rendered as its own stem, measured (below), then panned toward its animal's wall: the
gorilla's snare left, the wolves' choir right, the bullfrog's bass a little left, the woodpecker's
hats a little right. Then a small reverb is added. Each song is about 90 s. Rendering one takes about
a second in optimised .NET on four threads, and longer in the Editor's Mono. It renders in the
background while the previous song plays, so only the first song is waited for, and the fireflies
fill the room while it renders.

### A song of your own

The plan's format: a folder per song, holding a MIDI file, one stem per part, and a mapping file. Put
it under `Assets/StreamingAssets/AnimalOrchestra/<Song>/`. The playlist plays every such folder
first, then the built-in songs.

```json
{
  "title": "My Song",
  "midi": "song.mid",
  "parts": [
    { "part": "Kick",   "stem": "kick.wav",  "track": "Drums", "pitches": "36" },
    { "part": "Snare",  "stem": "snare.wav", "track": "Drums", "pitches": "38,40,41,43,45,47,48,50" },
    { "part": "HiHat",  "stem": "hats.wav",  "track": "Drums", "pitches": "42,44,46" },
    { "part": "Vocal",  "stem": "vox.wav",   "track": "Lead Vox" },
    { "part": "Bass",   "stem": "bass.wav",  "track": "Bass" },
    { "part": "Pads",   "stem": "pads.wav" }
  ]
}
```

- `part` is one of `Kick SubBass Snare Choir Arp Texture Vocal Bass HiHat Pads Keys Roar`. `Roar` is the
  drops: map it to the crash cymbal (`"pitches": "49,57"`) and the tiger roars on every crash.
- `track` is a MIDI track's name (or its index). `channel` (1–16) and `pitches` (a list or a range
  like `"60-84"`) narrow it, which is how one drum track feeds the elephant, the gorilla and the
  woodpecker.
- A part with a stem but no track moves on its stem alone. A part with neither idles.
- Stems are WAV or OGG, all starting at the MIDI file's time zero, which is how a DAW exports them.

The drum parts read General MIDI pitches: the gorilla tells toms (41, 43, 45, 47, 48, 50) from
snare, and the woodpecker tells the open hat (46) from closed. `MidiFile.cs` is a minimal Standard
MIDI File reader covering formats 0 and 1, the tempo map, running status and track names. It doesn't
support SMPTE time division, which no DAW exports by default. It was checked against a generated file
with a tempo change, running status, velocity-0 note-offs and overlapping notes of one pitch. **No
real song folder has been run yet**, so the first one is worth watching in the Console.

### The music folder: the visualize mode

The other music rooms' tracks (`Assets/_Project/Audio/Music`) are finished mixes, with no stems and no
MIDI. The visualize mode plays them anyway, and reads every part off the mix (`MixedSong.cs`):

- **The notes** are the pipe rig's score (`RigScore`, the pipe rig's Visualize mode), which turns a mix
  into onsets per band, with a pitch class for the tuned ones. They are dealt out like this:

  | The score finds | Goes to |
  | --- | --- |
  | Kick | The elephant |
  | Snare, tom | The gorilla: chest on the snare, alternating fists on the toms |
  | Hi-hat | The woodpecker, and a firefly flicker |
  | Crash, where the music jumps up | **The tiger's roar**, with the elephant's trumpet, the woodpecker's open hat and a burst of fireflies |
  | Bass note | The bullfrog; the strongest, at most two a second, the whale too |
  | Mid note (the chords' and keys' range) | The octopus |
  | High note (the melody's range) | The starlings and the parrot; the strongest, firefly glints |

  Pitches are pitch classes only, so each part plays in one octave of its own. A velocity is how hard
  the note is against the rest of its part, plus how loud the song is there, so choruses hit harder.
- **The beat** is the length the onsets repeat at, at one, two, three and four beats, weighed toward the
  usual tempos, then the grid they line up on exactly. One tempo per track. On eleven of the folder's
  tracks it found the published tempo for every one that has a known one (Hey, Soul Sister 97, Faded 90,
  Happier 100, Shelter 100, Am I Wrong 120 …). It can still land on twice or half the tempo, which only
  makes the animals bob twice a beat or every other one, still on it.
- **The chords** are a chroma every eighth of a second, averaged per beat, matched to the major and minor
  triads, then smoothed so a chord holds until the harmony moves. Hey, Soul Sister comes out E–B–C#m–A
  and Faded E♭m–B–F♯–C♯. The jellyfish's pads play every chord. The wolves' choir sings them only where
  the song is in its loudest two fifths, so they howl in the choruses (a third to a half of most tracks).
- **The loudness** of each part is `StemAnalysis` on that part's band of the mix: under 150 Hz for the
  elephant, under 90 Hz for the whale, 300 Hz–3.4 kHz for the parrot, above 6 kHz for the woodpecker,
  250 Hz–2 kHz for the pads, keys and choir, and the whole mix for the fireflies.

Reading a four-minute track takes about 8 s (5–11 s on the eleven tried, in batch Unity on this PC),
in the background. The first track picked waits that long, with the fireflies up; the next track is read
while one plays, so walking on through the folder doesn't wait, and the last eight read are kept, so going
back to one starts at once. The mode plays on through the folder, round to the start, until it is left.
The conductor's list of tracks is filled by the builder, again when Play starts in the editor, and by
`VisualizerPlaylistProcessor` in a player build, as the other rooms' decks are. Only clips that hand back
their samples can be read: **Decompress On Load**, a dropped-in MP3's default.

The track list is the other rooms' front-wall panel (`WallMenu`). The joystick push that brings it up is
also the first step through it: down from the track playing selects the next one. It goes on the
`CaveOverlay` layer, so in 2D-Fixed it is drawn live under the wand's ray rather than into the fixed
pictures, and it sorts after the room's other see-through things, so the starlings behind it don't draw
over it.

## How the animals keep time

The audio clock is the only timeline. `OrchestraConductor` starts every stem with `PlayScheduled` on
the same DSP sample, and song time is the DSP time since then. The DSP clock only steps once per
audio buffer (about 21 ms here), so between steps the frame clock carries it forward, and it never
runs backwards. Frame time never drives the music, so a slow frame skips the animation ahead rather
than letting it fall behind.

**Triggers.** A performer asks the song for the note coming next and the note just gone
(`Performer.TryHit`). It shapes the move as a function of time from that note (`Motion.Hit`): a
windup from at most 200 ms before, an accelerating strike that reaches its pose exactly on the
note, then a recovery. That is the plan's *"schedule clips ahead from MIDI so the hit frame lands
on the note."* Nothing is queued, and every pose is recomputed from the clock each frame. For fast
repeats the lead shrinks to three quarters of the gap, so every hit still gets a windup.

**Continuous signals.** Each stem is measured once, when the song loads (`StemAnalysis.cs`): loudness
plus a low band (below 150 Hz) and a high band (above 4 kHz), 100 times a second. An envelope follower
smooths them, rising in 15 ms and falling over 180 ms, and they are normalised to the stem's own loud
passages. This is the plan's *"envelope follower and a few FFT bands per stem, smoothed"*, measured
ahead of time, so it has no analysis latency and doesn't jitter with the frame rate. All three bands
share one scale, so a band reads as its share of the stem: a hi-hat's low band stays near zero.

**Tuning the sync in the room.** The conductor's **Visual Offset (ms)** shifts every animal against
the sound. Film the room at 240 fps, count the frames between the audio hit and the visual hit for
the kick, the snare and the vocal, and set the offset to cancel the difference. It starts at 0.
Audio output latency (the DSP buffer is 1024 samples) and the CAVE's 2–3 frames of display pipeline
pull in opposite directions, so only the room can say which wins.

## The cast

| Animal | Part | Discrete (notes) | Continuous (stem) | Idle |
| --- | --- | --- | --- | --- |
| Elephant | Kick | Alternate front feet stomp; body dips toward the foot; foot pad glows; **the water splashes under the foot** | Ears flap with the low band | Weight shifts foot to foot, trunk sways |
| Whale | SubBass | Each note sends a glow pulse down the spots, nose to flukes; hard notes flick the tail into the water | Swim speed and body glow follow the low band | Slow figure-eight under the inner animals |
| Gorilla | Snare | Snare: alternating hands beat the chest; toms: alternating fists pound the log; chest and log glow | — | Breathing, sway, looks around |
| Wolf pack | Choir | One wolf per voice: the head rises higher the higher its pitch | Jaw opens with loudness; the moon brightens | Ears, glances, tails |
| Starlings | Arp | Each note ripples a flash through the flock | The flock's centre rises and falls with pitch while sweeping the wall over 8 bars | Wheels about the band |
| Fireflies | Texture | Glints (high notes) light a handful at once | The high band sets how many are lit and how fast they blink | All lit, blinking slowly: the room between songs |
| Parrot | Vocal | On held notes: head tipped back further, body leans back, beak and head quiver with vibrato, wings flare | Beak gapes (both halves part, showing a dark mouth and a pink tongue), head tips back and chest puffs as it sings out, crest fans; shut fully between phrases | Bobs to the beat, **looks at the tracked head** |
| Bullfrog | Bass | Throat sac inflates by note velocity; body squashes | — | Throat breathes, blinks |
| Woodpecker | HiHat | One peck per hat note, striking the trunk on the note; accents glow; the open hat lifts the head and crest | — | An occasional tap, head tilts |
| Jellyfish | Pads | Bell contracts on each chord and the jelly rises a little | Gentle throb with loudness; colour from the chord root | Slow pulse and drift |
| Octopus | Keys | One arm per sounding note, low notes left, high right, like a keyboard; suckers flash on note-on | Mantle swells with loudness | Waves down the arms, breathing, blinks |
| Tiger cub | Vocal (with the parrot) | On held notes: both front paws up and out, head tipped further back, jaw and head quiver with vibrato | Jaw drops (dark mouth, pink tongue, little fangs), head tips back, ears prick up and chest puffs as it sings out; shut fully between phrases | Bobs to the beat, swishes its tail, blinks, **looks at the tracked head** |
| Tiger | Roar | Before a drop it crouches, ears back, and draws breath; on it, it rears up, turns to the viewer and roars, jaws wide, eyes lit, tail lashing; holds it about a second and settles | — | Sits on its crag looking about — the moon, the wolves, the room — breathing, tail sweeping |

Each performer has a **Gain** (0–2), the plan's *"per-stem gain on visual intensity: mix the visuals
like audio"*. It scales that animal's glows and continuous motion.

Colours from pitch (`Motion.PitchColor`) go round the circle of fifths rather than the chromatic
scale, so chords a fifth apart get neighbouring hues and a song's colours drift rather than jump.

## Controls

On the wand. Nothing is drawn on the screens but the track list, and that only while it is up:

| Wand | Key | Does |
| --- | --- | --- |
| 1 / 2 / 3 | 1 / 2 / 3 | 3D / 2D / 2D-Fixed views ([CAVE_View_Modes.md](CAVE_View_Modes.md)) |
| Joystick right | → | The visualize mode: the music folder, from the track it was last on (the first, the first time) |
| Joystick left | ← | Back to the orchestra's own songs |
| Joystick up / down | ↑ / ↓ | Bring up the music folder's tracks on the front wall, one up or down from the track playing |
| *with the list up:* up / down, right or trigger, left | ↑ / ↓, → or Enter, ← or Esc | Move through it (held, it repeats); play the one selected in the visualize mode; put the list away |
| 4 | N or 4 | Skip to the next song (the next track in the visualize mode) |
| 6 | L or 6 | Low intensity mode on/off |

A joystick push counts once: holding it does nothing more, and the next push counts once the stick has
come back to the middle. So the push right that picks a track doesn't also do anything once the list has
closed.

The views are on 1–3 as in Sit-Rep-CAVE and VR-Demo's bounded volumes, so skip and low intensity are
not on 3 and 2 as in the other music rooms. Button indices are Inspector fields on the conductor and on
`CAVE_View`, because they are the `.vrx`'s, not standardised; `MiddleVRWandButtonProbe` lists them. The
skip, intensity and arrow keys go through Unity's keyboard, so the Editor or the build needs focus; the view
keys also read MiddleVR's keyboard (`MiddleVRKeyboard`, brought over from VR-Demo with the views), which
works without focus.

**Photosensitivity.** Nothing flashes faster than the conductor's **Max Flash Hz** (3 Hz). Each animal
scales a per-note flash down when its notes come faster than that: the woodpecker's 16th-note hats
glow only on accents, and only softly. Fireflies blink on independent phases, so the room never
flashes as a whole. **Low intensity mode** dims every glow to 45% and lowers the cap to 1.5 Hz. The
plan also says to warn at the door, which is for whoever runs the session.

## Room check

After building, the builder checks every animal against the room and logs a warning naming the
animal and the rule it breaks:

- the wall animals stand wholly **behind their own wall**, at least 10 cm from the glass;
- every part of a wall animal is seen **through its own wall** from the default eye, not through a
  neighbouring screen: that is what anchoring it to the middle of one wall means once it is beyond
  the glass, and it keeps every seam clear of the animals;
- everything else is inside the room;
- the inner animals are under 1.5 m across;
- the inner animals are **visible**. From 27 test eyes (heights 1.5, 1.6 and 1.75 m, around the
  default spot at (0, 0, −0.35)), every line through an animal's centre and top corners must land on
  a screen. The CAVE has no ceiling and no back wall, so a line leaving through either means part of
  the animal vanishes.

Checked numerically before the first build, from the performers' reported extents (motion included):
from all 27 test eyes, every corner of the elephant's, the gorilla's and the wolves' boxes is seen
through its own wall, 24, 17 and 17 cm clear of the glass. At 1.35 times the wolves' ridge was longer
than the right wall's window and reached round the front corner, which is why it is 1.15. Only the
corners of the moon's halo and of the starlings' band touch the top of the windows, for the tallest
eyes.

The jellyfish are why the visibility check exists. "Overhead" in this CAVE has a ceiling: a crown is
seen only while the line from the eye through it still lands on the front wall, so the nearer a jelly
is to the viewer, the lower it has to hang. Their first heights were set by this rule; where they hang
now was chosen by hand, and still passes it.

**The inner five were placed by hand** in the scene (Kevin, 2026-11-02) and saved into the builder as
they stood, rotations included, so they no longer turn to face the default eye. The bullfrog and the
octopus now stand beside the default spot, a little behind it, where the CAVE has no back screen: from
that spot the room check finds parts of them out of sight. The jellies are no longer overhead: one hangs
high by the left wall, one lower by the right, and one at chest height 40 cm in front of the viewer's
spot. They can be dragged in the scene too; each moves about wherever it was left.

The tracked viewer's default spot is 35 cm behind the room centre, so the inner volume is in front of
them. The inner animals are fixed in room coordinates and don't follow the head, so if the tracker
drops out they simply stay put (the plan's fallback). Only the parrot's gaze uses the head, and it
falls back to the default eye.

## Cost

Drawing is light: a few hundred primitive parts across eleven animals (the jellyfish's beaded
tentacles are most of them), on two instanced materials, plus 410 particles. The rippling water is a
143 × 162 sheet now that it runs past the front wall, 23,000 vertices rewritten each step; the lagoon
is eight triangles. Per frame on the CPU, the
jellyfish update about 235 property blocks (their colour drifts continuously), the whale rewrites
its body mesh, and the starlings run 150 springs. All of that is small next to MiddleVR's eight
eye cameras.

## Checking it at a desk

Press Play. Fireflies fill the room for a few seconds while the first song renders; the Console logs
`Rendered 'Lagoon Lights'`, then `Now playing …` with the parts the song has. Then the sky glows
orange behind the elephant, the elephant stomps and the water rings toward you, and the gorilla beats
its chest on 2 and 4. Pads come in first, so the jellyfish pulse from the opening chord, and the
wolves howl in the intro. Press **N** to skip and **L** for low intensity. Press **2** for 2D and
**3** for 2D-Fixed: the stereo collapses, then every screen holds steady however the head moves; **1**
brings 3D back.

For the visualize mode, press **↓**: the track list comes up on the front wall with the first track
selected. **↓** again moves down, and **→** plays the one selected: the Console logs
`Visualize mode: '…', reading it`, then `Read '…' in 8 s: 90 BPM, 2780 notes over 11 parts` and
`Now playing …`. **N** skips to the next track, which starts at once (it was read while the last one
played); **←** goes back to the orchestra's songs and **→** returns to the track it was on. Checked like
this in batch Unity with a virtual keyboard, on a copy of the project, every step as described. The
joystick itself has only been read through the same code, not pushed in the room.

If the animals are magenta, a shader didn't compile and the Console will say why; the builder logs an
error rather than building if `Shader.Find` can't see one. If a screen shows the old coloured wall
instead of sky, the wall wasn't switched off; the line starting `[AnimalOrchestra] Switched off`
lists what was. Judge sync and frame rate only in a standalone build launched on the CAVE's A6000,
not from Play mode, which runs at about 14 FPS here (`MiddleVR_A6000_GPU_Placement.md`).

## Against the plan

| Phase | Built | Gate (in the room) |
| --- | --- | --- |
| 1. Pipeline and room check | Song format, MIDI and stem loader, transport clock, built-in songs | One song in sync. Measure with a 240 fps phone and set **Visual Offset** |
| 2. Outer layer | Fireflies, starlings, whale, elephant, gorilla, wolves | A full song reads from the open back with no glasses |
| 3. First inner performer | Parrot at eye level, looking at the tracked head | Lip sync looks right to an untrained viewer |
| 4. Rest of the inner ensemble | Bullfrog, woodpecker, jellyfish | All five zones for three songs back to back at a stable frame rate |
| 5. Stretch and polish | Octopus, playlist and attract mode (the idle between songs), low intensity mode | Runs unattended for a full session |

Not done:

- the plan's *"reuse the room audio visualizer from the hands-off demo"*: `RoomVisualizer` is in this
  repository, but the orchestra doesn't use it yet, so the outer layer is built fresh;
- the plan's 2D rigged sprites for the outer animals: they are 3D figures standing beyond their walls
  instead, a change from the plan's *"nothing is placed beyond the walls"* made at Kevin's ask; each is
  still anchored to the middle of one wall.

Still open, as in the plan: the speaker layout and routing out of Unity (the songs are panned for
stereo); real songs with stems and MIDI; and the dry run.
