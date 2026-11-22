using UnityEngine;

public class GameOverPlayerBehaviour : MonoBehaviour
{
    private SquirrelPlayer squirrel;
    void Awake()
    {
        squirrel = GetComponent<SquirrelPlayer>();
    }
    
    void Update()
    {
        squirrel.SetSleeping(true);
    }
}
