#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class AddCheckpointIdentityToPrefabs
{
    [MenuItem("Tools/Checkpoint/Add CheckpointIdentity To Prefabs (Powerups/Boxes)")]
    public static void AddToPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab", "Assets/Prefabs", "Assets/" });
        int added = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // Only target prefabs that contain PowerupBehavior or ScriptableBox (match by component type name)
            bool hasTarget = prefab.GetComponentsInChildren<Component>(true).Any(c => c != null && (c.GetType().Name == "PowerupBehavior" || c.GetType().Name == "ScriptableBox"));

            if (!hasTarget) continue;

            // Load the prefab instance for editing
            var instance = PrefabUtility.LoadPrefabContents(path);
            if (instance == null) continue;

            bool changed = false;
            var identities = instance.GetComponentsInChildren<CheckpointIdentity>(true);
            if (identities == null || identities.Length == 0)
            {
                // add to root
                var id = instance.AddComponent<CheckpointIdentity>();
                id.PrefabName = instance.name;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                added++;
                Debug.Log($"[CheckpointTools] Added CheckpointIdentity to prefab: {path}");
            }

            PrefabUtility.UnloadPrefabContents(instance);
        }

        Debug.Log($"[CheckpointTools] Completed. Added to {added} prefabs.");
    }
}
#endif
