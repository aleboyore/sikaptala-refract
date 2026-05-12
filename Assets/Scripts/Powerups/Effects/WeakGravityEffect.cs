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
        PlayerController ctrl = player.GetComponent<PlayerController>();
        if (ctrl == null) return;

        ctrl.SetFallMultiplier(gravityMultiplier);
        PowerupEffectRunner.Run(player, this, WeakGravityRoutine(player), 5f);
    }

    public override void Remove(GameObject player)
    {
        player.GetComponent<PlayerController>()?.SetFallMultiplier(1f);
    }

    public override string GetDisplayName()
    {
        return $"Weak Gravity {gravityMultiplier:0.##}x";
    }

    private IEnumerator WeakGravityRoutine(GameObject player)
    {
        yield return new WaitForSeconds(5f);
        Remove(player);
    }
}
