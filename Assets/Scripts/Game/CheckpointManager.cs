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

	private CheckpointSnapshot _initialSnapshot;
	private bool _hasInitialSnapshot = false;

	// Snapshot stored for the current checkpoint (in-memory and optional JSON on disk)
	private CheckpointSnapshot _currentSnapshot;

	// Track objects that were active at the current checkpoint (legacy quick set)
	private HashSet<GameObject> _checkpointActiveObjects = new();

	private static bool IsPlayerHierarchy(GameObject go)
	{
		return go != null && (go.GetComponentInParent<PlayerController>() != null || go.transform.root.CompareTag("Player"));
	}

	private static bool IsSceneInstance(GameObject go)
	{
		return go != null && go.scene.IsValid() && go.scene.isLoaded;
	}

	private void OnEnable()
	{
		_hasInitialSnapshot = false;
		_initialSnapshot = null;
		_currentSnapshot = null;
		CheckpointRevision = 0;
		PlayerPosition = Vector3.zero;
		_checkpointActiveObjects.Clear();
	}

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
			if (!IsSceneInstance(go))
				continue;

			// If this identity belongs to the player, skip toggling it here.
			// GameManager is responsible for player lifecycle (spawn/respawn/etc.).
			if (IsPlayerHierarchy(go))
				continue;
			
			// Capture component state (rigidbody type, collider state, etc.) for boxes only
			// Powerups manage their own lifecycle and collider state in RestoreToCheckpoint()
			SerializedComponentState compState = null;
			if (go.GetComponent<ScriptableBox>() != null)
			{
				compState = new SerializedComponentState(go);
			}
			bool isPowerup = go.GetComponent<PowerupBehavior>() != null;
			
			var entry = new CheckpointEntry
			{
				id = id.Id,
				prefabName = id.PrefabName,
				active = isPowerup ? true : go.activeInHierarchy,
				position = go.transform.position,
				rotation = go.transform.rotation.eulerAngles,
				scale = go.transform.localScale,
				componentState = compState
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
	/// Captures the scene's initial spawn state so a death before any checkpoint
	/// can restore the world exactly as it was when the level loaded.
	/// </summary>
	public void CaptureInitialSnapshot(Vector3 playerPos)
	{
		if (_hasInitialSnapshot)
			return;

		_initialSnapshot = CaptureSnapshot(0, playerPos);
		_hasInitialSnapshot = true;
		Debug.Log($"[Checkpoint] Captured initial spawn snapshot at {playerPos}");
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

			// Never toggle the player object here; GameManager owns player lifecycle.
			if (go != null && go.CompareTag("Player"))
				continue;

			if (lookup.TryGetValue(id.Id, out var entry))
			{
				// Object existed at checkpoint: restore transform and active state
				bool isPowerup = go.GetComponent<PowerupBehavior>() != null;
				go.SetActive(isPowerup ? true : entry.active);
				go.transform.position = entry.position;
				go.transform.rotation = Quaternion.Euler(entry.rotation);
				go.transform.localScale = entry.scale;
				
				// Restore all component state (rigidbody type, collider state, etc.)
				if (entry.componentState != null)
				{
					entry.componentState.Apply(go);
					Debug.Log($"[Checkpoint] Restored component state for '{go.name}'");
				}

				var restorer = go.GetComponent<ICheckpointRestorer>();
				restorer?.RestoreToCheckpoint();
			}
			else
			{
				// Object not in snapshot -> created after checkpoint, revert
				// Skip player objects to avoid deactivating the player during restore
				if (go != null && go.CompareTag("Player"))
					continue;
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
	/// Restore world state back to the initial spawn snapshot.
	/// Used when the player dies before reaching any checkpoint.
	/// </summary>
	public void RestoreInitialSnapshot()
	{
		if (!_hasInitialSnapshot || _initialSnapshot == null)
		{
			Debug.LogWarning("[Checkpoint] No initial snapshot to restore from");
			return;
		}

		RestoreSnapshot(_initialSnapshot, "initial spawn");
	}

	private CheckpointSnapshot CaptureSnapshot(int revision, Vector3 playerPos)
	{
		var snapshot = new CheckpointSnapshot
		{
			revision = revision,
			sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
			playerPosition = playerPos,
			entries = new List<CheckpointEntry>()
		};

		var identities = Resources.FindObjectsOfTypeAll<CheckpointIdentity>();
		foreach (var id in identities)
		{
			var go = id.gameObject;
			if (!IsSceneInstance(go))
				continue;
			if (IsPlayerHierarchy(go))
				continue;
			
			// Capture component state for boxes only
			// Powerups manage their own lifecycle in RestoreToCheckpoint()
			SerializedComponentState compState = null;
			if (go.GetComponent<ScriptableBox>() != null)
			{
				compState = new SerializedComponentState(go);
			}
			bool isPowerup = go.GetComponent<PowerupBehavior>() != null;
			
			snapshot.entries.Add(new CheckpointEntry
			{
				id = id.Id,
				prefabName = id.PrefabName,
				active = isPowerup ? true : go.activeInHierarchy,
				position = go.transform.position,
				rotation = go.transform.rotation.eulerAngles,
				scale = go.transform.localScale,
				componentState = compState
			});
		}

		var playerCtrl = FindAnyObjectByType<PlayerController>();
		if (playerCtrl != null)
		{
			var tracker = playerCtrl.GetComponent<EffectTracker>();
			if (tracker != null)
			{
				snapshot.playerEffects = tracker.CaptureState();
			}
		}

		return snapshot;
	}

	private void RestoreSnapshot(CheckpointSnapshot snapshot, string label)
	{
		if (snapshot == null)
		{
			Debug.LogWarning($"[Checkpoint] No {label} snapshot to restore from");
			return;
		}

		var lookup = new Dictionary<string, CheckpointEntry>();
		foreach (var e in snapshot.entries)
			lookup[e.id] = e;

		var identities = Resources.FindObjectsOfTypeAll<CheckpointIdentity>();
		foreach (var id in identities)
		{
			var go = id.gameObject;
			if (!IsSceneInstance(go))
				continue;
			if (IsPlayerHierarchy(go))
				continue;
			if (lookup.TryGetValue(id.Id, out var entry))
			{
				go.SetActive(entry.active);
				go.transform.position = entry.position;
				go.transform.rotation = Quaternion.Euler(entry.rotation);
				go.transform.localScale = entry.scale;
				
				// Restore all component state (rigidbody type, collider state, etc.)
				if (entry.componentState != null)
				{
					entry.componentState.Apply(go);
				}

				var restorer = go.GetComponent<ICheckpointRestorer>();
				restorer?.RestoreToCheckpoint();
			}
			else
			{
				// For the initial-spawn snapshot, avoid deactivating anything we failed to capture.
				// This prevents scene boxes/powerups from disappearing because the initial capture
				// happened before their identity state was fully stable.
				if (snapshot.revision == 0)
					continue;

				go.SetActive(false);
				var restorer = go.GetComponent<ICheckpointRestorer>();
				restorer?.RevertPostCheckpoint();
			}
		}

		if (snapshot.playerEffects != null && snapshot.playerEffects.Count > 0)
		{
			var playerCtrl = FindAnyObjectByType<PlayerController>();
			if (playerCtrl != null)
			{
				var tracker = playerCtrl.GetComponent<EffectTracker>();
				if (tracker != null)
				{
					tracker.RestoreState(snapshot.playerEffects, playerCtrl.gameObject);
				}
			}
		}

		Debug.Log($"[Checkpoint] Restored world state from {label} snapshot rev {snapshot.revision}");
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
		// Component state snapshots for boxes, powerups, and other stateful objects
		public SerializedComponentState componentState;
	}

	/// <summary>
	/// Captures the state of modifiable components so they can be fully restored at checkpoint.
	/// Stores rigidbody type, collider enabled state, and other runtime-modifiable properties.
	/// </summary>
	[System.Serializable]
	public class SerializedComponentState
	{
		// Rigidbody2D state
		public int rbodyType = (int)RigidbodyType2D.Dynamic;
		public int rbodyConstraints = (int)RigidbodyConstraints2D.FreezeRotation;
		public float rbodyGravityScale = 0f;
		public float rbodyLinearDamping = 8f;
		
		// Collider state
		public bool colliderEnabled = true;
		
		public SerializedComponentState() { }
		
		public SerializedComponentState(GameObject go)
		{
			var rb = go.GetComponent<Rigidbody2D>();
			if (rb != null)
			{
				rbodyType = (int)rb.bodyType;
				rbodyConstraints = (int)rb.constraints;
				rbodyGravityScale = rb.gravityScale;
				rbodyLinearDamping = rb.linearDamping;
			}
			
			var col = go.GetComponent<Collider2D>();
			if (col != null)
			{
				colliderEnabled = col.enabled;
			}
		}
		
		public void Apply(GameObject go)
		{
			var rb = go.GetComponent<Rigidbody2D>();
			if (rb != null)
			{
				rb.bodyType = (RigidbodyType2D)rbodyType;
				rb.constraints = (RigidbodyConstraints2D)rbodyConstraints;
				rb.gravityScale = rbodyGravityScale;
				rb.linearDamping = rbodyLinearDamping;
			}
			
			var col = go.GetComponent<Collider2D>();
			if (col != null)
			{
				col.enabled = colliderEnabled;
			}
		}
	}
}
