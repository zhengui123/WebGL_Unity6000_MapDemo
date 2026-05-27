using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Car 场景启动时仅为 mercedes（车身层）应用全息材质；边缘线使用场景内 mercedes_edge 副本。
/// </summary>
public class CarSceneHologramBootstrap : MonoBehaviour
{
    [SerializeField] private string bodyObjectName = "mercedes";

    private void Awake()
    {
        if (!SceneManager.GetActiveScene().name.Contains("Car"))
        {
            return;
        }

        GameObject bodyRoot = GameObject.Find(bodyObjectName);
        if (bodyRoot == null)
        {
            Debug.LogWarning("[CarSceneHologramBootstrap] 未找到车身对象: " + bodyObjectName);
            return;
        }

        MercedesHologramApplier applier = bodyRoot.GetComponent<MercedesHologramApplier>();
        if (applier == null)
        {
            applier = bodyRoot.AddComponent<MercedesHologramApplier>();
        }

        applier.ApplyHologramMaterial();
    }
}
