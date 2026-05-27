using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Car 场景启动时自动为 mercedes 应用全息材质（编辑器菜单与 FBX Remap 的补充）。
/// </summary>
public class CarSceneHologramBootstrap : MonoBehaviour
{
    [SerializeField] private string targetObjectName = "mercedes";

    private void Awake()
    {
        if (!SceneManager.GetActiveScene().name.Contains("Car"))
        {
            return;
        }

        GameObject target = GameObject.Find(targetObjectName);
        if (target == null)
        {
            Debug.LogWarning("[CarSceneHologramBootstrap] 未找到目标对象: " + targetObjectName);
            return;
        }

        MercedesHologramApplier applier = target.GetComponent<MercedesHologramApplier>();
        if (applier == null)
        {
            applier = target.AddComponent<MercedesHologramApplier>();
        }

        applier.ApplyHologramMaterial();
    }
}
