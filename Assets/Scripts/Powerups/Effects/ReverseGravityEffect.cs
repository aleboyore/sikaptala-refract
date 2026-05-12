using UnityEngine;

/// <summary>
/// Reverse gravity effect — permanently flips player gravity. Applies once only.
/// Set oneShot = true and persistAcrossStateSwap = true in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Reverse Gravity")]
public class ReverseGravityEffect : PowerupEffect
{
    public override void Apply(GameObject player)
    {
        EffectTracker tracker = player.GetComponent<EffectTracker>();
        if (tracker != null && tracker.IsOneShotApplied(this)) return;

        // NOTE: PlayerController sets _rb.gravityScale = 0 in Awake and manages
        // gravity manually via _frameVelocity. To implement gravity flip, add a
        // gravityFlipped flag to PlayerController and call FlipGravity() here.
        PlayerController ctrl = player.GetComponent<PlayerController>();
        ctrl?.FlipGravity();

        tracker?.RegisterEffect(this, null);
    }
}
