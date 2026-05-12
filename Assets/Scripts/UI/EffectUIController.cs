using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(EffectTracker))]
public class EffectUIController : MonoBehaviour
{
    [Header("UI References")]
    public Image effectIcon;
    public TextMeshProUGUI effectText;    // child named "Effect"
    public TextMeshProUGUI durationText;  // child named "Duration"
    public TextMeshProUGUI countText;     // child named "Count"

    EffectTracker _tracker;
    PlayerController _controller;
    Animator _animator;
    PowerupEffect _lastDisplayedEffect;
    const string AnimationLayerName = "Base Layer";
    const string AnimationStateName = "EffectAnimation";

    void Awake()
    {
        _tracker = GetComponent<EffectTracker>();
        _controller = GetComponent<PlayerController>();

        // Auto-find UI children if not wired in inspector
        if (effectIcon == null)
        {
            var iconTf = FindDeepChild(transform, "EffectIcon");
            if (iconTf != null) effectIcon = iconTf.GetComponent<Image>();
        }

        if (effectText == null)
        {
            var tf = FindDeepChild(transform, "Effect");
            if (tf != null) effectText = tf.GetComponent<TextMeshProUGUI>();
        }

        if (durationText == null)
        {
            var tf = FindDeepChild(transform, "Duration");
            if (tf != null) durationText = tf.GetComponent<TextMeshProUGUI>();
        }

        if (countText == null)
        {
            var tf = FindDeepChild(transform, "Count");
            if (tf != null) countText = tf.GetComponent<TextMeshProUGUI>();
        }

        // Get or add Animator to effectIcon
        if (effectIcon != null)
        {
            _animator = effectIcon.GetComponent<Animator>();
            if (_animator == null)
                _animator = effectIcon.gameObject.AddComponent<Animator>();
        }

        Debug.Log($"[UI] Found effectIcon: {effectIcon}, effectText: {effectText}, durationText: {durationText}, countText: {countText}, animator: {_animator}");
        UpdateUI();
    }

    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        var active = _tracker.GetDisplayedEffects();
        // Debug.Log($"[UI] Active effects: {active.Count}");

        if (active.Count == 0)
        {
            // Nothing timed running — show placeholders
            if (effectIcon != null) effectIcon.enabled = false;
            if (effectText != null) effectText.text = "Effect: ---";
            if (durationText != null) durationText.text = "Duration: ---";
            if (countText != null) countText.text = "Count: ---";
            // Debug.Log("[UI] No active effects, showing placeholders");
            return;
        }

        // Pick the most recent (last registered)
        var entry = active[active.Count - 1];
        var effect = entry.effect;
        var runner = entry.runner;
        
        // Debug.Log($"[UI] Effect: {(effect != null ? effect.name : "NULL")} | Display name: {(effect != null ? effect.GetDisplayName() : "NULL")} | Count: {(effect != null ? effect.GetCountDisplayText(_controller) : "NULL")}");

        if (effectIcon != null) effectIcon.enabled = effect != null && effect.Sprite != null;
        if (effectIcon != null && effect != null && effect.Sprite != null) effectIcon.sprite = effect.Sprite;

        // Apply animation if effect changed
        if (effect != _lastDisplayedEffect)
        {
            _lastDisplayedEffect = effect;
            if (_animator != null && effect != null && effect.SpriteAnimation != null)
            {
                ApplyAnimationClip(_animator, effect.SpriteAnimation, effect.AnimationSpeed);
                // Debug.Log($"[UI] Applied animation: {effect.SpriteAnimation.name} at speed {effect.AnimationSpeed}");
            }
        }

        if (effectText != null)
        {
            string displayName = effect != null ? effect.GetDisplayName() : "Unknown";
            effectText.text = $"Effect: {displayName}";
            // Debug.Log($"[UI] Set effectText to: '{effectText.text}'");
        }
        else
        {
            // Debug.LogWarning("[UI] effectText is NULL - UI will not update!");
        }

        if (durationText != null)
        {
            if (runner != null && runner.totalDuration > 0f)
            {
                float elapsed = Time.time - runner.startTime;
                float remaining = Mathf.Max(0f, runner.totalDuration - elapsed);
                durationText.text = $"Duration: {Mathf.CeilToInt(remaining)}";
                // Debug.Log($"[UI] Set durationText to: '{durationText.text}'");
            }
            else
            {
                durationText.text = "Duration: ---";
            }
        }
        else
        {
            // Debug.LogWarning("[UI] durationText is NULL!");
        }

        if (countText != null)
        {
            string countDisplay = effect != null ? effect.GetCountDisplayText(_controller) : "---";
            countText.text = "Count: " + countDisplay;
            // Debug.Log($"[UI] Set countText to: '{countText.text}'");
        }
        else
        {
            // Debug.LogWarning("[UI] countText is NULL!");
        }
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;

        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }

        return null;
    }

    private void ApplyAnimationClip(Animator animator, AnimationClip clip, float speed)
    {
        if (animator == null || clip == null) return;

        // Create a temporary AnimatorOverrideController to override the animation
        AnimatorOverrideController overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        
        // Get all animation clips from the original controller
        var clipsList = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(clipsList);
        
        if (clipsList.Count > 0)
        {
            // Override the first clip with our effect animation
            overrideController[clipsList[0].Key.name] = clip;
            animator.runtimeAnimatorController = overrideController;
            
            // Set animation speed
            animator.speed = speed;
            
            // Restart animation from the beginning
            animator.SetTrigger(AnimationStateName);
        }
        else
        {
            // Debug.LogWarning("[UI] No animation clips found in animator override controller");
        }
    }
}
