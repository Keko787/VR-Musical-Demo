# Phase 2 — Editor Checklist (Targets + Gallery_Carnival)

**Goal:** a playable `Gallery_Carnival.unity` — stand behind a booth, shoot a wall of
targets for a 60-second round, score updates live, round ends and can restart.

Scripts already written by me (let them compile, watch the Console for errors first):

| Script | Folder | Role |
|---|---|---|
| `MatchConfig.cs` | `Core/` | ScriptableObject: mode, duration, display name (`MatchMode` enum reserves TargetCount/Endless) |
| `MatchService.cs` | `Core/` | Score + timer + run state; subscribes to all targets on StartMatch |
| `AutoStartMatch.cs` | `Core/` | TEMP: auto-starts the round, B-button restarts (replaced in Phase 3) |
| `TargetBase.cs` | `Targets/` | Abstract: scoring, hittable gate, shared knockdown/respawn |
| `Target_Disc.cs` | `Targets/` | Flips back on hit, rights itself |
| `Target_Duck.cs` | `Targets/` | Pop-up cycle; hittable only when up |
| `Target_Bonus.cs` | `Targets/` | Pop-up + particle burst, high points |
| `Target_Rail.cs` | `Targets/` | Slides A↔B; flips on hit |
| `Target_Penalty.cs` | `Targets/` | Negative points; stays down |
| `HUDController.cs` | `UI/` | Binds TMP score/timer text to MatchService |

`IShootable` and the projectile's hit dispatch are already done from Phase 1.

---

## 1. Match config asset

- [ ] In `Assets/_Project/Settings/`, right-click → **Create → VR Shooting Gallery → Match Config**
- [ ] Name it **`Match_TimeAttack60`**: Mode = `TimeAttack`, Duration = `60`, Display Name = `Time Attack 60s`

## 2. Target prefabs (primitive meshes — reuse, no modeling)

General rules for **every** target prefab:
- The GameObject with the `Target_*` script **must have a non-trigger Collider** (so darts collide and `GetComponentInParent<IShootable>` finds it).
- Leave it on the **Default layer** (the gun's Aim Mask includes Default; only `Projectile` is excluded).
- No Rigidbody on targets — the dart's rigidbody drives the collision.
- Bright, readable materials from `Assets/_Project/Art/Materials/` (e.g. reuse/duplicate `M_Target.mat`).

### 2a. `Target_Disc`
- [ ] 3D Object → **Cylinder**, scale ~`(0.2, 0.02, 0.2)`, rotated so the flat face points at the player (face +Z toward the booth)
- [ ] Add **Target Disc** component; Points `10`, Reset Delay `1`
- [ ] Leave `Visual` empty (defaults to itself)
- [ ] Save as `Assets/_Project/Prefabs/Targets/Target_Disc.prefab`

### 2b. `Target_Duck`
- [ ] Empty root `Target_Duck`; child **Capsule** (body) + small **Sphere** (head) → this child is the **Visual**
- [ ] Put the **Collider** (Capsule Collider) on the root (or on the Visual — just make sure it's on the object carrying the script's hit target; simplest: collider on root, Visual = the child mesh group)
- [ ] Add **Target Duck** component on the root: Points `25`, Pop Height `0.6`, Up Duration `1.5`, Down Duration `1.5`, Move Speed `6`; drag the mesh child into **Visual**
- [ ] Position note: the duck sinks **down** by Pop Height when ducking — place it so "up" is the visible pose and it hides behind the booth/wall lip when down
- [ ] Save prefab

### 2c. `Target_Bonus`
- [ ] Duplicate `Target_Duck`, rename `Target_Bonus`, swap mesh to a **star-ish** shape (a flattened, rotated cube or a sphere with emissive gold material)
- [ ] Replace the Target Duck component with **Target Bonus**: Points `100`, Up Duration `0.8` (short window), Down Duration `2.5`
- [ ] Optional: add a child **Particle System** (confetti) and drag it into **Hit Vfx** — you can reuse `Assets/VRTemplateAssets/Prefabs/Blaster/Confetti.prefab`
- [ ] Save prefab

### 2d. `Target_Rail`
- [ ] Root `Target_Rail`; child mesh (a **Cube** plaque or cylinder) = Visual, with the Collider
- [ ] Create two empty child markers `PointA` and `PointB` (or place them as siblings on the wall) ~1.5 m apart horizontally
- [ ] Add **Target Rail**: Points `50`, Speed `0.4`, assign PointA / PointB
- [ ] Save prefab (note: PointA/B as children move with the prefab — when you place instances, re-position the markers per location)

### 2e. `Target_Penalty`
- [ ] Duplicate `Target_Disc`, rename `Target_Penalty`; give it a clearly "don't shoot" look (red cross / civilian colour)
- [ ] Replace Target Disc with **Target Penalty**: Points `-50`, Respawn After Hit = off
- [ ] Save prefab

## 3. HUD prefab

- [ ] Create a **world-space Canvas** (`GameObject → UI → Canvas`, Render Mode = **World Space**)
- [ ] Add two **TextMeshPro - Text (UI)** children: `ScoreText` (top-left) and `TimerText` (top-centre, large)
- [ ] Add **HUD Controller** to the canvas root; assign Score Text + Timer Text
- [ ] Scale the canvas tiny (~`0.003`) so it reads at ~3 m
- [ ] Save as `Assets/_Project/Prefabs/UI/HUD.prefab`

## 4. Build the scene `Gallery_Carnival.unity`

- [ ] File → New Scene (Basic) → save as `Assets/_Project/Scenes/Maps/Gallery_Carnival.unity`
- [ ] Delete the default Main Camera
- [ ] **Floor:** Plane scaled to ~6 × 4 m. Add a coloured quad/decal marking the **2.5 × 1.5 m** walkable strip
- [ ] **Booth counter:** a Cube scaled ~`2.5 × 0.95 × 0.4 m`, placed **1.5 m in front** of the target wall, waist height. Keep its **Box Collider** so the player can't walk through it
- [ ] **Target wall:** a Plane/Cube backdrop ~`4 × 2.5 m` behind the targets
- [ ] Place target instances in three rows (distances from the booth):
  - [ ] Near (~1.5 m): 3 × `Target_Duck`
  - [ ] Mid (~2.5 m): 4 × `Target_Disc` + 1 × `Target_Rail`
  - [ ] Far (~3.5 m): 5 × `Target_Disc` (smaller) + 1 × `Target_Bonus`
  - [ ] 2 × `Target_Penalty` mixed in
- [ ] Targets at roughly standing eye height (1.55 m ± 0.5 m)
- [ ] Drag in `XR Rig - Stationary.prefab`, positioned **behind the counter facing the wall**
- [ ] Drag in `HUD.prefab`, placed on/above the back wall (~3 m up), facing the player
- [ ] One **Directional Light**; mark static decoration as Static for batching

## 5. Match manager object

- [ ] Create an empty GameObject `MatchManager`
- [ ] Add **Match Service**
- [ ] Add **Auto Start Match** (TEMP): assign Match = the Match Service on this object, Config = `Match_TimeAttack60`

## 6. Build settings

- [ ] Add `Gallery_Carnival` to **Build Profiles → Scene List** (after Boot)

## 7. Build & verify on Quest 3

- [ ] Scene loads; you stand behind the counter and **can't walk through it**
- [ ] Round auto-starts; timer counts down from 60 in the HUD
- [ ] Shooting a **Disc** flips it back, adds 10, it rights itself after ~1 s
- [ ] **Duck** pops up/down; only scores when up; hitting it drops it early
- [ ] **Rail** slides side to side and can be hit
- [ ] **Bonus** appears briefly, big points + confetti
- [ ] **Penalty** subtracts points and stays down
- [ ] Score updates live and matches expectations
- [ ] At 0 s the HUD shows "TIME!" and scoring stops; **B button restarts** the round
- [ ] Frame rate holds (72 Hz min) with all targets active + darts flying
- [ ] No `[StationaryRigGuard]` warning (rig still stationary)

---

When section 7 passes, Phase 2 is done → Phase 3 (menus + state flow + leaderboard).

**Gotcha to watch:** if shooting a target does nothing, check the target's **Collider is present and not a trigger**, and that the dart actually collides (its `Projectile` collider is non-trigger too). If darts pass through, the target collider is probably missing or set to trigger.
