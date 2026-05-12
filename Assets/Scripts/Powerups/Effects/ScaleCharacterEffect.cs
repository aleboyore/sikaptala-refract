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

        ApplyVisualScale(player, scaleMultiplier);

        // Register as one-shot so the tracker records it even without a runner
        tracker?.RegisterEffect(this, null);
    }

    public override string GetDisplayName()
    {
        return $"Scale {scaleMultiplier:0.##}x";
    }

    public static void ApplyVisualScale(GameObject player, float multiplier)
    {
        if (player == null) return;

        Vector3 current = player.transform.localScale;
        // Preserve facing direction (sign of X from HandleSpriteDirection)
        float facing = Mathf.Sign(current.x == 0f ? 1f : current.x);
        player.transform.localScale = new Vector3(
            multiplier * facing,
            multiplier,
            current.z);
    }

    public static void ResetVisualScale(GameObject player)
    {
        if (player == null) return;

        Vector3 current = player.transform.localScale;
        float facing = Mathf.Sign(current.x == 0f ? 1f : current.x);
        player.transform.localScale = new Vector3(facing, 1f, current.z);
    }

    private static Transform FindVisualTransform(Transform root)
    {
        if (root == null) return null;

        if (root.name == "Visual")
            return root;

        Transform direct = root.Find("Visual");
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindVisualTransform(child);
            if (found != null)
                return found;
        }

        return null;
    }

    // No Remove override — scale is intentionally permanent.
}
