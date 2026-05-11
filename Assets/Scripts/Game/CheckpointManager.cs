using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
	public static CheckpointManager Instance { get; private set; }
	public Vector3 P1Position { get; private set; }
	public Vector3 P2Position { get; private set; }

	private void Awake() => Instance = this;

	public void SaveCheckpoint(Vector3 p1Pos, Vector3 p2Pos)
	{
		P1Position = p1Pos;
		P2Position = p2Pos;
		DecoupleManager.Instance?.RestoreCharges();
	}
}
