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

        HashSet<ScriptableBox> boxesThisFrame = new();
        Dictionary<ScriptableBox, Vector2> resolvedDirectionByBox = new();
        Dictionary<ScriptableBox, int> scoreByBox = new();

        CastRayInDirection(Vector2.down, boxesThisFrame, resolvedDirectionByBox, scoreByBox);
        CastRayInDirection(Vector2.up, boxesThisFrame, resolvedDirectionByBox, scoreByBox);
        CastRayInDirection(Vector2.left, boxesThisFrame, resolvedDirectionByBox, scoreByBox);
        CastRayInDirection(Vector2.right, boxesThisFrame, resolvedDirectionByBox, scoreByBox);

        foreach (var kvp in resolvedDirectionByBox)
        {
            ScriptableBox box = kvp.Key;
            if (box == null) continue;

            _activeBoxContacts[box] = 1;
            box.OnPlayerInteract(gameObject, kvp.Value);
        }

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
            box.ClearEffectStateForPlayer(gameObject);

            if (!box.CanStandOnBy(skin))
            {
                box.EvictPlayer(gameObject);
            }
        }

        ScriptableBox[] allBoxes = FindObjectsByType<ScriptableBox>(FindObjectsInactive.Exclude);
        foreach (ScriptableBox box in allBoxes)
        {
            if (box == null) continue;
            box.RefreshVisibility(skin);
        }
    }

    private void CastRayInDirection(
        Vector2 direction,
        HashSet<ScriptableBox> boxesThisFrame,
        Dictionary<ScriptableBox, Vector2> resolvedDirectionByBox,
        Dictionary<ScriptableBox, int> scoreByBox)
    {
        if (_col == null)
            return;

        Vector2 origin = _col.bounds.center;
        Vector2 size = _col.bounds.size;

        // Probe only a thin slice just beyond the player's collider face in the requested direction.
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, direction, raycastPadding, boxLayer);
        
        if (hit.collider == null)
        {
            // Debug raycast misses for downward direction (jumping)
            if (direction == Vector2.down)
            {
                Debug.Log($"[BoxDetector] RAYCAST MISS dir={direction} at origin={origin} size={size} padding={raycastPadding} boxLayer={boxLayer}");
            }
            return;
        }

        ScriptableBox box = hit.collider.GetComponent<ScriptableBox>();
        if (box == null)
        {
            Debug.LogWarning($"[BoxDetector] Hit collider '{hit.collider.name}' but no ScriptableBox component found");
            return;
        }

        // Log detection details for debugging push/fall-through gating
        PlayerSkinState skin = _charState != null ? _charState.current : default;
        try
        {
            Debug.Log($"[BoxDetector] RAYCAST HIT box '{hit.collider.name}' dir={direction} skin={skin} canPush={box.CanBePushedBy(skin)} canPass={box.CanPassThroughBy(skin)} canStandOn={box.CanStandOnBy(skin)}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BoxDetector] Detection log failed: {ex.Message}");
        }

        // Track this box as active this frame and invoke interact once.
        boxesThisFrame.Add(box);

        int score = ScoreDirectionForBox(box, skin, direction);
        if (!scoreByBox.TryGetValue(box, out int currentScore) || score > currentScore)
        {
            scoreByBox[box] = score;
            resolvedDirectionByBox[box] = direction;
        }
    }

    private int ScoreDirectionForBox(ScriptableBox box, PlayerSkinState skin, Vector2 direction)
    {
        bool isTop = direction == Vector2.down;
        bool isSide = direction == Vector2.left || direction == Vector2.right;

        if (isTop)
        {
            bool canStand = box.CanStandOnBy(skin);
            bool canPass = box.CanPassThroughBy(skin);

            // Only strongly prefer top when it represents a real standing/support interaction.
            if (canStand && !canPass)
                return 300;

            return 120;
        }

        if (isSide)
        {
            bool canPush = box.CanBePushedBy(skin);
            bool canKillOnPush = box.CanKillOnPushBy(skin);

            if (canKillOnPush)
                return 280;
            if (canPush)
                return 220;

            return 180;
        }

        // Bottom/up contact is generally least useful for gameplay decisions.
        return 100;
    }
}
