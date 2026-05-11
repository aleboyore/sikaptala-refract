using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
	public static CheckpointManager Instance { get; private set; }
	public Vector3 PlayerPosition { get; private set; }

	private void Awake() => Instance = this;

	public void SaveCheckpoint(Vector3 playerPos)
	{
		PlayerPosition = playerPos;
	}
}
