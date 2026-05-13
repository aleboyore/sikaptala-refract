using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject creditsPanel;

    private void Awake()
    {
        // ensure it's hidden when the scene starts
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    public void ShowCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }

    public void HideCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }
}