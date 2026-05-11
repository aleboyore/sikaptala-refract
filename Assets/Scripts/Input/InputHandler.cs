using UnityEngine;
using UnityEngine.InputSystem;

public struct FrameInput
{
    public Vector2 Move;
    public bool JumpDown;
    public bool JumpHeld;
}

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    public FrameInput P1Input { get; private set; }

    public PlayerInputActions Actions { get; private set; }

    private void Awake()
    {
        Instance = this;
        Actions = new PlayerInputActions();
        Actions.Gameplay.Enable();
    }

    private void OnDestroy()
    {
        Actions.Gameplay.Disable();
        Actions.Dispose();
    }

    private void Update()
    {
        if (Actions == null) return;

        var raw = Actions.Gameplay.Move.ReadValue<Vector2>();
        bool jumpDown = Actions.Gameplay.Jump.WasPressedThisFrame();
        bool jumpHeld = Actions.Gameplay.Jump.IsPressed();

        P1Input = new FrameInput
        {
            Move = raw,
            JumpDown = jumpDown,
            JumpHeld = jumpHeld
        };
    }
}
