using UnityEngine;

public class PlayerLayerSwap : MonoBehaviour
{
    [Header("Layer Names")]
    public string shardLayerName = "Shard";
    public string veilLayerName = "Veil";

    // Called by CharacterState.onStateChanged
    public void OnStateChanged(PlayerSkinState newState)
    {
        string targetLayer = (newState == PlayerSkinState.Shard) ? shardLayerName : veilLayerName;
        SetLayerRecursive(gameObject, LayerMask.NameToLayer(targetLayer));
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
