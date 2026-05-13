using UnityEngine;

public class PowerupBehavior : MonoBehaviour, ICheckpointRestorer
{
    [SerializeField] private PowerupDefinition definition;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private CharacterState _charState;
    private bool _consumed;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = gameObject.AddComponent<Animator>();
        
        // Find the player's CharacterState in the scene
        _charState = FindAnyObjectByType<CharacterState>();
        if (_charState != null)
        {
            _charState.onStateChanged.AddListener(OnPlayerStateChanged);
        }
        
        UpdateVisuals();
    }

    void OnDestroy()
    {
        if (_charState != null)
        {
            _charState.onStateChanged.RemoveListener(OnPlayerStateChanged);
        }
    }

    private void OnPlayerStateChanged(PlayerSkinState newState)
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_spriteRenderer == null || definition == null || _charState == null) return;

        Sprite sprite = definition.GetSprite(_charState.current);
        if (sprite != null)
            _spriteRenderer.sprite = sprite;

        // Apply animation clip if present. Prefer definition animation; fall back to effect's animation.
        PowerupEffect effect = definition.GetEffect(_charState.current);
        AnimationClip clip = definition.GetAnimation(_charState.current);
        float speed = definition.GetAnimationSpeed();
        if (clip == null && effect != null)
        {
            clip = effect.SpriteAnimation;
            // if definition speed not configured (0 or negative), use effect speed
            if (speed <= 0f) speed = effect.AnimationSpeed;
        }

        if (_animator != null && clip != null)
        {
            ApplyAnimationClip(_animator, clip, speed > 0f ? speed : 1f);
        }
    }

    private void ApplyAnimationClip(Animator animator, AnimationClip clip, float speed)
    {
        if (animator == null || clip == null) return;

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[Powerup] Animator on {gameObject.name} has no runtime controller to override.");
            return;
        }

        var overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        var clipsList = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(clipsList);

        if (clipsList.Count > 0)
        {
            overrideController[clipsList[0].Key.name] = clip;
            animator.runtimeAnimatorController = overrideController;
            animator.speed = speed;
            animator.SetTrigger("EffectAnimation");
        }
        else
        {
            Debug.LogWarning($"[Powerup] No clips found in animator controller for {gameObject.name}");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed) return;

        CharacterState state = other.GetComponent<CharacterState>();
        if (state == null || definition == null) return;

        PlayerSkinState skin = state.current;
        PowerupEffect effect = definition.GetEffect(skin);
        AudioClip sfx = definition.GetSfx(skin);

        if (sfx != null)
            AudioSource.PlayClipAtPoint(sfx, transform.position);

        if (effect == null && !definition.consumeWhenEffectMissing)
            return;

        _consumed = true;
        if (effect != null)
        {
            EffectTracker tracker = other.GetComponent<EffectTracker>();
            if (tracker == null || !tracker.IsOneShotApplied(effect))
                effect.Apply(other.gameObject);
        }

        // Mark as consumed - deactivate instead of destroy so we can restore on checkpoint
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Restore this powerup to its checkpoint state (not consumed).
    /// Called when player dies and respawns at a checkpoint after this powerup was collected.
    /// </summary>
    public void RestoreToCheckpoint()
    {
        _consumed = false;
        // Ensure the powerup is active and collider is enabled for interaction
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            Debug.Log($"[Checkpoint] Powerup '{gameObject.name}' re-enabled (was deactivated)");
        }
        
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.enabled)
        {
            col.enabled = true;
            Debug.Log($"[Checkpoint] Powerup '{gameObject.name}' collider re-enabled");
        }
        
        Debug.Log($"[Checkpoint] Powerup '{gameObject.name}' restored to available state");
    }

    /// <summary>
    /// Revert if this powerup was created after the checkpoint.
    /// Deactivates it so it doesn't affect the restored world state.
    /// </summary>
    public void RevertPostCheckpoint()
    {
        gameObject.SetActive(false);
        Debug.Log($"[Checkpoint] Powerup {gameObject.name} reverted (post-checkpoint)");
    }
}
