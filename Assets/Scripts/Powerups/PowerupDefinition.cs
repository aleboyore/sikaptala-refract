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
}