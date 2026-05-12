using UnityEngine;

/// <summary>
/// Double jump effect — grants a configurable number of additional jumps in mid-air.
/// Charges are consumed and do not refill until a new orb is picked up.
/// Set persistAcrossStateSwap = true so charges are kept across skin swaps.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Double Jump")]
public class DoubleJumpEffect : PowerupEffect
{
    [SerializeField, Min(0)] private int jumpCount = 1;

    public DoubleJumpEffect()
    {
        persistAcrossStateSwap = true; // charges persist across skin swaps
    }

    public override void Apply(GameObject player)
    {
        Debug.Log($"[DoubleJumpEffect] Apply called with jumpCount={jumpCount}");
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) { Debug.LogWarning("[DoubleJumpEffect] No PlayerController found"); return; }

        controller.SetAirJumpCount(jumpCount);
        EffectTracker tracker = player.GetComponent<EffectTracker>();
        tracker?.RegisterEffect(this, null);
        Debug.Log($"[DoubleJumpEffect] Registered effect, jumpCount now={jumpCount}");
    }

    public override string GetCountDisplayText(PlayerController controller)
    {
        if (controller == null) return jumpCount.ToString();
        return controller.GetAirJumpsRemaining().ToString();
    }
}
