using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeSceneManager : UnitySingle<ChangeSceneManager>
{

    void Start()
    {
        
    }

    public　void ChangeScene(string sceneName)
    {
        Debug.Log("ChangeScene: " + sceneName);
        SceneManager.LoadSceneAsync(sceneName);
    }
}
