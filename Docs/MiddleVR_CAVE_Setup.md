# Getting a Unity VR Template Project to Render in the CAVE (MiddleVR)

**Area:** MiddleVR / CAVE rendering / project setup
**Applies to:** Unity **2022.3.9f1 LTS**, MiddleVR **2.4.0**, URP **14.0.11**
**Category:** VR display bring-up — porting an XR-Template/OpenXR project to a CAVE
**CAVE config used:** `C:\Users\Exx\Desktop\visbox\config\MiddleVR\MVR.vrx` (VisBox)

---

## TL;DR

This project started life as the **Unity VR Template** (XR Interaction Toolkit + OpenXR,
built for a headset). MiddleVR drives the CAVE itself — multiple screens, cluster,
tracking — and **does not use Unity's XR stack at all**. Making the two coexist doesn't
work; you have to peel off the headset/OpenXR path and hand display control to MiddleVR.

The full chain that had to be true before a single pixel showed on the CAVE:

1. **Compiles** — MiddleVR 2.4.0's Unity-version guard rejects 2022.2+. Patch/embed it.
2. **Builds** — Standalone scripting backend set to **Mono** (IL2CPP module not installed).
3. **URP path present** — add the `com.middlevr.urp` package **and** drop the `MVR_URP`
   prefab in the scene.
4. **Unity XR off** — disable **OpenXR** in XR Plug-in Management. Deleting the XR rig
   GameObject is *not* enough; the loader still initializes and steals the display.
5. **A camera exists** — deleting the XR rig also deletes the Main Camera. MiddleVR clones
   a base camera onto its CAVE cameras; with zero cameras it renders nothing.
6. **Config assigned** — `MVRManager.EditorConfigFile` points at the real CAVE `.vrx`.

Pressing **Play in the editor on a dev PC shows MiddleVR's *preview window***, not the
physical walls. The CAVE itself only lights up when the app runs **on the CAVE system**.

---

## The journey, in order

### 0. Symptom that started it
Build & Run failed and the game never appeared in the CAVE. Underneath were five separate
issues stacked on top of each other, each hiding the next.

### 1. MiddleVR won't compile — `error CS1029: This version of Unity is not supported`

**Root cause.** MiddleVR 2.4.0 (built May 2022) predates Unity 2022.2/2022.3. Its only
version guard, in `MVRManagerScript.cs`, whitelists Unity up to 2022.1:

```csharp
#if !UNITY_2019_4 && !UNITY_2020_1 && !UNITY_2020_2 && !UNITY_2020_3 && !UNITY_2021_1 && !UNITY_2021_2 && !UNITY_2022_1
#error This version of Unity is not supported.
#endif
```

Unity 2022.3 defines `UNITY_2022_3` (not `UNITY_2022_1`), so the guard fires. It's a
conservative assertion only — 2022.1→2022.3 is API-compatible and MiddleVR runs fine.

**Why other demos didn't hit it.** Every project references the *same* tarball
(`C:\Program Files\MiddleVR2\unity_packages\com.middlevr.tgz`), so the source is identical
everywhere. The working 2022.3 demos (`Dec20_Demo_MiddleVR_Unity2022.3`, `VRACE`) simply
had this guard **commented out** in their copy. A freshly-extracted project gets the
unmodified guard back.

**Fix.** Comment out the three guard lines (`//#if` / `//#error` / `//#endif`).

> ⚠️ **Durability gotcha.** The package comes from a local **tarball**, so Unity extracts
> it into the **read-only** `Library/PackageCache/`. Editing the cache copy works until
> Unity re-resolves or the `Library` is wiped — then it reverts to the tarball and the
> error returns ("immutable asset was unexpectedly altered"). For a permanent fix, **embed**
> the package: extract `com.middlevr.tgz` into `Packages/com.middlevr/` and patch it there
> (embedded packages are mutable and version-controlled).

### 2. Build fails — `scripting backend (IL2CPP) is not installed`

**Root cause.** Player settings selected IL2CPP for both Android and Standalone, but the
**Windows IL2CPP build-support module** isn't installed (only the base Mono module is).

**Fix.** Standalone → **Mono** (`Project Settings ▸ Player ▸ Windows ▸ Scripting Backend`).
Leave **Android on IL2CPP** (Android/Quest requires it). PCVR/CAVE on Windows is fine on
Mono. In `ProjectSettings.asset`: `scriptingBackend: { Android: 1, Standalone: 0 }`.

### 3. MVRManager warns — "You must add the MiddleVR_URP package and the MVR_URP prefab"

**Root cause.** The project renders with **URP**. MiddleVR needs its URP companion package,
which carries `MVR_URP.prefab`. The base `com.middlevr` package alone isn't enough.

**Fix.**
- Add to `Packages/manifest.json`:
  `"com.middlevr.urp": "file:C:/Program Files/MiddleVR2/unity_packages/com.middlevr.urp.tgz"`
- Drop the **`MVR_URP`** prefab (from `Packages ▸ MiddleVR URP`) anywhere in the scene —
  **it does NOT need to be a child of MVRManager**; it's a sibling settings object.

**What MVR_URP does.** It disables URP post-processing effects that break across multiple
CAVE screens — Vignette, Bloom, Chromatic Aberration, Lens Distortion, Motion Blur, Depth
of Field, HDRI Sky — and forces anti-aliasing to **SMAA** (FXAA/TAA aren't multi-display
safe). These effects are screen-space, so they'd cause visible seams where the walls meet.

### 4. Runs, but nothing renders — OpenXR is stealing the display

**Root cause.** This is the VR Template, so **OpenXR is enabled in XR Plug-in Management**
with **Initialize XR on Startup = on**. On Play, the OpenXR loader initializes an HMD
display and takes over output — *even after you delete the XR rig from the scene*, because
the loader is a project-level setting, not a scene object. MiddleVR and Unity-XR can't both
own the display.

**Fix.** `Project Settings ▸ XR Plug-in Management ▸ Windows/Standalone tab` → **uncheck
OpenXR** / uncheck **Initialize XR on Startup**. In
`Assets/XR/XRGeneralSettingsPerBuildTarget.asset` this is the Standalone `m_Loaders` list +
`m_InitManagerOnStart`. After this, the editor showed **"Display 1: No cameras rendering"** —
which is *correct*: MiddleVR routes to its own viewports, not Unity's Display 1.

### 5. Still "No cameras rendering" — the scene has no camera

**Root cause.** Deleting the XR rig (XR Origin) also deleted the **Main Camera** nested
inside it. The scene had **zero cameras**. MiddleVR builds one camera per CAVE viewport from
the `.vrx` config and **clones a base scene camera's settings** (clear flags, skybox, culling
mask, post-processing) onto them. With no camera to clone, there's nothing to render.

**Fix.** Add a plain `GameObject ▸ Camera` tagged **MainCamera** at the origin (no
tracked-pose / XR components). MiddleVR disables it and uses it as the template.

### 6. The setting that lit it up

Assigning the real CAVE config to **`MVRManager ▸ Editor Config File`** was the final piece:

```
C:\Users\Exx\Desktop\visbox\config\MiddleVR\MVR.vrx
```

Not `Default.vrx` — that's a generic config and won't match the VisBox CAVE's screens.

---

## Known-good MVRManager configuration (mirrors `CAVEWeather`)

| Setting | Value | Why |
|---|---|---|
| **Editor Config File** | `…\visbox\config\MiddleVR\MVR.vrx` | The actual VisBox CAVE layout |
| **Attach To Camera** | **off** | Working demos use off with a Main Camera present |
| **Preview Window** | **on** | So you can see the CAVE composite in the editor |
| **Cluster Properties ▸ Simple Cluster** | **on** | Launches the CAVE render nodes locally |
| **Advanced ▸ Show FPS** | **off** | Avoids a benign Unity-2022 `Arial.ttf` error (see below) |
| Scene has a **Main Camera** | required | MiddleVR clones it onto the CAVE cameras |

---

## Mental model — why this is all necessary

MiddleVR is a **parallel display + tracking + cluster system**. It does not extend Unity's
XR; it replaces it. So a CAVE project is essentially a **non-XR Unity project** with:

- Unity's XR (OpenXR) turned **off**,
- a normal Main Camera in the scene,
- the **MVRManager** (drives cameras/cluster from a `.vrx` config) and, for URP/HDRP, the
  matching **MVR_URP / MVR_HDRP** settings prefab.

The Unity VR Template is the *opposite* of that out of the box, which is why every step here
was about **removing** the headset path and **handing display control to MiddleVR**.

---

## Editor vs. physical CAVE (don't get fooled)

- **Dev PC, press Play** → MiddleVR opens a **preview window** showing the combined CAVE
  view. Unity's Game view says "No cameras rendering" — expected. The physical walls do
  **not** light up from here.
- **On the CAVE system** → MiddleVR (with Simple Cluster / the cluster config) launches
  render nodes onto the CAVE's displays. This is where the walls actually render.

---

## Minor noise: the `Arial.ttf` stack trace

If **Show FPS** is on, `MVRManagerScript.EnableFPSDisplay()` calls
`Resources.GetBuiltinResource<Font>("Arial.ttf")`, which Unity 2022 removed (renamed to
`LegacyRuntime.ttf`). It logs a stack trace but runs at the *end* of `Start()`, after core
init — **harmless for rendering**. Leave Show FPS off to keep the console clean.

---

## Quick checklist for the next CAVE port

- [ ] MiddleVR compiles (version guard patched/embedded)
- [ ] Standalone scripting backend = **Mono**
- [ ] `com.middlevr.urp` in manifest **and** `MVR_URP` prefab in the scene
- [ ] **OpenXR disabled** in XR Plug-in Management (Standalone)
- [ ] A plain **Main Camera** exists in the scene
- [ ] `MVRManager.EditorConfigFile` = the real CAVE `.vrx`
- [ ] Attach To Camera **off**, Preview Window **on**, Simple Cluster **on**
- [ ] Press Play → preview window renders → deploy/run on the CAVE for the walls
