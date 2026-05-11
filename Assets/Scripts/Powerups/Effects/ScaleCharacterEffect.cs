using UnityEngine;
using System.Collections;

/// <summary>
/// Scale character effect — enlarges the character.
/// </summary>
[CreateAssetMenu(menuName = "Refract/Effects/Scale Character")]
public class ScaleCharacterEffect : PowerupEffect
{
    [SerializeField] private float scaleMultiplier = 2f;

    public override void Apply(GameObject player)
    {
        PowerupEffectRunner.Run(player, ScaleRoutine(player));
    }

    private IEnumerator ScaleRoutine(GameObject player)
    {
        Vector3 originalScale = player.transform.localScale;
        Vector3 largeScale = originalScale * scaleMultiplier;

        player.transform.localScale = largeScale;

        yield return new WaitForSeconds(5f);

        player.transform.localScale = originalScale;
    }
}
