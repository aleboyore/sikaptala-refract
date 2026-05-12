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

    public bool CanStandOnBy(PlayerSkinState skin)
    {
        if (profile == null) return true;
        return skin == PlayerSkinState.Shard ? profile.shardCanStandOn : profile.veilCanStandOn;
    }

    public bool CanBePushedBy(PlayerSkinState skin)
    {
        if (profile == null) return skin == PlayerSkinState.Shard;
        return skin == PlayerSkinState.Shard ? profile.shardCanPush : profile.veilCanPush;
    }

    public bool CanPassThroughBy(PlayerSkinState skin)
    {
        if (profile == null) return false;
        return skin == PlayerSkinState.Shard ? profile.shardCanPassThrough : profile.veilCanPassThrough;
    }

    public bool CanFallThroughBy(PlayerSkinState skin)
    {
        if (profile == null) return false;
        return skin == PlayerSkinState.Shard ? profile.shardCanFallThrough : profile.veilCanFallThrough;
    }

    public bool CanKillOnPushBy(PlayerSkinState skin)
    {
        if (profile == null) return false;
        return skin == PlayerSkinState.Shard ? profile.shardKillsOnPush : profile.veilKillsOnPush;
    }

    public PowerupEffect GetContactEffect(PlayerSkinState skin)
    {
        if (profile == null) return null;
        return skin == PlayerSkinState.Shard ? profile.shardContactEffect : profile.veilContactEffect;
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

        PlayerSkinState skin = charState.current;
        RefreshCollisionIgnore(player, skin);

        // Kill-on-touch (no push involved)
        if (profile.killsOnTouch)
        {
            GameManager.Instance?.TriggerDeath();
            return;
        }

        // Pass-through — player ignores this box for the current contact
        if (CanPassThroughBy(skin)) return;

        bool canFallThrough = CanFallThroughBy(skin);

        // If the player is standing on top of this box AND can stand on it, parent them so they move with it.
        if (!canFallThrough && pushDir == Vector2.down && CanStandOnBy(skin))
        {
            player.transform.SetParent(transform, true);
            player.GetComponent<PlayerController>()?.SetGroundSupport(transform);
        }

        // Push — allow push from the sides even if the player cannot stand on this box.
        if (pushDir != Vector2.down && CanBePushedBy(skin) && IsOneWayDirectionValid(pushDir))
            ReceivePush(pushDir, 10f);

        // Contact effect behavior depends on profile: repeat every interact tick, or once per contact.
        PowerupEffect fx = GetContactEffect(skin);
        if (fx != null)
        {
            if (profile.contactEffectRepeats)
            {
                fx.Apply(player);
            }
            else if (effectAppliedSet.Add(player))
            {
                // For one-shot effects, also check the player-side tracker
                // so multiple boxes of the same type don't stack the effect.
                EffectTracker tracker = player.GetComponent<EffectTracker>();
                if (tracker == null || !tracker.IsOneShotApplied(fx))
                    fx.Apply(player);
            }
        }

        // Crush: if box is moving fast at the player after a push
        if (profile.crushOnContact && rb.linearVelocity.magnitude > 5f)
            GameManager.Instance?.TriggerDeath();
    }

    /// <summary>
    /// Called by BoxPushHandler when player exits collision.
    /// Clears contact tracking and one-shot effect flag for future re-entry.
    /// </summary>
    public void OnPlayerExit(GameObject player)
    {
        contactSet.Remove(player);
        effectAppliedSet.Remove(player);

        if (player.transform.parent == transform)
            player.transform.SetParent(null, true);

        player.GetComponent<PlayerController>()?.ClearGroundSupport(transform);
        CharacterState charState = player.GetComponent<CharacterState>();
        RefreshCollisionIgnore(player, charState != null ? charState.current : PlayerSkinState.Shard);
        
        // Clear one-shot effects so they can reapply if player walks back onto box
        EffectTracker tracker = player.GetComponent<EffectTracker>();
        if (tracker != null)
        {
            PowerupEffect fx = profile != null ? GetContactEffect(player.GetComponent<CharacterState>()?.current ?? PlayerSkinState.Shard) : null;
            if (fx != null && fx.oneShot)
                tracker.ClearOneShotFlag(fx);
        }
    }

    public void RefreshCollisionIgnore(GameObject player, PlayerSkinState skin)
    {
        if (player == null) return;
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null || boxCollider == null) return;

        bool shouldIgnore = CanPassThroughBy(skin);

        if (!shouldIgnore && CanFallThroughBy(skin))
        {
            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            Bounds playerBounds = playerCollider.bounds;
            Bounds boxBounds = boxCollider.bounds;
            shouldIgnore = playerRb != null
                && playerRb.linearVelocity.y < -0.1f
                && playerBounds.center.y >= boxBounds.min.y
                && playerBounds.max.x > boxBounds.min.x
                && playerBounds.min.x < boxBounds.max.x;
        }

        Physics2D.IgnoreCollision(playerCollider, boxCollider, shouldIgnore);
    }

    /// <summary>
    /// Removes the player from riding this box without clearing contact or effects.
    /// Used when a skin swap means the player should no longer remain parented or grounded.
    /// </summary>
    public void EvictPlayer(GameObject player)
    {
        if (player == null) return;

        if (player.transform.parent == transform)
            player.transform.SetParent(null, true);

        player.GetComponent<PlayerController>()?.ClearGroundSupport(transform);
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
        
        // Reset position and state to spawn position
        gameObject.SetActive(true);
        rb.bodyType = RigidbodyType2D.Dynamic;
        transform.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;
        restPosition = rb.position;
        
        // Clear all contact state
        contactSet.Clear();
        effectAppliedSet.Clear();
        
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