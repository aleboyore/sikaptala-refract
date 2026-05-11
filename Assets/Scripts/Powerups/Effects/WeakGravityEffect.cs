using UnityEngine;
using System.Collections;

/// <summary>
/// Weak gravity effect — reduces gravity, allowing slower/longer falls.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Weak Gravity")]
public class WeakGravityEffect : PowerupEffect
{
    [SerializeField] private float gravityMultiplier = 0.3f;

    public override void Apply(GameObject player)
    {
        PowerupEffectRunner.Run(player, WeakGravityRoutine(player));
    }

    private IEnumerator WeakGravityRoutine(GameObject player)
    {
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) yield break;

        float originalGravityScale = rb.gravityScale;
        rb.gravityScale = originalGravityScale * gravityMultiplier;

        yield return new WaitForSeconds(5f);

        rb.gravityScale = originalGravityScale;
    }
}
