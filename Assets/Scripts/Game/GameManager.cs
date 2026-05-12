using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	[SerializeField] private PlayerController _player;
	[SerializeField] private GameObject _playerPrefab;
	[SerializeField] private List<string> _levelNames; // List of all level names in order
	private int _currentLevelIndex = 0;

	private bool _isProcessingDeath = false;
	private Vector3 _levelOriginalSpawnPoint = Vector3.zero; // Original spawn point for current level
	private bool _hasLoadedFirstLevel = false;

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

	private void Start()
	{
		// Load the first level if this is the first time GameManager is initialized
		if (!_hasLoadedFirstLevel && _currentLevelIndex == 0)
		{
			_hasLoadedFirstLevel = true;
			LoadFirstLevel();
		}
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
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

		// If player isn't set (or was destroyed during scene unload), try to find it in the newly loaded scene.
		// If no scene player exists, spawn one from the configured prefab.
		if (_player == null)
		{
			_player = FindAnyObjectByType<PlayerController>();
			if (_player != null)
			{
				RegisterPlayer(_player);
				Debug.Log($"[GameManager] Auto-registered PlayerController on scene load '{scene.name}' -> '{_player.gameObject.name}'");
			}
			else if (_playerPrefab != null)
			{
				Vector3 spawnPosition = spawnPoint != null ? spawnPoint.transform.position : _levelOriginalSpawnPoint;
				GameObject spawnedPlayer = Instantiate(_playerPrefab, spawnPosition, Quaternion.identity);
				_player = spawnedPlayer.GetComponent<PlayerController>();
				if (_player != null)
				{
					RegisterPlayer(_player);
					Debug.Log($"[GameManager] Spawned player prefab '{_playerPrefab.name}' at x = {spawnPosition.x}, y = {spawnPosition.y} for scene '{scene.name}'");
				}
				else
				{
					Debug.LogError($"[GameManager] Spawned player prefab '{_playerPrefab.name}' but no PlayerController component was found on it");
				}
			}
			else
			{
				Debug.LogWarning($"[GameManager] No PlayerController found and no player prefab assigned for scene '{scene.name}'");
			}
		}

		if (CheckpointManager.Instance != null)
		{
			StartCoroutine(CaptureInitialSnapshotNextFrame());
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
		if (_player == null)
		{
			if (_playerPrefab == null) return;

			Vector3 prefabSpawnPos = CheckpointManager.Instance != null && CheckpointManager.Instance.CheckpointRevision > 0
				? CheckpointManager.Instance.PlayerPosition
				: _levelOriginalSpawnPoint;

			GameObject spawnedPlayer = Instantiate(_playerPrefab, prefabSpawnPos, Quaternion.identity);
			spawnedPlayer.SetActive(true);
			_player = spawnedPlayer.GetComponent<PlayerController>();
			if (_player != null)
			{
				RegisterPlayer(_player);
				Debug.Log($"[GameManager] Respawned player prefab '{_playerPrefab.name}' at x = {prefabSpawnPos.x}, y = {prefabSpawnPos.y}");
			}
			else
			{
				Debug.LogError($"[GameManager] Respawned player prefab '{_playerPrefab.name}' but no PlayerController component was found on it");
			}
			_isProcessingDeath = false;
			return;
		}
		if (_isProcessingDeath) return;

		// Check if player can actually die (shields/invulnerability)
		if (!_player.CanDie()) return;

		if (CheckpointManager.Instance == null) return;

		_isProcessingDeath = true;
		int currentRevision = CheckpointManager.Instance.CheckpointRevision;

		Debug.Log($"[GameManager] Death triggered. Restoring to checkpoint at {CheckpointManager.Instance.PlayerPosition} (rev {currentRevision})");

		// Clear player effects before resetting position
		_player.gameObject.SetActive(true);
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
		// If a checkpoint exists, restore from the snapshot. Otherwise, return all
		// checkpoint-aware objects to their initial spawn state.
		if (currentRevision > 0)
			CheckpointManager.Instance.RestoreFromSnapshot();
		else
			CheckpointManager.Instance.RestoreInitialSnapshot();

		StartCoroutine(ClearDeathProcessingNextFrame());
	}

	private System.Collections.IEnumerator ClearDeathProcessingNextFrame()
	{
		yield return null;
		_isProcessingDeath = false;
	}

	private System.Collections.IEnumerator CaptureInitialSnapshotNextFrame()
	{
		yield return null;

		if (CheckpointManager.Instance == null)
			yield break;

		CheckpointManager.Instance.CaptureInitialSnapshot(_levelOriginalSpawnPoint);
	}

	
	private void LoadFirstLevel()
	{
		if (_levelNames == null || _levelNames.Count == 0)
		{
			Debug.LogWarning("[GameManager] Level names list is empty or not set in Inspector");
			return;
		}

		string firstLevel = _levelNames[0];
		Debug.Log($"[GameManager] Loading first level: {firstLevel} (index 0)");
		SceneManager.LoadScene(firstLevel);
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
