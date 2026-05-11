using UnityEngine;

[CreateAssetMenu(menuName = "Refract/Box Behavior Profile")]
public class BoxBehaviorProfile : ScriptableObject
{
    [Header("Physics")]
    public float gravityScale = 0f;
    public float linearDamping = 8f;
    public float pushResistance = 2f;
    public float returnToRestSpeed = 0f;

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

    [Header("One-Way Push")]
    public bool oneWayEnabled = false;
    public bool pushLeftOnly = false;  // if true, only accept left push; if false, only accept right push

    [Header("Crush & Respawn")]
    public bool crushOnContact = false;  // kills player on any collision
    public bool killsOnTouch = false;    // kills on contact, separate from push
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