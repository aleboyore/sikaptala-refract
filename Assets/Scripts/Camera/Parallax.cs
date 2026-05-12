using UnityEngine;

public class Parallax : MonoBehaviour
{
    [Tooltip("How much the background moves horizontally. 0.5 is a good default.")]
    [SerializeField] private float parallaxMultiplierX = 0.5f;

    [Tooltip("How much the background moves vertically. Keep at 0 to prevent jump flickering!")]
    [SerializeField] private float parallaxMultiplierY = 0f;

    [Header("Smoothing")]
    [Tooltip("Damping for parallax velocity (0 = instant, 0.15 = smooth). Higher = more damping.")]
    [SerializeField] private float velocityDamping = 0.15f;

    private Transform cameraTransform;
    private Vector3 lastCameraPosition;
    private Vector3 parallaxVelocity = Vector3.zero;

    void Start()
    {
        // Cinemachine moves the Main Camera, so we grab the Main Camera's transform
        cameraTransform = Camera.main.transform;
        lastCameraPosition = cameraTransform.position;
    }

    void FixedUpdate()
    {
        Vector3 rawDeltaMovement = cameraTransform.position - lastCameraPosition;
        
        // Apply damping to velocity for smooth parallax (reduces jitter on direction changes)
        parallaxVelocity = Vector3.Lerp(parallaxVelocity, rawDeltaMovement, velocityDamping);
        
        transform.position += new Vector3(parallaxVelocity.x * parallaxMultiplierX, parallaxVelocity.y * parallaxMultiplierY, 0);
        lastCameraPosition = cameraTransform.position;
    }
}