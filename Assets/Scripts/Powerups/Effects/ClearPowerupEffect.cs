using UnityEngine;

/// <summary>
/// Clears active player power-up state and restores the visual scale.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Clear Powerups")]
public class ClearPowerupEffect : PowerupEffect
{
    public override void Apply(GameObject player)
    {
        if (player == null) return;

        EffectTracker tracker = player.GetComponent<EffectTracker>();
        tracker?.ClearAllEffects(player);

        PlayerController controller = player.GetComponent<PlayerController>();
        controller?.ResetPowerupState();

        ScaleCharacterEffect.ResetVisualScale(player);
    }

    public override string GetDisplayName()
    {
        return "Clear";
    }
}