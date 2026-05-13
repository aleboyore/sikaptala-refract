using UnityEngine;

/// <summary>
/// Assign a persistent identity to checkpoint-aware objects so snapshots can reference them.
/// The Id is auto-generated in Reset() if empty. You can override it in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class CheckpointIdentity : MonoBehaviour
{
    [SerializeField]
    private string id;

    [Tooltip("Optional human-readable prefab/asset name to aid debugging")]
    public string PrefabName;

    public string Id => id;

    private void Awake()
    {
        // Prefab assets serialize a shared ID, so every scene instance needs its own runtime ID.
        // This keeps checkpoint snapshots from overwriting one box/powerup with another clone of the same prefab.
        if (gameObject.scene.IsValid())
            id = System.Guid.NewGuid().ToString();
    }

    private void Reset()
    {
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(PrefabName) && gameObject.scene.IsValid())
            PrefabName = gameObject.name;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString();
    }
#endif
}
