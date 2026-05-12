using UnityEngine;

/// <summary>
/// Marks the original player spawn location for a level.
/// Place one of these in each level where the player should start.
/// GameManager uses this to restore the player if they die before reaching a checkpoint.
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
	private void OnDrawGizmos()
	{
		// Draw a green sphere in the editor to show spawn point location
		Gizmos.color = Color.green;
		Gizmos.DrawSphere(transform.position, 0.5f);
	}
}
