using UnityEngine;
using UnityEngine.SceneManagement;

public class sceneswapper : MonoBehaviour
{
    public string nextScene;

    void OnEnable()
    {
        SceneManager.LoadScene(nextScene);
    }
}
