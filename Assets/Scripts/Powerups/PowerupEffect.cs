using UnityEngine;

public abstract class PowerupEffect : ScriptableObject
{
    [SerializeField] private Sprite sprite;
    public Sprite Sprite => sprite;

    [Header("Animation")]
    [SerializeField] private AnimationClip spriteAnimation;
    [SerializeField] private float animationSpeed = 1f;
    public AnimationClip SpriteAnimation => spriteAnimation;
    public float AnimationSpeed => animationSpeed;

    [Header("Lifecycle")]
    [Tooltip("If true, this effect is NOT cancelled when the player swaps skin (Shard ↔ Veil).")]
    public bool persistAcrossStateSwap = false;

    [Tooltip("If true, this effect can only ever be applied once per player. Repeat calls are silently ignored.")]
    public bool oneShot = false;

    /// <summary>Apply the effect to the player.</summary>
    public abstract void Apply(GameObject player);

    /// <summary>
    /// Text shown after the Effect label.
    /// </summary>
    public virtual string GetDisplayName()
    {
        return name;
    }

    /// <summary>
    /// Text shown after the Count label.
    /// </summary>
    public virtual string GetCountDisplayText(PlayerController controller)
    {
        return "---";
    }

    /// <summary>
    /// Revert any values this effect changed.
    /// Called by EffectTracker before the coroutine runner is destroyed.
    /// Default is a no-op; override in effects that modify persistent state.
    /// </summary>
    public virtual void Remove(GameObject player) { }
}