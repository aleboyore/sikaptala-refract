using UnityEngine;
using TarodevController;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	[SerializeField] private PlayerController _p1;
	[SerializeField] private MirrorController _p2;

	private void Awake() => Instance = this;

	public void NotifyLockIn(PlayerController player)
	{
		if (_p1 == null || _p2 == null) return;
		if (_p1.State == PlayerState.LockedIn && _p2.State == PlayerState.LockedIn)
			LoadNextLevel();
	}

	public void TriggerDeath()
	{
		if (_p1 == null || _p2 == null) return;
		if (CheckpointManager.Instance == null) return;
		_p1.ResetToCheckpoint(CheckpointManager.Instance.P1Position);
		_p2.ResetToCheckpoint(CheckpointManager.Instance.P2Position);
		DecoupleManager.Instance?.RestoreCharges();
	}

	private void LoadNextLevel()
	{
		// TODO: implement scene loading
	}
}
