using UnityEngine;
using System.Collections;

/// <summary>
/// Speed boost effect — temporarily increases movement speed.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Speed Boost")]
public class SpeedBoostEffect : PowerupEffect
{
    [SerializeField] private float multiplier = 1.5f;

    public override void Apply(GameObject player)
    {
        // TODO: Implement speed boost on PlayerController.
        // For now, just a placeholder that gets picked up.
    }
}
