using UnityEngine;
using UnityEngine.Events;

public class CharacterState : MonoBehaviour
{
    public PlayerSkinState current = PlayerSkinState.Shard;

    [Header("Sprite Roots")]
    public GameObject shardSprite;
    public GameObject veilSprite;

    [Header("Sprite Renderers & Animators")]
    public SpriteRenderer shardRenderer;
    public SpriteRenderer veilRenderer;
    public Animator shardAnimator;
    public Animator veilAnimator;

    [Header("Tags")]
    public string shardTag = "Shard";
    public string veilTag = "Veil";

    [Header("Events")]
    public UnityEvent<PlayerSkinState> onStateChanged;

    [Header("VFX")]
    // Single particle effect (ShardBurst) used for swap visuals for both states
    public ParticleSystem ShardBurst;

    void Start()
    {
        RefreshSprites();
        RefreshTag();
    }

    public void SwapState()
    {
        current = (current == PlayerSkinState.Shard) ? PlayerSkinState.Veil : PlayerSkinState.Shard;
        RefreshSprites();
        RefreshTag();
        PlaySwapBurstIfNeeded();
        onStateChanged?.Invoke(current);
    }

    void RefreshSprites()
    {
        bool showShard = current == PlayerSkinState.Shard;

        // Toggle sprite renderers first; fall back to the sprite root GameObjects if needed.
        if (shardRenderer != null) shardRenderer.enabled = showShard;
        else if (shardSprite != null) shardSprite.SetActive(showShard);

        if (veilRenderer != null) veilRenderer.enabled = !showShard;
        else if (veilSprite != null) veilSprite.SetActive(!showShard);
    }

    void RefreshTag()
    {
        gameObject.tag = (current == PlayerSkinState.Shard) ? shardTag : veilTag;
    }

    void PlaySwapBurstIfNeeded()
    {
        if (ShardBurst != null)
            ShardBurst.Play();
    }

    // Helpers to sync animator parameters across both animators so state is preserved on swap
    public void SetAnimBool(string name, bool value)
    {
        if (shardAnimator != null) shardAnimator.SetBool(name, value);
        if (veilAnimator != null) veilAnimator.SetBool(name, value);
    }

    public void SetAnimFloat(string name, float value)
    {
        if (shardAnimator != null) shardAnimator.SetFloat(name, value);
        if (veilAnimator != null) veilAnimator.SetFloat(name, value);
    }

    public void SetAnimTrigger(string name)
    {
        if (shardAnimator != null) shardAnimator.SetTrigger(name);
        if (veilAnimator != null) veilAnimator.SetTrigger(name);
    }
}
