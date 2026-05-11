using UnityEngine;

/// <summary>
/// Shield effect — grants one-hit protection against death.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Shield")]
public class ShieldEffect : PowerupEffect
{
    public override void Apply(GameObject player)
    {
        // TODO: Implement shield flag on player (reduce death count or flag).
        // For now, just a placeholder that gets picked up.
    }
}
