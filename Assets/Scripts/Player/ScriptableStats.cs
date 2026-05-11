using UnityEngine;

[CreateAssetMenu(menuName = "Refract/ScriptableStats")]
public class ScriptableStats : ScriptableObject
{
    public float MaxSpeed = 14f;
    public float Acceleration = 120f;
    public float GroundDeceleration = 60f;
    public float AirDeceleration = 30f;
    public float GroundingForce = -1.5f;
    public float GrounderDistance = 0.05f;
    public float JumpPower = 36f;
    public float MaxFallSpeed = 40f;
    public float FallAcceleration = 110f;
    public float JumpEndEarlyGravityModifier = 3f;
    public float CoyoteTime = 0.15f;
    public float JumpBuffer = 0.2f;
    public bool SnapInput = true;
    public float HorizontalDeadZoneThreshold = 0.2f;
    public float VerticalDeadZoneThreshold = 0.2f;

    public LayerMask GroundLayer = ~0;
    public LayerMask PlayerLayer;
}
