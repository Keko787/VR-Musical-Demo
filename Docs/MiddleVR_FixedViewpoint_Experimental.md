# Experimental Fixed-Viewpoint CAVE Config — Open Thread

**Area:** MiddleVR / CAVE / multi-user viewing
**Applies to:** Unity **2022.3.9f1 LTS**, MiddleVR **2.4.0**
**Scene:** `Assets/_Project/Scenes/BootMiddleVR2.unity`
**Config:** `Assets/_Project/Config/MVR_Experimental.vrx`
**Status:** ⚠️ **Unfinished** — renders sky in the editor preview; fix identified but not yet applied.

---

## Goal

A **fixed / static lead viewpoint** so multiple people can stand in the CAVE and watch
comfortably. With normal head tracking, the projection is computed from the *tracked
(lead) user's* head — when they move, the whole virtual world shears for everyone else,
which causes group cybersickness. Pinning the viewpoint removes that head-follow.

`BootMiddleVR2.unity` is the experimental scene (a copy of the CAVE bring-up) wired to
`MVR_Experimental.vrx`. The wand/`HandNode` stays tracked so the gun still works; only the
`HeadNode` is meant to be fixed.

---

## The symptom

Press **Play** in `BootMiddleVR2` → the CAVE preview renders **only sky** (cameras pointed
up). The scene has a ground plane at the origin, walls, and spheres, so it should show the
room.

## What we ruled out

- **Template MainCamera tilt.** It had a 15° X-pitch; zeroed it (`Transform.Rotation = 0`).
  Helped slightly, did **not** fix the sky.
- **MVRManager rig rotation.** Identity (`0,0,0`), `AttachToCamera = 0`. Not the cause.
- **Scene geometry.** `Ground` is a standard plane at `(0,0,0)`; walls/spheres present. The
  geometry is there — the cameras just aren't pointed at it.
- **Config format / eye height.** Tried static head at `y=1.6`, tracked-but-frozen, and a
  full v2.4.0.4 rebuild copied from `MVR.vrx`. **All static variants still render sky.**

## The actual diagnosis

The deciding pattern (all observed in the editor preview):

| `HeadNode` setting | Editor preview |
|---|---|
| **Tracked** (`Tracker="ART.BodyTracker0"`, `UseTracker*="1"`) | Room renders ✅ |
| **Static** (`Tracker="0"`, fixed `PositionLocal`) | Sky ❌ |
| **Tracked-but-frozen** (`Tracker` set, `UseTracker*="0"`) | Sky ❌ |

The dev PC has **no physical ART tracker**, so a *tracked* head is driven by MiddleVR's
**head simulator**, which places it at a proper standing pose → the room renders. The moment
the head is static/frozen, nothing drives it and it falls back to the config's authored
`PositionLocal`/`OrientationLocal`, which in this CAVE looks up → sky.

So this is **not** a config-format or eye-height bug — a static head simply isn't getting
the simulator's pose. The fixed-viewpoint config is conceptually correct for the *real*
CAVE (which has a tracker); it just can't be previewed statically on the dev PC as-is.

---

## Next step (the fix we landed on, not yet done)

**Capture the simulator's pose and bake it into the static head.**

1. Set **MVRManager ▸ Editor Config File** to `MVR.vrx` (the tracked config that renders).
2. Press **Play**. In the Hierarchy, expand the runtime MiddleVR nodes
   (`… → CenterNode → HeadNode → cameras`) and select **`HeadNode`**.
3. Read its **Transform Position and Rotation** — the pose the simulator computed.
4. Put those exact values into `MVR_Experimental.vrx`'s `HeadNode` as `PositionLocal` and
   `OrientationLocal`, with `Tracker="0"` and `UseTracker*="0"`.

Result: a static viewpoint that renders the same as the working tracked view in the editor,
and stays pinned (no head-follow) on the real CAVE.

> Alternative if that still misbehaves: keep the config fully tracked (identical to
> `MVR.vrx`) for editor testing, and only switch the head to fixed for CAVE deployment —
> validating the fixed viewpoint on the physical CAVE rather than in the dev-PC preview.

---

## ⚠️ Gotcha: the config keeps auto-reverting

`MVR_Experimental.vrx` was repeatedly rewritten back to an **older v1.7.3.0 format with the
head reset to `0,0,0`** between edits (something in the workflow — likely the MiddleVR
Configuration GUI or a save action — regenerates it). `MVR.vrx` is **not** affected because
it isn't being regenerated.

Consequences:
- A full v2.4.0.4 rebuild written to the file did **not** survive — it flipped back to
  v1.7.3.0.
- Only the *last* edit before testing tends to stick.

**Before relying on this config, confirm `git status` is clean** after any Unity session.
If it shows `MVR_Experimental.vrx` modified back to `Version="1.7.3.0"` with the head at
`0,0,0`, the regenerator struck again — set the head pose through whatever tool is rewriting
it (e.g. set **Head Position** in the MiddleVR Configuration GUI and save *there*), not by
editing the file directly.

---

## Committed checkpoint

Commit `2f19018` on `middleVR-version`: adds `BootMiddleVR2.unity`, `MVR_Experimental.vrx`,
the CAVE wall materials, and a `MVR.vrx` head-node change. The committed
`MVR_Experimental.vrx` is the tracked-but-frozen head in v1.7.3.0 format — the sky-in-preview
issue is still open per above.
