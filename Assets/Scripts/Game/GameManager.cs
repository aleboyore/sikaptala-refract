using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	[SerializeField] private PlayerController _player;
	[SerializeField] private List<string> _levelNames; // List of all level names in order
	private int _currentLevelIndex = 0;

	private int _lastRestoredCheckpointRevision = -1;
	private Vector3 _levelOriginalSpawnPoint = Vector3.zero; // Original spawn point for current level

	private void Awake() {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Prevent deletion on scene load
        }
        else
        {
            Destroy(gameObject); // Clean up duplicates if we return to Main Menu
        }

		// Auto-find player controller if not explicitly assigned in Inspector
		if (_player == null)
		{
			_player = FindAnyObjectByType<PlayerController>();
			if (_player != null)
			{
				RegisterPlayer(_player);
				Debug.Log($"[GameManager] Auto-registered PlayerController '{_player.gameObject.name}'");
			}
			else
			{
				Debug.LogWarning("[GameManager] PlayerController not found automatically. Call RegisterPlayer() or assign in Inspector.");
			}
		}

		// Subscribe to sceneLoaded so we can auto-register players spawned on scene load
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// If player isn't set (or was destroyed during scene unload), try to find it in the newly loaded scene
		if (_player == null)
		{
			_player = FindAnyObjectByType<PlayerController>();
			if (_player != null)
			{
				RegisterPlayer(_player);
				Debug.Log($"[GameManager] Auto-registered PlayerController on scene load '{scene.name}' -> '{_player.gameObject.name}'");
			}
		}
		
		// Find the level's original spawn point
		var spawnPoint = FindAnyObjectByType<PlayerSpawnPoint>();
		if (spawnPoint != null)
		{
			_levelOriginalSpawnPoint = spawnPoint.transform.position;
			Debug.Log($"[GameManager] Found PlayerSpawnPoint at x = {_levelOriginalSpawnPoint.x}, y = {_levelOriginalSpawnPoint.y} for level '{scene.name}'");
		}
		else
		{
			Debug.LogWarning($"[GameManager] No PlayerSpawnPoint found in level '{scene.name}' - level spawn will default to zero");
			_levelOriginalSpawnPoint = Vector3.zero;
		}
	}

	public void NotifyLockIn(PlayerController player)
	{
		if (_player == null) return;
		if (_player.State == PlayerState.LockedIn)
			LoadNextLevel();
	}

    // Call this from the PlayerController's Start() or Awake() method
    public void RegisterPlayer(PlayerController player)
    {
        _player = player;
    }

	public void TriggerDeath()
	{
		if (_player == null) return;

		// Check if player can actually die (shields/invulnerability)
		if (!_player.CanDie()) return;

		if (CheckpointManager.Instance == null) return;

		// Prevent duplicate restore for the same checkpoint revision
		int currentRevision = CheckpointManager.Instance.CheckpointRevision;
		if (_lastRestoredCheckpointRevision == currentRevision)
		{
			Debug.Log("[GameManager] Death restore skipped — already restored for current checkpoint revision.");
			return;
		}

		Debug.Log($"[GameManager] Death triggered. Restoring to checkpoint at {CheckpointManager.Instance.PlayerPosition} (rev {currentRevision})");

		// Clear player effects before resetting position
		_player.GetComponent<EffectTracker>()?.ClearAllEffects(_player.gameObject);

		// Determine respawn position: use checkpoint if reached, otherwise use level's original spawn point
		Vector3 respawnPos = (currentRevision > 0) 
			? CheckpointManager.Instance.PlayerPosition 
			: _levelOriginalSpawnPoint;

		// Restore the player position and state
		_player.ResetToCheckpoint(respawnPos);
		
		if (currentRevision == 0)
		{
			Debug.Log($"[GameManager] No checkpoint reached yet; respawning at level spawn point x = {respawnPos.x}, y = {respawnPos.y}");
		}

		// Restore world objects (pickups, boxes) based on checkpoint snapshot
		// Only if a checkpoint has actually been saved (revision > 0)
		if (currentRevision > 0)
		{
			CheckpointManager.Instance.RestoreFromSnapshot();
		}

		_lastRestoredCheckpointRevision = currentRevision;
	}

	/// <summary>
	/// Restores all world objects to their checkpoint state.
	/// Objects created after the checkpoint are deactivated/removed; objects from the checkpoint are restored.
	/// </summary>
	private void RestoreCheckpointWorldState()
	{
		var checkpointObjects = CheckpointManager.Instance.GetCheckpointObjects();
		var allRestorers = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
		
		foreach (var obj in allRestorers)
		{
			if (obj is not ICheckpointRestorer restorer) continue;
			
			if (checkpointObjects.Contains(obj.gameObject))
			{
				// Object existed at checkpoint: restore it
				restorer.RestoreToCheckpoint();
			}
			else
			{
				// Object created after checkpoint: deactivate it
				restorer.RevertPostCheckpoint();
			}
		}
		
		Debug.Log("[GameManager] World state restored to checkpoint");
	}

	
	private void LoadNextLevel()
	{
		if (_levelNames == null || _levelNames.Count == 0)
		{
			Debug.LogWarning("[GameManager] Level names list is empty or not set in Inspector");
			return;
		}
		
		_currentLevelIndex++;
		if (_currentLevelIndex >= _levelNames.Count)
		{
			Debug.Log("[GameManager] All levels completed!");
			return;
		}
		
		string nextLevel = _levelNames[_currentLevelIndex];
		Debug.Log($"[GameManager] Loading next level: {nextLevel} (index {_currentLevelIndex})");
		SceneManager.LoadScene(nextLevel);
	}
}
