using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    //start button
    public void PlayGame()
    {
        // This loads the next scene in your Build list (Level 1)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    //Credits Button
    public void ShowCredits()
    {
        
        Debug.Log("Credits button was clicked!");
    }

    //Exit Button
    public void QuitGame()
    {
        // Note: Application.Quit() only works in the fully built game. 
        // We add Debug.Log so you can see it working while testing inside the Unity Editor.
        Debug.Log("Game is Exiting...");
        Application.Quit();
    }
}