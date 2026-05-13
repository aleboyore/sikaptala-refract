using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransition : MonoBehaviour
{
    [Header("Transition Settings")]
    public Animator fadeAnimator;
    public float transitionTime = 1f;

    private bool isTransitioning = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Triggers only if the object is Shard OR Veil, and prevents running twice
        if ((collision.CompareTag("Shard") || collision.CompareTag("Veil")) && !isTransitioning)
        {
            isTransitioning = true; // Lock the door
            StartCoroutine(LoadNextLevelOrMainMenu());
        }
    }

    IEnumerator LoadNextLevelOrMainMenu()
    {
        // Play the fade out animation
        if (fadeAnimator != null)
            fadeAnimator.Play("FadeToBlack");

        // Wait for the animation to finish visually
        yield return new WaitForSeconds(transitionTime);

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;

        // If there is no next scene in Build Settings, go back to Main Menu (scene 0)
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(0);
        else
            SceneManager.LoadScene(nextIndex);
    }
}