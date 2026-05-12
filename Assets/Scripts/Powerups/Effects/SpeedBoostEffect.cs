using UnityEngine;
using System.Collections;

/// <summary>
/// Speed boost effect — temporarily increases movement speed.
/// Reverts on expiry or on skin swap (persistAcrossStateSwap = false by default).
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Speed Boost")]
public class SpeedBoostEffect : PowerupEffect
{
    [SerializeField] private float multiplier = 1.5f;
    [SerializeField] private float duration   = 5f;

    public override void Apply(GameObject player)
    {
        PlayerController ctrl = player.GetComponent<PlayerController>();
        if (ctrl == null) return;

        ctrl.SetSpeedMultiplier(multiplier);
        PowerupEffectRunner.Run(player, this, ExpireRoutine(player), duration);
    }

    public override void Remove(GameObject player)
    {
        PlayerController ctrl = player.GetComponent<PlayerController>();
        ctrl?.SetSpeedMultiplier(1f);
    }

    private IEnumerator ExpireRoutine(GameObject player)
    {
        yield return new WaitForSeconds(duration);
        Remove(player);
    }
}
