using UnityEngine;

public class PowerupBehavior : MonoBehaviour
{
    [SerializeField] private PowerupDefinition definition;
    private SpriteRenderer _spriteRenderer;
    private CharacterState _charState;
    private bool _consumed;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Find the player's CharacterState in the scene
        _charState = FindAnyObjectByType<CharacterState>();
        if (_charState != null)
        {
            _charState.onStateChanged.AddListener(OnPlayerStateChanged);
        }
        
        UpdateOrbSprite();
    }

    void OnDestroy()
    {
        if (_charState != null)
        {
            _charState.onStateChanged.RemoveListener(OnPlayerStateChanged);
        }
    }

    private void OnPlayerStateChanged(PlayerSkinState newState)
    {
        UpdateOrbSprite();
    }

    private void UpdateOrbSprite()
    {
        if (_spriteRenderer == null || definition == null || _charState == null) return;

        Sprite sprite = definition.GetSprite(_charState.current);
        if (sprite != null)
            _spriteRenderer.sprite = sprite;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed) return;

        CharacterState state = other.GetComponent<CharacterState>();
        if (state == null || definition == null) return;

        PlayerSkinState skin = state.current;
        PowerupEffect effect = definition.GetEffect(skin);
        AudioClip sfx = definition.GetSfx(skin);

        if (sfx != null)
            AudioSource.PlayClipAtPoint(sfx, transform.position);

        if (effect == null && !definition.consumeWhenEffectMissing)
            return;

        _consumed = true;
        if (effect != null)
        {
            EffectTracker tracker = other.GetComponent<EffectTracker>();
            if (tracker == null || !tracker.IsOneShotApplied(effect))
                effect.Apply(other.gameObject);
        }

        // Consumed after a valid pickup path.
        Destroy(gameObject);
    }
}
