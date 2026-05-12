# Box Behavior Plan Fix

## Summary of Bugs

Three distinct broken interactions between `CanStand`, `CanPush`, `CanFallThrough`, and `CanPassThrough`:

1. **Can Stand + Can't Push → Still pushes the box** (locking logic fails)
2. **Fall Through → Player hovers above box in a falling-but-not-grounded state**
3. **Pass Through + Kill On Touch → Kill doesn't trigger** (minor, mentioned in context)

---

## Bug 1 — Can Stand but Can't Push still pushes the box

### Root Cause

In `ScriptableBox.OnPlayerInteract()`, the locking block runs on `isSideContact || isTopContact` when `!canPush`. But the box's Rigidbody2D is **Dynamic** and the player's own Rigidbody2D applies friction forces during Physics2D contact resolution — even before `OnPlayerInteract` is called. By the time the lock (`FreezeAll`) is applied, the physics engine has already nudged the box.

Also: `_isLockedByPlayer` only tracks **one player**. If Veil is standing (locking the box) and Shard walks in, the else-branch fires and **unlocks** the box (`_lockerPlayer == player` is false for Shard, so Shard never unlocks it — actually the `else if` condition will never fire for Shard since `_lockerPlayer` is Veil). This means the single-locker design silently breaks for multi-skin scenarios.

Additionally, when `isTopContact && !canPush`, the lock correctly freezes the box — but the **parent call** (`player.transform.SetParent(transform, true)`) still happens just below the lock block because `CanStandOnBy` returns true. Now the player is parented to a frozen box. When the player walks sideways, their movement carries the box transform indirectly through the parent-child relationship and Unity will still resolve contacts, potentially sliding the frozen rb slightly. **The real problem: physics friction between the two Rigidbodies is not suppressed.**

### Fix

**In `ScriptableBox.OnPlayerInteract()`:**

Replace the current "lock" approach with **`Physics2D.IgnoreCollision` suppression for horizontal push** by separating standing support from side-push permission:

```csharp
// After determining canPush and isTopContact/isSideContact:

// 1. If player can stand but NOT push — suppress side-collision friction
//    by ignoring horizontal push force but keeping vertical support.
if (isTopContact && !canPush && CanStandOnBy(playerState))
{
    // Parent and ground the player (standing is allowed)
    player.transform.SetParent(transform, true);
    player.GetComponent<PlayerController>()?.SetGroundSupport(transform);

    // Freeze only horizontal movement on the box so standing weight
    // doesn't slowly drift it, but DON'T use FreezeAll (that kills gravity).
    if (!_isLockedByPlayer)
    {
        _prevConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        _isLockedByPlayer = true;
        _lockerPlayer = player;
    }
    return; // done — no push logic below
}

if (isSideContact && !canPush)
{
    // Player walks into the side but cannot push.
    // Freeze X so physics friction doesn't move it.
    if (!_isLockedByPlayer)
    {
        _prevConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        _isLockedByPlayer = true;
        _lockerPlayer = player;
    }
    return; // no push
}
```

**Replace `FreezeAll` with `FreezePositionX | FreezeRotation`** so the box can still fall (gravity still applies on Y). `FreezeAll` was the reason the box became a ghost mid-air if the player stood on it.

**Change `_lockerPlayer` to a `HashSet<GameObject>` to support two skins:**

```csharp
// Replace:
private bool _isLockedByPlayer = false;
private GameObject _lockerPlayer = null;

// With:
private readonly HashSet<GameObject> _lockingPlayers = new();
private RigidbodyConstraints2D _prevConstraints;
```

Update lock/unlock to add/remove from the set, and only restore constraints when the set is empty.

---

## Bug 2 — Fall Through causes player to hover above box (not grounded, not falling)

### Root Cause

`ShouldFallThroughNow()` only returns `true` when `playerRb.linearVelocity.y < -0.1f` — i.e., the player is **actively falling downward**. 

But the sequence is:
1. Player walks onto the box top normally → `CanStandOnBy` is true → player is **parented and grounded**.
2. `CanFallThroughBy` is also true, but `ShouldFallThroughNow` returns `false` (velocity.y is ~0 while grounded).
3. Result: Player is parented, grounded, and standing — but the intent was fall-through.

In `RefreshCollisionIgnore()`, there's also a logic short-circuit:

```csharp
bool shouldIgnore = forceIgnore || CanPassThroughBy(playerState);
if (!shouldIgnore && CanFallThroughBy(playerState) && forceIgnore) { ... }
```

The inner block only runs if `forceIgnore` is already true AND `CanFallThroughBy` is true — but `forceIgnore` is the *result of* `ShouldFallThroughNow`, which is false when grounded. So collision is never ignored.

The hover happens because: the player is in a `_grounded = true` state via `SetGroundSupport`, gravity is suppressed (`GroundingForce` is applied instead of fall), but the visual/animation system plays falling because `isFalling` anim bool is set based on `_grounded` being false after an eviction conflict.

### Fix

**In `ScriptableBox.OnPlayerInteract()`, add a pre-check at the top for fall-through:**

```csharp
bool canFallThrough = CanFallThroughBy(playerState);

// Fall-through takes priority over standing.
// If the player CAN fall through, NEVER parent or ground them — ever.
if (canFallThrough)
{
    // Make sure they are not parented to this box
    if (player.transform.parent == transform)
        player.transform.SetParent(null, true);

    player.GetComponent<PlayerController>()?.ClearGroundSupport(transform);

    // Enable collision ignore so they sink through
    Physics2D.IgnoreCollision(player.GetComponent<Collider2D>(), boxCollider, true);
    return;
}
```

This must happen **before** the `CanStandOnBy` parenting block. Fall-through is an override — if it's on, the player should never be grounded on this box regardless of velocity.

**In `RefreshCollisionIgnore()`, simplify the fall-through branch:**

```csharp
public void RefreshCollisionIgnore(GameObject player, PlayerSkinState playerState, bool forceIgnore = false)
{
    if (player == null || playerCollider == null || boxCollider == null) return;

    bool shouldIgnore = forceIgnore
        || CanPassThroughBy(playerState)
        || CanFallThroughBy(playerState); // fall-through always ignores collision

    Physics2D.IgnoreCollision(playerCollider, boxCollider, shouldIgnore);
}
```

This ensures that on skin swap (`OnPlayerStateChanged` in `BoxDetector`), if the new skin has `CanFallThrough = true`, collision is immediately ignored — not dependent on velocity.

---

## Bug 3 — Pass Through + Kill on Walk Through

### Current Behavior

`OnPlayerInteract` checks `killsOnTouch` before `canPassThrough`, so kill fires for both skins regardless. But `canKillOnPush` is checked only for `isSideContact` and returns early before the `canPassThrough` guard.

If the goal is: **"Skin can walk through it and the box kills them"**, `killsOnTouch` already works — it fires before pass-through. No change needed there.

If the goal is: **"Skin can walk through it and it does NOT kill"**, toggle `killsOnTouch = false` and `veilKillsOnPush = false`. The pass-through guard then exits cleanly.

If the goal is: **"One skin walks through safely, the other walks through and dies"**, the fix is to make `killsOnTouch` per-skin:

**In `BoxBehaviorProfile.cs`, add:**

```csharp
[Header("Kill On Touch (Per Skin)")]
public bool shardKillsOnTouch = false;
public bool veilKillsOnTouch = false;
```

**In `ScriptableBox.cs`, add a helper:**

```csharp
public bool KillsOnTouchBy(PlayerSkinState playerState)
{
    if (profile == null) return false;
    return playerState == PlayerSkinState.Shard ? profile.shardKillsOnTouch : profile.veilKillsOnTouch;
}
```

**In `OnPlayerInteract`, replace:**

```csharp
if (profile.killsOnTouch)
```

**With:**

```csharp
if (KillsOnTouchBy(playerState))
```

This way each skin's kill-on-touch is independent of the other, and pass-through still exits cleanly after the kill check.

---

## Files to Change

| File | Changes |
|---|---|
| `Scripts/Objects/BoxBehaviorProfile.cs` | Add `shardKillsOnTouch`, `veilKillsOnTouch` fields; remove old `killsOnTouch` (or keep for backwards compat) |
| `Scripts/Objects/ScriptableBox.cs` | Fix lock logic (FreezePositionX not FreezeAll), change locker to HashSet, fix fall-through priority (early return before stand logic), fix RefreshCollisionIgnore, add `KillsOnTouchBy()` |
| `Scripts/Player/BoxDetector.cs` | No changes needed — the state-change refresh call is correct; fixing ScriptableBox is sufficient |
| `Scripts/Player/PlayerController.cs` | No changes needed |

---

## Interaction Matrix After Fix

| Skin | CanStand | CanPush | CanFallThrough | CanPassThrough | Expected Result |
|---|---|---|---|---|---|
| Veil | ✅ | ❌ | ❌ | ❌ | Stands on box, box does NOT move |
| Shard | ✅ | ✅ | ❌ | ❌ | Stands on box, can push it |
| Veil | ✅ | ✅ | ❌ | ❌ | Stands on box, can also push it |
| Shard | ✅ | ❌ | ❌ | ❌ | Stands on box, box does NOT move |
| Veil | ❌ | ❌ | ✅ | ❌ | Falls through box, never grounded |
| Shard | ❌ | ❌ | ❌ | ✅ | Walks through box, no collision |
| Veil | ❌ | ❌ | ❌ | ✅ | Walks through, no kill |
| Shard | ❌ | ❌ | ❌ | ✅ | Walks through, kills (via shardKillsOnTouch) |