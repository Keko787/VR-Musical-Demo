# Bullet Trajectory Hierarchy Fix

**Area:** Gun / projectiles
**File:** `Assets/_Project/Scripts/Gun/ProjectilePool.cs`
**Category:** Unity physics gotcha — dynamic Rigidbody under a moving parent

---

## TL;DR

Never parent a non-kinematic Rigidbody to a transform that moves. Pooled projectiles
were being instantiated as children of the `ProjectilePool` (which sits on the gun, which
rides the controller). Moving the gun mid-flight dragged darts that were already in the
air. Fix: spawn projectiles into a **world-space container that never moves**.

---

## Symptom

Darts fired in the correct direction, but if you **moved or rotated the gun while a dart
was still in the air, the dart's path changed** — it got yanked sideways / re-aimed
instead of continuing on its own arc.

## Root cause

`ProjectilePool` instantiated each projectile as a child of its own transform:

```csharp
var p = Instantiate(m_Prefab, transform);   // transform = the pool = the Gun root
```

The component hierarchy at runtime:

```
Right Controller        (tracks the physical hand — moves every frame)
└── Gun                 (ProjectilePool component lives here)
    └── Projectile(s)    ← spawned as children → inherit the controller's motion
```

A child's **world** position is `parent.worldMatrix * child.localPosition`. So when the
controller moves, every child's world position is recomputed from the new parent pose —
**regardless of physics**.

For a non-kinematic Rigidbody this is worse than just "it follows the parent": the physics
engine is trying to own the body's world pose each `FixedUpdate`, while the transform
hierarchy is simultaneously re-deriving it from the moving parent. The two fight, and the
visible result is the dart's trajectory snapping around as the gun moves.

## The fix

Decouple projectiles from the gun by parenting them to a standalone container created at
runtime — a root GameObject that never moves:

```csharp
ObjectPool<Projectile> m_Pool;
Transform m_Container;

// A world-space root that never moves. Projectiles must NOT be parented to the gun
// (which rides the controller) or the moving parent drags in-flight darts.
Transform Container => m_Container != null
    ? m_Container
    : m_Container = new GameObject($"{name} (Projectiles)").transform;

Projectile CreateInstance()
{
    var p = Instantiate(m_Prefab, Container);   // was: Instantiate(m_Prefab, transform)
    p.SetPool(this);
    return p;
}
```

After this, the gun only sets a dart's **initial** position and velocity at the instant of
firing (`m_Muzzle.position` / `m_Muzzle.forward` in `GunController.Fire`). Once launched,
the dart's flight is governed purely by physics. Moving the gun afterward has no effect on
darts already in the air.

No prefab/scene/Editor change is needed — the bug and the fix are entirely in the spawn
parent.

## Why the container, not just `Instantiate(m_Prefab)` with no parent

`Instantiate(m_Prefab)` (null parent) also works — it spawns at the active scene root. The
named container is purely for **Hierarchy tidiness**: all pooled darts gather under one
`(Projectiles)` object instead of cluttering the scene root. It also avoids inheriting any
non-1 scale the gun might have.

---

## General principle for this project

> **Dynamic (non-kinematic) Rigidbodies must live in world space, never under a moving
> transform.** Anything that is launched, thrown, dropped, or otherwise simulated by
> physics should be spawned unparented or into a static container — not under the hand,
> gun, or any tracked/animated object.

This applies beyond darts. When we add physics-driven targets, debris, shells, or
pickups, spawn them the same way. If a future object genuinely needs to ride a moving
parent (e.g. a holstered gun), it should be **kinematic** while attached and only switch
to dynamic once released into the world.

## Related notes

- The arc-follow rotation in `Projectile.FixedUpdate` (`m_Rb.MoveRotation(LookRotation(velocity))`)
  is independent of this fix and works correctly with the world-space container.
- Pool release (`SetActive(false)`) leaves inactive darts parked under the container until
  reused; `ProjectilePool.Get` repositions them on reuse, so their parked location doesn't
  matter.
