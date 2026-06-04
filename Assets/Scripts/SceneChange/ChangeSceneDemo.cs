using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 键盘切换场景（WebGL 需防重复实例与并发加载）。
/// </summary>
public class ChangeSceneDemo : MonoBehaviour
{
    private static ChangeSceneDemo _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ChangeSceneManager.Instance.ChangeScene("EarthModelDemo");
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            ChangeSceneManager.Instance.ChangeScene("3DTilesDemo");
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            ChangeSceneManager.Instance.ChangeScene("CityScenes");
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            ChangeSceneManager.Instance.ChangeScene("Car");
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            ChangeSceneManager.Instance.ChangeScene("MyTest");
        }
    }

    
}
