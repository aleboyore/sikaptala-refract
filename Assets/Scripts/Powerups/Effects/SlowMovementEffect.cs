using UnityEngine;
using System.Collections;

/// <summary>
/// Slow movement effect — shrinks character and reduces movement speed.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Slow Movement")]
public class SlowMovementEffect : PowerupEffect
{
    [SerializeField] private float scaleMultiplier = 0.5f;

    public override void Apply(GameObject player)
    {
        PowerupEffectRunner.Run(player, SlowMovementRoutine(player));
    }

    private IEnumerator SlowMovementRoutine(GameObject player)
    {
        Vector3 originalScale = player.transform.localScale;
        Vector3 slowScale = originalScale * scaleMultiplier;

        player.transform.localScale = slowScale;

        yield return new WaitForSeconds(5f);

        player.transform.localScale = originalScale;
    }
}
