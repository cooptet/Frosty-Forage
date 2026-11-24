using UnityEngine;
using UnityEngine.SceneManagement;

public class LoseCondition : MonoBehaviour
{
    [SerializeField] private GameObject squirrel;
    [SerializeField] private GameObject sleepParticles;
    [SerializeField] private GameObject gameOverPopUpText;
    public void EndGame()
    {
        squirrel.GetComponent<SquirrelPlayer>().SetSleeping(true);
        sleepParticles.SetActive(true);
        gameOverPopUpText.SetActive(true);
        
        Invoke(nameof(LoadGameOver), 3);
    }

    public void LoadGameOver()
    {
        SceneManager.LoadScene(3); //Load Game over scene
    }
}
