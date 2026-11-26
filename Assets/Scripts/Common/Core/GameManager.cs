using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
   public void LoadSurvivalScene()
    {
        SceneManager.LoadScene("SurvivalScene");
    }
}
