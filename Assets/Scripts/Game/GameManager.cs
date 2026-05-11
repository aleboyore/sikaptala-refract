using UnityEngine;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	[SerializeField] private PlayerController _player;

	private void Awake() => Instance = this;

	public void NotifyLockIn(PlayerController player)
	{
		if (_player == null) return;
		if (_player.State == PlayerState.LockedIn)
			LoadNextLevel();
	}

	public void TriggerDeath()
	{
		if (_player == null) return;
		if (CheckpointManager.Instance == null) return;
		_player.ResetToCheckpoint(CheckpointManager.Instance.PlayerPosition);
	}

	private void LoadNextLevel()
	{
		// TODO: implement scene loading
	}
}
