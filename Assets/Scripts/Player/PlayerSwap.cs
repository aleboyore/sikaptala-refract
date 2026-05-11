using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSwap : MonoBehaviour
{
    private CharacterState state;
    public float swapCooldown = 0.2f;
    private float lastSwap;

    void Awake()
    {
        state = GetComponent<CharacterState>();
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.qKey.wasPressedThisFrame) return;
        TrySwap();
    }

    // Wire this to your Input Action (e.g. "Swap" action, default: Q or LB)
    public void OnSwap(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        TrySwap();
    }

    void TrySwap()
    {
        if (Time.time - lastSwap < swapCooldown) return;
        lastSwap = Time.time;
        state?.SwapState();
    }
}
