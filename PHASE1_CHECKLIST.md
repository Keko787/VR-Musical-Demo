# Phase 1 — Editor Checklist (Gun Core)

Scripts already written by me (in `Assets/_Project/Scripts/`):

| Script | Folder | Role |
|---|---|---|
| `IShootable.cs` | `Targets/` | Hit contract (targets implement in Phase 2) |
| `IGunInputSource.cs` | `Gun/` | Fire-input abstraction (pinch-to-fire later = new file only) |
| `ControllerTriggerInputSource.cs` | `Gun/` | Right trigger → fire. Builds its own InputAction (no asset dependency) |
| `Projectile.cs` | `Gun/` | Pooled physics dart/BB; self-returns on hit/lifetime |
| `ProjectilePool.cs` | `Gun/` | Object pool (no per-shot Instantiate) |
| `GunController.cs` | `Gun/` | Aim raycast + fire + cooldown |
| `Tracer.cs` | `Gun/` | Tracer dot + beam, independently toggleable |
| `GunVisibilityToggle.cs` | `Gun/` | Hide mesh without breaking muzzle/tracer |
| `GunDebugToggles.cs` | `Gun/` | TEMP in-headset toggles (remove in Phase 3) |

Let Unity compile these (watch for console errors before continuing — tell me if any).

---

## 1. Create a Projectile layer (prevents the aim dot snapping onto flying darts)

- [ ] Edit → Project Settings → Tags and Layers → add a User Layer named **`Projectile`**
- [ ] Edit → Project Settings → Physics → in the layer collision matrix, optionally uncheck `Projectile`×`Projectile` (darts don't need to collide with each other)

## 2. Foam dart projectile prefab

- [ ] Create an empty GameObject, name **`Projectile_FoamDart`**
- [ ] Add a child **Capsule** (3D Object → Capsule): scale ~`(0.02, 0.04, 0.02)`, rotate so its long axis is +Z; remove the capsule's default collider
- [ ] Material: new `Assets/_Project/Art/Materials/M_Dart.mat`, bright orange, URP/Lit
- [ ] On the **root** `Projectile_FoamDart`:
  - [ ] Add **Rigidbody**: Mass `0.02`, Use Gravity ✅, Collision Detection **Continuous Dynamic**, Interpolate **Interpolate**
  - [ ] Add a **Sphere Collider** (or Capsule Collider), radius ~`0.02`, **Is Trigger = OFF**
  - [ ] Add component **Projectile** (script); leave Lifetime = 3
  - [ ] Set the GameObject **Layer = Projectile** (apply to children too if prompted)
- [ ] Drag it into `Assets/_Project/Prefabs/Gun/` to make it a prefab, then delete the scene copy

## 3. BB projectile prefab

- [ ] Duplicate `Projectile_FoamDart.prefab` → rename **`Projectile_BB`**
- [ ] Open it: child mesh → small **Sphere**, scale ~`(0.012,0.012,0.012)`, yellow material `M_BB.mat`
- [ ] Rigidbody: Mass `0.005`, Use Gravity **OFF** (near-hitscan feel), Collision Detection Continuous Dynamic
- [ ] Sphere Collider radius ~`0.006`
- [ ] Save prefab

## 4. Gun prefab

- [ ] Create empty GameObject, name **`Gun`**, position/rotation zeroed
- [ ] Child **`Model`**: drag `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/DemoAssets/Models/Primitive_Blaster.fbx` under `Gun`. Give it a material from `Art/Materials` (a bright primary colour). Model has **no collider**.
- [ ] Child empty **`Muzzle`**: position it at the tip of the barrel; rotate so its **blue +Z axis points down the barrel** (this is the fire/aim direction — verify with the Move/Rotate gizmo)
- [ ] Child **`TracerDot`**: a small **Quad** or flattened Sphere, scale ~`(0.03,0.03,0.03)`, unlit emissive red material `M_TracerDot.mat`. Remove any collider. Leave it active for now (the script controls visibility).
- [ ] Child **`TracerLine`**: empty GameObject + add **Line Renderer** component:
  - Positions: size 2 (values don't matter — script sets them)
  - **Use World Space = ✅** (important — script feeds world positions)
  - Width `0.005`, material an unlit additive line material `M_TracerLine.mat`
  - Uncheck the Line Renderer's **Enabled**? Leave enabled; the script disables it on Start.

### Components on the `Gun` root (Add Component for each)

- [ ] **Controller Trigger Input Source** — Binding Path `<XRController>{RightHand}/trigger`, Press Point `0.5`
- [ ] **Projectile Pool** — Prefab = `Projectile_FoamDart`, Default Capacity 20, Max Size 50
- [ ] **Gun Controller** —
  - Muzzle = the `Muzzle` child
  - Pool = the Projectile Pool component on this same object
  - Launch Speed `25`, Fire Cooldown `0.08`, Aim Max Distance `50`
  - **Aim Mask**: set to Everything, then **uncheck the `Projectile` layer** (so the dot ignores flying darts)
  - Input Source Behaviour: drag the **Controller Trigger Input Source** component here (or leave empty — it auto-finds it on this GameObject)
- [ ] **Tracer** — Gun = this Gun Controller, Muzzle = `Muzzle`, Line = `TracerLine`'s Line Renderer, Dot = `TracerDot` transform, Line Lifetime `0.08`, Line/Dot Enabled = unchecked (default off)
- [ ] **Gun Visibility Toggle** — drag the `Model`'s MeshRenderer(s) into the Model Renderers array, Model Visible ✅
- [ ] **Gun Debug Toggles** (TEMP) — Tracer = the Tracer component, Visibility = the Gun Visibility Toggle component
- [ ] Drag `Gun` into `Assets/_Project/Prefabs/Gun/` → prefab. Delete the scene copy.

## 5. Mount the gun on the right controller

- [ ] Open `Assets/_Project/Prefabs/Rig/XR Rig - Stationary.prefab` in Prefab Mode
- [ ] Find **`Right Controller`** in the hierarchy
- [ ] Drag the `Gun` prefab as a **child** of `Right Controller`
- [ ] Zero its local position. Set local rotation so the `Muzzle` +Z points where you'd naturally point the controller (Touch controllers aim ~`(45, 0, 0)` X-rotation downward from controller forward — adjust after a test)
- [ ] Save the prefab

## 6. Test scene setup (Boot.unity)

- [ ] Open `Assets/_Project/Scenes/Boot.unity`
- [ ] Make sure the scene's rig is the **prefab instance** (so the gun change propagates); if you see a stale override warning, right-click → Revert or re-add the prefab
- [ ] Add a **3D Object → Cube** at `(0, 1.3, 3)`, name `TestTarget`, give it a collider (cubes have one by default), bright material — this is what you'll shoot
- [ ] Save scene

## 7. Build & verify on Quest 3

Build And Run, then check:

- [ ] **Right trigger** spawns a projectile from the muzzle that flies forward (foam dart arcs with gravity)
- [ ] Hold the trigger → fire rate is cooldown-limited (no instant spam)
- [ ] Projectile hits `TestTarget` and disappears (returns to pool — no Console errors)
- [ ] Press **left X** → gun model hides/shows
- [ ] Press **left Y** → tracer beam draws briefly on each shot
- [ ] Press **right A** → tracer dot appears at the aim point and tracks across surfaces
- [ ] With model hidden + dot/line on: still aims and fires correctly (cave-mode preview)
- [ ] Profiler (or just observe): firing 50+ shots doesn't spike memory (pooling works)

If the gun doesn't fire: first suspect the trigger binding. Tell me and I'll have you confirm `<XRController>{RightHand}/trigger` resolves on Quest (some profiles expose it as `triggerPressed` / `triggerValue` — easy one-line change in `ControllerTriggerInputSource`).

---

When all of section 7 passes, Phase 1 is done and we move to Phase 2 (targets + Gallery_Carnival). Report results or paste any Console error.
