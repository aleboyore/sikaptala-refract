using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	[SerializeField] private PlayerController _player;
	[SerializeField] private GameObject _playerPrefab;
	[SerializeField] private float _respawnDelaySeconds = 0.75f;
	[SerializeField] private bool _autoRespawnIfPlayerMissing = true;
	[SerializeField] private List<string> _levelNames; // List of all level names in order
	private int _currentLevelIndex = 0;

	private bool _isProcessingDeath = false;
	private Vector3 _levelOriginalSpawnPoint = Vector3.zero;
	private bool _hasLoadedFirstLevel = false;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
			return; // Don't run the rest of Awake on the duplicate
		}

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

		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void Start()
	{
		if (!_hasLoadedFirstLevel && _currentLevelIndex == 0)
		{
			_hasLoadedFirstLevel = true;
			LoadFirstLevel();
		}
	}

	private void Update()
	{
		// BUG FIX: The old Update block would see the player inactive during the
		// respawn delay window (player is SetActive(false) in TriggerDeath) and
		// immediately re-enable + reposition them via EnsureActivePlayerForRespawn,
		// completely bypassing the respawn delay and snapshot restore.
		// The fix: only do the auto-recover if we are NOT already processing a death.
		// The _isProcessingDeath guard already exists, but the block below it was
		// calling EnsureActivePlayerForRespawn + ResetToCheckpoint directly, which
		// fights with RespawnAfterDelay. It is removed entirely — RespawnAfterDelay
		// is the single owner of re-enabling the player after death.

		if (!_autoRespawnIfPlayerMissing)
			return;

		if (_isProcessingDeath)
			return;

		if (CheckpointManager.Instance == null)
			return;

		// Only handle the case where the player reference is fully lost (null).
		// A disabled-but-valid player is handled exclusively by RespawnAfterDelay.
		if (_player == null)
		{
			Debug.LogWarning("[GameManager] Auto-respawn triggered because player reference is null.");
			TriggerDeath();
		}
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// Reset death flag on scene load so a death mid-transition doesn't soft-lock.
		_isProcessingDeath = false;

		// Find the level's original spawn point.
		var spawnPoint = FindAnyObjectByType<PlayerSpawnPoint>();
		Vector3 spawnPosition;
		if (spawnPoint != null)
		{
			_levelOriginalSpawnPoint = spawnPoint.transform.position;
			Debug.Log($"[GameManager] Found PlayerSpawnPoint at x = {_levelOriginalSpawnPoint.x}, y = {_levelOriginalSpawnPoint.y} for level '{scene.name}'");
			spawnPosition = _levelOriginalSpawnPoint;
		}
		else
		{
			Debug.LogWarning($"[GameManager] No PlayerSpawnPoint found in level '{scene.name}' - level spawn will default to zero");
			_levelOriginalSpawnPoint = Vector3.zero;
			spawnPosition = _levelOriginalSpawnPoint;
		}

		// If player isn't set (or was destroyed during scene unload), find or spawn one.
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
				GameObject spawnedPlayer = Instantiate(_playerPrefab, spawnPosition, Quaternion.identity);
				spawnedPlayer.SetActive(true);
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

		if (_player != null)
		{
			_player.gameObject.SetActive(true);
			_player.ResetToCheckpoint(spawnPosition);
			Debug.Log($"[GameManager] Positioned player '{_player.gameObject.name}' at level spawn x = {spawnPosition.x}, y = {spawnPosition.y} for scene '{scene.name}'");
		}

		// BUG FIX: CheckpointManager lives in the scene and may not have finished
		// its Awake() by the time OnSceneLoaded fires (execution order dependent).
		// Poll for it instead of doing a one-frame yield that can miss it.
		StartCoroutine(CaptureInitialSnapshotWhenReady());
	}

	public void NotifyLockIn(PlayerController player)
	{
		if (_player == null) return;
		if (_player.State == PlayerState.LockedIn)
			LoadNextLevel();
	}

	public void RegisterPlayer(PlayerController player)
	{
		_player = player;
	}

	public void TriggerDeath()
	{
		if (_isProcessingDeath) return;

		// Compute respawn position before touching player state.
		Vector3 respawnPos = CheckpointManager.Instance != null && CheckpointManager.Instance.CheckpointRevision > 0
			? CheckpointManager.Instance.PlayerPosition
			: _levelOriginalSpawnPoint;

		// BUG FIX: EnsureActivePlayerForRespawn re-enables the player immediately.
		// We need the player active to call CanDie(), but we must NOT leave them
		// active if we're about to SetActive(false) for the delay. So we check CanDie
		// before EnsureActive when possible, and only call EnsureActive to get a
		// reference if _player is null.
		if (_player == null)
		{
			_player = FindAnyObjectByType<PlayerController>();
			if (_player == null)
			{
				if (_playerPrefab != null)
				{
					var go = Instantiate(_playerPrefab, respawnPos, Quaternion.identity);
					go.SetActive(true);
					_player = go.GetComponent<PlayerController>();
					if (_player != null) RegisterPlayer(_player);
				}
				if (_player == null)
				{
					Debug.LogWarning("[GameManager] TriggerDeath: no player found, cannot process death.");
					return;
				}
			}
		}

		// Check if player can die (shields/invulnerability)
		if (!_player.CanDie())
		{
			return;
		}

		if (CheckpointManager.Instance == null)
		{
			Debug.LogWarning("[GameManager] TriggerDeath: CheckpointManager not available, cannot respawn.");
			return;
		}

		_isProcessingDeath = true;
		int currentRevision = CheckpointManager.Instance.CheckpointRevision;

		Debug.Log($"[GameManager] Death triggered. Respawn pos={respawnPos} (rev {currentRevision})");

		_player.GetComponent<EffectTracker>()?.ClearAllEffects(_player.gameObject);

		// Do not disable the player on death; wait and teleport them back to respawn.
		StartCoroutine(RespawnAfterDelay(respawnPos, currentRevision));
	}

	private bool EnsureActivePlayerForRespawn(Vector3 respawnPos)
	{
		// If the registered player exists but is inactive, leave it as-is (we no longer disable on death).
		if (_player != null && !_player.gameObject.activeInHierarchy)
		{
			Debug.LogWarning($"[GameManager] Registered player '{_player.gameObject.name}' is inactive. Will reuse instance without changing active state.");
		}

		// Try scene search.
		if (_player == null)
		{
			_player = FindAnyObjectByType<PlayerController>();
			if (_player != null) RegisterPlayer(_player);
		}

		// Last resort: spawn from prefab.
		if (_player == null)
		{
			if (_playerPrefab == null)
			{
				Debug.LogError("[GameManager] EnsureActivePlayerForRespawn: no player and no prefab assigned.");
				return false;
			}

			GameObject spawnedPlayer = Instantiate(_playerPrefab, respawnPos, Quaternion.identity);
			spawnedPlayer.SetActive(true);
			_player = spawnedPlayer.GetComponent<PlayerController>();
			if (_player == null)
			{
				Debug.LogError($"[GameManager] Respawned player prefab '{_playerPrefab.name}' but no PlayerController component found.");
				return false;
			}

			RegisterPlayer(_player);
			Debug.Log($"[GameManager] Respawned player prefab '{_playerPrefab.name}' at x = {respawnPos.x}, y = {respawnPos.y}");
		}

		return true;
	}

	private IEnumerator RespawnAfterDelay(Vector3 respawnPos, int currentRevision)
	{
		if (_respawnDelaySeconds > 0f)
			yield return new WaitForSeconds(_respawnDelaySeconds);

		// Re-acquire the player in case something changed during the delay.
		if (!EnsureActivePlayerForRespawn(respawnPos))
		{
			Debug.LogError("[GameManager] RespawnAfterDelay: could not find or create a player.");
			_isProcessingDeath = false;
			yield break;
		}

		_player.ResetToCheckpoint(respawnPos);

		Debug.Log($"[GameManager] Respawned player at {respawnPos} (rev {currentRevision})");

		// Restore world objects.
		if (CheckpointManager.Instance != null)
		{
			if (currentRevision > 0)
				CheckpointManager.Instance.RestoreFromSnapshot();
			else
				CheckpointManager.Instance.RestoreInitialSnapshot();
		}
		else
		{
			Debug.LogWarning("[GameManager] RespawnAfterDelay: CheckpointManager gone, world state not restored.");
		}

		// Clear the flag one frame later so any physics events this frame don't
		// immediately re-trigger death.
		yield return null;
		_isProcessingDeath = false;
	}

	// BUG FIX: replaced CaptureInitialSnapshotNextFrame (single yield return null,
	// which races with CheckpointManager.Awake) with a polling coroutine that waits
	// until CheckpointManager.Instance is ready or a timeout expires.
	private IEnumerator CaptureInitialSnapshotWhenReady()
	{
		float timeout = 3f;
		while (CheckpointManager.Instance == null && timeout > 0f)
		{
			timeout -= Time.unscaledDeltaTime;
			yield return null;
		}

		if (CheckpointManager.Instance == null)
		{
			Debug.LogWarning("[GameManager] CheckpointManager never became available — initial snapshot not captured. Deaths before any checkpoint will not restore world state.");
			yield break;
		}

		CheckpointManager.Instance.CaptureInitialSnapshot(_levelOriginalSpawnPoint);
		CheckpointManager.Instance.SaveCheckpoint(_levelOriginalSpawnPoint);
		Debug.Log($"[GameManager] Seeded spawn checkpoint at x = {_levelOriginalSpawnPoint.x}, y = {_levelOriginalSpawnPoint.y}");
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