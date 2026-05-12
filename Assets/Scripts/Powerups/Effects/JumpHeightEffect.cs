using UnityEngine;
using System.Collections;

/// <summary>
/// Jump height effect — temporarily increases jump power.
/// Reverts on expiry or on skin swap.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Jump Height")]
public class JumpHeightEffect : PowerupEffect
{
    [SerializeField, Min(0.1f)] private float jumpHeightMultiplier = 1.5f;
    [SerializeField]            private float duration             = 5f;

    public override void Apply(GameObject player)
    {
        PlayerController ctrl = player.GetComponent<PlayerController>();
        if (ctrl == null) return;

        ctrl.SetJumpHeightMultiplier(jumpHeightMultiplier);
        PowerupEffectRunner.Run(player, this, ExpireRoutine(player), duration);
    }

    public override void Remove(GameObject player)
    {
        PlayerController ctrl = player.GetComponent<PlayerController>();
        ctrl?.SetJumpHeightMultiplier(1f);
    }

    private IEnumerator ExpireRoutine(GameObject player)
    {
        yield return new WaitForSeconds(duration);
        Remove(player);
    }
}
