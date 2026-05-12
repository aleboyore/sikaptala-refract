using UnityEngine;

[CreateAssetMenu(menuName = "Refract/Powerup Definition")]
public class PowerupDefinition : ScriptableObject
{
    [Header("Affinity")]
    public PowerupEffect shardEffect;
    public PowerupEffect veilEffect;

    [Header("Audio")]
    public AudioClip shardSFX;
    public AudioClip veilSFX;

    [Header("Visuals")]
    public Sprite shardSprite;
    public Sprite veilSprite;
    [Header("Animations")]
    public AnimationClip shardAnimation;
    public AnimationClip veilAnimation;
    [Tooltip("Animation speed applied to the animation clips (applies to both shard/veil if set).")]
    public float animationSpeed = 1f;

    [Header("Pickup")]
    public bool consumeWhenEffectMissing = true;

    public PowerupEffect GetEffect(PlayerSkinState state)
    {
        return state == PlayerSkinState.Shard ? shardEffect : veilEffect;
    }

    public AudioClip GetSfx(PlayerSkinState state)
    {
        return state == PlayerSkinState.Shard ? shardSFX : veilSFX;
    }

    public Sprite GetSprite(PlayerSkinState state)
    {
        Sprite spriteFromDefinition = state == PlayerSkinState.Shard ? shardSprite : veilSprite;
        if (spriteFromDefinition != null) return spriteFromDefinition;

        // Fallback: try to get sprite from effect itself
        PowerupEffect effect = GetEffect(state);
        return effect != null ? effect.Sprite : null;
    }

    public AnimationClip GetAnimation(PlayerSkinState state)
    {
        return state == PlayerSkinState.Shard ? shardAnimation : veilAnimation;
    }

    public float GetAnimationSpeed()
    {
        return animationSpeed;
    }
}