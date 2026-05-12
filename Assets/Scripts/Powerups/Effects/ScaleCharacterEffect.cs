using UnityEngine;

/// <summary>
/// Scale character effect — scales the player by a multiplier (up or down). Applies once only per application.
/// Multiplier > 1 = enlarge, Multiplier < 1 = shrink. Set oneShot = true and persistAcrossStateSwap = true in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Scale Character")]
public class ScaleCharacterEffect : PowerupEffect
{
    [SerializeField] private float scaleMultiplier = 1f;  // Default 1x (no change). Set to 2f for 2x, 0.5f for half-size, etc.

    public override void Apply(GameObject player)
    {
        // Guard: one-shot check is enforced here AND by PowerupBehavior/ScriptableBox callers
        EffectTracker tracker = player.GetComponent<EffectTracker>();
        if (tracker != null && tracker.IsOneShotApplied(this)) return;

        // Apply scale — preserve facing direction (sign of x)
        Vector3 s       = player.transform.localScale;
        float baseScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
        float facing    = Mathf.Sign(s.x == 0f ? 1f : s.x);
        player.transform.localScale = new Vector3(
            baseScale * scaleMultiplier * facing,
            baseScale * scaleMultiplier,
            s.z);

        // Register as one-shot so the tracker records it even without a runner
        tracker?.RegisterEffect(this, null);
    }

    // No Remove override — scale is intentionally permanent.
}
