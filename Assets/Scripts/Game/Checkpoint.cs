using UnityEngine;

/// <summary>
/// Checkpoint trigger that saves the player respawn position when entered.
/// Place this as a 2D trigger collider in each level at logical checkpoint locations.
/// When the script is attached, a `BoxCollider2D` will be automatically added and configured as a trigger.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
	private PlayerController _player;
	private bool _hasBeenTriggered = false;

	private void Start()
	{
		var players = Resources.FindObjectsOfTypeAll<PlayerController>();
		_player = players != null && players.Length > 0 ? players[0] : null;
		
		if (_player != null)
		{
			Debug.Log($"[Checkpoint] Start: found PlayerController '{_player.gameObject.name}'");
		}
		else
		{
			Debug.LogWarning($"[Checkpoint] Start: NO PlayerController found!");
		}
		
		var col = GetComponent<Collider2D>();
		if (col != null)
		{
			col.isTrigger = true;
			Debug.Log($"[Checkpoint] Start: collider found (isTrigger={col.isTrigger}) on '{gameObject.name}'");
		}
		else
		{
			Debug.LogWarning($"[Checkpoint] Start: no Collider2D found on '{gameObject.name}' - checkpoint won't trigger");
		}
	}

	/// <summary>
	/// Called when the component is first added or Reset in the Inspector.
	/// Ensure a 2D collider exists and is configured as a trigger.
	/// </summary>
	private void Reset()
	{
		// Ensure a BoxCollider2D exists (RequireComponent will add one when script is attached,
		// but Reset provides extra safety and configuration when edited in Inspector)
		var col = GetComponent<Collider2D>();
		if (col == null)
		{
			col = gameObject.AddComponent<BoxCollider2D>();
		}
		col.isTrigger = true;
	}

	private void OnValidate()
	{
		// Keep collider set as trigger when properties change in the editor
		var col = GetComponent<Collider2D>();
		if (col != null)
		{
			if (!col.isTrigger)
			{
				col.isTrigger = true;
				Debug.Log($"[Checkpoint] OnValidate: set isTrigger=true on '{gameObject.name}'");
			}
		}
	}

	private void OnTriggerEnter2D(Collider2D collision)
	{
		Debug.Log($"[Checkpoint] OnTriggerEnter2D called with '{collision.gameObject.name}' (tag='{collision.tag}')");
		
		if (!collision.CompareTag("Player"))
		{
			Debug.Log($"[Checkpoint] Collision object '{collision.gameObject.name}' does not have 'Player' tag (has '{collision.tag}')");
			return;
		}
		
		if (_hasBeenTriggered)
		{
			Debug.Log($"[Checkpoint] Already triggered, ignoring this collision");
			return;
		}
		
		_hasBeenTriggered = true;
		var checkpointPos = transform.position;
		CheckpointManager.Instance.SaveCheckpoint(checkpointPos);
		Debug.Log($"[Checkpoint] Player reached checkpoint at x = {checkpointPos.x}, y = {checkpointPos.y}");
	}

	/// <summary>
	/// Reset the trigger so it can be activated again (useful for testing/level editors).
	/// Normally checkpoints are one-way, but this allows re-triggering if needed.
	/// </summary>
	public void ResetTrigger()
	{
		_hasBeenTriggered = false;
	}
}
