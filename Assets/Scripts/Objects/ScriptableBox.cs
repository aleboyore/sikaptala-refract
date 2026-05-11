using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class ScriptableBox : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private BoxBehaviorProfile profile;

    Rigidbody2D rb;
    Vector2 restPosition;
    Vector3 spawnPosition;
    readonly HashSet<GameObject> contactSet = new();

    public BoxBehaviorProfile Profile => profile;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

        if (profile == null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 8f;
            return;
        }

        rb.gravityScale = profile.gravityScale;
        rb.freezeRotation = true;
        rb.linearDamping = profile.linearDamping;
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
    }

    /// <summary>
    /// Called by BoxPushHandler when player collides with this box.
    /// Resolves state, applies contact effects, handles push/kill/pass-through.
    /// </summary>
    public void OnPlayerInteract(GameObject player, Vector2 pushDir)
    {
        if (profile == null) return;

        CharacterState charState = player.GetComponent<CharacterState>();
        if (charState == null) return;

        PlayerSkinState skin = charState.current;

        // Kill-on-touch (no push involved)
        if (profile.killsOnTouch)
        {
            GameManager.Instance?.TriggerDeath();
            return;
        }

        // Pass-through — player ignores this box for the current contact
        if (CanPassThroughBy(skin)) return;

        // Push
        if (CanBePushedBy(skin) && IsOneWayDirectionValid(pushDir))
            ReceivePush(pushDir, 10f);

        // Contact effect (once per contact entry, unless repeats = true)
        bool isNew = contactSet.Add(player);
        if (isNew || profile.contactEffectRepeats)
        {
            PowerupEffect fx = GetContactEffect(skin);
            fx?.Apply(player);
        }

        // Crush: if box is moving fast at the player after a push
        if (profile.crushOnContact && rb.linearVelocity.magnitude > 5f)
            GameManager.Instance?.TriggerDeath();
    }

    /// <summary>
    /// Called by BoxPushHandler when player exits collision.
    /// Clears contact tracking for future re-entry.
    /// </summary>
    public void OnPlayerExit(GameObject player)
    {
        contactSet.Remove(player);
    }
}