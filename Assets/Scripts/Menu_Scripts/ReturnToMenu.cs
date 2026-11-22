using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToMenu : MonoBehaviour
{
    public void Return()
    {
        SceneManager.LoadScene(0); //Load the Main Menu
    }
}
