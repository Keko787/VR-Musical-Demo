# Holographic guitar (Guitar MiddleVR)

A six-string guitar hung in the air in front of the tracked user, neck to their left, low E at the
top — a guitar on a wall — that the wand strums. The room has one wand, so the fretting hand is the
software's: a progression of chord shapes, each on its own wand button, drawn on the neck as dots
with the chord's name above. Sweep the wand down across the strings and whichever chord is up
sounds, each string at its fretted pitch.

It is the harp's machinery — the same strings, the same synth, the same sweep — with a fretboard on
the front and one thing the harp does not need: a strum has to keep its rake.

## Build it

| Menu item | What it does |
| --- | --- |
| **VR Shooting Gallery ▸ Guitar ▸ Create Guitar Scene** | Copies the physics scene to `Guitar MiddleVR.unity`, strips the tether demo and the gun out of it, and builds the guitar |
| **VR Shooting Gallery ▸ Guitar ▸ Rebuild Guitar** | Rebuilds the guitar in whatever scene is open, as long as it has a `CAVE_User_Room` |

Same flow as the [harp](HolographicHarp_Demo.md) and the [marble run](MarbleRun_Demo.md): the scene
starts as a copy of `Physics MiddleVR.unity` because that scene already carries the whole MiddleVR
setup, the copy is saved before anything is deleted from it, and Create adds it to **File ▸ Build
Settings** switched off.

Everything lives under `CAVE_User_Room/Holographic_Guitar` plus the shared `Wand_Plectrum` root, all
registered with Undo. A rebuild clears the guitar's children; the `HolographicGuitar` component on
the root is reused, so **the chord list and everything else in its Inspector survive a rebuild**.
Layout, tuning, timbre and palette are constants at the top of `HolographicGuitarBuilder.cs`.

The guitar root's **position and rotation are yours**. It is built flat, strings exactly horizontal,
so a vertical sweep is a clean strum; tilt the neck up (rotate the root about Z by −20° or so) if a
playing angle reads better for the audience, and it stays that way through rebuilds.

## Playing it

**Pick a chord, then strum.** Chord selection has two modes (`m_Selection`):

- **Direct** (default) — every chord has its own wand button (`Button` on each chord in the list).
  Press it and that chord is up. The progression sign under the body shows the button number in
  front of each chord name, so a visitor handed the wand can see which button plays what.
- **Step** — `m_NextChordButton` (1) and `m_PreviousChordButton` (2) walk the progression, wrapping.

Then sweep the wand across the strings. Down (low E to high) is a downstroke, up is an upstroke;
faster is louder (`m_SoftestPluck` 40% up to full at `m_FullPluckSpeed` 2 m/s). The plucked strings
swing and flash, the chord's colour pulses on the walls, and a muted string — one the chord X's
out — still bends when the wand hits it, so it reads as touched, but makes no sound.

At a desk with no tracker: **↑ strums down, ↓ strums up**, **← →** step through the chords in either
mode, and the **number-row keys** stand in for wand buttons with the same digit. This stays on in
builds (`m_KeyboardPlay`) so the room's audio routing can be checked from the server node.

### The progression

The five shapes from the chord finder, exactly as it drew them — top row is the high E string, X is
a muted string. Low E first, `-` muted:

| Button | Name | Frets | Notes | The finder called it |
| --- | --- | --- | --- | --- |
| 1 | **A** | `- - - 2 2 -` | A C♯ | A |
| 2 | **D/A** | `- - - 2 3 2` | A D F♯ | A6sus (with a warning) |
| 3 | **Asus4** | `- - - 2 3 0` | A D E | Asus |
| 4 | **E7sus4** | `- - 2 2 3 0` | E A D E | E7sus |
| 6 | **E9sus4** | `- - 2 2 3 2` | E A D F♯ | Esus9 |

The names are the conventional ones; the finder's are listed so the two line up. Button 5 is
skipped because the [VisBox wand has no index 5](MiddleVR_Controller_Input.md) — its live buttons
are 0–4, 6–8, 11 and 15, with 0 the trigger. Buttons are per chord in the Inspector, so remap freely
once the probe has confirmed the indices in the room.

The list is an ordinary Inspector array of `GuitarChord` (`Name`, `Frets[6]`, `Button`). Add a chord,
change a shape, reorder — no rebuild needed, and no recordings either: every note a chord can ask of
a string is synthesised when the scene loads.

## How a strum is found, and why it stays a strum

Detection is the harp's `StringSweep`: the tip's path since the last frame, measured against every
string as a segment, with a 2 cm catch radius, 4 cm release and 80 ms per-string cooldown. See the
harp notes for why it is geometric rather than a trigger collider.

What is new is timing. A strum crosses all six strings — 25 cm — in a frame or two. If each string
simply played on the next audio buffer, the whole chord would land at once and every strum would
sound like a hit. The sweep reports *where along the frame's movement* each string was met, and the
guitar schedules each string on the audio clock at that offset (`AudioSource.PlayScheduled`), in the
order the tip met them. The first string plays as soon as possible; the rest follow by their share of
the frame — a 16 ms rake at 60 Hz, which is about what a real pick takes. Nothing waits longer than a
frame, so the latency story is the harp's: tracking → `LateUpdate` → a pre-rendered clip on a 256
sample DSP buffer.

## The sound

Same Karplus–Strong synth as the harp (`PluckedStringSynth`), tuned to a steel-string guitar:

- **Pick position 15%** of the string, not the harp's 28%. The comb filter this puts on the attack
  keeps the upper harmonics a harp's pluck position removes, which is most of the difference between
  the two instruments' sounds before the body is even considered.
- **Brighter attack** (`m_Tone` 0.6 on the wound bass strings up to 0.85 on the plain trebles).
- **Longer ring**: 6 s on the low E down to 3.5 s on the high E.
- Standard tuning, E2 A2 D3 G3 B3 E4.

Each string renders its own open note at load. The guitar renders every *fretted* note the
progression can ask for — keyed by string as well as pitch, because the same note on a wound string
and a plain one is not the same sound — and hands the right clip to the string on each pluck. Five
chords need only six distinct string/fret clips, so this is a few tens of milliseconds at load.
Everything stays within 0.1 cent of equal temperament, as measured on the harp.

Strings are 2D (`m_SpatialBlend` 0) with a narrow stereo spread (`m_StereoSpread` 0.2 — one
instrument, not the harp's row of them). `Play`, not `PlayOneShot`, so a re-strummed string restarts
the way a real one does.

## What it looks like

Everything is in the guitar's own metres, string plane at z 0, the player on the −Z side:

- **Scale length 1.2 m**, nut to bridge — about double a real guitar's, because the strings need
  **5 cm** between them for a wand (a real guitar's 1.1 cm would be one catch radius wide) and the
  neck has to stay in proportion. Twelve frets are drawn on a twelfth-root-of-two scale, with inlays
  at 3, 5, 7, 9 and a pair at 12, a nut, and a headstock with six pegs.
- **Body**: two overlapping flattened cylinders — an upper bout and a larger lower bout, so there is
  a waist — with a dark soundhole disc and a bridge. Translucent hologram cyan, sitting just behind
  the string plane so the strings and chord markers always draw in front of it.
- **Strings**: the three wound bass strings glow amber, the three plain trebles silver-white, which
  is also how they look on a real guitar. Same tube mesh and bend as the harp.
- **Chord markers**: six orange spheres (the chord finder's colour), one per string, moved on every
  chord change to the middle of the fretted fret space, parked just behind the nut for an open
  string, and hidden for a muted one. The muted string itself dims to a quarter of its glow.
- **Signs**: the chord name in large text over the body; the whole progression underneath with the
  current chord picked out in gold and, in Direct mode, each chord's button number in front of it.
- **Room pulse**: a point light in front of the body that flashes the plucked string's colour on the
  walls and floor — the part of the response that reads without stereo glasses.

The nut sits at room (−0.5, 1.25, 0.5): chest height, half way to the front wall like the harp, and
left of centre so the body — where the strumming happens — lands in front of someone standing in the
middle of the floor. The whole instrument spans about 1.65 m, headstock to lower bout.

## How it is put together

```
CAVE_User_Room/Holographic_Guitar      HolographicGuitar — chords, sweep, scheduling, signs, pulse
├── Body/                              Upper_Bout, Lower_Bout, Soundhole, Bridge
├── Neck/                              Fretboard, Nut, Fret_01 … Fret_12, inlays, Headstock, pegs
├── Strings/                           String_0_E2 … String_5_E4  (PluckedString + AudioSource)
├── Chord_Dots/                        Dot_0_E2 … Dot_5_E4
├── Pulse_Light                        point light, 3 m range, idle at 0
├── Chord_Name                         TextMeshPro
└── Progression                        TextMeshPro, rich text

Wand_Plectrum                          root; MVRAttachToNode (HandNode); Tip sphere — shared with the harp
```

The string tubes are the harp's mesh turned to lie along the guitar's X (rotated −90° about Z);
their local Z, the direction they bend, is still the plane's normal. `HolographicGuitar` has
`DefaultExecutionOrder(-50)` so its `LateUpdate` — and therefore `SetChord`, which mutes strings
and moves dots — runs ahead of every `PluckedString`'s.

## Tuning

Chords, buttons, selection mode, catch/release/cooldown, strum strength, stereo, pulse light and the
sign colours are Inspector fields on `HolographicGuitar` and survive a rebuild. Geometry, tuning,
ring times, pick position, tone and the palette are constants in `HolographicGuitarBuilder.cs`;
rebuild after editing. The builder writes ring time, swing decay and swing rate onto each string on
every build, so those three are builder constants rather than per-string Inspector knobs.
