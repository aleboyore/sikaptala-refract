using UnityEngine;

/// <summary>
/// Double jump effect — grants a configurable number of additional jumps in mid-air.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Double Jump")]
public class DoubleJumpEffect : PowerupEffect
{
    [SerializeField, Min(0)] private int jumpCount = 1;

    public override void Apply(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        controller.SetAirJumpCount(jumpCount);
    }
}
