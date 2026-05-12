using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks active power-up effects on the player.
/// On skin swap, reverts and cancels all effects.
/// </summary>
public class EffectTracker : MonoBehaviour
{
    private CharacterState _characterState;

    // Pairs of (effect definition, coroutine host) for each active timed effect
    private readonly List<(PowerupEffect effect, PowerupEffectRunner runner)> _activeEffects = new();

    // UI-visible effects, including one-shot effects without runners.
    private readonly List<(PowerupEffect effect, PowerupEffectRunner runner)> _displayEffects = new();

    // Permanent record of one-shot effects that have been applied this session
    private readonly HashSet<PowerupEffect> _appliedOneShots = new();

    private void Awake()
    {
        _characterState = GetComponent<CharacterState>();
        if (_characterState != null)
            _characterState.onStateChanged.AddListener(OnStateChanged);
    }

    private void OnDestroy()
    {
        if (_characterState != null)
            _characterState.onStateChanged.RemoveListener(OnStateChanged);
    }

    /// <summary>
    /// Returns true if a one-shot effect has already been applied to this player.
    /// Call this before Apply() for effects with oneShot = true.
    /// </summary>
    public bool IsOneShotApplied(PowerupEffect effect)
        => effect != null && effect.oneShot && _appliedOneShots.Contains(effect);

    /// <summary>
    /// Clears the one-shot applied flag for an effect, allowing it to be applied again.
    /// Called when player exits a box to allow reapplication on re-entry.
    /// </summary>
    public void ClearOneShotFlag(PowerupEffect effect)
    {
        if (effect != null)
            _appliedOneShots.Remove(effect);
    }

    /// <summary>
    /// Register a newly started effect. Called by PowerupEffectRunner.Run.
    /// </summary>
    public void RegisterEffect(PowerupEffect effect, PowerupEffectRunner runner)
    {
        if (effect == null) return;

        // Record one-shot effects permanently (survive state swaps)
        if (effect.oneShot)
            _appliedOneShots.Add(effect);

        if (runner != null)
        {
            _activeEffects.Add((effect, runner));
            _displayEffects.Add((effect, runner));
        }
        else
        {
            _displayEffects.Add((effect, null));
        }
    }

    /// <summary>
    /// Returns a snapshot list of UI-visible effects and their runners.
    /// </summary>
    public List<(PowerupEffect effect, PowerupEffectRunner runner)> GetDisplayedEffects()
    {
        return new List<(PowerupEffect, PowerupEffectRunner)>(_displayEffects);
    }

    [System.Serializable]
    public class SerializableEffectState
    {
        public string effectName;
        public bool isOneShot;
        // remaining time is optional; 0 = unknown/full
        public float remainingTime;
    }

    /// <summary>
    /// Capture a serializable snapshot of the currently active effects on this player.
    /// </summary>
    public List<SerializableEffectState> CaptureState()
    {
        var list = new List<SerializableEffectState>();

        // Capture running timed effects
        foreach (var (effect, runner) in _activeEffects)
        {
            if (effect == null) continue;
            float remaining = 0f;
            if (runner != null && runner.totalDuration > 0f)
                remaining = Mathf.Max(0f, runner.totalDuration - (Time.time - runner.startTime));

            list.Add(new SerializableEffectState
            {
                effectName = effect.name,
                isOneShot = effect.oneShot,
                remainingTime = remaining
            });
        }

        // Capture one-shot effects that have been applied but have no runner
        foreach (var pair in _displayEffects)
        {
            var effect = pair.effect;
            var runner = pair.runner;
            if (effect == null) continue;
            if (runner == null && effect.oneShot)
            {
                // ensure we don't duplicate entries
                if (!list.Exists(e => e.effectName == effect.name))
                {
                    list.Add(new SerializableEffectState
                    {
                        effectName = effect.name,
                        isOneShot = true,
                        remainingTime = 0f
                    });
                }
            }
        }

        return list;
    }

    /// <summary>
    /// Restore effects from a captured snapshot. This will clear existing effects, then re-apply by effect name.
    /// Note: re-applied timed effects start fresh (or with remainingTime if > 0 when possible).
    /// </summary>
    public void RestoreState(List<SerializableEffectState> snapshot, GameObject player)
    {
        // Clear current effects
        ClearAllEffects(player);

        if (snapshot == null || snapshot.Count == 0) return;

        // Find all PowerupEffect assets in project/scene
        var allEffects = Resources.FindObjectsOfTypeAll<PowerupEffect>();

        foreach (var s in snapshot)
        {
            if (string.IsNullOrEmpty(s.effectName)) continue;

            var effect = System.Array.Find(allEffects, e => e != null && e.name == s.effectName);
            if (effect == null)
            {
                Debug.LogWarning($"[EffectTracker] Could not find effect asset '{s.effectName}' to restore");
                continue;
            }

            // Apply effect normally. Timed effects will start their own runner.
            effect.Apply(player);

            // If it's a one-shot, mark it as applied so it won't reapply
            if (s.isOneShot)
                MarkOneShotApplied(effect);
        }
    }

    /// <summary>
    /// Mark a one-shot effect as already applied for this tracker.
    /// </summary>
    public void MarkOneShotApplied(PowerupEffect effect)
    {
        if (effect != null && effect.oneShot)
            _appliedOneShots.Add(effect);
    }

    private void OnStateChanged(PlayerSkinState _)
    {
        ClearAllEffects(gameObject);
    }

    /// <summary>
    /// Called by PowerupEffectRunner when its coroutine finishes naturally.
    /// Removes the completed entry so the list does not grow unbounded.
    /// </summary>
    public void UnregisterRunner(PowerupEffectRunner runner)
    {
        _activeEffects.RemoveAll(e => e.runner == runner);
        _displayEffects.RemoveAll(e => e.runner == runner);
    }

    /// <summary>
    /// Clears all tracked effects and UI-visible effect state.
    /// </summary>
    public void ClearAllEffects(GameObject player)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var (effect, runner) = _activeEffects[i];
            effect?.Remove(gameObject);
            if (runner != null)
                Destroy(runner);
        }

        _activeEffects.Clear();
        _displayEffects.Clear();
        _appliedOneShots.Clear();

        if (player != null)
            ScaleCharacterEffect.ResetVisualScale(player);
    }
}
