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
        Debug.Log($"[DashEffect] Apply called with dashCount={dashCount}, dashSpeed={dashSpeed}");
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) { Debug.LogWarning("[DashEffect] No PlayerController found"); return; }

        controller.SetDashCount(dashCount, dashSpeed);
        EffectTracker tracker = player.GetComponent<EffectTracker>();
        tracker?.RegisterEffect(this, null);
        Debug.Log($"[DashEffect] Registered effect, dashCount now={dashCount}");
    }

    public override string GetCountDisplayText(PlayerController controller)
    {
        if (controller == null) return dashCount.ToString();
        return controller.GetDashesRemaining().ToString();
    }
}
