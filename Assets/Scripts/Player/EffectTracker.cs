using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks active effects on the player and clears them on state swap.
/// </summary>
public class EffectTracker : MonoBehaviour
{
    private CharacterState _characterState;
    private readonly List<PowerupEffectRunner> _activeRunners = new();

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

    public void RegisterRunner(PowerupEffectRunner runner)
    {
        if (runner != null && !_activeRunners.Contains(runner))
            _activeRunners.Add(runner);
    }

    private void OnStateChanged(PlayerSkinState newState)
    {
        // Clear all active effect runners on state swap
        foreach (var runner in _activeRunners)
        {
            if (runner != null)
                Destroy(runner);
        }
        _activeRunners.Clear();
    }
}
