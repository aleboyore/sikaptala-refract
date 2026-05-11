using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime coroutine host for ScriptableObject power-up effects.
/// </summary>
public sealed class PowerupEffectRunner : MonoBehaviour
{
    public static void Run(GameObject owner, IEnumerator routine)
    {
        if (owner == null || routine == null) return;

        PowerupEffectRunner runner = owner.GetComponent<PowerupEffectRunner>();
        if (runner == null)
            runner = owner.AddComponent<PowerupEffectRunner>();

        // Register with effect tracker if available
        EffectTracker tracker = owner.GetComponent<EffectTracker>();
        if (tracker != null)
            tracker.RegisterRunner(runner);

        runner.StartCoroutine(routine);
    }
}