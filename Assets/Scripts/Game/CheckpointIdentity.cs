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
        EnsureId();
    }

    private void Reset()
    {
        EnsureId();

        if (string.IsNullOrEmpty(PrefabName) && gameObject.scene.IsValid())
            PrefabName = gameObject.name;
    }

    private void EnsureId()
    {
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureId();
    }
#endif
}
