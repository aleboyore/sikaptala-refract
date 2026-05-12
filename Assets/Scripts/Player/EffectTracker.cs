using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks active power-up effects on the player.
/// On skin swap, reverts and cancels all non-persistent effects.
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

    private void OnStateChanged(PlayerSkinState _)
    {
        // Iterate in reverse so we can safely remove while iterating
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var (effect, runner) = _activeEffects[i];

            if (effect.persistAcrossStateSwap)
                continue; // leave this effect running

            // Revert the effect's changes before killing the coroutine
            effect.Remove(gameObject);

            if (runner != null)
                Destroy(runner);

            _activeEffects.RemoveAt(i);
            _displayEffects.RemoveAll(e => e.runner == runner || e.effect == effect);
        }
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
