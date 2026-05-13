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

	private CheckpointSnapshot _currentSnapshot;

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
			if (!IsSceneInstance(go)) continue;
			if (IsPlayerHierarchy(go)) continue;

			// Skip any identity whose GUID hasn't been assigned yet — these are
			// objects whose Awake() hasn't run, snapshotting them with id="" would
			// create a poisoned entry that matches nothing on restore.
			if (string.IsNullOrEmpty(id.Id))
			{
				Debug.LogWarning($"[Checkpoint] Skipping '{go.name}' — CheckpointIdentity has no ID yet (Awake not run?)");
				continue;
			}

			SerializedComponentState compState = null;
			if (go.GetComponent<ScriptableBox>() != null)
				compState = new SerializedComponentState(go);

			var entry = new CheckpointEntry
			{
				id = id.Id,
				prefabName = id.PrefabName,
				active = go.activeInHierarchy,
				position = go.transform.position,
				rotation = go.transform.rotation.eulerAngles,
				scale = go.transform.localScale,
				componentState = compState
			};

			_currentSnapshot.entries.Add(entry);
			if (go.activeInHierarchy) _checkpointActiveObjects.Add(go);
		}

		var playerCtrl = FindAnyObjectByType<PlayerController>();
		if (playerCtrl != null)
		{
			var tracker = playerCtrl.GetComponent<EffectTracker>();
			if (tracker != null)
				_currentSnapshot.playerEffects = tracker.CaptureState();
		}

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
		Debug.Log($"[Checkpoint] Captured initial spawn snapshot ({_initialSnapshot.entries.Count} entries) at {playerPos}");
	}

	/// <summary>
	/// Restore world state using the saved snapshot for the current checkpoint.
	/// </summary>
	public void RestoreFromSnapshot()
	{
		if (_currentSnapshot == null)
		{
			Debug.LogWarning("[Checkpoint] No snapshot to restore from");
			return;
		}

		var lookup = new Dictionary<string, CheckpointEntry>();
		foreach (var e in _currentSnapshot.entries)
			lookup[e.id] = e;

		var identities = Resources.FindObjectsOfTypeAll<CheckpointIdentity>();
		foreach (var id in identities)
		{
			var go = id.gameObject;
			if (go != null && go.CompareTag("Player")) continue;

			if (lookup.TryGetValue(id.Id, out var entry))
			{
				go.SetActive(entry.active);
				go.transform.position = entry.position;
				go.transform.rotation = Quaternion.Euler(entry.rotation);
				go.transform.localScale = entry.scale;

				if (entry.componentState != null)
					entry.componentState.Apply(go);

				go.GetComponent<ICheckpointRestorer>()?.RestoreToCheckpoint();
			}
			else
			{
				if (go != null && go.CompareTag("Player")) continue;
				go.SetActive(false);
				go.GetComponent<ICheckpointRestorer>()?.RevertPostCheckpoint();
			}
		}

		Debug.Log($"[Checkpoint] Restored world state from snapshot rev {_currentSnapshot.revision}");

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
			if (!IsSceneInstance(go)) continue;
			if (IsPlayerHierarchy(go)) continue;

			// Skip uninitialized identities — snapshotting with id="" creates a poisoned
			// entry that will never match anything on restore.
			if (string.IsNullOrEmpty(id.Id))
			{
				Debug.LogWarning($"[Checkpoint] Skipping '{go.name}' in initial snapshot — CheckpointIdentity has no ID yet");
				continue;
			}

			SerializedComponentState compState = null;
			if (go.GetComponent<ScriptableBox>() != null)
				compState = new SerializedComponentState(go);

			snapshot.entries.Add(new CheckpointEntry
			{
				id = id.Id,
				prefabName = id.PrefabName,
				active = go.activeInHierarchy,
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
				snapshot.playerEffects = tracker.CaptureState();
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
			if (!IsSceneInstance(go)) continue;
			if (IsPlayerHierarchy(go)) continue;

			if (lookup.TryGetValue(id.Id, out var entry))
			{
				// Found in snapshot: restore exactly as captured.
				go.SetActive(entry.active);
				go.transform.position = entry.position;
				go.transform.rotation = Quaternion.Euler(entry.rotation);
				go.transform.localScale = entry.scale;

				if (entry.componentState != null)
					entry.componentState.Apply(go);

				go.GetComponent<ICheckpointRestorer>()?.RestoreToCheckpoint();
			}
			else
			{
				// Not in snapshot. Two cases:
				//
				// revision > 0 (checkpoint snapshot): object was created after the checkpoint
				// was saved, so it shouldn't exist — deactivate it.
				//
				// revision == 0 (initial snapshot): object wasn't captured, most likely because
				// its CheckpointIdentity.Awake() hadn't run yet when the snapshot was taken.
				// Safe assumption: it's a level-start object and should be active. Re-enable it
				// and reset internal state rather than silently leaving it in whatever state it
				// was in when the player died (e.g. collected/inactive).
				if (snapshot.revision == 0)
				{
					Debug.LogWarning($"[Checkpoint] '{go.name}' not in initial snapshot (late Awake?) — forcing active and resetting state");
					go.SetActive(true);
					go.GetComponent<ICheckpointRestorer>()?.RestoreToCheckpoint();
				}
				else
				{
					go.SetActive(false);
					go.GetComponent<ICheckpointRestorer>()?.RevertPostCheckpoint();
				}
			}
		}

		if (snapshot.playerEffects != null && snapshot.playerEffects.Count > 0)
		{
			var playerCtrl = FindAnyObjectByType<PlayerController>();
			if (playerCtrl != null)
			{
				var tracker = playerCtrl.GetComponent<EffectTracker>();
				if (tracker != null)
					tracker.RestoreState(snapshot.playerEffects, playerCtrl.gameObject);
			}
		}

		Debug.Log($"[Checkpoint] Restored world state from {label} snapshot rev {snapshot.revision}");
	}

	public bool ExistedAtCheckpoint(GameObject obj)
	{
		return obj != null && _checkpointActiveObjects.Contains(obj);
	}

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
		public SerializedComponentState componentState;
	}

	[System.Serializable]
	public class SerializedComponentState
	{
		public int rbodyType = (int)RigidbodyType2D.Dynamic;
		public int rbodyConstraints = (int)RigidbodyConstraints2D.FreezeRotation;
		public float rbodyGravityScale = 0f;
		public float rbodyLinearDamping = 8f;
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
				colliderEnabled = col.enabled;
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
				col.enabled = colliderEnabled;
		}
	}
}