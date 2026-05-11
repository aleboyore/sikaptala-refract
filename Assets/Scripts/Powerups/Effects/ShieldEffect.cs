using UnityEngine;

/// <summary>
/// Shield effect — grants one-hit protection against death.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Shield")]
public class ShieldEffect : PowerupEffect
{
    public override void Apply(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        controller.GrantShield();
    }
}
