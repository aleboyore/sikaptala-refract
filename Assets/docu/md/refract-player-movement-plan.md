# Refract — Player Movement Plan
> Tarodev Ultimate 2D Controller (Source) · Unity 6.4 (6000.4.6f1) · Input System 1.19.0 · GDD v1.0

---

## Source Files Required

Copy these directly into `Assets/Scripts/Player/` — no package import needed:

```
Assets/
  Scripts/
    Player/
      PlayerController.cs       ← Tarodev source (provided)
      ScriptableStats.cs        ← Tarodev source (provided)
      MirrorController.cs       ← NEW: extends PlayerController for P2
    Input/
      InputHandler.cs           ← NEW: routes shared input to both players
    State/
      PlayerState.cs            ← NEW: enum (Normal, Frozen, LockedIn, Dead)
    Game/
      GameManager.cs            ← NEW: lock-in tracking, death, checkpoint restore
      CheckpointManager.cs      ← NEW: stores/restores both player positions
      SyncManager.cs            ← NEW: sync button logic, restriction checks, feedback
```

---

## 1. ScriptableStats — Suggested Values for Refract

Create **two** `ScriptableStats` assets (Right-click → Create in Project):
- `PlayerStats_P1.asset` — normal gravity player
- `PlayerStats_P2.asset` — inverted gravity player (same values; gravity handled in code)

| Field | Value | Notes |
|---|---|---|
| `MaxSpeed` | `14` | Tarodev default; feels snappy |
| `Acceleration` | `120` | Fast, responsive ground accel |
| `GroundDeceleration` | `60` | Stops quickly on ground |
| `AirDeceleration` | `30` | Floatier stop in air |
| `GroundingForce` | `-1.5` | Keeps players on slopes |
| `GrounderDistance` | `0.05` | Tight ground detection |
| `JumpPower` | `36` | Tarodev default; strong pop |
| `MaxFallSpeed` | `40` | Terminal velocity cap |
| `FallAcceleration` | `110` | Heavy fall gravity |
| `JumpEndEarlyGravityModifier` | `3` | Variable jump height multiplier |
| `CoyoteTime` | `0.15` | 150ms coyote window |
| `JumpBuffer` | `0.2` | 200ms jump buffer window |
| `SnapInput` | `true` | Ensures keyboard/gamepad parity |

---

## 2. New Input System Setup

### 2a. Unity 6.4 — No Installation Needed

Unity 6 (6000.4.x) ships with **Input System 1.19.0 pre-installed**. For new projects created in Unity 6, the package is already present and `Active Input Handling` is already set to `Input System` — no package install, no backend switch, no editor restart required.

Verify in **Edit → Project Settings → Player → Other Settings → Active Input Handling** — it should read `Input System Package (New)`. If it shows `Both` or `Input Manager (Old)`, switch it to `Input System Package (New)`.

> ⚠️ The original Tarodev `GatherInput()` uses legacy `Input.GetButtonDown` / `Input.GetAxisRaw`. With `Active Input Handling` set to `Input System Package (New)`, **these calls will throw errors at runtime** and return nothing. Step 3b below replaces them entirely with New Input System reads.

### 2b. Create the PlayerInputActions Asset

Unity 6 creates a default `InputSystem_Actions` asset at the project root. You can either extend that or create a dedicated one for Refract — the latter is cleaner for a jam project.

1. Right-click in `Assets/Input/` → **Create → Input Actions** → name it `PlayerInputActions`
2. Open the asset, create one action map: `Gameplay`
3. Add these actions:

| Action | Type | Bindings |
|---|---|---|
| `Move` | Value / Vector2 | WASD, Arrow Keys, Left Stick |
| `Jump` | Button | Space, C, Gamepad South |
| `Decouple` | Button | Left Shift, Gamepad West |
| `LockIn` | Button | W, Up Arrow, Gamepad North |
| `Sync` | Button | R, Gamepad East |

4. In the asset Inspector, tick **Generate C# Class** and hit **Apply** — Unity generates and keeps the `PlayerInputActions` C# wrapper in sync automatically.
5. Set the C# class name to `PlayerInputActions`, namespace to `TarodevController` (or leave blank).

### 2c. InputHandler.cs — Single Input Source

`InputHandler` is the **only** place that touches `PlayerInputActions`. All other scripts read from it. This avoids duplicate action map instances and double-enabling bugs.

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using TarodevController;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    // Both players read from these each frame
    public FrameInput P1Input { get; private set; }
    public FrameInput P2Input { get; private set; }

    // Expose raw actions so other systems (Decouple, LockIn) can subscribe
    public PlayerInputActions Actions { get; private set; }

    private void Awake()
    {
        Instance = this;
        Actions = new PlayerInputActions();
        Actions.Gameplay.Enable();
    }

    private void OnDestroy()
    {
        Actions.Gameplay.Disable();
        Actions.Dispose();
    }

    private void Update()
    {
        var raw = Actions.Gameplay.Move.ReadValue<Vector2>();
        bool jumpDown = Actions.Gameplay.Jump.WasPressedThisFrame();
        bool jumpHeld = Actions.Gameplay.Jump.IsPressed();

        // P1: normal X direction
        P1Input = new FrameInput
        {
            Move = raw,
            JumpDown = jumpDown,
            JumpHeld = jumpHeld
        };

        // P2: X is flipped per GDD M-01 — same jump button, same semantics
        P2Input = new FrameInput
        {
            Move = new Vector2(-raw.x, raw.y),
            JumpDown = jumpDown,
            JumpHeld = jumpHeld
        };
    }
}
```

> **Scene setup:** Add `InputHandler` as a component on a dedicated `_Managers` GameObject in every scene. It must exist before either player controller's `Awake()` runs — use Unity's **Script Execution Order** (`Edit → Project Settings → Script Execution Order`) to set `InputHandler` to `-100`.

---

## 3. PlayerController.cs — Modifications for Refract

The Tarodev source is used **as-is** with the following targeted changes only.

### 3a. Set gravityScale = 0 in Awake()

Tarodev's controller manages gravity entirely via `_frameVelocity.y` and sets `_rb.velocity` directly. Unity's built-in gravity must be disabled so they don't fight each other.

```csharp
private void Awake()
{
    _rb = GetComponent<Rigidbody2D>();
    _col = GetComponent<CapsuleCollider2D>();
    _rb.gravityScale = 0;   // ADD THIS — Tarodev handles gravity manually
    _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
}
```

### 3b. Replace GatherInput() — pull from InputHandler

```csharp
// Replace the old GatherInput() body:
private void GatherInput()
{
    _frameInput = InputHandler.Instance.P1Input;

    if (_stats.SnapInput)
    {
        _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold
            ? 0 : Mathf.Sign(_frameInput.Move.x);
        _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold
            ? 0 : Mathf.Sign(_frameInput.Move.y);
    }

    if (_frameInput.JumpDown)
    {
        _jumpToConsume = true;
        _timeJumpWasPressed = _time;
    }
}
```

### 3c. Change private fields to protected

`MirrorController` inherits from `PlayerController` and needs access to these:

```csharp
// Change from private to protected:
protected Rigidbody2D _rb;
protected CapsuleCollider2D _col;
protected FrameInput _frameInput;
protected Vector2 _frameVelocity;
protected bool _grounded;
protected float _frameLeftGrounded;
protected bool _jumpToConsume;
protected bool _bufferedJumpUsable;
protected bool _endedJumpEarly;
protected bool _coyoteUsable;
protected float _timeJumpWasPressed;
protected float _time;
protected bool _cachedQueryStartInColliders;
[SerializeField] protected ScriptableStats _stats;
```

### 3d. Make key methods virtual for overriding

```csharp
protected virtual void Awake() { ... }
protected virtual void GatherInput() { ... }
protected virtual void CheckCollisions() { ... }
protected virtual void HandleJump() { ... }
protected virtual void ExecuteJump() { ... }
protected virtual void HandleGravity() { ... }
```

### 3e. Add PlayerState + freeze/lock-in methods

```csharp
// Add to PlayerController.cs:
public PlayerState State { get; private set; } = PlayerState.Normal;

public void EnterFrozenState(float duration)
{
    if (State == PlayerState.LockedIn) return;
    State = PlayerState.Frozen;
    _rb.constraints = RigidbodyConstraints2D.FreezeAll;
    StartCoroutine(FreezeRoutine(duration));
}

private IEnumerator FreezeRoutine(float duration)
{
    yield return new WaitForSeconds(duration);
    State = PlayerState.Normal;
    _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
}

public void EnterLockedInState()
{
    State = PlayerState.LockedIn;
    _rb.constraints = RigidbodyConstraints2D.FreezeAll;
}

public void ResetToCheckpoint(Vector3 position)
{
    State = PlayerState.Normal;
    _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    _rb.velocity = Vector2.zero;
    _frameVelocity = Vector2.zero;
    transform.position = position;
}
```

### 3f. Guard FixedUpdate — skip if not Normal

```csharp
private void FixedUpdate()
{
    if (State != PlayerState.Normal) return;   // ADD THIS LINE

    CheckCollisions();
    HandleJump();
    HandleDirection();
    HandleGravity();
    ApplyMovement();
}
```

### 3g. Add Speedy Apex to HandleDirection()

Not in the provided source. Add to `HandleDirection()` in the base class (MirrorController inherits it automatically):

```csharp
// Add these two serialized fields near the top of PlayerController.cs:
[SerializeField] private float _apexThreshold = 2f;
[SerializeField] private float _apexBonus = 2f;

// Replace HandleDirection() with:
private void HandleDirection()
{
    if (_frameInput.Move.x == 0)
    {
        var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
        _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
    }
    else
    {
        // Speedy apex: boost speed near the jump peak (near-zero vertical velocity)
        float apexBonus = 0f;
        if (!_grounded && Mathf.Abs(_rb.velocity.y) < _apexThreshold)
            apexBonus = _apexBonus * _frameInput.Move.x;

        _frameVelocity.x = Mathf.MoveTowards(
            _frameVelocity.x,
            _frameInput.Move.x * _stats.MaxSpeed + apexBonus,
            _stats.Acceleration * Time.fixedDeltaTime);
    }
}
```

### 3h. Lock-in trigger detection

```csharp
// Add to PlayerController.cs:
private bool _isAtExitZone;

private void OnTriggerEnter2D(Collider2D other)
{
    if (other.CompareTag("ExitZone")) _isAtExitZone = true;
}

private void OnTriggerExit2D(Collider2D other)
{
    if (other.CompareTag("ExitZone")) _isAtExitZone = false;
}

// In Update(), after GatherInput():
private void Update()
{
    _time += Time.deltaTime;
    GatherInput();

    // LockIn reads directly from InputHandler.Actions — not from Move.y
    // This avoids conflict with the upward movement input
    if (_isAtExitZone
        && InputHandler.Instance.Actions.Gameplay.LockIn.WasPressedThisFrame()
        && State == PlayerState.Normal)
    {
        EnterLockedInState();
        GameManager.Instance.NotifyLockIn(this);
    }
}
```

---

## 4. MirrorController.cs — Player 2 (Inverted Gravity)

Full file. Place at `Assets/Scripts/Player/MirrorController.cs`:

```csharp
using UnityEngine;

namespace TarodevController
{
    public class MirrorController : PlayerController
    {
        protected override void Awake()
        {
            base.Awake();
            // P2 falls toward +Y (ceiling = their floor)
            // gravityScale stays 0 — HandleGravity() manages everything
        }

        // P2 reads X-flipped input from InputHandler
        protected override void GatherInput()
        {
            _frameInput = InputHandler.Instance.P2Input;

            if (_stats.SnapInput)
            {
                _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold
                    ? 0 : Mathf.Sign(_frameInput.Move.x);
                _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold
                    ? 0 : Mathf.Sign(_frameInput.Move.y);
            }

            if (_frameInput.JumpDown)
            {
                _jumpToConsume = true;
                _timeJumpWasPressed = _time;
            }
        }

        // P2 ground is UP, ceiling is DOWN
        protected override void CheckCollisions()
        {
            Physics2D.queriesStartInColliders = false;

            bool groundHit = Physics2D.CapsuleCast(
                _col.bounds.center, _col.size, _col.direction, 0,
                Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);

            bool ceilingHit = Physics2D.CapsuleCast(
                _col.bounds.center, _col.size, _col.direction, 0,
                Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);

            // Clamp velocity away from ceiling (world floor)
            if (ceilingHit) _frameVelocity.y = Mathf.Max(0, _frameVelocity.y);

            if (!_grounded && groundHit)
            {
                _grounded = true;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
            }
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }

        // P2 jumps toward +Y (toward their ceiling = their floor)
        protected override void ExecuteJump()
        {
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _coyoteUsable = false;
            _frameVelocity.y = _stats.JumpPower;  // +Y = toward P2's floor
            Jumped?.Invoke();
        }

        // P2 "rising" is +Y; early release cuts the +Y arc
        protected override void HandleJump()
        {
            // For P2, rising = positive Y velocity
            if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.velocity.y > 0)
                _endedJumpEarly = true;

            if (!_jumpToConsume && !HasBufferedJump) return;
            if (_grounded || CanUseCoyote) ExecuteJump();
            _jumpToConsume = false;
        }

        // P2 gravity pulls toward +Y (away from world floor, toward world ceiling)
        protected override void HandleGravity()
        {
            if (_grounded && _frameVelocity.y >= 0f)
            {
                // Grounding force: push gently into their floor (world ceiling direction = +Y)
                _frameVelocity.y = Mathf.Abs(_stats.GroundingForce);
            }
            else
            {
                var inAirGravity = _stats.FallAcceleration;
                // Early release while rising (+Y): apply extra gravity to cut arc
                if (_endedJumpEarly && _frameVelocity.y > 0)
                    inAirGravity *= _stats.JumpEndEarlyGravityModifier;

                // P2 falls toward +MaxFallSpeed (toward world ceiling)
                _frameVelocity.y = Mathf.MoveTowards(
                    _frameVelocity.y, _stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
            }
        }
    }
}
```

---

## 5. Feel-Good Tricks — Where They Live in Source

| Feature | Location | Works for P2? |
|---|---|---|
| **Coyote Time** | `CanUseCoyote` property, `_frameLeftGrounded` set in `CheckCollisions()` | ✅ Set in overridden `CheckCollisions()` |
| **Jump Buffer** | `HasBufferedJump` property, `_timeJumpWasPressed` in `GatherInput()` | ✅ Identical logic, no changes |
| **Variable Jump Height** | `_endedJumpEarly` + `JumpEndEarlyGravityModifier` in `HandleGravity()` | ✅ Overridden: detects +Y rise for P2 |
| **Speedy Apex** | Added to `HandleDirection()` in base class | ✅ Inherited by MirrorController |

---

## 6. Decouple System

> GDD M-04: Hold to freeze the other player for 3s. One charge per checkpoint.

```csharp
// DecoupleManager.cs
using UnityEngine;
using TarodevController;

public class DecoupleManager : MonoBehaviour
{
    public static DecoupleManager Instance { get; private set; }

    [SerializeField] private PlayerController _p1;
    [SerializeField] private MirrorController _p2;

    private int _p1Charges = 1;

    private void Awake() => Instance = this;

    private void Update()
    {
        // Read from InputHandler — no separate PlayerInputActions instance needed
        if (InputHandler.Instance.Actions.Gameplay.Decouple.WasPressedThisFrame()
            && _p1Charges > 0
            && _p1.State == PlayerState.Normal
            && _p2.State == PlayerState.Normal)
        {
            _p2.EnterFrozenState(3f);
            _p1Charges--;
        }
    }

    public void RestoreCharges() => _p1Charges = 1;
}
```

---

## 7. Sync System

> Both players snap to the world-space midpoint between them. Requires: same height (±1 unit) AND proximity (±4 units horizontal). On failure: red flash + screen shake.

### 7a. Sync Conditions

Two checks must both pass before a snap is allowed:

| Condition | Check | Threshold |
|---|---|---|
| **Same height** | `Mathf.Abs(p1.y - p2.y) <= heightThreshold` | `1f` unit |
| **Close enough** | `Mathf.Abs(p1.x - p2.x) <= proximityThreshold` | `4f` units |

P2's Y is in world space. Because P2 is on the ceiling, they will generally share a Y band with P1 when both are standing on their respective floors at the same horizontal tier of the level. The ±1 unit threshold is intentionally tight — this is a skill action, not a catch-all.

### 7b. Midpoint Snap

When conditions pass, both players move to the **horizontal midpoint** of their positions, keeping each player's Y unchanged (so P1 stays on the floor, P2 stays on the ceiling):

```
midX = (p1.position.x + p2.position.x) / 2f
P1 snaps to (midX, p1.position.y)
P2 snaps to (midX, p2.position.y)
```

Both players keep their own Y — only X is synced to the midpoint. This preserves their respective floor contacts and avoids clipping.

### 7c. SyncManager.cs

```csharp
using System.Collections;
using UnityEngine;
using TarodevController;

public class SyncManager : MonoBehaviour
{
    public static SyncManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerController _p1;
    [SerializeField] private MirrorController _p2;

    [Header("Restrictions")]
    [SerializeField] private float _heightThreshold = 1f;      // ±Y units
    [SerializeField] private float _proximityThreshold = 4f;   // ±X units

    [Header("Cooldown")]
    [SerializeField] private float _syncCooldown = 2f;         // seconds between uses
    private float _lastSyncTime = -999f;

    [Header("Feedback")]
    [SerializeField] private SyncFeedback _feedback;           // assign in Inspector

    private void Awake() => Instance = this;

    private void Update()
    {
        if (!InputHandler.Instance.Actions.Gameplay.Sync.WasPressedThisFrame()) return;
        if (_p1.State != PlayerState.Normal || _p2.State != PlayerState.Normal) return;
        if (Time.time < _lastSyncTime + _syncCooldown) return;

        if (CanSync())
            ExecuteSync();
        else
            _feedback.PlayFailure();
    }

    private bool CanSync()
    {
        float deltaY = Mathf.Abs(_p1.transform.position.y - _p2.transform.position.y);
        float deltaX = Mathf.Abs(_p1.transform.position.x - _p2.transform.position.x);
        return deltaY <= _heightThreshold && deltaX <= _proximityThreshold;
    }

    private void ExecuteSync()
    {
        float midX = (_p1.transform.position.x + _p2.transform.position.x) / 2f;

        // Snap X only — each player keeps their own Y (floor/ceiling contact)
        _p1.SnapToX(midX);
        _p2.SnapToX(midX);

        _lastSyncTime = Time.time;
        _feedback.PlaySuccess();
    }
}
```

### 7d. SnapToX() — Add to PlayerController.cs

```csharp
// Add to PlayerController.cs:
public void SnapToX(float worldX)
{
    // Zero out horizontal velocity so there's no carry-over momentum
    _frameVelocity.x = 0f;
    _rb.velocity = new Vector2(0f, _rb.velocity.y);
    transform.position = new Vector3(worldX, transform.position.y, transform.position.z);
}
```

### 7e. SyncFeedback.cs — Visual & Audio Warning

```csharp
using System.Collections;
using UnityEngine;

public class SyncFeedback : MonoBehaviour
{
    [Header("Failure — Red Flash")]
    [SerializeField] private SpriteRenderer _p1Sprite;
    [SerializeField] private SpriteRenderer _p2Sprite;
    [SerializeField] private Color _failureColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float _flashDuration = 0.15f;
    [SerializeField] private int _flashCount = 2;

    [Header("Failure — Screen Shake")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _shakeMagnitude = 0.12f;
    [SerializeField] private float _shakeDuration = 0.25f;

    [Header("Success — White Flash")]
    [SerializeField] private Color _successColor = new Color(1f, 1f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _failureClip;
    [SerializeField] private AudioClip _successClip;

    public void PlayFailure()
    {
        StopAllCoroutines();
        StartCoroutine(FlashSprites(_failureColor, _flashCount));
        StartCoroutine(ShakeCamera());
        if (_failureClip) _audioSource.PlayOneShot(_failureClip);
    }

    public void PlaySuccess()
    {
        StopAllCoroutines();
        StartCoroutine(FlashSprites(_successColor, 1));
        if (_successClip) _audioSource.PlayOneShot(_successClip);
    }

    private IEnumerator FlashSprites(Color flashColor, int count)
    {
        Color p1Original = _p1Sprite.color;
        Color p2Original = _p2Sprite.color;

        for (int i = 0; i < count; i++)
        {
            _p1Sprite.color = flashColor;
            _p2Sprite.color = flashColor;
            yield return new WaitForSeconds(_flashDuration);
            _p1Sprite.color = p1Original;
            _p2Sprite.color = p2Original;
            yield return new WaitForSeconds(_flashDuration);
        }
    }

    private IEnumerator ShakeCamera()
    {
        Vector3 origin = _cameraTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < _shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * _shakeMagnitude;
            float y = Random.Range(-1f, 1f) * _shakeMagnitude;
            _cameraTransform.localPosition = origin + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _cameraTransform.localPosition = origin;
    }
}
```

### 7f. Sync State Rules

| Situation | Sync allowed? |
|---|---|
| Both `Normal`, conditions met | ✅ Snaps both to midX |
| Either player `Frozen` | ❌ Blocked (state guard in `Update()`) |
| Either player `LockedIn` | ❌ Blocked (state guard in `Update()`) |
| Height or proximity out of range | ❌ Failure feedback fires |
| Within cooldown window | ❌ Silently ignored (cooldown, not a mistake) |
| Synced player overlapping a wall | ⚠️ Physics resolves next frame — ensure midX lands on open floor; consider a short overlap check before snapping |

### 7g. Sync Condition HUD (Optional)

To help players know when they're in range, drive a UI indicator from `SyncManager`:

```csharp
// Call this from a UI script each frame:
public bool IsSyncReady =>
    _p1.State == PlayerState.Normal &&
    _p2.State == PlayerState.Normal &&
    CanSync() &&
    Time.time >= _lastSyncTime + _syncCooldown;
```

Show a green icon when `IsSyncReady` is true, grey when not. This gives players feedback without spelling out the exact numbers.

---

## 8. GameManager & CheckpointManager

```csharp
// GameManager.cs
using UnityEngine;
using TarodevController;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private PlayerController _p1;
    [SerializeField] private MirrorController _p2;

    private void Awake() => Instance = this;

    public void NotifyLockIn(PlayerController player)
    {
        if (_p1.State == PlayerState.LockedIn && _p2.State == PlayerState.LockedIn)
            LoadNextLevel();
    }

    public void TriggerDeath()
    {
        _p1.ResetToCheckpoint(CheckpointManager.Instance.P1Position);
        _p2.ResetToCheckpoint(CheckpointManager.Instance.P2Position);
        DecoupleManager.Instance.RestoreCharges();
    }

    private void LoadNextLevel()
    {
        // UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
    }
}

// CheckpointManager.cs
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }
    public Vector3 P1Position { get; private set; }
    public Vector3 P2Position { get; private set; }

    private void Awake() => Instance = this;

    public void SaveCheckpoint(Vector3 p1Pos, Vector3 p2Pos)
    {
        P1Position = p1Pos;
        P2Position = p2Pos;
    }
}
```

Checkpoint objects call `CheckpointManager.Instance.SaveCheckpoint(p1.transform.position, p2.transform.position)` on trigger.

---

## 9. Layer Setup (Unity Physics2D Matrix)

| Layer | Collides with | Used for |
|---|---|---|
| `RealOnly` | Ground, Shared | P1 platforms and walls |
| `MirrorOnly` | Ground, Shared | P2 platforms and walls |
| `Shared` | Both | Teal box, triggers |
| `Player_P1` | RealOnly, Shared | P1 character |
| `Player_P2` | MirrorOnly, Shared | P2 character |

Set in **Edit → Project Settings → Physics 2D → Layer Collision Matrix**.

Update `~_stats.PlayerLayer` in `CheckCollisions()` — for P2, pass `Player_P2` as the excluded layer.

---

## 10. Implementation Order (Day 1 Sprint)

1. **Verify Input System** — confirm `Active Input Handling` is `Input System Package (New)` in Project Settings → Player (Unity 6.4 sets this by default; Input System 1.19.0 is pre-installed)
2. **Create `PlayerInputActions` asset** in `Assets/Input/` — action map `Gameplay` with `Move` (Vector2), `Jump`, `Decouple`, `LockIn`, `Sync` (Buttons); tick Generate C# Class and Apply
3. Add `PlayerController.cs` and `ScriptableStats.cs` — create two stat assets
4. Set `_rb.gravityScale = 0` in `Awake()` — confirm Tarodev gravity still functions
5. Change all relevant `private` fields to `protected`
6. Make key methods `virtual`
7. Add `PlayerState` enum and state methods (`EnterFrozenState`, `EnterLockedInState`, `ResetToCheckpoint`, `SnapToX`)
8. Add `FixedUpdate` state guard
9. Create `InputHandler.cs` — set Script Execution Order to `-100`
10. Replace `GatherInput()` in `PlayerController.cs` to read `InputHandler.Instance.P1Input`
11. Fix lock-in `Update()` to use `InputHandler.Instance.Actions.Gameplay.LockIn`
12. Add Speedy Apex fields + logic to `HandleDirection()`
13. Create `MirrorController.cs` — all overrides
14. Create `DecoupleManager.cs` — reads `InputHandler.Instance.Actions.Gameplay.Decouple`
15. Create `SyncManager.cs` + `SyncFeedback.cs` — wire sprites, camera, audio in Inspector
16. Create `GameManager.cs`, `CheckpointManager.cs`
17. Set up layer collision matrix
18. Tag objects: `ExitZone`, `Hazard`, `Checkpoint`

---

## 11. Tuning Checklist

Test each item for **both players independently** before Day 2:

- [ ] P1 walks/runs at correct speed, stops cleanly
- [ ] P2 moves with X-flipped input
- [ ] P1 jumps upward; P2 jumps toward ceiling
- [ ] Coyote jump fires for both at ledge/ceiling edges
- [ ] Jump buffer fires for both after early press
- [ ] Tap = shorter arc than hold (both players)
- [ ] Speedy apex gives visible horizontal boost at peak (both)
- [ ] Decouple freezes P2 for 3s; charge depletes and restores at checkpoint
- [ ] Lock-in commits player, removes from input
- [ ] Both locked-in → level clears
- [ ] Death resets both players to checkpoint simultaneously
- [ ] P1 only collides with RealOnly + Shared layers
- [ ] P2 only collides with MirrorOnly + Shared layers
- [ ] Sync snaps both players to midX when height (±1u) and proximity (±4u) conditions are met
- [ ] Sync preserves each player's Y position after snap
- [ ] Sync zeroes horizontal velocity on both players after snap
- [ ] Sync blocked when either player is Frozen or LockedIn
- [ ] Sync failure: red flash + screen shake fires when conditions are NOT met
- [ ] Sync success: white flash fires when snap executes
- [ ] Sync cooldown prevents rapid re-use (2s window)
- [ ] Sync HUD indicator turns green when `IsSyncReady` is true

---

*Plan authored for Refract · SIKAPtala 2026 Game Jam · Engine: Unity 6.4 (6000.4.6f1) · Input System 1.19.0*
