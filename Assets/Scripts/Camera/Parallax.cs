using UnityEngine;

public class Parallax : MonoBehaviour
{
    [Tooltip("How much the background moves horizontally. 0.5 is a good default.")]
    [SerializeField] private float parallaxMultiplierX = 0.5f;

    [Tooltip("How much the background moves vertically. Keep at 0 to prevent jump flickering!")]
    [SerializeField] private float parallaxMultiplierY = 0f;

    private Transform cameraTransform;
    private Vector3 lastCameraPosition;

    void Start()
    {
        // Cinemachine moves the Main Camera, so we grab the Main Camera's transform
        cameraTransform = Camera.main.transform;
        lastCameraPosition = cameraTransform.position;
    }

    void LateUpdate()
    {
        Vector3 deltaMovement = cameraTransform.position - lastCameraPosition;
        // Calculate the raw new position
        Vector3 newPosition = transform.position + new Vector3(deltaMovement.x * parallaxMultiplierX, deltaMovement.y * parallaxMultiplierY, 0);
        transform.position = newPosition;
        lastCameraPosition = cameraTransform.position;
    }
}