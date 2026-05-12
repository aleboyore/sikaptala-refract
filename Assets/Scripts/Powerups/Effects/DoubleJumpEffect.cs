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
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        controller.SetAirJumpCount(jumpCount);
    }
}
