using UnityEngine;

/// <summary>
/// 将 City-Maker 世界包围盒中心对齐到相机视口中央（俯视帧）。
/// </summary>
public static class CityMakerScreenFramer
{
    /// <summary>
    /// 移动相机架并使子相机保持俯视高度，让城市中心落在屏幕中央。
    /// </summary>
    public static bool TryFrameAtScreenCenter(
        Transform cityRoot,
        Camera camera,
        Transform cameraRig,
        Transform cameraTransform,
        float cameraLocalHeight)
    {
        if (cityRoot == null || camera == null || cameraRig == null || cameraTransform == null)
        {
            return false;
        }

        if (!TryGetWorldBounds(cityRoot, out Bounds bounds))
        {
            return false;
        }

        Vector3 center = bounds.center;
        Vector3 rigPos = cameraRig.position;
        rigPos.x = center.x;
        rigPos.z = center.z;
        cameraRig.position = rigPos;

        Vector3 localPos = cameraTransform.localPosition;
        localPos.y = cameraLocalHeight;
        cameraTransform.localPosition = localPos;

        // 保持俯视，由 CameraPivot 的 -90° X 决定；子相机本地旋转归零
        cameraTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        return true;
    }

    public static bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = r.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return initialized;
    }
}
