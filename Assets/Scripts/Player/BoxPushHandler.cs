using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BoxPushHandler : MonoBehaviour
{
    Collider2D playerCollider;

    void Awake()
    {
        playerCollider = GetComponent<Collider2D>();
    }

    void OnCollisionStay2D(Collision2D col)
    {
        // Find ScriptableBox component on the colliding object or its parent
        var scriptableBox = col.collider.GetComponentInParent<ScriptableBox>();
        if (scriptableBox == null) return;

        // Get player input direction
        if (InputHandler.Instance == null) return;
        var move = InputHandler.Instance.P1Input.Move;
        if (Mathf.Approximately(move.x, 0f)) return;

        Vector2 pushDir = new Vector2(Mathf.Sign(move.x), 0f);

        // Notify the box of the collision with player and push direction
        scriptableBox.OnPlayerInteract(gameObject, pushDir);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        // Notify box that player has exited
        var scriptableBox = col.collider.GetComponentInParent<ScriptableBox>();
        if (scriptableBox != null)
            scriptableBox.OnPlayerExit(gameObject);
    }
}
