using UnityEngine;

public class PowerupBehavior : MonoBehaviour
{
    [SerializeField] private PowerupDefinition definition;

    void OnTriggerEnter2D(Collider2D other)
    {
        CharacterState state = other.GetComponent<CharacterState>();
        if (state == null || definition == null) return;

        PlayerSkinState skin = state.current;
        PowerupEffect effect = definition.GetEffect(skin);
        AudioClip sfx = definition.GetSfx(skin);

        if (sfx != null)
            AudioSource.PlayClipAtPoint(sfx, transform.position);

        if (effect == null && !definition.consumeWhenEffectMissing)
            return;

        effect?.Apply(other.gameObject);

        // Consumed after a valid pickup path.
        Destroy(gameObject);
    }
}
