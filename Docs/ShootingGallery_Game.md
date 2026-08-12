# Shooting gallery round (ShootingGallery MiddleVR)

The playable round for the CAVE: a countdown, a targets-shot counter on the left wall, three
shoot-to-press buttons on the front kick panel, and a spawner that keeps moving targets coming.

## Build it

Open `Assets/_Project/Scenes/ShootingGallery MiddleVR.unity`, then run:

**VR Shooting Gallery ▸ Build ▸ Build CAVE Gallery Round**

`VR Shooting Gallery` is its own top-level menu in the Unity menu bar, not a Tools submenu.

It is idempotent — running it again refreshes what it made instead of duplicating it, and it never
overwrites a spawn area you have since tuned by hand. Everything it does is one Undo step.

To put the original hand-placed spheres back:
**VR Shooting Gallery ▸ Build ▸ Restore Original Static Targets**

## What ends up in the scene

| Object | Where | What it does |
| --- | --- | --- |
| `Game_Systems` | scene root | `MatchService` + `MatchConfig_CaveGallery` (TimeAttack, 60 s) |
| `CAVE_User_Room/HUD_Scoreboard` | left wall, x −1.74, facing +X | `HUDController`: targets hit, countdown, score, status line |
| `CAVE_User_Room/Match_Controls` | flat on the floor at z 0.65, just ahead of the player's feet | START / RESTART / QUIT tiles on a slab, facing up |
| `Shooting_Gallery_Wall/Target_Spawner` | gallery wall | `TargetSpawner`, area derived from the original sphere layout |
| `Assets/_Project/Prefabs/Targets/Target_Moving.prefab` | — | sphere + kinematic body + `Target_MovingSphere` |

The hand-placed spheres are switched off, not deleted — the spawner owns the wall while a round runs.

## How a round plays

1. Idle: the wall is empty, the board reads `SHOOT START TO PLAY`.
2. START spawns `m_InitialCount` targets and starts the 60 s clock.
3. Shooting a target flashes it black, shrinks it out, bumps the counter, and queues a replacement
   `m_RespawnDelay` later. Independently, the spawner tops the wall back up to `m_MaxAlive` on a timer.
4. As the clock runs down targets get smaller, faster and more frequent (`m_RampUp`).
5. At 0:00 the wall clears and the board shows the final tally. START or RESTART goes again.

## Buttons

`ShootableButton` implements `IShootable`, so the console reuses the projectile pipeline — no pointer
or UI raycaster needed, which is what makes it work in the CAVE.

The console lies flat on the floor rather than on the kick panel. On the panel it sat inside the arc
of any low shot at the gallery; on the floor you have to point the gun at your own feet to reach it.
Sanity check: a shot from the muzzle at the *lowest* gallery target passes over the console at about
0.77 m, and the tallest part of a button collider is 0.10 m.

All three buttons are single-shot. QUIT briefly required a confirming second shot, back when the
console sat on the kick panel in the firing line; on the floor with a semi-auto gun nothing strays
there, and the silent arming step just read as a dead button. `m_ConfirmRequired` on any
`ShootableButton` brings it back — and it now logs when it arms, so it can never be silent again.

`RunAction` logs every press. If a button appears dead, that line tells you whether the shot ever
reached it — no log means a physics problem, a log with nothing happening means a wiring problem.

## Colours

The builder repaints the two spaces so they read as different rooms and the HUD stays legible:

| Material | Applied to | Colour |
| --- | --- | --- |
| `M_CAVE_Room` | every wall of `CAVE_User_Room` | near-black cool `#1A1D23`, smoothness 0.15 |
| `M_CAVE_Panel` | `Front_Wall_Pannel_Bottom`, floor console slab | `#2C313B` |
| `M_Gallery_Backdrop` | the walls of `Shooting_Gallery_Wall` | warm ochre `#9A5B2D`, smoothness 0.2 |

The two spaces are separated on **both** axes: value (near-black vs. mid-tone) and hue (cool vs. warm,
close to complementary). A cool-blue gallery read as "same room, slightly different grey"; warm
against near-black does not.

Matte matters as much as hue: a glossy wall throws specular glare across the CAVE screens and washes
the text out. Targets and the button faces keep their own materials. The `Ground` plane is a separate
scene root and is left alone.

## Text facing — read this before moving any label

TMP text reads correctly when its **forward points away from the viewer** (a default-rotation TMP
object is read by a camera sitting on its −Z side). Get it backwards and every label renders
mirrored, which is easy to miss at a glance.

- Scoreboard: player is at +X of the left wall → board faces −X, `Euler(0, -90, 0)`.
- Buttons: player is at −Z of the kick panel → button faces +Z (into the panel), `Euler(20, 0, 0)`.
  That is also the direction the button travels when pressed, and it puts the label at local −Z.

## Tuning

Everything lives on two components:

- `TargetSpawner` — spawn area, population, cadence, difficulty ramp, motion mix. The spawn area
  draws as a green wireframe when the spawner is selected.
- `Target_MovingSphere` — per-target motion, speed, path size, lifetime, vanish timing. The spawner
  overwrites these per spawn; edit the prefab only to change what an unspawned target looks like.

## TMP font asset repair

This project's `LiberationSans SDF.asset` was imported with its `material` reference unassigned. TMP
dereferences that field in `TextMeshPro.Awake`, so every text object throws, ends up disabled with no
material, and draws nothing — with no visible clue beyond `UnassignedReferenceException` spam in the
Console. The builder detects this and repoints the font at the `LiberationSans SDF Material`
sub-asset already sitting in the same file. It logs when it does.

## Gun feel

`GunController.m_SemiAuto` is **on**: one round per trigger pull. It reads
`IGunInputSource.FirePressedThisFrame` (the press edge) rather than `FireHeld`, which is what stops a
held trigger spraying and what makes a release take effect on the same frame — there is no lingering
"still held" state to let another round through. Turn it off for full auto.

The press edge is latched inside each input source, not taken from the underlying API:

- `MiddleVRWandInputSource` — the wand is sampled on the cluster's cadence, not Unity's, so MiddleVR's
  own "toggled" flag can stay set across more than one frame and fire twice from one pull. Polling is
  lazy (on first read, not in `Update`) so the edge stays frame-accurate regardless of script
  execution order. The first sample is swallowed, so a trigger already held at scene load cannot let
  a round off before the player touches anything.
- `ControllerTriggerInputSource` — Schmitt trigger on the analog pull (`m_PressPoint` /
  `m_ReleasePoint`), so a trigger resting on the threshold cannot chatter out extra rounds.

Other settings on `Gun_MiddleVR` / `Projectile_FoamDart`:

- `m_FireCooldown` **0.05 s** — in semi-auto this is only a debounce; the pull rate is the limit.
- `m_LaunchSpeed` **18 m/s**. 25 was invisible (the round crossed the gallery in under a tenth of a
  second); 14 was watchable but soft. 18 sits between them. Gravity is off, so changing this does not
  move the point of impact away from the aim dot.
- `Projectile_FoamDart` **Use Gravity off**. The aim dot comes from a straight raycast, so once the
  round is slow enough to watch, a ballistic arc makes the dot lie about where the shot lands.

## The dart's collider — read this before touching Projectile_FoamDart

`Projectile_FoamDart` shipped with a **CapsuleCollider `m_Height: 1`, `m_Direction: 1`** on its root:
a one-metre pole on the Y axis, while the visible dart is the child mesh at 8 cm long. `Projectile`
aims the nose with `LookRotation(velocity)`, which puts local **+Z** along the flight path — so the
pole flew **broadside**, sticking half a metre out either side of the dart.

Wall targets sit at y ≈ 1.0–1.8, so the tip stayed clear of the floor and they always registered.
Anything near the floor did not: the tip entered the `Ground` collider first, `OnCollisionEnter` fired
against Ground, and `Release()` killed the round short of the button. At the old kick-panel height of
y 0.5 the tip sat at *exactly* y 0, so both console positions were dead for the same reason.

It is now a `SphereCollider` of radius 0.02, matching the dart you can see. If you ever swap the
projectile prefab, check that its collider matches its mesh and is not axis-elongated.

## Physics note — hits are resolved by sweep, not by contact

A collision event only fires when a round happens to *end* a physics step inside a collider. At
18 m/s it covers 0.36 m per step, so anything shallower gets stepped straight over — a 0.14 m ball is
missed roughly six times in ten, and a button plate essentially always. A 1 m target sphere is the
only thing big enough for contacts to be reliable, which is why targets appeared to work while
nothing else did.

`Projectile.m_SweepForHits` (on by default) therefore makes the swept ray **authoritative for every
hit**, not just shootable ones:

- The segment spans the gap since the last step *and* the step about to be taken, so nothing slips
  between steps and nothing is reported after the round has already flown past.
- It stops at the **first** collider, so a round cannot pass through a wall — and, being an exact
  line, a round threaded past one object really does carry on to whatever is behind it.
- `IShootable` gets `OnShot`. Any non-kinematic `Rigidbody` gets the round's momentum applied by hand
  (`mass × speed`), because the sweep retires the round before PhysX would have delivered that
  impulse itself. That is what makes loose scene objects such as the tethered balls react.

With the sweep on, the round uses **Discrete** collision detection. Speculative CCD is worse than
useless here: its contacts are approximate, so a round can be stopped by something it visibly flew
past. With the sweep off it falls back to `ContinuousSpeculative`, the best available contact mode.

`OnCollisionEnter` survives as a backstop for the sweep-off case. It checks for `IShootable` before
releasing, since when a round ends a step touching two colliders Unity delivers one callback per
collider in arbitrary order and releasing on the first would drop the shot.

## Backdrop placement

The player looks down +Z at the gallery, so anything at a smaller local z than the target plane
stands in front of the targets and hides the playfield. `Shooting_Gallery_Wall/Front_Wall` shipped at
local z −0.422 with the targets at −0.086, which hid all of them.

`PushOccludersBehindTargets` moves any such wall to `targetPlaneZ + k_BackdropGap` (0.9 m behind the
targets) and logs what it moved. Side walls run alongside the targets and are left alone. Change
`k_BackdropGap` if you want more or less depth in the box.
