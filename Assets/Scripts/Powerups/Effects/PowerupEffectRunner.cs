using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime coroutine host for ScriptableObject power-up effects.
/// Attach to the player at runtime; removed when the effect ends or is cancelled.
/// </summary>
public sealed class PowerupEffectRunner : MonoBehaviour
{
    // Optional timing metadata for UI
    public float totalDuration = 0f; // 0 = no duration
    public float startTime = 0f;
    /// <summary>
    /// Start a timed effect coroutine on the player.
    /// </summary>
    /// <param name="owner">The player GameObject.</param>
    /// <param name="effect">The PowerupEffect that owns this routine (used for lifecycle tracking).</param>
    /// <param name="routine">The IEnumerator coroutine to run.</param>
    public static void Run(GameObject owner, PowerupEffect effect, IEnumerator routine, float totalDuration = 0f)
    {
        if (owner == null || routine == null) return;

        var runner = owner.AddComponent<PowerupEffectRunner>(); // one runner per effect instance
        runner.totalDuration = totalDuration;
        runner.startTime = Time.time;
        var tracker = owner.GetComponent<EffectTracker>();
        tracker?.RegisterEffect(effect, runner);

        runner.StartCoroutine(runner.RunAndCleanup(routine, tracker));
    }

    private IEnumerator RunAndCleanup(IEnumerator routine, EffectTracker tracker)
    {
        yield return StartCoroutine(routine);
        tracker?.UnregisterRunner(this);
        Destroy(this);
    }
}