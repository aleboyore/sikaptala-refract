using UnityEngine;
using TarodevController;

public class DecoupleManager : MonoBehaviour
{
    public static DecoupleManager Instance { get; private set; }

    [SerializeField] private PlayerController _p1;
    [SerializeField] private MirrorController _p2;

    [SerializeField] private int _maxCharges = 1;

    private int _p1DecoupleCharges;
    private int _p2DecoupleCharges;

    private void Awake()
    {
        Instance = this;
        RestoreCharges();
    }

    private void Update()
    {
        if (InputHandler.Instance == null || _p1 == null || _p2 == null) return;

        var gameplay = InputHandler.Instance.Actions.Gameplay;

        if (gameplay.Decouple.WasPressedThisFrame())
        {
            TryFreeze(_p2, _p1, ref _p1DecoupleCharges);
        }

        if (gameplay.DecoupleAlt.WasPressedThisFrame())
        {
            TryFreeze(_p1, _p2, ref _p2DecoupleCharges);
        }
    }

    private void TryFreeze(PlayerController freezeTarget, PlayerController activePlayer, ref int charges)
    {
        if (freezeTarget.State != PlayerState.Normal) return;
        if (activePlayer.State != PlayerState.Normal) return;
        if (charges <= 0) return;

        freezeTarget.EnterFrozenState(5f);
        charges--;
    }

    public void RestoreCharges()
    {
        _p1DecoupleCharges = _maxCharges;
        _p2DecoupleCharges = _maxCharges;
    }
}
