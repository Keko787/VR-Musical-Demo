# Tethered ball (Physics MiddleVR)

A weight on a cord hung from the CAVE room's ceiling — the garage parking-guide ball that taps the
windscreen when the car is far enough in. It swings, it collides, and the gun in the scene can knock
it about.

## Build it

Open `Assets/_Project/Scenes/Physics MiddleVR.unity`, then run either of:

| Menu item | What it does |
| --- | --- |
| **VR Shooting Gallery ▸ Physics ▸ Build Tethered Ball** | Rebuilds every rig, each keeping its own geometry |
| **VR Shooting Gallery ▸ Physics ▸ Rebuild Tethered Balls From Placeholder** | Re-reads the placeholder sphere and gives every rig that same geometry |

Both act on **every** `Tether_Rig*` in the room, so a grid of duplicates stays consistent. Each rig's
own position and rotation are left alone — only its contents are rebuilt, because the chain length is
baked into the joint anchors and patching one in place would be more fragile than making a fresh one.
Scale is forced back to one, since the cord visuals are sized in the rig's own space.

Any hand-tuning *inside* a rig is lost on rebuild. Tune on the components listed below instead.

## Swing and bounce

Damping is deliberately near zero. Every one of the twelve links rotates as the pendulum swings, so
angular drag on the links is multiplied twelve times over — at the original 0.5 it quietly ate the
arc, and it was the single biggest thing stopping the ball swinging far. Links are now `drag 0`,
`angularDrag 0.05`; the ball is `drag 0.01`, `angularDrag 0.05`.

Bounce lives on the ball, in `PM_TetherBall.physicMaterial`. Nothing in the room carries a physic
material of its own, so the ball's restitution only survives a contact because `bounceCombine` is
**Maximum**. `frictionCombine` is **Minimum** so the ball glances off a wall instead of gripping it
and killing the swing. The room walls are zero-thickness boxes, which is why the ball uses
`ContinuousSpeculative` — without it a fast swing passes straight through one.

### What it can and cannot reach

A pendulum can never get farther from its anchor than `cord + ball diameter`. With the 1.12 m cord
that is 1.26 m, measured from the anchor at (0.025, 2.196, −0.118):

| Wall | Distance | Reached? |
| --- | --- | --- |
| `Front_Wall` | 1.23 m | yes — contact at 76.5° from vertical |
| `Back_Wall` | 0.99 m | yes, but that wall is switched off in the scene |
| `Right_Wall` | 1.75 m | no |
| `Left_Wall` | 1.80 m | no |

Reaching the front wall needs 4.24 m/s at the bottom of the swing. A square dart hit delivers
0.02 kg × 18 m/s ÷ 0.06 kg = 6.0 m/s, so a good shot gets there with room to spare and a glancing one
does not — which makes hitting the wall a reward rather than a given.

### Shooting the ball

The ball has no script on it and is not `IShootable`. It reacts because `Projectile`'s swept ray
hands its momentum to any non-kinematic `Rigidbody` it hits. That matters when several rigs are in
the scene: the sweep is an exact line stopping at the first collider, so a shot threaded past one
ball genuinely carries on to the one behind it. Relying on PhysX contacts instead — as this did
originally — meant a 0.14 m ball was missed roughly six times in ten, and speculative contacts could
stop a round on something it had visibly flown past. See the physics note in
`ShootingGallery_Game.md`.

The cord has no colliders, so shots pass through it. Only the ball can be hit.

Reaching the **side** walls would need a 1.66 m cord, which drops the ball from y 1.00 to y 0.46 —
knee height, a wrecking ball rather than a garage guide ball. That trade-off is why the cord is where
it is; change `k_FallbackBallHeight`/the placeholder height and rebuild if you want the other one.

## Where it hangs

The builder reads the scene rather than hard-coding a spot:

- **Anchor** — straight up from the placeholder sphere already parented to `CAVE_User_Room`, at the
  height of the `Ceiling` child (y 2.196).
- **Cord length** — whatever puts the ball's centre exactly where that placeholder sat (y 1.004), so
  the ball lands where the scene already said it should. Falls back to 1.05 m if there is no
  placeholder.
- The placeholder is switched **off**, not deleted — the new ball now occupies that space.

"Placeholder" means any child of `CAVE_User_Room` named `Sphere*` with a `SphereCollider` and no
`Rigidbody`. An **active** one wins outright; otherwise the one nearest to directly under the
ceiling's centre does. That second rule matters because the first build switches the marker off, and
because the room contains other stray spheres — of the four in this scene the marker sits 0.11 m from
the ceiling centre and the rest are 1.7–2.0 m away, so it is picked unambiguously.

Plain **Build** does not re-measure. It reuses each rig's existing geometry — the anchor from its own
transform, the cord length from `TetheredBall.m_CordLength`, which the builder records for exactly
this reason — so rebuilding never silently moves a ball. Use **Rebuild From Placeholder** when you
have moved the marker and want every rig to follow it.

## How it is put together

```
CAVE_User_Room/Tether_Rig
├── Anchor              kinematic body, flush with the ceiling
├── Cord_00 … Cord_11   12 links, 0.02 kg each, no colliders
├── Tethered_Ball       0.06 kg sphere, 0.14 m across
└── Cord_Visual_00 …    13 cylinders, one per gap between nodes
```

Each link is pinned to the one above with a `ConfigurableJoint`: linear motion **locked**, angular
motion **free**. That is what makes it behave like cord rather than a spring. `enablePreprocessing`
is off — preprocessing lets PhysX "help" badly-conditioned joints, which on a chain shows up as links
snapping to odd poses — with joint projection pulling any drift back instead.

The links carry no colliders. PhysX therefore has no shape to derive an inertia tensor from and picks
one that makes a light link feel oddly resistant to twisting, so the builder writes a small explicit
tensor to keep the cord limp.

## Two settings that do not live where you would expect

- **Solver iterations** are applied by `TetheredBall.Awake`, not by the builder.
  `Rigidbody.solverIterations` is not a serialized field, so anything set in the editor is discarded
  and PhysX falls back to the project default of 6 — which lets a light chain under a heavier weight
  visibly stretch. `m_SolverIterations` on the `TetheredBall` component is the real knob.
- **The cord is real cylinder geometry**, not a `LineRenderer`. A LineRenderer billboards toward the
  camera, which reads wrong in a CAVE where the projection is off-axis and each eye sees a different
  view of the same cord.

## Resetting

Wand button 4 re-hangs everything on the plumb line and kills all motion
(`TetheredBall.m_ResetButton`, negative to disable). `ResetTether()` is public, so a
`ShootableButton` or any other trigger can call it too.

## Tuning

Everything worth changing is a constant at the top of
`Assets/_Project/Scripts/Editor/TetheredBallBuilder.cs`: segment count, ball radius and mass, link
mass, cord thickness. Rebuild after editing. Damping and interpolation are ordinary serialized
`Rigidbody` fields, so those can be tweaked in the Inspector and will survive until the next rebuild.
