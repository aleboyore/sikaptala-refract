using UnityEngine;
using TarodevController;

public class SyncManager : MonoBehaviour
{
	public static SyncManager Instance { get; private set; }

	[Header("References")]
	[SerializeField] private PlayerController _p1;
	[SerializeField] private MirrorController _p2;

	[Header("Restrictions")]
	[SerializeField] private float _heightThreshold = 1f;
	[SerializeField] private float _proximityThreshold = 4f;

	[Header("Cooldown")]
	[SerializeField] private float _syncCooldown = 2f;
	private float _lastSyncTime = -999f;

	[Header("Feedback")]
	[SerializeField] private SyncFeedback _feedback;

	private void Awake() => Instance = this;

	private void Update()
	{
		if (InputHandler.Instance == null) return;
		if (!InputHandler.Instance.Actions.Gameplay.Sync.WasPressedThisFrame()) return;
		if (_p1.State != PlayerState.Normal || _p2.State != PlayerState.Normal) return;
		if (Time.time < _lastSyncTime + _syncCooldown) return;

		if (CanSync()) ExecuteSync();
		else _feedback?.PlayFailure();
	}

	private bool CanSync()
	{
		float deltaY = Mathf.Abs(_p1.transform.position.y - _p2.transform.position.y);
		float deltaX = Mathf.Abs(_p1.transform.position.x - _p2.transform.position.x);
		return deltaY <= _heightThreshold && deltaX <= _proximityThreshold;
	}

	private void ExecuteSync()
	{
		float midX = (_p1.transform.position.x + _p2.transform.position.x) / 2f;

		_p1.SnapToX(midX);
		_p2.SnapToX(midX);

		_lastSyncTime = Time.time;
		_feedback?.PlaySuccess();
	}

	public bool IsSyncReady =>
		_p1.State == PlayerState.Normal &&
		_p2.State == PlayerState.Normal &&
		CanSync() &&
		Time.time >= _lastSyncTime + _syncCooldown;
}
