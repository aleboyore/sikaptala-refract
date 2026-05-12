using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Interface for objects that can restore themselves to a checkpoint state.
/// Implemented by PowerupBehavior, ScriptableBox, and other checkpoint-aware objects.
/// </summary>
public interface ICheckpointRestorer
{
	/// <summary>Restore this object to its checkpoint state.</summary>
	void RestoreToCheckpoint();
	
	/// <summary>Handle cleanup when this object was created after the checkpoint and should be reverted.</summary>
	void RevertPostCheckpoint();
}

/// <summary>
/// Manages checkpoint state snapshots and restoration.
/// Stores the current checkpoint position and tracks world objects that existed at checkpoint time.
/// On death, restores only objects collected/changed after the checkpoint.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
	public static CheckpointManager Instance { get; private set; }
	public Vector3 PlayerPosition { get; private set; }
	public int CheckpointRevision { get; private set; } = 0;

	// Snapshot stored for the current checkpoint (in-memory and optional JSON on disk)
	private CheckpointSnapshot _currentSnapshot;

	// Track objects that were active at the current checkpoint (legacy quick set)
	private HashSet<GameObject> _checkpointActiveObjects = new();

	private void Awake() 
	{
		if (Instance == null)
			Instance = this;
		else
			DestroyImmediate(gameObject);
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	/// <summary>
	/// Saves the current checkpoint position and captures the active world state.
	/// Called when player enters a checkpoint trigger.
	/// </summary>
	public void SaveCheckpoint(Vector3 playerPos)
	{
		PlayerPosition = playerPos;
		Debug.Log($"[Checkpoint] Spawn updated: x = {playerPos.x}, y = {playerPos.y}");
		CheckpointRevision++;
		// Build a snapshot of all objects with persistent identity
		_currentSnapshot = new CheckpointSnapshot
		{
			revision = CheckpointRevision,
			sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
			playerPosition = playerPos,
			entries = new List<CheckpointEntry>()
		};

		_checkpointActiveObjects.Clear();

		var identities = Resources.FindObjectsOfTypeAll<CheckpointIdentity>();
		foreach (var id in identities)
		{
			var go = id.gameObject;
			var entry = new CheckpointEntry
			{
				id = id.Id,
				prefabName = id.PrefabName,
				active = go.activeInHierarchy,
				position = go.transform.position,
				rotation = go.transform.rotation.eulerAngles,
				scale = go.transform.localScale
			};

			_currentSnapshot.entries.Add(entry);

			if (go.activeInHierarchy) _checkpointActiveObjects.Add(go);
		}

		// Capture player effect state if available
		var playerCtrl = FindAnyObjectByType<PlayerController>();
		if (playerCtrl != null)
		{
			var tracker = playerCtrl.GetComponent<EffectTracker>();
			if (tracker != null)
			{
				_currentSnapshot.playerEffects = tracker.CaptureState();
			}
		}

		// Optional: write snapshot to disk for debugging/inspection
		try
		{
			string json = JsonUtility.ToJson(_currentSnapshot, true);
			string path = System.IO.Path.Combine(Application.persistentDataPath, $"checkpoint_rev_{CheckpointRevision}.json");
			System.IO.File.WriteAllText(path, json);
			Debug.Log($"[Checkpoint] Saved snapshot to {path}");
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"[Checkpoint] Failed to write snapshot to disk: {ex.Message}");
		}

		Debug.Log($"[Checkpoint] Saved at {playerPos}, revision {CheckpointRevision}, {_checkpointActiveObjects.Count} objects tracked (snapshot entries: {_currentSnapshot.entries.Count})");
	}

	/// <summary>
	/// Restore world state using the saved snapshot for the current checkpoint.
	/// If no snapshot exists, this is a no-op.
	/// </summary>
	public void RestoreFromSnapshot()
	{
		if (_currentSnapshot == null)
		{
			Debug.LogWarning("[Checkpoint] No snapshot to restore from");
			return;
		}

		// Build a lookup of snapshot entries by id
		var lookup = new Dictionary<string, CheckpointEntry>();
		foreach (var e in _currentSnapshot.entries)
			lookup[e.id] = e;

		var identities = Resources.FindObjectsOfTypeAll<CheckpointIdentity>();
		foreach (var id in identities)
		{
			var go = id.gameObject;
			if (lookup.TryGetValue(id.Id, out var entry))
			{
				// Object existed at checkpoint: restore transform and active state
				go.SetActive(entry.active);
				go.transform.position = entry.position;
				go.transform.rotation = Quaternion.Euler(entry.rotation);
				go.transform.localScale = entry.scale;

				var restorer = go.GetComponent<ICheckpointRestorer>();
				restorer?.RestoreToCheckpoint();
			}
			else
			{
				// Object not in snapshot -> created after checkpoint, revert
				go.SetActive(false);
				var restorer = go.GetComponent<ICheckpointRestorer>();
				restorer?.RevertPostCheckpoint();
			}
		}

		Debug.Log($"[Checkpoint] Restored world state from snapshot rev {_currentSnapshot.revision}");

		// Restore player effects if snapshot contains them
		if (_currentSnapshot.playerEffects != null && _currentSnapshot.playerEffects.Count > 0)
		{
			var playerCtrl = FindAnyObjectByType<PlayerController>();
			if (playerCtrl != null)
			{
				var tracker = playerCtrl.GetComponent<EffectTracker>();
				if (tracker != null)
				{
					tracker.RestoreState(_currentSnapshot.playerEffects, playerCtrl.gameObject);
					Debug.Log("[Checkpoint] Restored player effects from snapshot");
				}
			}
		}
	}

	/// <summary>
	/// Returns true if an object existed at the current checkpoint.
	/// Used by restore logic to decide which objects should be restored on death.
	/// </summary>
	public bool ExistedAtCheckpoint(GameObject obj)
	{
		return obj != null && _checkpointActiveObjects.Contains(obj);
	}

	/// <summary>
	/// Returns the list of objects that should be restored on death.
	/// Objects that existed at checkpoint stay as-is; objects created after should restore their checkpoint state.
	/// </summary>
	public HashSet<GameObject> GetCheckpointObjects()
	{
		return new HashSet<GameObject>(_checkpointActiveObjects);
	}

	[System.Serializable]
	public class CheckpointSnapshot
	{
		public int revision;
		public string sceneName;
		public Vector3 playerPosition;
		public List<CheckpointEntry> entries;
		public List<global::EffectTracker.SerializableEffectState> playerEffects;
	}

	[System.Serializable]
	public class CheckpointEntry
	{
		public string id;
		public string prefabName;
		public bool active;
		public Vector3 position;
		public Vector3 rotation;
		public Vector3 scale;
	}
}
