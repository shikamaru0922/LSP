using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private string gameplaySceneName = "SampleScene";

    public void StartGame()
    {
        Debug.Log("StartGame");
        if (!string.IsNullOrEmpty(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
