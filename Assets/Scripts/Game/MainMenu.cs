using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset gameManagerScene;
#endif
    [SerializeField] private string gameManagerSceneName = "GameManager";

    //start button
    public void PlayGame()
    {
        if (string.IsNullOrWhiteSpace(gameManagerSceneName))
        {
            Debug.LogError("[MainMenu] GameManager scene name is not set.");
            return;
        }

        Debug.Log($"[MainMenu] Loading scene '{gameManagerSceneName}'...");
        SceneManager.LoadScene(gameManagerSceneName);
    }

    //Credits Button
    public void ShowCredits()
    {
        
        Debug.Log("Credits button was clicked!");
    }

    //Exit Button
    public void ExitGame()
    {
        // quit app for exe file
        Application.Quit();

#if UNITY_EDITOR
        //  Exit button also work while testing in the Editor
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (gameManagerScene != null)
        {
            gameManagerSceneName = gameManagerScene.name;
        }
#endif
    }
}