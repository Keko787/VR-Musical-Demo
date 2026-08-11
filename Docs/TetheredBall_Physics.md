# Tethered ball (Physics MiddleVR)

A weight on a cord hung from the CAVE room's ceiling — the garage parking-guide ball that taps the
windscreen when the car is far enough in. It swings, it collides, and the gun in the scene can knock
it about.

## Build it

Open `Assets/_Project/Scenes/Physics MiddleVR.unity`, then run:

**VR Shooting Gallery ▸ Physics ▸ Build Tethered Ball**

The rig is torn down and rebuilt on every run. The chain length is baked into the joint anchors, so
patching an existing chain in place would be more fragile than making a fresh one — meaning any
hand-tuning inside `Tether_Rig` is lost on rebuild. Tune on the components listed below instead.

## Where it hangs

The builder reads the scene rather than hard-coding a spot:

- **Anchor** — straight up from the placeholder sphere already parented to `CAVE_User_Room`, at the
  height of the `Ceiling` child (y 2.196).
- **Cord length** — whatever puts the ball's centre exactly where that placeholder sat (y 1.004), so
  the ball lands where the scene already said it should. Falls back to 1.05 m if there is no
  placeholder.
- The placeholder is switched **off**, not deleted — the new ball now occupies that space.

"Placeholder" means any active child of `CAVE_User_Room` named `Sphere*` that has a `SphereCollider`
and no `Rigidbody`. Move that sphere and rebuild to move the ball.

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
