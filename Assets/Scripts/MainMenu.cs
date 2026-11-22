using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    void Start()
    {
        
    }

    public void StartGame()
    {
        SceneManager.LoadScene(1); //load main scene
    }
    public void ExitGame()
    {
        Application.Quit();
    }

    public void OpenInstructions()
    {
        SceneManager.LoadScene(4); //load instruction scene
    }
    
}
