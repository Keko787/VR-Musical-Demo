# Pipe rig (PipeRig MiddleVR)

A self-playing machine in the CAVE. A low bank of pipes just in front of the people throws balls in
slow arcs onto instruments all round them — bass and guitar strings, mallet and glockenspiel bars,
strings, tubular chimes, drums and a gong — and every ball lands exactly on its note. It is the
deck's *self-playing CGI orchestra*, built to the design notes *Self-Playing Pipe Rig for the CAVE*
(30 Oct 2026): every ball's path is solved backward from the note it must hit and
checked against the room before it is thrown, so the whole machine stays inside the room. The
instruments, the layout and the backing beat are original; nothing is copied from the reference.

It has two modes:

| Mode | What it is |
| --- | --- |
| **Play** | Point at an instrument and pull the trigger to play it, in the machine's own synthesised voices. Turn the loop on and what you play is kept and thrown back by the pipes on every pass; a backing beat can play underneath. |
| **Visualize** | The machine plays along with a track from the music folder. The track is read ahead into notes, and each note is thrown so its ball lands as the note is heard. Silent — the track is the sound. |

## Build it

| Menu item | What it does |
| --- | --- |
| **VR Shooting Gallery ▸ Pipe Rig ▸ Create Pipe Rig Scene** | Copies the physics scene to `PipeRig MiddleVR.unity`, strips the tether demo and the gun out of it, and builds the rig |
| **VR Shooting Gallery ▸ Pipe Rig ▸ Rebuild Pipe Rig** | Rebuilds it in whatever scene is open, as long as it has a `CAVE_User_Room` |

Same flow as the other demos. It can also be built without opening the editor:

```
Unity -batchmode -projectPath . -executeMethod VRShootingGallery.EditorTools.PipeRigBuilder.CreateFromCommandLine -quit
```

The last thing the builder does is solve every path and check it against the room; anything that
would leave the screens or cross the people is logged as an error naming the pipe and the target.
The rig checks again at load and never throws a bad path.

**Tracks** come from `Assets/_Project/Audio/Music`, shared with the other rooms and read the same
way. **Nothing is bought or downloaded**: the machine is built from primitives and every sound is
synthesised as the scene starts.

## Running it

| Wand | Desk | Play mode | Visualize mode |
| --- | --- | --- | --- |
| Trigger (0) | Mouse click | Strike what you point at | — |
| 1 | M (Shift+M switches straight over) | Modes menu | Modes menu |
| 2 | R (C empties) | Tap: loop records / stops recording. Hold: empty the loop | — |
| 3 | N (Shift+N plays the next track) | Tracks menu (picking one goes to visualize) | Tracks menu |
| 4 | Space | Backing beat on/off | Pause / play the music |
| — | B | Backing beat on/off | — |

The number-row keys stand in for the wand buttons with the same digit. Button 2 is low intensity in
the other rooms; the rig has no low-intensity mode, so it is the loop button here. In a menu: point
and pull the trigger, or the joystick, or the arrows and Enter. The menu is the shared front-wall
`WallMenu`, standing half a metre out so it clears the mallet shelf. A dim status line on the floor
at the open back says what the room is doing, for whoever runs the demo. Wand buttons only count
once they have settled for a moment without the trigger: the VisBox wand reports more indices than
it has buttons, and if the trigger reports another one too (the console says which, once), a strike
can no longer also empty the loop or open a menu.

The room opens in **play mode** with the music waiting, paused, on the first track.

### Play mode

Point the wand at a bar, a string, a chime, a drum or the gong — it brightens — and pull the
trigger. It sounds at once, moves (the bar dips, the string swings, the drum head pumps, the cymbal
rocks, a chime or the gong swings where it hangs) and the wall or floor behind it glows. At a desk
the mouse does the pointing through the main camera. The guitar's strings are picked out over its
body, where they are furthest apart.

The **loop** is two bars of 4/4 at 100 bpm. Tap button 2 (or R) and it records — the red lamp at the
left of the bank lights — and every strike is kept, moved to the nearest sixteenth, alongside the
ones already there; from the next pass on the pipes throw them all back so each lands on time, the
machine learning your riff. Tap again to stop recording; the loop keeps playing. The eight lamps
along the front of the bank walk the beats, brighter where the loop has notes. Hold button 2 (or
press C) to empty it: the record lamp charges toward white and at 0.8 s the loop is gone. Balls
already in the air still land; the ones waiting in their pipes are taken back with their sounds.

The **backing beat** (Space or B) is an original two-bar groove — kick, snare, off-beat hats, a fill
down the three toms, and a bass line moving C–G–E then A–E–G — played by the machine for you to
play over.

Every looped or backing note is scheduled on the audio hardware's clock with
`AudioSource.PlayScheduled`, a flight time ahead, so the sound and the strike agree.

### Visualize mode

Pick a track (or press Space) and the machine plays along: kick, snare, toms, hi-hat and crash on
the drums, the bass line on the bass strings, the middle of the mix on the mallet bars, the top on
the strings. The track is the clock — pause it and every ball hangs in the air; restart it and the
old throws are cleared.

The rest of the machine joins in from the same notes:

| Instrument | What it plays |
| --- | --- |
| Guitar | Doubles the bass line's accents |
| Glockenspiel | Doubles the mallets' accents |
| Chimes | Double the strings' accents |
| Toms | A tom hit on its own is the middle tom; hits within 0.6 s of each other are a fill, walked down from the rack tom to the floor tom |
| Gong | The biggest drops: a crash at least 85% the size of the track's biggest drop, and no more than once in 8 s. Every other crash stays on the crash cymbal |

An accent is a note at least 0.1 stronger than its part's recent average (a running average over
the last few notes), and a partner doubles at most one note every 0.3 s. Doubling uses the same
pitch-class matching as the parts themselves, each partner keeping its own octave. The score is read
far ahead of the music, so the first hit of a fill and the size of the biggest drop are known in time.

The now-playing strip on the front wall is shown only in this mode.

## How it works

### Every throw is solved backward from its note

For a note at time *t* on a target at **p₁**, thrown from a pipe mouth at **p₀** with flight time *T*,
the ball leaves at *t − T* with

**v₀** = (**p₁** − **p₀** + ½ g T² ŷ) / T

and *T* is the longest flight whose apex stays under the height cap: the slowest, most readable
arc, always coming down onto the target from above. The notes find it by bisection; it has a closed
form — the time to rise to the cap plus the time to fall from it — so `RigTrajectory.Solve` uses
that. Gravity is 4 m/s², so room-sized arcs read as floaty throws; flights come out at 1.1–1.45 s.

A ball's position is a pure function of the clock minus its launch time — on its arc, hopping off
what it struck, or rolling home — with no rigidbody. A dropped frame cannot make it miss, a pause
freezes it, a seek just clears the throws.

### The room is the stage

`RigVolume` (on the `PipeRig` component) is the room box pulled in 0.25 m off the side walls and
0.2 m off the front, a height cap at 1.9 m (no ceiling screen), the open back kept clear, and a
0.5 m cylinder round the standing area at the back middle. The solver uses its cap; the validator
samples every arc at 30 points and fails it if it leaves the box or touches the cylinder, and checks
every hop and roll against the cylinder too. A target that cannot be reached under the cap is
flagged, not clamped.

Layout, from above: the pipe bank low in the middle, just in front of where the people stand (its
front 0.02 m in front of the keep-out cylinder, the middle of its twelve pipes 0.57 m from the
cylinder's centre); bass strings on a frame 0.3 m off the left wall; fifteen mallet bars on a shelf
1.3 m up along the front wall, 0.2–0.5 m out, tilted 10° toward the people; strings on a frame
0.3 m off the right wall; a guitar lying on a stand in the middle past the bank, tipped 25° toward
the people; a glockenspiel beside the bank on the right; drums either side. Beside the people stand
a floor tom and a gong on the left, and a hi-hat and a row of tubular chimes on the right. Pitch
runs low to high round the room — bass from the back of the left wall forward, bars left to right,
strings from the front of the right wall back and on into the chimes — matching the stereo pan.
Every ball flies away from the people toward a wall or the front, and nothing stands in a corner.

| Group | Notes | Where | Pipes |
| --- | --- | --- | --- |
| Bass | C2–B2, 7 strings | left wall frame | 0 (then 1) |
| Guitar | E2 A2 D3 G3 B3 E4, open strings | lying in the middle, past the bank | 3 (then 2) |
| Mallets | C3–C5 diatonic, 15 bars | front wall shelf | 4, 5, 6 by register |
| Glockenspiel | C6–C7 diatonic, 8 steel bars | beside the bank, right | 7 (then 8) |
| Strings | D5–D6 diatonic, 8 strings | right wall frame | 11 (then 10) |
| Chimes | C5–C6 diatonic, 8 tubes | beside the people, right | 10 (then 11) |
| Drums | kick, rack tom, tom / floor tom, gong | left of the bank / beside the people, left | 1, 2 / 2, 1 |
| Drums | snare, crash / hi-hat | right of the bank / beside the people, right | 8, 9 / 9, 8 |

A group's second pipe takes over when its first has fired within 0.22 s, so a fast part spreads
across pipes instead of one pipe firing faster. The pipes run left to right in the order of what
they feed, each banded in its group's colour.

The guitar's strings are three times a real guitar's spacing at the bridge (0.04 m) so a wand can
pick one out; balls come down on them between the sound hole and the bridge. The gong is turned to
face pipe 2 and is struck face on; the chimes are struck on their caps, as real chimes are.

After a strike a ball hops off — back over a bar into the front trough, over the guitar's far edge
into the front trough, over a glockenspiel bar's end and under the strings' frame into the right
trough, off a chime's cap toward the wall, through a string into the side trough, off a drum onto
the floor, down off the gong's face — and rolls home along glowing lanes on the floor: along the
side troughs (which run on past the people, for what stands beside them) to the lanes across the
floor in front of the bank, and into its sides; down the lane beside the bank from the toms and the
snare; along the front trough to its middle and into the bank's back.

### Reading a track ahead

`RigScore` reads the clip's samples a few seconds a frame on the main thread into one mono buffer at
half rate, and a worker thread analyses them as they arrive — a four-minute track in three to four
seconds, so after the first moments it is far ahead of the music. Each band — kick 35–110 Hz, snare
body 150–400, snare wires 1.8–6 kHz, hi-hat 7–10.5 kHz, bass 80–220, mid 220–700 (the mallets'
range), high 700–2600 (the strings') — has its energy taken every 5 ms. An onset is a peak in the
band's rise in dB (over 10–30 ms, longer for the bass) that stands clear of its neighbours and of
the band's recent average, *and* comes within 15–20 dB of the band's recent loudest: a rise in dB
alone would count a little spill into a quiet band as a hit.

A hit spills into the bands round it, so at the same moment (within 25 ms) the kick wins over the
bass and the hats, the snare and tom over the hats, the bass and the middle, and any drum or the
middle over the top. A snare also needs a thump in its body band (a hi-hat's sizzle has none); a
tom is a snare-band hit that is mostly body. A crash is a kick or snare where the next second is
well louder than the quietest of the three before it — a drop. A crash's strength is how big that
jump is: 6 dB is 0.35 and 12.5 dB or more is 1, which is what lets the gong take only the biggest.

For the tuned bands, a Goertzel filter at every semitone in range just after the onset gives a
chroma; its strongest pitch class picks the bar or string (the octave nearest the last note that
group played, so a line moves like a line), and an onset with no clear pitch is dropped.

Checked against a synthetic clip of the rig's own voices at known times, kicks and snares land
within 4 ms of the truth and mallet-band pitches are right every time they are found; on the music
folder's tracks it finds roughly 1.5–3 kicks, 0.5–3 snares, 0.5–1.5 hats, 1–1.7 bass notes, 1.5–2
mallet notes and 0.2–0.5 string notes a second. Onset detection from a finished mix is a heuristic:
it follows the rhythm and the register of the music, not every note of it.

Only clips that hand back their samples work: **Decompress On Load**, the default for a dropped-in
MP3. Anything else shows "cannot read it ahead" on the status line. Scores are kept per clip, so a
track played again is not read twice.

### The 2D layer

Everything the people without glasses need is on the screens themselves: each ball's soft shadow
on the floor (also the tracked viewer's depth cue — the floor is a dark slate rather than black so
the shadows read, the walls near-black so the glows do), a glow on the wall behind each bar, string
and chime, on the left wall behind the gong, on the floor under each drum and past each glockenspiel
bar when it is struck, a line on the front wall under the mallet shelf for each guitar string, and
the balls rolling home along the floor lanes.

## Safety

- Glows on the walls and floor only come to full strength three times a second per glow; strikes
  in between top them up to a third. That cap is always on (`PipeRig ▸ Max Flashes Per Second`).
  There is no separate low-intensity mode: it was left out of this experience on purpose.
- Nothing flies at a face; the keep-out cylinder is enforced by the check. Throws to what stands
  beside the people pass them at the side, at least 0.3 m outside the cylinder, and stop at the
  volume's back edge, a quarter metre short of the open back.

## In the CAVE

- **Measure first.** The volume's numbers are from `MVR.vrx`. Measure the real wall height and room
  extents in MiddleVR coordinates and adjust `PipeRig ▸ Room` before trusting the cap and insets.
- **Latency.** If strikes land before their notes are heard in the room, raise
  `PipeRig ▸ Visual Delay` (seconds) to match the speakers' delay.
- **Speakers.** The machine's voices are panned by where each instrument stands; confirm the room's
  speaker layout carries a left-to-right pan.

## How it is put together

| File | What it is |
| --- | --- |
| `Scripts/PipeRig/PipeRig.cs` | The director: modes, clock, scheduling, the loop and the beat, voices, pointer, buttons, sign |
| `Scripts/PipeRig/RigTrajectory.cs` | `RigVolume`, the solver, and `RigPaths`: every path solved and checked once |
| `Scripts/PipeRig/RigScore.cs` | A track read ahead into notes, on a worker thread |
| `Scripts/PipeRig/RigBalls.cs` | The ball pool: every ball placed from the clock, trails, shadows, nozzles aimed |
| `Scripts/PipeRig/RigTarget.cs` | A bar, string, drum, cymbal, chime or gong: its hit point, pipes, hop and roll home, pointer box, and how it moves |
| `Scripts/PipeRig/RigPipe.cs` | A pipe: its mouth, and the nozzle that swings to the next throw and kicks as it fires |
| `Scripts/PipeRig/RigFlash.cs` | A rate-capped glow on a wall or the floor |
| `Scripts/PipeRig/RigVoices.cs` | The synthesised sounds: plucked bass, guitar and strings, mallet and glockenspiel bars, chimes, gong, kick, three toms, snare, hi-hat, crash |
| `Scripts/PipeRig/PipeRigMenu.cs` | The front-wall menu: the two modes and the tracks |
| `Scripts/Editor/PipeRigBuilder.cs` | Builds the scene; checks every path |
| `Art/Shaders/PipeRigShadow.shader` | The balls' soft floor shadows |

## Tuning

On the `PipeRig` component: gravity, the room volume, tempo, loop length and quantize, loudness,
visual delay, the least gap between throws at one target (0.09 s) and from one pipe (0.22 s), and
in visualize mode the accent margin (0.1) and least gap between doubled notes (0.3 s), the gap that
makes tom hits a fill (0.6 s), and the gong's share of the biggest drop (0.85) and rest (8 s). On each
`RigTarget`: its pipes, apex, hop, landing and roll home, pointer box and look. Rebuilding keeps the
`PipeRig` component's settings and re-lays everything under it.

## Open items

- Measure the wall height and room extents in MiddleVR coordinates and set the volume from them.
- Confirm the speaker layout and audio routing for the left-to-right pan, and set the visual delay.
- A track can also be scored exactly from a MIDI file instead of from its audio; not done.
- The partners' accent margin, the tom fill gap and the gong's share of the biggest drop are first
  guesses; tune them against the music folder once they have been heard and seen with real tracks.
