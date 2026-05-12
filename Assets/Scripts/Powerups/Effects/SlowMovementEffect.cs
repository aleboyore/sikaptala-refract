using UnityEngine;
using System.Collections;

/// <summary>
/// Slow movement effect — temporarily reduces player movement speed.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Slow Movement")]
public class SlowMovementEffect : PowerupEffect
{
    [SerializeField] private float speedMultiplier = 0.5f;
    [SerializeField] private float duration        = 5f;

    public override void Apply(GameObject player)
    {
        PlayerController ctrl = player.GetComponent<PlayerController>();
        if (ctrl == null) return;

        ctrl.SetSpeedMultiplier(speedMultiplier);

        PowerupEffectRunner.Run(player, this, SlowMovementRoutine(player), duration);
    }

    public override void Remove(GameObject player)
    {
        player.GetComponent<PlayerController>()?.SetSpeedMultiplier(1f);
    }

    public override string GetDisplayName()
    {
        return $"Slow Movement {speedMultiplier:0.##}x";
    }

    private IEnumerator SlowMovementRoutine(GameObject player)
    {
        yield return new WaitForSeconds(duration);
        Remove(player);
    }
}
