using UnityEngine;
using TarodevController;

public class DecoupleManager : MonoBehaviour
{
    public static DecoupleManager Instance { get; private set; }

    [SerializeField] private PlayerController _p1;
    [SerializeField] private MirrorController _p2;

    private int _p1Charges = 1;

    private void Awake() => Instance = this;

    private void Update()
    {
        if (InputHandler.Instance == null) return;

        if (InputHandler.Instance.Actions.Gameplay.Decouple.WasPressedThisFrame()
            && _p1Charges > 0
            && _p1.State == PlayerState.Normal
            && _p2.State == PlayerState.Normal)
        {
            _p2.EnterFrozenState(3f);
            _p1Charges--;
        }
    }

    public void RestoreCharges() => _p1Charges = 1;
}
