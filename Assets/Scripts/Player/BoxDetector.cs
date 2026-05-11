using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Detects boxes around the player using raycasts in 4 directions.
/// Replaces collision-based BoxPushHandler with predictable raycast detection.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BoxDetector : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private float raycastDistance = 0.6f;
    [SerializeField] private LayerMask boxLayer = LayerMask.GetMask("Shared");

    private Rigidbody2D _rb;
    private CharacterState _charState;

    // Track active box contacts for exit detection
    private Dictionary<ScriptableBox, int> _activeBoxContacts = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _charState = GetComponent<CharacterState>();
    }

    private void FixedUpdate()
    {
        CastRaysAndDetectBoxes();
    }

    private void CastRaysAndDetectBoxes()
    {
        // Create a set to track boxes found this frame
        HashSet<ScriptableBox> boxesThisFrame = new();

        // Cast in 4 directions
        CastRayInDirection(Vector2.left, boxesThisFrame);
        CastRayInDirection(Vector2.right, boxesThisFrame);
        CastRayInDirection(Vector2.up, boxesThisFrame);
        CastRayInDirection(Vector2.down, boxesThisFrame);

        // Check for boxes that are no longer in contact
        List<ScriptableBox> boxesToRemove = new();
        foreach (var box in _activeBoxContacts.Keys)
        {
            if (!boxesThisFrame.Contains(box))
            {
                box.OnPlayerExit(gameObject);
                boxesToRemove.Add(box);
            }
        }

        foreach (var box in boxesToRemove)
        {
            _activeBoxContacts.Remove(box);
        }
    }

    private void CastRayInDirection(Vector2 direction, HashSet<ScriptableBox> boxesThisFrame)
    {
        RaycastHit2D hit = Physics2D.Raycast(_rb.position, direction, raycastDistance, boxLayer);

        if (hit.collider == null)
            return;

        ScriptableBox box = hit.collider.GetComponent<ScriptableBox>();
        if (box == null)
            return;

        // Track this box as active this frame
        boxesThisFrame.Add(box);

        // Determine if this is a new contact or continuing contact
        bool isNewContact = !_activeBoxContacts.ContainsKey(box);
        
        // Add/update to active contacts
        _activeBoxContacts[box] = 1;

        // Call OnPlayerInteract with the raycast direction
        box.OnPlayerInteract(gameObject, direction);
    }
}
