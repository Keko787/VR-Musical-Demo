# VR Shooting Gallery — Implementation Plan

Companion to [DESIGN.md](DESIGN.md). This is the concrete, file-level, ordered task list. Phases match the design doc's section 9.

**Conventions used below**
- `[E]` = Editor-only task (Unity Inspector / scene work)
- `[C]` = Code task (write/edit a `.cs` file)
- `[A]` = Asset task (create prefab, material, scene)
- `[V]` = Verification step — must pass before moving on
- File paths assume the proposed `Assets/_Project/` layout from DESIGN §8.

---

## Phase 0 — Project setup & locomotion lockdown

**Goal:** A scene with our XR rig where the headset tracks physical walking on Quest 3, but joystick / teleport / turn do nothing. No gameplay yet.

### 0.1 Folder skeleton
- `[E]` Create `Assets/_Project/` with subfolders: `Art/{Materials,Models,Textures,VFX}`, `Audio/{SFX,Music}`, `Prefabs/{Gun,Targets,Environment,Rig,UI}`, `Scenes/{Maps}`, `Scripts/{Core,Rig,Gun,Targets,UI,Audio}`, `Settings`.
- `[A]` Add an assembly definition `Assets/_Project/Scripts/_Project.Runtime.asmdef`. References: `Unity.XR.Interaction.Toolkit`, `Unity.XR.CoreUtils`, `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`. Keeps our compile times fast and isolates us from sample-asset churn.

### 0.2 Input actions — do NOT swap the asset
- **Lesson learned:** an earlier plan duplicated `XRI Default Input Actions` into a stripped copy and pointed the rig's Input Action Manager at it. This orphans every rig component (camera pose driver, snap/continuous turn, controllers, near-far, UI) because they all reference the *original* asset by GUID — cause of the dead-head-tracking and dead-snap-turn debugging marathon.
- `[E]` Keep the rig's Input Action Manager on the **original** `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions`. No duplication, no stripping for Phase 0.
- "No virtual locomotion" is enforced **structurally** (deleting provider GameObjects in 0.3), not by the input asset. An enabled Move/Teleport action is inert with no provider to consume it.
- Any runtime gating we want later (e.g. disabling turn during gameplay) is done in code via `SettingsService`/`GameStateManager`, not by editing the input asset.

### 0.3 Stationary XR rig prefab
- `[A]` Duplicate `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab` → `Assets/_Project/Prefabs/Rig/XR Rig - Stationary.prefab`. (Use *Duplicate*, not a Prefab Variant.)
- `[E]` Locomotion providers live under a **`Locomotion`** parent GameObject (which holds the `Locomotion Mediator`). In Prefab Mode, under `Locomotion`, delete: `Move`, `Grab Move`, `Teleportation`, `Climb`, `Jump`. **Keep** `Locomotion` (parent), `Locomotion/Turn`, `Locomotion/Gravity`.
- `[E]` On `Locomotion/Turn`: **keep both** `Snap Turn Provider` (Turn Amount = 45°, enabled) and `Continuous Turn Provider` (Turn Speed = 60°/s, **disabled** at startup so Snap is default). Leave both providers' input references on the original asset — do not re-point them.
- `[E]` On both `Left Controller` and `Right Controller`: remove `Controller Input Action Manager` component.
- `[E]` On the root: set `XR Origin.Tracking Origin Mode = Floor`. Confirm the `Input Action Manager`'s Action Assets list holds the **original** `XRI Default Input Actions` (not a stripped copy).
- `[E]` Verify the Main Camera's `Tracked Pose Driver` is intact (Track Type = Rotation And Position, Update Type = Update And Before Render, inputs populated). With the original asset enabled, no manual rebind is needed; a direct `<XRHMD>/centerEyePosition`/`centerEyeRotation` bind is an acceptable extra-robust alternative.

### 0.4 StationaryRigGuard
- `[C]` Create `Assets/_Project/Scripts/Rig/StationaryRigGuard.cs`:
  - `MonoBehaviour` on the `XR Origin` GameObject, default execution order `int.MaxValue`.
  - In `LateUpdate`, compute per-frame position delta `(transform.position - m_LastPosition)`. If delta magnitude exceeds `m_MaxPerFrameDriftMeters` (default 0.5 m), log a one-shot warning naming the likely culprit (rogue Move/Teleport provider).
  - Does *not* auto-correct or check rotation — snap turn rotates the origin and pivots produce small one-frame position deltas, both of which are intentional.
- `[E]` Add component to the rig prefab.

### 0.5 Boot smoke scene
- `[A]` Create `Assets/_Project/Scenes/Boot.unity` (will become the boot scene later; for now it's our smoke test):
  - Instance of `XR Rig — Stationary.prefab`
  - Directional light, simple ground plane (10×10), a couple of waist-height cubes for parallax reference
  - World-space TMP text reading "Stationary rig OK — walk to test"
- `[E]` Add `Boot` to **File → Build Profiles → Scene List** as index 0.

### 0.6 Build configuration for Quest 3
- `[E]` Switch Build Target to **Android**, Texture Compression: ASTC.
- `[E]` Player Settings:
  - Color Space: **Linear**
  - Graphics APIs: **Vulkan** only (drop GLES3 unless we need a fallback)
  - Scripting Backend: **IL2CPP**, Target Architectures: **ARM64**
  - Minimum API Level: **29 (Android 10)**, Target API: 32+
  - XR Plug-in Management → Android tab → enable **OpenXR**
  - OpenXR → enable **Meta Quest Support** interaction profile, **Oculus Touch Controller Profile**
- `[E]` Quality Settings → URP asset for Quest: disable real-time shadows on the URP asset used at runtime, set Anti Aliasing = MSAA 4×.

### 0.7 Phase 0 verification
- `[V]` Project compiles with no errors.
- `[V]` `Boot.unity` runs in the Editor; XR Device Simulator (or headset link) shows the camera at floor height.
- `[V]` Build-and-run on Quest 3: headset enters scene, physical walking moves the view, **left/right joystick does nothing**, **A/B/triggers do nothing**, **no teleport ray appears**.
- `[V]` `StationaryRigGuard` does not warn (XR Origin truly hasn't moved).

---

## Phase 1 — Gun core

**Goal:** A gun attached to the right controller that shoots foam darts / BBs in `Boot.unity`. Tracer line, tracer dot, and gun-model visibility all toggleable from a debug keybind or inspector checkbox.

### 1.1 Gun prefab
- `[A]` Create `Assets/_Project/Prefabs/Gun/Gun.prefab`:
  - Root: empty GameObject `Gun`, anchored under the right controller (Right Controller → Attach Transform child, or a new child of the right hand).
  - Child mesh `Model` using `Primitive_Blaster.fbx` (from `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/DemoAssets/Models/`). Apply a bright primary-colour material from a new `Art/Materials/M_Gun_Body.mat`.
  - Child empty `Muzzle` at the barrel tip, +Z forward.
  - Child empty `TracerDot` with a small unlit-emissive quad/sphere (`Art/Materials/M_TracerDot.mat`), starts inactive.
  - Child `TracerLine` with a `LineRenderer` (2 verts, 0.005 m width, additive unlit material `M_TracerLine.mat`), starts inactive.

### 1.2 Projectile prefabs
- `[A]` `Assets/_Project/Prefabs/Gun/Projectile_FoamDart.prefab`:
  - Stretched orange capsule, ~0.08 m long. Rigidbody (mass 0.001, **Use Gravity = true**, collision detection Continuous Dynamic). SphereCollider, trigger = **false**.
  - Component: `Projectile.cs` (see 1.4).
- `[A]` `Assets/_Project/Prefabs/Gun/Projectile_BB.prefab`:
  - Small yellow sphere, ~0.012 m. Rigidbody (mass 0.0005, gravity false for near-hitscan feel), continuous collision.
  - Same `Projectile.cs` component, different launch speed.

### 1.3 ProjectilePool
- `[C]` `Assets/_Project/Scripts/Gun/ProjectilePool.cs`:
  - Wraps `UnityEngine.Pool.ObjectPool<Projectile>`.
  - Public `Projectile Get(Vector3 pos, Quaternion rot)`, `void Return(Projectile p)`.
  - Default capacity 20, max 50.
  - One pool per projectile prefab — `GunController` owns one active pool, swaps it when projectile type changes.

### 1.4 Projectile
- `[C]` `Assets/_Project/Scripts/Gun/Projectile.cs`:
  - Holds back-reference to its pool for self-return.
  - On `OnCollisionEnter`: try `other.collider.GetComponentInParent<IShootable>()` (interface added Phase 2) and call `Hit(hitInfo)`. Always return to pool after `m_Lifetime` (default 3 s) or on hit.
  - `Launch(Vector3 worldVelocity)` — sets `Rigidbody.linearVelocity` and resets timers.

### 1.5 GunController
- `[C]` `Assets/_Project/Scripts/Gun/GunController.cs`:
  - Serialized: `Transform m_Muzzle`, `InputActionReference m_FireAction` (bind to right-hand trigger), `ProjectilePool m_Pool`, `float m_LaunchSpeed = 25f`, `float m_FireCooldown = 0.08f`, `LayerMask m_AimMask` (everything except player/UI-block layer).
  - On `OnEnable`/`OnDisable`: subscribe to `m_FireAction.performed`.
  - `Fire()`:
    1. Reject if `Time.unscaledTime < m_NextFireTime`.
    2. `Projectile p = m_Pool.Get(m_Muzzle.position, m_Muzzle.rotation); p.Launch(m_Muzzle.forward * m_LaunchSpeed);`
    3. Fire `OnFired` UnityEvent (tracer line listens here).
    4. Haptic + SFX through services (Phase 4 wiring — leave hooks now).
  - `Update()` — runs raycast for the dot: `Physics.Raycast(m_Muzzle.position, m_Muzzle.forward, out RaycastHit hit, 50f, m_AimMask)`. Cache `m_LastHit` for `Tracer`/UI consumers.

### 1.6 Tracer
- `[C]` `Assets/_Project/Scripts/Gun/Tracer.cs`:
  - Serialized refs: `GunController m_Gun`, `LineRenderer m_Line`, `Transform m_Dot`, `float m_LineLifetime = 0.08f`.
  - Public properties: `bool LineEnabled`, `bool DotEnabled` — backed by `SettingsService` later, set inspector defaults to `false` for now.
  - `Update`: if `DotEnabled`, place dot at `m_Gun.LastHitPoint` (or far point at 10 m if no hit), face camera; else `m_Dot.gameObject.SetActive(false)`.
  - On `m_Gun.OnFired`: if `LineEnabled`, set line positions (muzzle → hit/far), `m_Line.enabled = true`, schedule `m_Line.enabled = false` after `m_LineLifetime` (coroutine).

### 1.7 GunVisibilityToggle
- `[C]` `Assets/_Project/Scripts/Gun/GunVisibilityToggle.cs`:
  - Serialized: `Renderer[] m_ModelRenderers`.
  - `public bool ModelVisible { get; set; }` — toggles `enabled` on each renderer (not `gameObject.SetActive`, so muzzle/tracer transforms still work).

### 1.8 Wire-up
- `[E]` Drop `Gun.prefab` under the right-hand controller in `XR Rig — Stationary.prefab`. Anchor to a sensible offset so the muzzle points where the user's index finger does on a Touch controller (roughly 30° down from controller forward).
- `[E]` On `GunController`, bind `m_FireAction` to `XRI Right Hand/Activate` (or `XRI Right Hand/Select` — whichever is the trigger in our stripped input asset). Confirm by hitting "Use in default" in the Input Actions window.
- `[E]` In `Boot.unity`, add a placeholder `DebugToggleHotkeys` component on a manager GameObject that flips `Tracer.LineEnabled`, `Tracer.DotEnabled`, and `GunVisibilityToggle.ModelVisible` on number keys 1/2/3 (Editor-only).

### 1.9 Phase 1 verification
- `[V]` In the editor (XR Simulator) and on Quest 3: pulling the right trigger spawns a projectile from the muzzle that arcs (foam dart) or zips (BB).
- `[V]` Toggle 1 → red tracer dot appears at aim point on any geometry; toggle 2 → brief tracer line draws on each shot; toggle 3 → gun mesh disappears but tracers still work.
- `[V]` Hold the trigger: rate-limited by cooldown — no projectile spam.
- `[V]` 50+ shots without `Instantiate`/`Destroy` allocations spiking (check Profiler).

---

## Phase 2 — Targets and first map

**Goal:** A playable `Gallery_Carnival.unity` where the user scores points hitting a wall of targets for a fixed timer.

### 2.1 Shooting interface
- `[C]` `Assets/_Project/Scripts/Targets/IShootable.cs`:
  - `interface IShootable { void OnShot(in ShotInfo info); }`
  - `struct ShotInfo { Vector3 worldPoint; Vector3 normal; Vector3 direction; int points; GameObject source; }`
- `[C]` Update `Projectile.OnCollisionEnter` to populate and dispatch `ShotInfo`.

### 2.2 Target base + variants
- `[C]` `TargetBase.cs` (abstract) — fields: `int m_Points`, `float m_ResetDelay`, event `OnHit(int points)`. Virtual `OnShot` knocks the visual back, raises score, schedules reset via coroutine.
- `[C]` `Target_Disc.cs` — static disc that flips backwards on hit; resets after `m_ResetDelay`.
- `[C]` `Target_Duck.cs` — pop-up cycle using `Mathf.PingPong` or a simple timer state machine; ignores shots while in the "down" pose.
- `[C]` `Target_Rail.cs` — translates between two waypoints with configurable speed; reverses on hit & schedules reset.
- `[C]` `Target_Bonus.cs` — extends Duck with shorter visible window, higher points, particle burst on hit.
- `[C]` `Target_Penalty.cs` — visually distinct (civilian/teddy bear?), negative points, no respawn until round ends.

### 2.3 MatchService
- `[C]` `Assets/_Project/Scripts/Core/MatchService.cs` (singleton, lives on persistent manager later but for Phase 2 lives in the scene):
  - State: `int Score`, `float TimeRemaining`, `bool IsRunning`.
  - Methods: `StartMatch(MatchConfig cfg)`, `EndMatch()`. Events: `OnScoreChanged`, `OnTick`, `OnMatchEnded(MatchResult)`.
  - `MatchConfig` ScriptableObject: `float duration`, `MapDescriptor map`, modifier set.
- `[C]` Subscribe all `TargetBase.OnHit` instances on `StartMatch`.

### 2.4 HUD
- `[A]` `Assets/_Project/Prefabs/UI/HUD.prefab` — world-space canvas anchored to the booth, TMP text for Score + Timer.
- `[C]` `Assets/_Project/Scripts/UI/HUDController.cs` — binds to `MatchService.OnScoreChanged`/`OnTick`.

### 2.5 Gallery_Carnival scene
- `[A]` Create `Assets/_Project/Scenes/Maps/Gallery_Carnival.unity`:
  - Skybox: solid carnival-night gradient or fairground HDRI (keep texture small).
  - Floor: 6 × 4 m wooden plank plane with a coloured floor decal marking the 2.5 × 1.5 m walkable zone.
  - **Booth counter:** 2.5 × 0.95 × 0.4 m wood box, 1.5 m in front of the target wall. Box collider on the counter front to physically block the user from walking too far forward.
  - **Target wall:** 4 × 2.5 m plane with striped backdrop. Place target instances on it across three rows:
    - Near row (1.5 m): 3 × `Target_Duck` (large, slow popup)
    - Mid row (2.5 m): 4 × `Target_Disc` + 1 × `Target_Rail`
    - Far row (3.5 m): 5 × `Target_Disc` (small) + 1 × `Target_Bonus`
  - Sprinkle 2 × `Target_Penalty` on the wall in static positions.
  - Side props: prize shelf, festoon-light strip, a couple of striped pennants. All static, no colliders beyond what's necessary.
  - Single baked directional light + light probes. Mark all decoration static for batching.
- `[E]` Instance `XR Rig — Stationary.prefab` positioned behind the counter, facing the wall.
- `[E]` Place `HUD.prefab` on the back wall behind the targets, ~3 m up, scale tuned to read at distance.
- `[E]` Add a `MatchService` GameObject to the scene with a simple `MatchConfig` asset (60 s, default modifiers).

### 2.6 Auto-start for Phase 2
- `[C]` Temporary `AutoStartMatch.cs` on the scene's `MatchService` GameObject — calls `StartMatch` in `Start()`, restarts on B-button. Will be replaced by the menu in Phase 3.

### 2.7 Phase 2 verification
- `[V]` Scene loads on Quest 3. Player stands behind the counter; the counter blocks them from walking through the wall.
- `[V]` Shooting any target type updates the score correctly; penalty subtracts; bonus targets work.
- `[V]` 60-second timer counts down, match ends, HUD shows final score; B-button restarts.
- `[V]` Frame rate stays at 72 Hz minimum with 15+ active targets and projectiles in flight.
- `[V]` `StationaryRigGuard` still silent — XR Origin not moving.

---

## Phase 3 — UI and full menu flow

**Goal:** Replace `AutoStartMatch` with a real Boot → StartScreen → MainMenu → Map → Pause flow, all driven by shootable buttons.

### 3.1 ShootableButton
- `[C]` `Assets/_Project/Scripts/UI/ShootableButton.cs`:
  - Extends Unity UI `Button` (or composes one). Has a `BoxCollider` on the button rect for projectile collision; implements `IShootable.OnShot` → calls `onClick.Invoke()` (with cooldown).
  - Also receives **hover** from the gun's raycast: `GunController.LastHitCollider` is checked each frame by a `ShootableButtonHoverDispatcher` MonoBehaviour on the canvas; it sets visual hover state and listens for "confirm" input (right-trigger short-press while hovering).
  - Visual states: idle / hovered / disabled / pressed — driven by `Image` colour + scale tween.

### 3.2 Menu input
- `[E]` In the stationary input actions, ensure `XRI Right Hand/Primary Button` (A) and `XRI Right Hand/Secondary Button` (B) are exposed.
- `[C]` `Assets/_Project/Scripts/UI/MenuInput.cs`:
  - Reads joystick (we'll re-enable just the *value* of `XRI Right Hand/Thumbstick` for navigation only — no locomotion).
  - Emits `OnNavigate(Vector2)`, `OnConfirm`, `OnBack` events consumed by `MenuController`.
- `[C]` `MenuController.cs` — manages focused button index for keyboard-style navigation; handles back stack across submenus.

### 3.3 GameStateManager
- `[C]` `Assets/_Project/Scripts/Core/GameStateManager.cs`:
  - Enum `GameState { Boot, StartScreen, Menu, Playing, Paused, Results }`.
  - Singleton, DontDestroyOnLoad. Exposes `Transition(GameState next)` and event `OnStateChanged`.
  - Uses `SceneManager.LoadSceneAsync` for scene swaps and `LoadSceneMode.Additive` for the pause overlay.
  - **Turn gating** (cave calibration support): on every transition, decides `bool turningAllowed = (state is StartScreen | Menu | Results)`, finds both `SnapTurnProvider` and `ContinuousTurnProvider` (`FindFirstObjectByType<T>`), and calls a helper `ApplyTurnGate(turningAllowed)` on `SettingsService`. The helper enables the provider matching `SettingsService.TurnStyle` only when `turningAllowed` is true; the other provider stays disabled always.

### 3.4 SettingsService
- `[C]` `Assets/_Project/Scripts/Core/SettingsService.cs`:
  - PlayerPrefs-backed: `tracerLine`, `tracerDot`, `gunVisible`, `projectileType (enum)`, `targetSpeedMultiplier`, `sfxVolume`, `musicVolume`, `haptics`, `dominantHand`, `turnStyle` (enum: `Snap | Continuous`, default `Snap`), `snapTurnAngle` (enum: `Deg30 | Deg45 | Deg90`, default `Deg45`), `continuousTurnSpeed` (enum: `Slow30 | Medium60 | Fast90`, default `Medium60`).
  - Static `Settings Current { get; }`. On change, fires `OnSettingsChanged` so `Tracer`, `GunVisibilityToggle`, `ProjectilePool`, `AudioService`, and the active turn provider react live.
  - Exposes `ApplyTurnGate(bool turningAllowed)` called by `GameStateManager`: based on `TurnStyle`, enables only the matching provider when `turningAllowed`, otherwise disables both. Also pushes `snapTurnAngle` to `SnapTurnProvider.turnAmount` and `continuousTurnSpeed` to `ContinuousTurnProvider.turnSpeed` on every settings change.

### 3.5 Scenes
- `[A]` `Boot.unity` (replace Phase 0 contents):
  - Spawns persistent managers prefab (`PersistentManagers.prefab` containing `GameStateManager`, `SettingsService`, `MatchService`, audio mixer, etc.).
  - Loads `StartScreen` additively, unloads itself.
- `[A]` `StartScreen.unity`:
  - Big "VR Shooting Gallery" title, single shootable Start button.
  - XR rig instance (stationary), gun active but no targets.
  - Optional ambient music.
- `[A]` `MainMenu.unity`:
  - World-space canvas with tabbed panels: Maps / Modifiers / Match / Settings.
  - Start Match button — listens to `SettingsService.CurrentMap != null`, disables/enables accordingly.

### 3.6 Pause menu
- `[A]` `Pause.unity` (additive over gameplay):
  - Half-transparent backdrop quad in front of the player, world-space menu canvas with Resume / Restart / Quit-to-Menu shootable buttons.
- `[C]` Gameplay scenes listen for B-button → `GameStateManager.Transition(Paused)` → load `Pause` additively + `Time.timeScale = 0f` (but projectile physics already in-flight should be cleared — call `ProjectilePool.ClearAll`).

### 3.7 Map selection plumbing
- `[A]` `Assets/_Project/Settings/Maps/Map_GalleryCarnival.asset` ScriptableObject of type `MapDescriptor` (name, scene reference via `SceneAsset` + `AssetReference` if using Addressables; for v1 just a scene path string), preview image.
- `[C]` `MapDescriptor.cs` + `MapList.cs` (ScriptableObject holding `MapDescriptor[]`). MainMenu renders one button per entry.

### 3.8 Phase 3 verification
- `[V]` Cold boot lands on StartScreen; only valid input is shooting the Start button.
- `[V]` MainMenu navigation: joystick highlights buttons, trigger-confirm activates; alternatively, shooting any button activates it.
- `[V]` Start Match button stays greyed out until a map is picked; once picked, shooting it loads `Gallery_Carnival`.
- `[V]` B-button mid-game opens the pause menu; Resume returns to gameplay, Quit-to-Menu returns to MainMenu.
- `[V]` Modifier toggles (tracer line/dot/gun visibility/projectile type) take effect live when changed in the Modifiers panel.
- `[V]` Settings persist across app restart.

---

## Phase 4 — Polish

**Goal:** Looks, sounds, and feels like a finished demo. No new gameplay systems — only quality and tuning.

### 4.1 Audio
- `[A]` Source / record SFX: `shot_foam`, `shot_bb`, `hit_metal`, `hit_wood`, `target_popup`, `target_bonus`, `ui_hover`, `ui_click`, `match_start`, `match_end`. Keep all mono, 16-bit, 22 kHz, max 1 s.
- `[A]` Audio Mixer asset with `Master / SFX / Music / UI` groups, exposed volume parameters.
- `[C]` `AudioService.cs` — `PlayOneShot(AudioClipRef, Vector3 pos, MixerGroup group)`. Pool of N `AudioSource`s.
- `[E]` Wire `GunController.OnFired`, `TargetBase.OnHit`, `ShootableButton.onClick/OnHover`, `MatchService.OnMatchStart/End` to AudioService.

### 4.2 Haptics
- `[C]` `HapticService.cs` — wraps `XRBaseInteractor.SendHapticImpulse` (or the XRI 3.x haptic API on the controller). Respects `SettingsService.haptics`.
- `[E]` Wire: short pulse on fire (0.2 amp, 0.05 s), stronger pulse on hit confirmation (0.5 amp, 0.08 s).

### 4.3 VFX
- `[A]` Muzzle flash particle (additive, 0.05 s lifetime) under `Muzzle`.
- `[A]` Hit splash particle prefab — `Projectile` spawns it on hit, scaled by surface tag (metal vs wood).
- `[A]` Bonus confetti — reuse `Assets/VRTemplateAssets/Prefabs/Blaster/Confetti.prefab` for `Target_Bonus`.
- `[E]` Add small camera-anchored vignette on damage-only frames? **Skip** — no damage system.

### 4.4 Difficulty & match config presets
- `[A]` `MatchConfig` ScriptableObjects: `Easy.asset` (90 s, slow targets), `Normal.asset` (60 s, default), `Hard.asset` (45 s, 1.4× target speed, smaller far row), accessed from Match Settings panel.

### 4.5 Performance pass
- `[E]` Mark every static prop static; bake lighting & occlusion.
- `[E]` URP Render Asset for Android: cap shadow distance, disable extra cameras.
- `[E]` Enable **OpenXR → Foveated Rendering** (Meta Quest).
- `[E]` GPU instancing on all repeated target materials.
- `[E]` Profile a 60 s match on device; budget targets from DESIGN §10 must hold.

### 4.6 Cave-mode preset button
- `[A]` In Modifiers panel add a single "Cave Mode" shootable button that sets gunVisible=false, tracerLine=true, tracerDot=true in one click.

### 4.7 Phase 4 verification
- `[V]` On-device: 90 Hz hit in `Gallery_Carnival` for a full 60 s match, 90th-percentile frame time under 11 ms.
- `[V]` Audio doesn't clip / pop; mixer volumes saved.
- `[V]` Difficulty presets noticeably change feel; all modifiers persist.
- `[V]` Demo-able: hand the headset to a non-developer; they can boot → start → play → quit without verbal coaching.

---

## Phase 5 — Cave prep (deferred)

**Goal:** Project is ready to drop into the cave system once dimensions arrive. No work here until then.

When the cave dimensions arrive:
1. Update `Gallery_Carnival` (or a new `Gallery_Cave` map) walkable strip + counter distance to match the real cave.
2. Confirm Cave Mode preset gives an acceptable invisible-gun experience on Quest 3 first.
3. Evaluate whether to use AR Foundation passthrough + scene meshing to align the virtual booth with the real cave walls — likely a separate `XR Rig — Stationary (Passthrough).prefab` variant.
4. Build pipeline for Android XR target if that's where the cave hardware lands; the OpenXR setup we did in Phase 0 should make this mostly a Build-Target switch.

---

## Cross-phase deliverables (the running checklist)

| Item | Phase | Status |
|---|---|---|
| `_Project/` folder + asmdef | 0 | – |
| Rig kept on original `XRI Default Input Actions` (no swap) | 0 | – |
| `XR Rig - Stationary.prefab` (structural locomotion strip) | 0 | – |
| `StationaryRigGuard.cs` | 0 | – |
| Quest 3 build config | 0 | – |
| `Gun.prefab` + projectiles | 1 | – |
| `GunController`, `Tracer`, `GunVisibilityToggle` | 1 | – |
| `ProjectilePool` | 1 | – |
| Target system (`IShootable`, 5 target types) | 2 | – |
| `MatchService` + HUD | 2 | – |
| `Gallery_Carnival.unity` | 2 | – |
| `GameStateManager` + `SettingsService` | 3 | – |
| Boot / StartScreen / MainMenu / Pause scenes | 3 | – |
| `ShootableButton` + menu navigation | 3 | – |
| Audio / Haptics / VFX | 4 | – |
| Performance pass | 4 | – |
| Cave-mode preset | 4 | – |
| Cave dimensions integration | 5 | – |

---

## Scope adjustments from locked decisions

These deltas are folded into the phases above — captured here so they're not buried.

- **Single map only (v1):** No map-list scroll work in Phase 3 — the Maps tab shows one card. Selection state still required so the Start button activation flow is the same one we'll need when more maps land.
- **Time attack only (v1):** `MatchConfig` keeps its `MatchMode` enum (`TimeAttack`, `TargetCount`, `Endless`) — only `TimeAttack` is wired. `MatchService` is structured around the enum so the other modes are small additions later, not rewrites.
- **Local leaderboard (new sub-task):** Add to Phase 3.
  - `[C]` `Assets/_Project/Scripts/Core/LeaderboardService.cs` — top-5 per `(mapId, mode)` tuple in PlayerPrefs (JSON-serialized list). API: `Submit(mapId, mode, score, dateTime) → int rank` and `GetTop(mapId, mode, n)`.
  - `[A]` `Leaderboard.prefab` panel — used in MainMenu tab and on the Results screen after a match ends.
  - `[E]` `MatchService.OnMatchEnded` → `LeaderboardService.Submit` → Results screen shows rank + top-5.
- **Reuse-primitives art:** Phase 1's gun mesh stays `Primitive_Blaster.fbx` with a styled material. Phase 2's targets are primitive shapes (disc = cylinder, duck = flattened capsule + sphere head, rail target = textured quad on a slider, bonus = star quad). No FBX modeling tasks anywhere in the plan.
- **Pinch-to-fire shim (future-proofing, not v1 work):** In Phase 1.5 (`GunController`), expose firing through a `IGunInputSource` interface with one current impl `ControllerTriggerInputSource`. Adding `HandPinchInputSource` later is a single-file change with no edits to `GunController`. Document in `GunController.cs` summary.
- **Cave dims:** Phase 2 uses the 2.5 × 2.5 m walkable strip + 4 × 2.5 m target wall from DESIGN §6 as-is. Phase 5 re-tunes when real numbers land.

## Risks and unknowns

- **Right-trigger binding** — once we strip the locomotion action maps, we need to double-check the right-trigger is still bound somewhere sensible for `GunController`. Plan B is to author a new action in the stationary input asset rather than reusing XRI's `Activate`.
- **Pinch-to-fire (hand tracking)** — open question #6 in DESIGN. If we say yes, `GunController` needs an alternative input path; the rig's `XRHandPoseDriver` setup will be additional Phase 1 work.
- **Counter collider vs. roomscale walking** — if the user's real-world floor is bigger than the virtual walkable strip, they can physically walk past the counter mesh and end up clipping into it. We can't fix that virtually; we can render a "boundary fade" shader (similar to Quest's guardian) on the counter when the head gets within 0.2 m of it. Defer to Phase 4 polish.
- **Hover detection through projectile path** — projectiles in flight might collide with menu buttons unintentionally. Solution: put menu buttons on a layer the projectile collider excludes, and let only the gun's raycast + a separate "menu-shoot" collider on the button handle activation. Decide during Phase 3.1.
