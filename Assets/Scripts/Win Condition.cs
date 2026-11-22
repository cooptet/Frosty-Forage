using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class WinCondition : MonoBehaviour
{
    [SerializeField] private SquirrelPlayer squirrelPlayer;
    private int acornsFound;
    [SerializeField] private int acornsUntilWon;
    [SerializeField] private TextMeshProUGUI acornUI;
    void OnEnable()
    {
        acornUI = GameObject.Find("Acorn Counter").GetComponent<TextMeshProUGUI>();
        UpdateUI();
        
        squirrelPlayer = GetComponent<SquirrelPlayer>();
        squirrelPlayer.AcornCollected += HandleAcornCollected;
    }

    private void OnDisable()
    {
        squirrelPlayer.AcornCollected -= HandleAcornCollected;
    }

    private void UpdateUI()
    {
        int acornsLeft = acornsUntilWon - acornsFound;
        if (acornsLeft == 1)
        {
            acornUI.text = "Still missing " + acornsLeft + " acorn!";
        }
        else
        {
            acornUI.text = "Still missing " + acornsLeft + " acorns!";
        }
    }

    private void HandleAcornCollected(SquirrelAcornTarget acorn)
    {
        acornsFound += 1;
        UpdateUI();

        if (acornsFound == acornsUntilWon)
        {
            Invoke(nameof(LoadWonScene), 0.5f);
        }
    }
    
    private void LoadWonScene()
    {
        SceneManager.LoadScene(2); //Load won scene
    }
}
