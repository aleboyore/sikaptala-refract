using UnityEngine;

[CreateAssetMenu(menuName = "Refract/Box Behavior Profile")]
public class BoxBehaviorProfile : ScriptableObject
{
    [Header("Physics")]
    public float gravityScale = 1f;  // default: affected by gravity. Set 0 for floating boxes.
    public float linearDamping = 8f;
    public float pushResistance = 2f;
    public float returnToRestSpeed = 0f;
    public bool allowVerticalMovement = true;

    [Header("Skin Access")]
    public bool shardCanStandOn = true;
    public bool veilCanStandOn = true;

    [Header("Push")]
    public bool shardCanPush = true;
    public bool veilCanPush = false;

    [Header("Damage On Push")]
    public bool shardKillsOnPush = false;
    public bool veilKillsOnPush = false;

    [Header("Pass Through")]
    public bool shardCanPassThrough = false;
    public bool veilCanPassThrough = false;

    [Header("Visibility")]
    public bool notVisibleToVeil = false;  // if true, sprite hidden when Veil skin is active
    public bool notVisibleToShard = false; // if true, sprite hidden when Shard skin is active

    [Header("One-Way Push")]
    public bool oneWayEnabled = false;
    public bool pushLeftOnly = false;  // if true, only accept left push; if false, only accept right push

    [Header("Crush & Respawn")]
    public bool crushOnContact = false;  // kills player on any collision
    public bool killsOnTouch = false;    // kills on contact, separate from push
    public bool shardKillsOnTouch = false;
    public bool veilKillsOnTouch = false;
    public float respawnDelay = 0f;      // 0 = no respawn, >0 = respawn after N seconds

    [Header("Movement Restrictions")]
    public bool moveOnlyWhenGrounded = false;

    [Header("Contact Effects")]
    [Tooltip("Effect applied to Shard when they touch this box. Leave null for no effect.")]
    public PowerupEffect shardContactEffect;

    [Tooltip("Effect applied to Veil when they touch this box. Leave null for no effect.")]
    public PowerupEffect veilContactEffect;

    [Tooltip("Play once per contact, or every frame while touching.")]
    public bool contactEffectRepeats = false;
}