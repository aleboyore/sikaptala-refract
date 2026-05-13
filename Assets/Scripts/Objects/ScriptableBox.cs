using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class ScriptableBox : MonoBehaviour, ICheckpointRestorer
{
    [Header("Behavior")]
    [SerializeField] private BoxBehaviorProfile profile;

    Rigidbody2D rb;
    Collider2D boxCollider;
    Vector2 restPosition;
    Vector3 spawnPosition;
    readonly HashSet<GameObject> contactSet = new();
    readonly HashSet<GameObject> effectAppliedSet = new();
    readonly HashSet<GameObject> effectLockedPlayers = new();
    readonly HashSet<GameObject> _lockingPlayers = new();
    
    private RigidbodyConstraints2D _prevConstraints;

    public BoxBehaviorProfile Profile => profile;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        ApplyProfile();
        restPosition = rb.position;
        spawnPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (profile == null || profile.returnToRestSpeed <= 0f) return;

        rb.MovePosition(Vector2.MoveTowards(rb.position, restPosition, profile.returnToRestSpeed * Time.fixedDeltaTime));
    }

    public void ApplyProfile()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();

        if (profile == null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 8f;
            return;
        }

        rb.gravityScale = profile.allowVerticalMovement ? profile.gravityScale : 0f;
        rb.freezeRotation = true;
        rb.linearDamping = profile.linearDamping;
    }

    public bool CanStandOnBy(PlayerSkinState playerState)
    {
        if (profile == null) return true;
        return playerState == PlayerSkinState.Shard ? profile.shardCanStandOn : profile.veilCanStandOn;
    }

    public bool CanBePushedBy(PlayerSkinState playerState)
    {
        if (profile == null) return playerState == PlayerSkinState.Shard;
        return playerState == PlayerSkinState.Shard ? profile.shardCanPush : profile.veilCanPush;
    }

    public bool CanPassThroughBy(PlayerSkinState playerState)
    {
        if (profile == null) return false;
        return playerState == PlayerSkinState.Shard ? profile.shardCanPassThrough : profile.veilCanPassThrough;
    }

    public bool IsNotVisibleToSkinState(PlayerSkinState playerState)
    {
        if (profile == null) return false;
        return playerState == PlayerSkinState.Shard ? profile.notVisibleToShard : profile.notVisibleToVeil;
    }

    public bool CanKillOnPushBy(PlayerSkinState playerState)
    {
        if (profile == null) return false;
        return playerState == PlayerSkinState.Shard ? profile.shardKillsOnPush : profile.veilKillsOnPush;
    }

    public bool KillsOnTouchBy(PlayerSkinState playerState)
    {
        if (profile == null) return false;

        bool perSkinKill = playerState == PlayerSkinState.Shard
            ? profile.shardKillsOnTouch
            : profile.veilKillsOnTouch;

        return profile.killsOnTouch || perSkinKill;
    }

    public PowerupEffect GetContactEffect(PlayerSkinState playerState)
    {
        if (profile == null) return null;
        return playerState == PlayerSkinState.Shard ? profile.shardContactEffect : profile.veilContactEffect;
    }

    public bool IsOneWayDirectionValid(Vector2 direction)
    {
        if (profile == null || !profile.oneWayEnabled) return true;

        float dotProduct = Vector2.Dot(direction, Vector2.right);
        if (profile.pushLeftOnly) return dotProduct < 0;  // only accept negative (left) direction
        else return dotProduct > 0;  // only accept positive (right) direction
    }

    public void ReceivePush(Vector2 direction, float force)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (profile != null && profile.moveOnlyWhenGrounded && !IsGrounded()) return;
        float resistance = profile != null ? profile.pushResistance : 2f;
        rb.AddForce(direction.normalized * (force / Mathf.Max(0.0001f, resistance)), ForceMode2D.Impulse);
    }

    public void TriggerRespawn()
    {
        if (profile == null || profile.respawnDelay <= 0f) return;
        StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        gameObject.SetActive(false);
        yield return new WaitForSeconds(profile.respawnDelay);
        gameObject.SetActive(true);
        rb.bodyType = RigidbodyType2D.Dynamic;
        transform.position = spawnPosition;
        restPosition = rb.position;
        contactSet.Clear();
        effectAppliedSet.Clear();
        effectLockedPlayers.Clear();
    }

    /// <summary>
    /// Called by BoxPushHandler when player collides with this box.
    /// Resolves state, checks stand-on, applies contact effects, handles push/kill.
    /// </summary>
    public void OnPlayerInteract(GameObject player, Vector2 pushDir)
    {
        if (profile == null) return;

        CharacterState charState = player.GetComponent<CharacterState>();
        if (charState == null) return;

        PlayerSkinState playerState = charState.current;
        bool canPassThrough = CanPassThroughBy(playerState);
        bool canPush = CanBePushedBy(playerState);
        bool canKillOnPush = CanKillOnPushBy(playerState);
        bool killsOnTouch = KillsOnTouchBy(playerState);
        bool isTopContact = pushDir == Vector2.down;
        bool isSideContact = pushDir == Vector2.left || pushDir == Vector2.right;

        RefreshCollisionIgnore(player, playerState);
        RefreshVisibility(playerState);
        Debug.Log($"[ScriptableBox] OnPlayerInteract '{gameObject.name}' player={player.name} playerState={playerState} pushDir={pushDir} canPassThrough={CanPassThroughBy(playerState)} canPush={CanBePushedBy(playerState)} oneWayOk={IsOneWayDirectionValid(pushDir)}");

        // Kill-on-touch (no push involved)
        if (killsOnTouch)
        {
            GameManager.Instance?.TriggerDeath();
            return;
        }

        // Kill-on-push can still trigger on side contact even when pass-through is enabled.
        if (isSideContact && canKillOnPush)
        {
            Debug.Log($"[ScriptableBox] Kill-on-push triggered by '{gameObject.name}' for player={player.name} playerState={playerState} (before push resolution)");
            GameManager.Instance?.TriggerDeath();
            return;
        }

        // Pass-through — player ignores this box for the current contact
        if (canPassThrough)
            return;

        // If the player is standing on top of this box AND can stand on it, parent them so they move with it.
        if (isTopContact && CanStandOnBy(playerState))
        {
            player.transform.SetParent(transform, true);
            player.GetComponent<PlayerController>()?.SetGroundSupport(transform);
        }

        if ((isSideContact || isTopContact) && !canPush)
        {
            LockForPlayer(player);
        }
        else
        {
            ReleaseLockForPlayer(player);
        }

        if (isSideContact && canPush && IsOneWayDirectionValid(pushDir))
        {
            Debug.Log($"[ScriptableBox] Applying push to '{gameObject.name}' dir={pushDir}");
            ReceivePush(pushDir, 10f);
        }

        // Contact effect behavior depends on profile: repeat every interact tick, or once per contact.
        PowerupEffect fx = GetContactEffect(playerState);
        if (fx != null)
        {
            bool effectLocked = profile.lockContactEffect || effectLockedPlayers.Contains(player);

            bool canApplyEffect = true;
            if (effectLocked)
            {
                if (effectLockedPlayers.Contains(player))
                    canApplyEffect = false;
                else
                    effectLockedPlayers.Add(player);
            }

            if (canApplyEffect && profile.contactEffectRepeats && !effectLocked)
            {
                fx.Apply(player);
            }
            else if (canApplyEffect && effectAppliedSet.Add(player))
            {
                // For one-shot effects, also check the player-side tracker
                // so multiple boxes of the same type don't stack the effect.
                EffectTracker tracker = player.GetComponent<EffectTracker>();
                if (tracker == null || !tracker.IsOneShotApplied(fx))
                    fx.Apply(player);
            }
        }

        // Crush: if box is moving fast at the player after a push
        if (profile.crushOnContact && rb != null && rb.linearVelocity.magnitude > 5f)
        {
            Debug.Log($"[ScriptableBox] Crush triggered on '{gameObject.name}' velocity={rb.linearVelocity.magnitude}");
            GameManager.Instance?.TriggerDeath();
        }
    }

    /// <summary>
    /// Called by BoxPushHandler when player exits collision.
    /// Clears contact tracking and one-shot effect flag for future re-entry.
    /// </summary>
    public void OnPlayerExit(GameObject player)
    {
        contactSet.Remove(player);
        effectAppliedSet.Remove(player);
        effectLockedPlayers.Remove(player);
        ReleaseLockForPlayer(player);

        if (player.transform.parent == transform)
            player.transform.SetParent(null, true);

        player.GetComponent<PlayerController>()?.ClearGroundSupport(transform);
        CharacterState charState = player.GetComponent<CharacterState>();
        PlayerSkinState playerState = charState != null ? charState.current : PlayerSkinState.Shard;
        RefreshCollisionIgnore(player, playerState);
        RefreshVisibility(playerState);
        // Clear one-shot effects so they can reapply if player walks back onto box
        EffectTracker tracker = player.GetComponent<EffectTracker>();
        if (tracker != null)
        {
            PowerupEffect fx = profile != null ? GetContactEffect(player.GetComponent<CharacterState>()?.current ?? PlayerSkinState.Shard) : null;
            if (fx != null && fx.oneShot)
                tracker.ClearOneShotFlag(fx);
        }
    }

    public void RefreshCollisionIgnore(GameObject player, PlayerSkinState playerState)
    {
        if (player == null) return;
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null || boxCollider == null) return;

        bool shouldIgnore = CanPassThroughBy(playerState);

        Physics2D.IgnoreCollision(playerCollider, boxCollider, shouldIgnore);
        Debug.Log($"[ScriptableBox] RefreshCollisionIgnore '{gameObject.name}' player={player.name} playerState={playerState} shouldIgnore={shouldIgnore}");
    }

    public void RefreshVisibility(PlayerSkinState playerState)
    {
        if (profile == null) return;

        bool isNotVisible = IsNotVisibleToSkinState(playerState);
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers == null || spriteRenderers.Length == 0) return;

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null) continue;

            // If this renderer is part of another gameplay object, skip it.
            // This prevents a box visibility rule from leaking into nested/adjacent blocks that share the same prefab tree.
            if (!IsOwnedVisibilityRenderer(spriteRenderer))
                continue;

            spriteRenderer.enabled = !isNotVisible;
            Debug.Log($"[ScriptableBox] Toggled renderer '{spriteRenderer.name}' on '{gameObject.name}' visible={!isNotVisible}");
        }

        Debug.Log($"[ScriptableBox] RefreshVisibility '{gameObject.name}' playerState={playerState} isNotVisible={isNotVisible}");
    }

    private bool IsOwnedVisibilityRenderer(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null) return false;

        // Only touch renderers that are actually under this box.
        ScriptableBox ownerBox = spriteRenderer.GetComponentInParent<ScriptableBox>();
        if (ownerBox != this)
            return false;

        // Do not let visibility rules reach into nested gameplay objects.
        if (spriteRenderer.GetComponentInParent<PlayerController>() != null)
            return false;
        if (spriteRenderer.GetComponentInParent<PowerupBehavior>() != null)
            return false;
        if (spriteRenderer.GetComponentInParent<CheckpointIdentity>() != null && spriteRenderer.GetComponent<CheckpointIdentity>() == null)
            return false;

        return true;
    }

    /// <summary>
    /// Removes the player from riding this box without clearing contact or effects.
    /// Used when a skin swap means the player should no longer remain parented or grounded.
    /// </summary>
    public void EvictPlayer(GameObject player)
    {
        if (player == null) return;

        ReleaseLockForPlayer(player);

        if (player.transform.parent == transform)
            player.transform.SetParent(null, true);

        player.GetComponent<PlayerController>()?.ClearGroundSupport(transform);
    }

    /// <summary>
    /// Clear effect-applied tracking when the player changes skin while still in contact.
    /// Also temporarily locks the effect to prevent immediate reapplication after the effect tracker has reset.
    /// The player will need to exit and re-enter the box to get the effect again.
    /// </summary>
    public void ClearEffectStateForPlayer(GameObject player)
    {
        if (player == null) return;
        effectAppliedSet.Remove(player);
        // Lock the effect temporarily so it won't reapply immediately after the swap clears the effect tracker
        effectLockedPlayers.Add(player);
        Debug.Log($"[ScriptableBox] Cleared and locked effect state for player '{player.name}' on box '{gameObject.name}' due to skin swap");
    }

    private void LockForPlayer(GameObject player)
    {
        if (player == null || rb == null) return;

        if (_lockingPlayers.Add(player))
        {
            if (_lockingPlayers.Count == 1)
                _prevConstraints = rb.constraints;

            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            Debug.Log($"[ScriptableBox] Locked '{gameObject.name}' horizontally for {player.name}");
        }
    }

    private void ReleaseLockForPlayer(GameObject player)
    {
        if (player == null || rb == null) return;

        if (!_lockingPlayers.Remove(player))
            return;

        if (_lockingPlayers.Count == 0)
        {
            rb.constraints = _prevConstraints;
            Debug.Log($"[ScriptableBox] Restored constraints for '{gameObject.name}' after {player.name} released the last lock");
        }
    }

    private bool IsGrounded()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();

        Vector2 origin = boxCollider != null ? (Vector2)boxCollider.bounds.center : rb.position;
        float halfHeight = boxCollider != null ? boxCollider.bounds.extents.y : 0.5f;
        LayerMask mask = LayerMask.GetMask("Ground", "Shared");

        return Physics2D.Raycast(origin, Vector2.down, halfHeight + 0.08f, mask);
    }

    /// <summary>
    /// Restore this box to its checkpoint state.
    /// Called when player dies and respawns at a checkpoint after this box was moved/interacted with.
    /// </summary>
    public void RestoreToCheckpoint()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // The checkpoint restore loop already restored the active state, transform,
        // and serialized component state. Do not overwrite those values here.
        // Only clear runtime motion and contact bookkeeping.
        // Ensure Rigidbody position matches the restored transform so rest/spawn
        // positions are consistent. Transform was restored earlier by CheckpointManager.
        rb.position = transform.position;
        rb.rotation = transform.rotation.eulerAngles.z;
        // Ensure physics body matches transform immediately and clear motion.
        rb.MovePosition(transform.position);
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.WakeUp();

        // Align saved positions used for respawn/rest behavior with current transform.
        restPosition = rb.position;
        spawnPosition = transform.position;

        // Clear all contact and lock state so the box can behave normally after respawn.
        contactSet.Clear();
        effectAppliedSet.Clear();
        effectLockedPlayers.Clear();
        _lockingPlayers.Clear();
        
        Debug.Log($"[Checkpoint] Box {gameObject.name} restored to spawn state");
    }

    /// <summary>
    /// Revert if this box was created after the checkpoint.
    /// Deactivates it so it doesn't affect the restored world state.
    /// </summary>
    public void RevertPostCheckpoint()
    {
        gameObject.SetActive(false);
        Debug.Log($"[Checkpoint] Box {gameObject.name} reverted (post-checkpoint)");
    }
}