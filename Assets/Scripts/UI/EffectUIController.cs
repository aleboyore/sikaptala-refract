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

    void Awake()
    {
        _tracker = GetComponent<EffectTracker>();
        _controller = GetComponent<PlayerController>();

        // Auto-find UI children if not wired in inspector
        if (effectIcon == null)
        {
            var iconTf = transform.Find("EffectIcon");
            if (iconTf != null) effectIcon = iconTf.GetComponent<Image>();
        }

        if (effectText == null)
        {
            var tf = transform.Find("Effect");
            if (tf != null) effectText = tf.GetComponent<TextMeshProUGUI>();
        }

        if (durationText == null)
        {
            var tf = transform.Find("Duration");
            if (tf != null) durationText = tf.GetComponent<TextMeshProUGUI>();
        }

        if (countText == null)
        {
            var tf = transform.Find("Count");
            if (tf != null) countText = tf.GetComponent<TextMeshProUGUI>();
        }

        UpdateUI();
    }

    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        var active = _tracker.GetActiveEffects();

        if (active.Count == 0)
        {
            // Nothing timed running — show placeholders
            if (effectIcon != null) effectIcon.enabled = false;
            if (effectText != null) effectText.text = "Effect: ---";
            if (durationText != null) durationText.text = "Duration: ---";
            if (countText != null) countText.text = "Count: ---";
            return;
        }

        // Pick the most recent (last registered)
        var entry = active[active.Count - 1];
        var effect = entry.effect;
        var runner = entry.runner;

        if (effectIcon != null) effectIcon.enabled = effect != null && effect.Sprite != null;
        if (effectIcon != null && effect != null && effect.Sprite != null) effectIcon.sprite = effect.Sprite;

        if (effectText != null)
            effectText.text = $"Effect: {(effect != null ? effect.name : "Unknown")}";

        if (durationText != null)
        {
            if (runner != null && runner.totalDuration > 0f)
            {
                float elapsed = Time.time - runner.startTime;
                float remaining = Mathf.Max(0f, runner.totalDuration - elapsed);
                durationText.text = $"Duration: {Mathf.CeilToInt(remaining)}";
            }
            else
            {
                durationText.text = "Duration: ---";
            }
        }

        if (countText != null)
        {
            // Show counts for known effect types
            string countStr = "---";
            if (effect != null)
            {
                string tname = effect.GetType().Name;
                if (tname.Contains("DoubleJump"))
                {
                    if (_controller != null) countStr = _controller.GetAirJumpsAllowed().ToString();
                }
                else if (tname.Contains("Dash"))
                {
                    if (_controller != null) countStr = _controller.GetDashesAllowed().ToString();
                }
            }

            countText.text = "Count: " + countStr;
        }
    }
}
