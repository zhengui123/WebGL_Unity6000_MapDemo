using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeSceneManager : MonoBehaviour
{

    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ChangeScene("EarthModelDemo");
        }           
        else if (Input.GetKeyDown(KeyCode.E))
        {
            ChangeScene("CityScenes");
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            ChangeScene("Car");
        }
    }

    public　void ChangeScene(string sceneName)
    {
        Debug.Log("ChangeScene: " + sceneName);
        SceneManager.LoadSceneAsync(sceneName);
    }
}
