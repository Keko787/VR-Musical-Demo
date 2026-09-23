# Holographic harp (Harp MiddleVR)

The harp from the CAVE music deck (slide 13): 22 glowing strings on one plane, standing inside the
room a forearm in front of the tracked user, played by sweeping the wand through them. A string
sounds when the wand's tracked point crosses it, swings visibly, flashes, and throws a pulse of its
colour onto the walls and floor — the response the deck asks for as "displacement plus a light pulse:
feedback that reads without glasses".

It is one handed because the room has one wand, and the strings sit on a single plane because that
is the geometry that maps into the tracked volume and stays in reach. Both are straight from the deck.

## Build it

| Menu item | What it does |
| --- | --- |
| **VR Shooting Gallery ▸ Harp ▸ Create Harp Scene** | Copies the physics scene to `Harp MiddleVR.unity`, strips the tether demo and the gun out of it, and builds the harp |
| **VR Shooting Gallery ▸ Harp ▸ Rebuild Harp** | Rebuilds the harp in whatever scene is open, as long as it has a `CAVE_User_Room` |

The new scene starts as a copy of `Physics MiddleVR.unity` for the same reason the
[marble run](MarbleRun_Demo.md) does — that scene already carries the whole MiddleVR side of the setup,
and rebuilding it is [five chances to get it subtly wrong](MiddleVR_CAVE_Setup.md). The copy is saved
before anything is deleted from it. Create also adds the scene to **File ▸ Build Settings**, switched off.

Everything the builder makes lives under `CAVE_User_Room/Holographic_Harp`, plus one root object
`Wand_Plectrum` for the wand tip (shared with the [guitar](HolographicGuitar_Demo.md)), and is
registered with Undo. A rebuild clears the harp's children, so hand-tuning *inside* it is lost — tune
the constants at the top of `HolographicHarpBuilder.cs` and rebuild, or tune the Inspector fields on
`HolographicHarp` and each `PluckedString`, which survive.

The harp root's own **position and rotation are yours**. Move it to suit the player and rebuild;
it stays put. Scale is forced back to one because the string lengths and swing amplitudes are metres
in the harp's own space.

The builder owns its materials (`M_Harp_String`, `M_Harp_String_C`, `M_Harp_String_F`,
`M_Harp_Frame`, `M_Wand_Plectrum`) and re-authors them on every build — the harp's look is defined by
the constants, and a material left over from an earlier palette is a bug, not a tweak. It also owns
the string mesh, `Art/Meshes/HarpString.asset`, rewritten in place so every string follows a rebuild.

## Playing it

Sweep the wand through the strings. A single string plucks on a touch; a sweep across several is a
glissando. Faster is louder: a slow touch plays at 35% and 2.5 m/s and above is full strength
(`m_SoftestPluck`, `m_FullPluckSpeed`).

No tracker? The keyboard plays it: the **number row** is strings 1–10 (C3 up to E4), **Q–P** the next
ten, and **A** and **S** the last two (A–L are mapped, so a longer scale gets more keys). This is on
in builds as well as the editor (`m_KeyboardPlay`), so the room's audio routing can be checked from
the server node without anyone picking up the wand — the deck lists confirming speaker layout and
routing as an open item.

### Tuning

Three octaves of C major, C3 to C6, bass on the left as the player faces it. Real harps colour their
C strings red and their F strings blue so a player can find their place among forty of them; these
do the same in hologram cyan, rose and indigo.

| String | Note | Hz | Length | Rings for |
| --- | --- | --- | --- | --- |
| 0 | C3 | 130.8 | 1.28 m | ~4.2 s |
| 7 | C4 | 261.6 | 1.05 m | ~2.9 s |
| 14 | C5 | 523.3 | 0.79 m | ~2.3 s |
| 21 | C6 | 1046.5 | 0.48 m | ~1.8 s |

Pentatonic makes anything a visitor sweeps sound good; it is a one-line change to `k_MajorScale`
(`{ 0, 2, 4, 7, 9 }`), and the string count follows.

## How a pluck is found

The test is **geometric, not physical**, and lives in `StringSweep` (`Scripts/Strings/`), which the
guitar uses too. Every frame it takes the tip's path since the last frame as a line segment and
measures it against each string's segment (closest points between two segments — Ericson, *Real-Time
Collision Detection* §5.1.9). A string is caught when that distance drops inside `m_CatchRadius`
(2 cm) plus the string's own 4 mm.

A trigger collider would not do. It only sees the tip where the physics step happens to sample it,
and a wand moving at 3 m/s crosses a 4 mm string in under two milliseconds — most plucks would fall
between two 50 Hz ticks. The sweep never misses, costs nothing, and runs in `LateUpdate` on the same
frame's tracking data the walls are about to draw.

Two things stop it re-triggering:

- **Hysteresis.** Once caught, a string is not released until the tip is `m_ReleaseRadius` (4 cm)
  away from it. A wand held against a string jitters by a few millimetres; without this it would
  re-pluck every frame.
- **Cooldown.** `m_Cooldown` (80 ms) per string, on top. This is the deck's "per string cooldown".

A tip movement longer than `m_MaxSweep` (1.5 m) in one frame is a tracking dropout or the wand being
picked up, and is ignored rather than strummed through every string in between.

A fast glissando crosses several strings in one frame. The sweep reports *where along the frame's
movement* each string was met, and the harp schedules them on the audio clock at those offsets
(`AudioSource.PlayScheduled`) in the order the tip met them — so a run stays a run instead of landing
as one chord on the next audio buffer. The first string of the frame still plays as soon as possible;
only the ones after it wait, by at most a frame.

### The tip

`Wand_Plectrum` is a root object carrying `MVRAttachToNode (HandNode)`, exactly as the gun prefab
does — MiddleVR reparents it under the tracked wand once the kernel is up. Its root position is the
offset it keeps from the wand's tracked origin, and it is authored at **zero**, so the tracked point
itself plucks; a small bright sphere marks it. If a tip out in front of the wand feels more natural,
move the object (the gun's muzzle sits at `(−0.141, 0.035, −0.015)` in the same node, for reference)
— but check the wand's axes in the room first, because they are whatever the `.vrx` says they are.

## The sound

There are no recordings. Each string's clip is **synthesised at scene load** by `PluckedStringSynth`
(`Scripts/Strings/`, shared with the guitar) with the Karplus–Strong loop: a burst of noise into a
delay line exactly one period long, averaged with its neighbour on every pass. The average is a low-pass, so the upper harmonics die first and the
burst settles into a tone that mellows as it rings, which is what a string does.

Three touches make it a harp rather than a guitar:

- The excitation is low-passed before it goes in, so the attack is a thumb rather than a pick.
- A comb filter for the pluck position (28% along the string) hollows out the harmonics with a node
  there. Harpists play nearer the middle of the string than guitarists — the guitar uses the same
  synth at 15% and sounds like a different instrument for it. Both are per-string fields on
  `PluckedString` (`m_PluckPosition`, `m_Tone`).
- The loop's loss is derived per string from a target ring time (`k_BassRing` 5 s down to
  `k_TrebleRing` 2.2 s), rather than being whatever falls out of the filter. The averaging filter eats
  another 15–20% off that in practice, which is why the table above reads shorter than the constants.

The fractional part of the period is read by interpolation. Rounding the delay to whole samples would
put the top strings up to a quarter of a semitone out; as built, every string measures within
0.1 cent of equal temperament.

Each string has its own `AudioSource` and uses `Play`, not `PlayOneShot`: a string plucked while still
ringing restarts, the way a real one does when the finger lands on it again. Volume follows pluck
strength. The sources are 2D (`m_SpatialBlend` 0) with a stereo pan across the harp
(`m_StereoSpread`): the audio listener sits on the template camera at the room's origin, not on the
tracked head, so positional audio would place the harp relative to the wrong point.

### Latency

The deck's risk on this demo is that latency is audible on a plucked instrument. The path here is:
tracking → `LateUpdate` sweep → `AudioSource.Play` on an already-rendered clip. Nothing is loaded,
decoded or scheduled at pluck time.

The one project-level change made for it: **`ProjectSettings/AudioManager.asset` DSP buffer is now
256 samples** ("Best latency"), down from 1024. At 48 kHz that is 5 ms per buffer instead of 21 ms.
No other demo plays audio, so nothing else is affected; if the CAVE's audio device stutters at 256,
512 is the next stop.

## The response

Each `PluckedString` is a thin tube mesh, one unit long on its own Y axis with the real length in the
transform's scale, so one shared mesh serves all 22. On a pluck:

- **Displacement.** The vertices bend out of the harp's plane in a half-sine (the fundamental mode),
  2.5 cm at the middle on a full-strength pluck, at 8–13 Hz, decaying over roughly a second. This is
  far larger and far slower than a real string — at true scale the motion is a blur too fine to read
  from across a room, and anything above about 15 Hz just strobes on a projector. The first swing goes
  the way the wand was moving.
- **Glow.** The string's emission jumps from a rest glow of 0.5× its colour to 5× and falls back,
  via a `MaterialPropertyBlock` so the shared material is untouched. The materials are authored with
  `_EMISSION` on and the rest glow baked in, so the harp looks the same in the editor as idle in play.
- **Room pulse.** One real point light, `Pulse_Light`, jumps to the plucked string and flashes its
  colour at intensity 8 with a 0.35 s fall, standing 0.25 m off the plane on the player's side. This
  is the part that reads **without stereo glasses**: it lands on the physical walls and floor as
  2D surface content, which is the layer the deck says scales to an audience.

The strings use real tube geometry rather than a `LineRenderer` for the reason given in
[the tethered ball notes](TetheredBall_Physics.md): a LineRenderer billboards toward the camera, which
reads wrong under off-axis stereo projection.

## Where it stands

The string plane is at **z 0.5 m**, half way from the room's centre to the front wall at 1.11 m —
a forearm from someone standing in the middle of the floor, inside what the deck calls arm's reach.
Strings run vertically, 5.5 cm apart (a real harp's 1.4 cm is far too tight for a wand held at arm's
length with a 2 cm catch radius), 1.155 m across in all, centred on x 0.

Bottoms sit on a soundboard rising from 0.62 m at the bass to 1.0 m at the treble; tops follow a neck
falling as a parabola from 1.9 m to 1.48 m — flat beside the pillar, dropping fastest at the treble,
the way a harp's harmonic curve does. Every string's middle lands at chest height. The pillar tops out
at 1.95 m, under the 2.2 m ceiling. The frame — pillar, soundboard, neck slabs, foot — is translucent
glowing geometry with no colliders; it is there so the strings hang from something and the whole
reads as a harp in silhouette from anywhere in the room.

## How it is put together

```
CAVE_User_Room/Holographic_Harp        HolographicHarp — sweep test, cooldowns, pulse light, keyboard
├── Frame/
│   ├── Pillar, Foot, Soundboard       translucent slabs, no colliders
│   └── Neck_00 … Neck_22              one slab per string top, plus the pillar join and overhang
├── Strings/
│   └── String_00_C3 … String_21_C6    PluckedString + MeshFilter (shared tube) + MeshRenderer + AudioSource
└── Pulse_Light                        point light, 3.5 m range, idle at 0

Wand_Plectrum                          root; MVRAttachToNode (HandNode); Tip sphere
```

`HolographicHarp` has `DefaultExecutionOrder(-50)` so its `LateUpdate` runs before every
`PluckedString`'s: a pluck it finds is drawn on the string the same frame it is heard.

The pieces the guitar shares — `PluckedString`, `PluckedStringSynth`, `StringSweep` — live in
`Scripts/Strings/`; the editor side shares `StringInstrumentBuilder` (scene-from-template flow, the
plectrum, the tube mesh, emissive materials, TMP signs).

## Tuning

Layout, tuning, ring times, swing rates and the palette are constants at the top of
`Assets/_Project/Scripts/Editor/HolographicHarpBuilder.cs`; rebuild after editing. The catch and
release radii, cooldown, pluck strength curve, pulse light and stereo spread are Inspector fields on
`HolographicHarp` and survive a rebuild, as do the per-string response fields on `PluckedString` — but
note the builder writes the per-string ring time, swing decay and swing rate on every build, so those
three are constants, not Inspector knobs.
