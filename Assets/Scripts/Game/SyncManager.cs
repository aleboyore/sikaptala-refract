using UnityEngine;
using TarodevController;

public class SyncManager : MonoBehaviour
{
	public static SyncManager Instance { get; private set; }

	[Header("References")]
	[SerializeField] private PlayerController _p1;
	[SerializeField] private MirrorController _p2;
	[SerializeField] private SyncFeedback _feedback;
	[SerializeField] private SyncChainVFX _syncChainVFX;

	[Header("Restrictions")]
	[SerializeField] private float _proximityThreshold = 4f;

	[Header("Cooldown")]
	[SerializeField] private float _syncCooldown = 2f;
	[SerializeField] private float _syncLerpDuration = 0.35f;
	private float _lastSyncTime = -999f;
	private bool _syncInProgress;
	private int _syncArrivals;

	private void Awake() => Instance = this;

	private void Update()
	{
		if (_p1 == null || _p2 == null || _syncInProgress) return;
		if (InputHandler.Instance == null) return;
		if (!InputHandler.Instance.Actions.Gameplay.Sync.WasPressedThisFrame()) return;
		if (_p1.State != PlayerState.Normal || _p2.State != PlayerState.Normal) return;
		if (Time.time < _lastSyncTime + _syncCooldown) return;

		if (CanSync()) ExecuteSync();
		else _feedback?.PlayFailure();
	}

	private bool CanSync()
	{
		if (!_p1.IsGrounded || !_p2.IsGrounded)
			return false;

		return Mathf.Abs(_p1.transform.position.x + _p2.transform.position.x) <= _proximityThreshold;
	}

	private void ExecuteSync()
	{
		_syncInProgress = true;
		_syncArrivals = 0;
		_lastSyncTime = Time.time;

		// Midpoint in P1's coordinate space
		float p2EquivalentX = -_p2.transform.position.x;
		float midX_P1 = (_p1.transform.position.x + p2EquivalentX) / 2f;
		float midX_P2 = -midX_P1;   // convert back to P2's space

		_syncChainVFX?.PlayChain(_p1.transform, _p2.transform, _syncLerpDuration);
		_feedback?.PlaySuccess();

		_p1.LerpToX(midX_P1, _syncLerpDuration, OnSyncArrival);
		_p2.LerpToX(midX_P2, _syncLerpDuration, OnSyncArrival);
	}

	private void OnSyncArrival()
	{
		_syncArrivals++;
		if (_syncArrivals < 2) return;

		_syncChainVFX?.PlayArrivalBurst();
		_syncInProgress = false;
	}

	public bool IsSyncReady =>
		_p1 != null &&
		_p2 != null &&
		_p1.State == PlayerState.Normal &&
		_p2.State == PlayerState.Normal &&
		CanSync() &&
		Time.time >= _lastSyncTime + _syncCooldown;
}
