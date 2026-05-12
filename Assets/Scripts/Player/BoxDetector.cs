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
    [SerializeField] private float raycastPadding = 0.08f;
    [SerializeField] private LayerMask boxLayer;

    private Rigidbody2D _rb;
    private CharacterState _charState;
    private CapsuleCollider2D _col;

    // Track active box contacts for exit detection
    private Dictionary<ScriptableBox, int> _activeBoxContacts = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<CapsuleCollider2D>();
        _charState = GetComponent<CharacterState>();
        if (boxLayer == default)
            boxLayer = LayerMask.GetMask("Shared");
    }

    private void OnEnable()
    {
        if (_charState != null)
            _charState.onStateChanged.AddListener(OnPlayerStateChanged);
    }

    private void OnDisable()
    {
        if (_charState != null)
            _charState.onStateChanged.RemoveListener(OnPlayerStateChanged);
    }

    private void FixedUpdate()
    {
        CastRaysAndDetectBoxes();
    }

    private void CastRaysAndDetectBoxes()
    {
        if (_activeBoxContacts == null)
            _activeBoxContacts = new Dictionary<ScriptableBox, int>();

        // Create a set to track boxes found this frame
        HashSet<ScriptableBox> boxesThisFrame = new();

        // Cast in 4 directions
        CastRayInDirection(Vector2.left, boxesThisFrame);
        CastRayInDirection(Vector2.right, boxesThisFrame);
        CastRayInDirection(Vector2.up, boxesThisFrame);
        CastRayInDirection(Vector2.down, boxesThisFrame);

        // Check for boxes that are no longer in contact
        List<ScriptableBox> boxesToRemove = new();
        List<ScriptableBox> activeBoxes = new(_activeBoxContacts.Keys);
        foreach (var box in activeBoxes)
        {
            if (box == null)
            {
                boxesToRemove.Add(box);
                continue;
            }

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

    private void OnPlayerStateChanged(PlayerSkinState skin)
    {
        if (_activeBoxContacts == null)
            _activeBoxContacts = new Dictionary<ScriptableBox, int>();

        List<ScriptableBox> activeBoxes = new(_activeBoxContacts.Keys);
        foreach (var box in activeBoxes)
        {
            if (box == null) continue;
            box.RefreshCollisionIgnore(gameObject, skin);

            if (!box.CanStandOnBy(skin) || box.CanFallThroughBy(skin))
            {
                box.EvictPlayer(gameObject);
            }
        }
    }

    private void CastRayInDirection(Vector2 direction, HashSet<ScriptableBox> boxesThisFrame)
    {
        Vector2 origin = _col != null ? _col.bounds.center : _rb.position;
        Vector2 extents = _col != null ? _col.bounds.extents : Vector2.one * 0.5f;
        float raycastDistance = direction.x != 0f ? extents.x + raycastPadding : extents.y + raycastPadding;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, raycastDistance, boxLayer);

        if (hit.collider == null)
            return;

        ScriptableBox box = hit.collider.GetComponent<ScriptableBox>();
        if (box == null)
            return;

        // Track this box as active this frame
        boxesThisFrame.Add(box);
        
        // Add/update to active contacts
        _activeBoxContacts[box] = 1;

        // Call OnPlayerInteract with the raycast direction
        box.OnPlayerInteract(gameObject, direction);
    }
}
