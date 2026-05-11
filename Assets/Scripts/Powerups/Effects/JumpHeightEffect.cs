using UnityEngine;

/// <summary>
/// Jump height effect — increases jump power multiplier.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Jump Height")]
public class JumpHeightEffect : PowerupEffect
{
    [SerializeField, Min(0.1f)] private float jumpHeightMultiplier = 1.5f;

    public override void Apply(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        controller.SetJumpHeightMultiplier(jumpHeightMultiplier);
    }
}
