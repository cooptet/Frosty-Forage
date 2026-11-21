using UnityEngine;
using UnityEngine.SceneManagement;

public class LoseCondition : MonoBehaviour
{
    public void EndGame()
    {
        //TODO: player should stop and do sleeping animation,
        //TODO: time delay, only load scene after some time has passed to see sleeping player
        
        SceneManager.LoadScene(3); //Load Game over scene
    }
}
