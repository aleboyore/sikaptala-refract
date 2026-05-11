using UnityEngine;

/// <summary>
/// Separate death effect for pickups that should kill on contact.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Powerup Effects/Death Effect")]
public class DeathEffect : PowerupEffect
{
    public override void Apply(GameObject player)
    {
        GameManager.Instance?.TriggerDeath();
    }
}