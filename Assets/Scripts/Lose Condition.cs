using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoseCondition : MonoBehaviour
{
    [SerializeField] private GameObject squirrel;
    public void EndGame()
    {
        squirrel.GetComponent<SquirrelPlayer>().SetSleeping(true);
         Invoke(nameof(LoadGameOver), 3);
    }

    public void LoadGameOver()
    {
        SceneManager.LoadScene(3); //Load Game over scene
    }
}
