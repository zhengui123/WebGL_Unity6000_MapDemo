using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeSceneManager : UnitySingle<ChangeSceneManager>
{
    private static bool _isLoading;

    void Start()
    {
        
    }

/// <summary>
    /// 异步加载场景；加载中、与当前场景同名时忽略。
    /// </summary>
    public void ChangeScene(string sceneName)
    {
        if (_isLoading)
        {
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[ChangeSceneDemo] 场景名为空。");
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.name == sceneName)
        {
            Debug.Log($"[ChangeSceneDemo] 已在场景 {sceneName}，跳过加载。");
            return;
        }

        _isLoading = true;
        Debug.Log("[ChangeSceneDemo] ChangeScene: " + sceneName);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            _isLoading = false;
            Debug.LogError("[ChangeSceneDemo] LoadSceneAsync 失败，请检查 Build Settings: " + sceneName);
            return;
        }

        op.completed += _ => { _isLoading = false; };
    }
}
