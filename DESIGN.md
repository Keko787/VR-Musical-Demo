# VR Shooting Gallery — Design Document

> Companion document: [IMPLEMENTATION.md](IMPLEMENTATION.md) — concrete, ordered, file-level tasks.

## 1. Vision

A stationary VR shooting gallery for Meta Quest 3, deliberately designed as a stepping stone to a **cave-system VR installation** where joystick locomotion is unacceptable (cybersickness + physical safety in a real walkable cave).

Tone is **"virtual carnival shooting game"** — approachable, kid-friendly, simple geometry, satisfying feedback. Not a tactical shooter.

### Hard constraints

| Constraint | Reason |
|---|---|
| No continuous-move / teleport locomotion | Cybersickness in cave; user must walk physically only |
| Turning (snap or continuous) allowed for **calibration only** | Useful pre-match to align the virtual booth with the cave; **disabled during gameplay** regardless of style |
| Snap is the default turn style | Lower cybersickness risk; continuous is opt-in via settings for users who want fine-grained alignment |
| Stationary play area, ~3 × 3 m roomscale | Quest 3 guardian + realistic cave room footprint |
| Single primary input: **aim and shoot** | Lowest friction for first-time VR users |
| Gun model toggle (invisible mode) | Cave system uses a real physical prop; only crosshair/tracer should be visible |
| Menu opened with A/B button | Frees triggers for shooting; works without locomotion |
| All menu options shootable | So the gun is also the cursor — no second input mode to learn |

### Target platforms

- **Phase 1 (now):** Meta Quest 3 standalone, OpenXR via `com.unity.xr.meta-openxr`
- **Phase 2 (later):** Cave system — Android XR or Quest 3 with passthrough; physical prop replaces gun model

---

## 2. User flow

```
[Boot]
   ↓
[Start Screen]            ← cannot move, cannot teleport
   • Only enabled input: aim + trigger
   • "Start" button is shootable
   ↓ (shoot Start)
[Main Menu]               ← still stationary; joystick navigates UI only
   • Map selection
   • Modifier toggles (tracer line, tracer dot, gun visibility, …)
   • Match settings (time limit, target count, difficulty)
   • Game settings (audio, haptics, comfort)
   • "Start Match" is greyed out until map selected
   ↓ (shoot Start Match)
[Gameplay]                ← stationary play; physical walking only
   • Targets spawn / animate in shooting area
   • Score + timer HUD
   • A/B button → pause menu (same shootable UI)
   ↓ (match ends or quit)
[Results → Main Menu]
```

**Menu input rules**
- Joystick = navigate options (highlight moves between buttons)
- Primary button (A/X) or trigger-on-highlighted = confirm
- Aim + trigger = shoot button directly (alternative path)
- B/Y = back / open pause menu during gameplay

---

## 3. Locomotion lockdown (the critical bit)

XRI's starter assets ship with locomotion providers as child GameObjects under the rig: `Move`, `Turn` (snap + continuous), `Teleportation`, `Climb`. To enforce "no virtual movement except turning-for-calibration":

1. **Strip** these locomotion actions from our input actions asset: `Move`, `Teleport Mode`, `Teleport Mode Cancel`, `Grab Move`, `Jump`. **Keep** `Snap Turn` and `Turn` (continuous).
2. **Delete** these child GameObjects from the rig: `Move`, `Teleportation`, `Climb`, `Right Controller Teleport Stabilized Origin`.
3. On the `Turn` GameObject, **keep both** `Snap Turn Provider` and `Continuous Turn Provider`. **Default `Continuous Turn Provider.enabled = false`** so Snap is the startup behavior; `SettingsService` toggles which is active based on `TurnStyle`.
4. **Remove** `Controller Input Action Manager` from both controllers — it was mediating teleport vs. ray modes that no longer exist.
5. **Keep** head tracking + room-scale tracking (the user walks physically — that must work).
6. **Tracking origin: Floor** (`XROrigin.requestedTrackingOriginMode = Floor`) — so the player's height matches reality. Critical for the cave.

### Turning as calibration, not locomotion

Both turn providers stay in the rig but are **gated on two axes**:

**Axis 1 — game state** (applies to whichever provider is currently active):

| State | Turn enabled? | Reason |
|---|---|---|
| Boot / Start screen | ✅ | First chance to align the world with the cave |
| Main menu | ✅ | Re-orient before starting if needed |
| Gameplay (Playing) | ❌ | Once aligned, a stray thumbstick must not misalign mid-match |
| Pause menu | ❌ | Same — preserve alignment |
| Results screen | ✅ | Allow re-orient before next match |

**Axis 2 — user setting** (`SettingsService.TurnStyle`):

| TurnStyle | Snap Turn Provider | Continuous Turn Provider | Notes |
|---|---|---|---|
| `Snap` (default) | enabled | disabled | Safer; recommended for first-time users and the cave install |
| `Continuous` | disabled | enabled | Smooth rotation; preferred for precise calibration angles |

The two providers cannot both run at once — they bind to the same thumbstick and would interfere (continuous rotation + intermittent snap jumps). Settings menu toggles between them.

`GameStateManager` and `SettingsService` together set `activeProvider.enabled = (state allows turning) && (this provider matches TurnStyle)` on every state transition or settings change. Snap angle (30°/45°/90°) and continuous speed (30/60/90 deg/sec) both exposed in Settings.

### Drift guard

A custom `StationaryRigGuard` script asserts at runtime that the origin doesn't move more than 0.5 m in any single frame. This catches rogue continuous-move/teleport providers but allows the small one-frame pivot that snap/continuous turn produces (both rotate the origin around the head).

---

## 4. Scene structure

Three Unity scenes, additively loaded where useful:

| Scene | Purpose | Loaded |
|---|---|---|
| `Boot.unity` | Bootstrap: XR init, persistent managers, then load Start | Single |
| `StartScreen.unity` | Title + "Start" shootable button | Single |
| `MainMenu.unity` | Map/modifier/settings UI | Single |
| `Gallery_Carnival.unity` | The default shooting gallery map | Single (replaces menu) |
| `PauseMenu` | Same UI prefab as MainMenu, loaded additively over gameplay | Additive |

Future maps (`Gallery_Western`, `Gallery_Sci_Fi`, …) follow the same template — same XR rig prefab, different decoration + target layouts.

### Persistent managers (DontDestroyOnLoad, spawned by Boot)

- `GameStateManager` — current state (Start, Menu, Playing, Paused, Results)
- `SettingsService` — serialised player prefs (toggles, volumes, last map)
- `MatchService` — score, timer, target counts for the active match
- `AudioService` — SFX/music with ducking
- `HapticService` — wraps XRI haptic API; respects user toggle

---

## 5. The gun

### Model & feel

- Base mesh: reuse `Primitive_Blaster.fbx` or a simple custom toy-gun mesh (chunky, brightly coloured, carnival-style).
- Held in **right hand by default**, left-hand swap as a setting.
- Anchored to controller — *not* a grab-to-pick-up gun. The user always has it. (Carnival booth: the gun is bolted to the counter conceptually.)

### Projectile

- Visual: short cylinder or sphere with foam-tip styling — bright orange/yellow Nerf-dart look, alternatively a "BB" round sphere.
- Two projectile styles selectable in modifiers: **Foam Dart** (slower, larger, arc-y) vs **BB** (faster, smaller, near-hitscan).
- Implementation: physical projectile via [`LaunchProjectile.cs`](Assets/VRTemplateAssets/Scripts/LaunchProjectile.cs) (already in project). Add object pooling so we don't `Instantiate` per shot.

### Toggles (all live in `SettingsService`, surfaced in menu modifiers)

| Toggle | Default | What it does |
|---|---|---|
| `tracerLineEnabled` | Off | Renders a thin LineRenderer beam from muzzle along aim for ~80 ms after shot |
| `tracerDotEnabled` | Off | Renders a small reticle dot at first raycast hit point, always visible while gun is held |
| `gunModelVisible` | On | Toggles the mesh renderers on the gun prefab |
| **Cave mode preset** | — | Convenience: turns gun model **off**, tracer line **on**, tracer dot **on** in one click |

### Aim resolution

Each frame the gun does a forward `Physics.Raycast` (length: longest map distance + headroom). Hit point feeds:
- The tracer dot position
- The tracer line endpoint (when firing)
- UI hover detection in menus (using a UI raycaster layer)

Raycast is the source of truth for menu hovering. Projectiles are visual + use collision for target hits — keeps gameplay feeling physical without making menu interaction depend on projectile travel.

### Audio + haptics on fire

- Single short SFX, randomised pitch ±5%
- Short haptic pulse on firing hand
- Mute / haptic-disable toggles in settings

---

## 6. Shooting area design

### Spatial budget

Assume Quest 3 guardian of **2.5 m × 2.5 m walkable**. Cave room TBD — design must scale.

```
        [TARGET WALL  ─ 4 m wide, 2.5 m tall]
        |  far row (3.5 m) ─ small/fast targets
        |  mid row (2.5 m) ─ medium targets
        |  near row (1.5 m) ─ large/popup targets
        |
       ─┼─────────────────────────────────  ← counter, w=2.5 m, h=0.95 m
        |  (player walkable strip behind counter, 2.5 × 1.5 m)
```

- **Counter / booth front** (waist-height invisible collider) at ~1.5 m from target wall — prevents leaning *through* targets and matches a real carnival booth.
- **Backdrop** wraps around target wall: striped canvas, bunting, festoon lights. Carnival aesthetic, low-poly.
- **Side props** (prize shelves, sandbags) outside the play box for atmosphere — not interactable.
- **Floor decal** marks the walkable strip; an audible/visual chime triggers if the user leaves it (so the cave install can warn them).

### Targets (v1)

| Type | Behaviour | Points |
|---|---|---|
| Static disc | Idle until hit; falls back | 10 |
| Pop-up duck | Rises 1.5s, ducks 1.5s, repeats | 25 |
| Sliding rail target | Translates left↔right along a rail | 50 |
| Bonus star | Rare, fast, short visible window | 100 |
| Penalty (civilian) | Shooting it deducts points | -50 |

All targets implement `IShootable` so the projectile collision handler is uniform.

### Sizes & distance tuning

- Smallest target ≥ 0.20 m at 3.5 m → visual angle ≥ 3.3° — comfortable Quest 3 resolution for accurate hits without eye strain.
- Targets at standing eye-height (1.55 m) ±0.5 m so the player doesn't have to crane or crouch.

---

## 7. UI

### Style

- Carnival ticket / wooden sign aesthetic for buttons, big readable type (TextMesh Pro already in project).
- World-space canvases anchored to the booth (not screen-space — VR.)
- Buttons have **two activation modes**:
  1. **Hover + trigger** — gun's forward raycast highlights it, trigger fires
  2. **Shoot directly** — a projectile collision also activates it (with a small cooldown so a stray shot doesn't double-fire)

### Menu structure

```
Main Menu
├─ Maps           → grid of map cards, click to select (highlights, doesn't start)
├─ Modifiers      → toggles: tracer line, tracer dot, gun visible, projectile type, target speed
├─ Match Settings → time limit, target count, difficulty preset
├─ Settings       → audio sliders, haptic toggle, dominant hand, comfort options
└─ [Start]        → greyed out until map is selected
```

Pause menu = same prefab, with "Resume / Restart / Quit to Menu" header.

---

## 8. Proposed folder structure (new)

```
Assets/
├─ _Project/                          ← all custom work goes here
│  ├─ Art/
│  │  ├─ Materials/
│  │  ├─ Models/                      (gun, targets, props)
│  │  ├─ Textures/
│  │  └─ VFX/                         (muzzle flash, hit splash, confetti)
│  ├─ Audio/
│  │  ├─ SFX/                         (shot, hit, miss, target_popup, ui_click)
│  │  └─ Music/
│  ├─ Prefabs/
│  │  ├─ Gun/                         (Gun.prefab, Projectile_FoamDart.prefab, Projectile_BB.prefab)
│  │  ├─ Targets/                     (Target_Disc, Target_Duck, Target_Rail, Target_Bonus, Target_Penalty)
│  │  ├─ Environment/                 (Booth, Backdrop, FloorDecal)
│  │  ├─ Rig/                         (XR Rig — Stationary.prefab — our locked-down variant)
│  │  └─ UI/                          (MenuPanel.prefab, MenuButton.prefab, HUD.prefab)
│  ├─ Scenes/
│  │  ├─ Boot.unity
│  │  ├─ StartScreen.unity
│  │  ├─ MainMenu.unity
│  │  └─ Maps/
│  │     └─ Gallery_Carnival.unity
│  ├─ Scripts/
│  │  ├─ Core/                        (GameStateManager, SettingsService, MatchService)
│  │  ├─ Rig/                         (StationaryRigGuard, GunAimController)
│  │  ├─ Gun/                         (GunController, ProjectilePool, Tracer, GunVisibilityToggle)
│  │  ├─ Targets/                     (IShootable, TargetBase, Target_Duck, Target_Rail, …)
│  │  ├─ UI/                          (ShootableButton, MenuController, HUDController)
│  │  └─ Audio/                       (AudioService, HapticService)
│  └─ Settings/
│     └─ Stationary Input Actions.inputactions   ← derived from XRI default, with Move/Turn/Teleport stripped
└─ (existing: Samples/, VRTemplateAssets/, XR/, XRI/, Settings/, Scenes/)
```

Keeping all custom work under `_Project/` makes it easy to see what's ours vs. imported, and the leading underscore floats it to the top of the Project window.

---

## 9. Phased delivery

### Phase 0 — Project setup (½ day)
- Create `_Project/` folder skeleton
- Duplicate XRI Default Input Actions → `Stationary Input Actions`; strip Move/Turn/Teleport
- Create `XR Rig — Stationary.prefab` variant of `Complete XR Origin Set Up Variant.prefab` with movement components removed
- Add `StationaryRigGuard.cs` and verify in `BasicScene.unity` that the rig truly cannot move
- Build-and-run smoke test on Quest 3

**Exit criteria:** scene loads on Quest 3, headset tracks physical walking, joystick does nothing.

### Phase 1 — Gun core (1 day)
- `GunController.cs` (attached to right controller): forward raycast, fire on trigger, projectile spawn via pool
- `ProjectilePool` + foam dart / BB projectile prefabs
- `Tracer` component: line renderer + dot, both toggleable at runtime
- `GunVisibilityToggle` for mesh renderers

**Exit criteria:** in `BasicScene`, user can shoot a cube; tracer line/dot/gun-model toggle via a debug keybind.

### Phase 2 — Targets + first map (1–2 days)
- `IShootable` + `TargetBase` + four target types
- `Gallery_Carnival` scene: booth, backdrop, target wall with mixed target rows
- Score events broadcast to `MatchService`

**Exit criteria:** can play a 60-second round, score updates, targets reset between rounds.

### Phase 3 — UI + menu flow (2 days)
- Shootable button component (works with both hover-trigger and projectile-hit)
- Start screen, main menu, pause menu prefabs
- Scene transitions via `GameStateManager`
- Modifier toggles wired to `SettingsService` + persisted to `PlayerPrefs`

**Exit criteria:** full flow Boot → Start → Menu → Map → Play → Pause → Menu works on device with only gun input.

### Phase 4 — Polish (1–2 days)
- Audio: shots, hits, popups, ambient
- Haptics on fire + hit
- Muzzle flash + hit VFX + confetti on bonus
- Difficulty presets and match-length options
- Performance pass: occlusion, GPU instancing on repeated targets, foveated rendering check

**Exit criteria:** runs at locked 72 Hz minimum on Quest 3 (target 90 Hz).

### Phase 5 — Cave preparation (later, when cave dims arrive)
- Cave-mode preset (gun invisible, tracers on)
- Replace `Gallery_Carnival` floor footprint with cave-matched dimensions
- Optional: integrate AR Foundation for cave-mesh scan / passthrough alignment
- Tune target distances to actual cave geometry

---

## 10. Performance budget (Quest 3, standalone)

- **Refresh:** 72 Hz minimum, 90 Hz target
- **Draw calls:** < 200 per eye
- **Triangles:** < 500 k per eye
- **Texture memory:** keep most under 1024², use ASTC compression
- **Lighting:** single baked directional + light probes; no realtime shadows on targets
- **Foam-dart projectile count:** pool of 50, no more than ~20 alive concurrently

URP is already configured. Will set up an XR-specific URP asset with foveated rendering enabled.

---

## 11. Locked decisions (v1 scope)

| Decision | Choice | Notes |
|---|---|---|
| Maps | `Gallery_Carnival` only | Themed maps (Western, Sci-Fi) deferred to v2 |
| Match format | Time attack — 60 s, max score | Target-count and endless/survival modes kept as future hooks in `MatchConfig` |
| Leaderboard | Local top-5 per map + mode, PlayerPrefs | Shown on results screen + a leaderboard tab in MainMenu |
| Art budget | Reuse `Primitive_Blaster` + primitives, restyled with materials | No bespoke modeling for v1 |
| Hand tracking | Controllers only | `GunController` input layer designed so pinch-to-fire can be added without refactor |
| Cave dimensions | Design for generic ~2.5 × 2.5 m | Re-tune in Phase 5 when real measurements arrive |

