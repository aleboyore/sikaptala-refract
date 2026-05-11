using UnityEngine;

public class CameraStateSync : MonoBehaviour
{
    public Camera mainCam;

    // Build these masks in the Inspector using layer names
    public LayerMask shardMask;  // LieWorld + Shared + UI
    public LayerMask veilMask;   // TruthWorld + Shared + UI

    // Wire to CharacterState.onStateChanged
    public void OnStateChanged(PlayerSkinState newState)
    {
        mainCam.cullingMask = (newState == PlayerSkinState.Shard)
            ? (int)shardMask
            : (int)veilMask;
    }
}
