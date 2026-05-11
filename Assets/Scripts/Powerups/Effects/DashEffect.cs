using UnityEngine;

/// <summary>
/// Dash effect — grants a configurable number of dashes to the player.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Dash")]
public class DashEffect : PowerupEffect
{
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField, Min(0)] private int dashCount = 1;

    public override void Apply(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        controller.SetDashCount(dashCount, dashSpeed);
    }
}
