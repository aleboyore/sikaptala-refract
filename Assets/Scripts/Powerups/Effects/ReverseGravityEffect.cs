using UnityEngine;
using System.Collections;

/// <summary>
/// Reverse gravity effect — flips gravity so player falls upward.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Reverse Gravity")]
public class ReverseGravityEffect : PowerupEffect
{

    public override void Apply(GameObject player)
    {
        PowerupEffectRunner.Run(player, ReverseGravityRoutine(player));
    }

    private IEnumerator ReverseGravityRoutine(GameObject player)
    {
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) yield break;

        float originalGravityScale = rb.gravityScale;
        rb.gravityScale = -originalGravityScale;

        yield return new WaitForSeconds(5f);

        rb.gravityScale = originalGravityScale;
    }
}
