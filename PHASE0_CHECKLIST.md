# Phase 0 — Editor Checklist

What I've already done from outside Unity:
- ✅ Created `Assets/_Project/` folder skeleton
- ✅ Created `Assets/_Project/Scripts/_Project.Runtime.asmdef`
- ✅ Created `Assets/_Project/Scripts/Rig/StationaryRigGuard.cs`

What you need to do inside Unity, in order. Tick items off as you go — there's a verification step at the end.

---

## 1. Open the project and let it import

- [ ] Open Unity Hub → open `D:/Unityhub/VR Demo` with Unity **6000.4.7f1**
- [ ] Wait for the asmdef to compile (bottom-right spinner). No errors expected; if any, stop and tell me

## 2. Input actions — DO NOT swap the asset (lesson learned)

> **Revised approach.** An earlier version of this checklist had you duplicate `XRI Default
> Input Actions` into a stripped `XRI Stationary Input Actions` and point the rig's Input
> Action Manager at the copy. **Do not do this.** Every component on the XRI rig (camera
> pose driver, snap/continuous turn providers, controllers, near-far interactor, UI)
> references actions in the *original* asset by GUID. Swapping the Input Action Manager to
> a separate copy orphans all of them at once — that's what caused the dead head tracking
> and dead snap turn we spent hours debugging.
>
> **Keep the rig's Input Action Manager pointed at the original
> `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions`.**
> "No virtual locomotion" is enforced **structurally** by deleting the locomotion provider
> GameObjects (next section), not by stripping the input asset. Enabling a Move/Teleport
> action does nothing if no provider exists to consume it.

- [ ] Confirm no input-actions duplication/stripping is needed for Phase 0. Skip to section 3.
- [ ] (If a previous attempt created `Assets/_Project/Settings/XRI Stationary Input Actions`
      and the rig's Input Action Manager points at it: change it back to the original
      `XRI Default Input Actions`. The stripped copy can be left in the project unused, or
      deleted — it is not part of the foundation.)

## 3. Duplicate the XR rig

- [ ] In Project window, navigate to `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/`
- [ ] Right-click **`XR Origin (XR Rig).prefab`** → **Copy**
  - (Note: there's also a `Complete XR Origin Set Up Variant.prefab` in `Assets/VRTemplateAssets/Prefabs/Setup/` — that one's heavier with tutorial callouts we don't want. Use the Starter Assets one above.)
- [ ] Paste into `Assets/_Project/Prefabs/Rig/`
- [ ] Rename it to **`XR Rig - Stationary.prefab`** (dash, not em-dash — easier to type in code refs)
- [ ] Double-click to open it in Prefab Mode

## 4. Strip locomotion from the rig (structural — keep original input asset)

Working in Prefab Mode on `XR Rig - Stationary`. Locomotion providers live as child
GameObjects under a **`Locomotion`** parent (which also holds the `Locomotion Mediator`
component the providers depend on).

**Actual rig hierarchy (confirmed from the prefab source):**

```
XR Origin (XR Rig)              ← root (XR Origin component + Input Action Manager)
├── Camera Offset
│   └── Main Camera             ← Tracked Pose Driver (verify, see below)
├── Left Controller
├── Right Controller
└── Locomotion                  ← KEEP — holds the Locomotion Mediator component
    ├── Turn                    ← KEEP — Snap Turn + Continuous Turn providers
    ├── Gravity                 ← KEEP — harmless for a stationary rig
    ├── Move                    ← DELETE
    ├── Grab Move               ← DELETE
    ├── Teleportation           ← DELETE
    ├── Climb                   ← DELETE
    └── Jump                    ← DELETE
```

### Delete the locomotion provider children (this is what enforces "no movement")

Expand the **`Locomotion`** GameObject in the Hierarchy. Keep `Locomotion` itself and
`Turn` and `Gravity`. Delete the rest:

- [ ] Right-click `Locomotion/Move` → **Delete**
- [ ] Right-click `Locomotion/Grab Move` → **Delete**
- [ ] Right-click `Locomotion/Teleportation` → **Delete**
- [ ] Right-click `Locomotion/Climb` → **Delete**
- [ ] Right-click `Locomotion/Jump` → **Delete**
- [ ] Confirm `Locomotion` (parent), `Locomotion/Turn`, `Locomotion/Gravity` remain

### Configure the Turn GameObject (keep both providers, default to Snap)

`Locomotion/Turn` has **two providers**: `Snap Turn Provider` and `Continuous Turn
Provider`. Keep both — `SettingsService` switches between them at runtime via `TurnStyle`.

- [ ] Click `Locomotion/Turn`
- [ ] **Snap Turn Provider**: `Turn Amount` = `45`, component **enabled** (checkbox on)
- [ ] **Continuous Turn Provider**: `Turn Speed` = `60`, component **disabled** (uncheck the
      box to the left of the component name) — Snap is the startup default
- [ ] Leave both providers' input references **as-is** (they point at the original
      `XRI Default Input Actions` — that's correct, see next section)

### Remove Controller Input Action Manager from each controller

- [ ] `Left Controller` → Inspector → **Controller Input Action Manager** → ⋮ → **Remove Component**
- [ ] `Right Controller` → same

### Tracking origin + Input Action Manager (KEEP original asset)

- [ ] Click the prefab root `XR Rig - Stationary` (the `XR Origin (XR Rig)` object)
- [ ] **XR Origin** component → **Tracking Origin Mode** → **Floor**
- [ ] **Input Action Manager** component → **Action Assets** list must contain the
      **original** `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default
      Input Actions` — **not** a stripped copy. If a stripped `XRI Stationary Input
      Actions` is in the list, remove it and add the original. This single setting is what
      makes head tracking + snap/continuous turn + controllers + UI all work, because every
      rig component references that asset by GUID.

### Verify the camera's Tracked Pose Driver

With the Input Action Manager on the original asset, the camera's pose actions resolve
normally — no manual rebind needed. Just confirm it's intact:

- [ ] Select `Camera Offset → Main Camera`
- [ ] **Tracked Pose Driver (Input System)**: Track Type = `Rotation And Position`,
      Update Type = `Update And Before Render`, Position/Rotation inputs populated
- [ ] (A direct `<XRHMD>/centerEyePosition` / `centerEyeRotation` binding also works and is
      extra-robust — fine to leave if a previous step set it.)

### Add StationaryRigGuard

- [ ] Still on **`XR Origin (XR Rig)`** root, click **Add Component** → type "Stationary Rig Guard"
- [ ] It appears under `Scripts → V R Shooting Gallery → Rig`
- [ ] Leave defaults: Max Per Frame Drift Meters `0.5`, Log On Drift ✅ (the guard now allows snap-turn pivots — only large rogue motions trigger the warning)

### Save and exit

- [ ] **Ctrl+S** to save the prefab
- [ ] Click the **back arrow** at the top-left of the Scene view to exit Prefab Mode

## 5. Create the Boot smoke scene

- [ ] File → New Scene → choose **Basic (URP)** template → save as `Assets/_Project/Scenes/Boot.unity`
- [ ] Delete the default `Main Camera` (the XR rig provides one)
- [ ] Drag `Assets/_Project/Prefabs/Rig/XR Rig - Stationary.prefab` into the scene
- [ ] Add a **GameObject → 3D Object → Plane** named `Ground`, scale (1,1,1), position (0,0,0), assign a simple grey URP/Lit material
- [ ] Add two **GameObject → 3D Object → Cube**s at positions `(-1.5, 1, 2.5)` and `(1.5, 1, 2.5)` so you have parallax reference points while walking
- [ ] Add a **GameObject → 3D Object → Text - TextMeshPro** (canvas → world space):
  - Canvas: Render Mode `World Space`, position `(0, 1.6, 3)`, scale `0.005`
  - Text: "Stationary rig OK — walk to test"
- [ ] Save the scene

## 6. Add Boot to build profiles

- [ ] **File → Build Profiles** (Unity 6) or **Build Settings**
- [ ] Add `Boot.unity` as scene index **0**
- [ ] Remove any other scenes from the list for now

## 7. Switch to Android + Quest 3 build target

- [ ] In Build Profiles, select **Android** → click **Switch Platform** (this can take several minutes the first time)
- [ ] Click **Player Settings** while still on the Android target:
  - **Other Settings**
    - [ ] Color Space: **Linear**
    - [ ] Auto Graphics API: **off**
    - [ ] Graphics APIs: **Vulkan** only (remove OpenGLES3)
    - [ ] Scripting Backend: **IL2CPP**
    - [ ] Target Architectures: **ARM64** only (uncheck ARMv7)
    - [ ] Minimum API Level: **Android 10 (API 29)**
    - [ ] Target API Level: **Automatic (highest installed)** is fine, or 32+
    - [ ] Active Input Handling: **Input System Package (New)**
  - **Publishing Settings**
    - [ ] No changes needed yet (signing keys come at release time)
- [ ] **XR Plug-in Management** (in Project Settings → XR Plug-in Management → Android tab):
  - [ ] Check **OpenXR**
  - [ ] Wait for it to install
- [ ] **OpenXR settings** (Project Settings → XR Plug-in Management → OpenXR → Android tab):
  - [ ] Render Mode: **Single Pass Instanced**
  - [ ] Interaction Profiles → **+** → add **Meta Quest Touch Pro Controller Profile** (or **Oculus Touch Controller Profile** — either works for Quest 3)
  - [ ] OpenXR Feature Groups → enable **Meta Quest Support**
- [ ] **Project Settings → Quality**:
  - [ ] For the Android-tier URP asset, set Anti Aliasing: **MSAA 4x**, disable real-time shadows (we'll bake lighting later)

## 8. Verification — Run on Quest 3

Pick **one** of these two paths:

### Path A: Direct build to device (recommended)
- [ ] Plug Quest 3 in via USB-C, accept the "Allow USB debugging" prompt inside the headset
- [ ] In Build Profiles, click **Build And Run** (target the Quest 3 device shown in the device dropdown)
- [ ] Headset puts you in `Boot.unity`

### Path B: Editor preview with XR Device Simulator
- [ ] Window → Package Manager → search XR Interaction Toolkit → Samples tab → **XR Device Simulator** → Import
- [ ] Open the simulator from the sample and Play in editor

### What to check
- [ ] Headset/simulator camera renders at floor height (your real head height)
- [ ] **Thumbstick forward/back**: nothing happens (no smooth move)
- [ ] **Thumbstick left/right**: **snap-turns 45°** per push (intentional — this is the calibration tool)
- [ ] Continuous turn does NOT fire (we left that provider disabled at startup; settings menu turns it on later)
- [ ] **No teleport ray** appears when pressing the thumbstick
- [ ] **A / B / X / Y buttons do nothing**
- [ ] **Triggers do nothing** (gun isn't built yet)
- [ ] **Physical walking** moves your view in the virtual world (Path A only)
- [ ] Console shows **no warning** from `StationaryRigGuard` (snap-turn pivots are far under the 0.5 m threshold)

If you want to verify Continuous Turn is wired up correctly: in the editor, temporarily re-enable the **Continuous Turn Provider** checkbox on the `Turn` GameObject and Play — the rig should rotate smoothly when you push the thumbstick left/right. Then disable it again for the actual build (Snap stays as the startup default).

---

When all items are checked, Phase 0 is done. Tell me and I'll start Phase 1 (gun core).

If anything fails, paste the symptom or the Console error and I'll fix it before we move on.
