using UnityEngine;

/// <summary>
/// 按世界包围盒与相机 FOV，估算使内容占视口指定比例时的观察距离 / 相机局部 Y。
/// 约定：局部 Y 越大越远（与 CameraController Min/MaxZoomY 一致）。
/// </summary>
public static class PlateMapCameraFitUtility
{
    /// <summary>
    /// 计算使 bounds 落入视口 fillRatio 所需的沿视线观察距离（越大越远）。
    /// 取子物体外包围盒 XZ 最长边，匹配同一通用屏幕占比区域。
    /// </summary>
    public static float ComputeViewDistanceToFitBounds(
        Camera camera,
        Bounds worldBounds,
        float viewportFillRatio)
    {
        if (camera == null)
        {
            return 0f;
        }

        float fill = Mathf.Clamp(viewportFillRatio, 0.05f, 3f);
        float halfFovV = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * Mathf.Max(0.01f, camera.aspect));
        // 用更紧的 FOV，让最长边落入统一可视区域
        float tanMin = Mathf.Tan(Mathf.Min(halfFovV, halfFovH));

        float halfLongest = 0.5f * Mathf.Max(worldBounds.size.x, worldBounds.size.z);
        halfLongest = Mathf.Max(halfLongest, 0.01f);

        return Mathf.Max(halfLongest / Mathf.Max(tanMin * fill, 1e-4f), 1f);
    }

    /// <summary>
    /// 将观察距离映射为相机局部 Y，并钳制到范围。
    /// </summary>
    public static float ComputeCameraLocalYToFitBounds(
        Camera camera,
        Bounds worldBounds,
        float viewportFillRatio,
        float minLocalY,
        float maxLocalY,
        float distanceToLocalYScale = 1f)
    {
        float distance = ComputeViewDistanceToFitBounds(camera, worldBounds, viewportFillRatio);
        float localY = distance * Mathf.Max(0.01f, distanceToLocalYScale);
        return Mathf.Clamp(localY, minLocalY, maxLocalY);
    }

    /// <summary>
    /// 判断在给定相机世界位姿下，包围盒 8 角是否都落在视口内（带 fill 边距）。
    /// </summary>
    public static bool AreBoundsInsideViewport(
        Camera camera,
        Bounds worldBounds,
        float viewportFillRatio,
        Vector3 cameraWorldPos,
        Quaternion cameraWorldRot)
    {
        if (camera == null)
        {
            return false;
        }

        float fill = Mathf.Clamp(viewportFillRatio, 0.05f, 3f);
        float margin = (1f - fill) * 0.5f;
        float minUV = margin;
        float maxUV = 1f - margin;

        Vector3 forward = cameraWorldRot * Vector3.forward;
        Vector3 right = cameraWorldRot * Vector3.right;
        Vector3 up = cameraWorldRot * Vector3.up;
        if (forward.sqrMagnitude < 1e-8f)
        {
            return false;
        }

        forward.Normalize();
        right.Normalize();
        up.Normalize();

        float halfFovV = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float tanV = Mathf.Tan(halfFovV);
        float tanH = tanV * Mathf.Max(0.01f, camera.aspect);

        Vector3 ext = worldBounds.extents;
        Vector3 cen = worldBounds.center;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = cen + new Vector3(
                (i & 1) == 0 ? -ext.x : ext.x,
                (i & 2) == 0 ? -ext.y : ext.y,
                (i & 4) == 0 ? -ext.z : ext.z);

            Vector3 toCorner = corner - cameraWorldPos;
            float z = Vector3.Dot(toCorner, forward);
            if (z <= 0.01f)
            {
                return false;
            }

            float x = Vector3.Dot(toCorner, right);
            float y = Vector3.Dot(toCorner, up);
            float halfH = tanH * z;
            float halfVScreen = tanV * z;
            float u = 0.5f + 0.5f * (x / Mathf.Max(halfH, 1e-4f));
            float v = 0.5f + 0.5f * (y / Mathf.Max(halfVScreen, 1e-4f));

            if (u < minUV || u > maxUV || v < minUV || v > maxUV)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 在「相机朝向模块中心、沿视线拉开 viewDistance」的前提下，
    /// 用二分把局部 Y / 视距抬到能装下整省（解决仅用 FOV 估算仍过近的问题）。
    /// </summary>
    public static float ComputeCameraLocalYVerified(
        Camera camera,
        Transform cameraRig,
        Transform cameraTransform,
        Bounds worldBounds,
        float viewportFillRatio,
        float minLocalY,
        float maxLocalY,
        float distanceToLocalYScale,
        out float viewDistanceAlongForward)
    {
        viewDistanceAlongForward = ComputeViewDistanceToFitBounds(camera, worldBounds, viewportFillRatio);
        float baseLocalY = Mathf.Clamp(
            viewDistanceAlongForward * Mathf.Max(0.01f, distanceToLocalYScale),
            minLocalY,
            maxLocalY);

        if (camera == null || cameraRig == null || cameraTransform == null)
        {
            return baseLocalY;
        }

        Quaternion camWorldRot = cameraRig.rotation * cameraTransform.localRotation;
        Vector3 forward = camWorldRot * Vector3.forward;
        if (forward.sqrMagnitude < 1e-8f)
        {
            return baseLocalY;
        }

        forward.Normalize();
        Vector3 moduleCenter = worldBounds.center;

        // depth 与 localY 联动抬升：过近则加大视距，直到 8 角进视口或碰到上限
        float depth = Mathf.Max(viewDistanceAlongForward, baseLocalY);
        float localY = baseLocalY;
        const int maxIter = 12;
        for (int iter = 0; iter < maxIter; iter++)
        {
            Vector3 camWorld = moduleCenter - forward * depth;
            if (AreBoundsInsideViewport(camera, worldBounds, viewportFillRatio, camWorld, camWorldRot))
            {
                viewDistanceAlongForward = depth;
                return Mathf.Clamp(localY, minLocalY, maxLocalY);
            }

            depth *= 1.2f;
            localY = Mathf.Min(maxLocalY, Mathf.Max(localY * 1.2f, depth * distanceToLocalYScale));
            if (localY >= maxLocalY - 0.1f && depth >= maxLocalY)
            {
                break;
            }
        }

        viewDistanceAlongForward = Mathf.Min(depth, maxLocalY * 2f);
        return maxLocalY;
    }

    /// <summary>合并 root 及子级全部 Renderer 的世界包围盒。</summary>
    public static bool TryGetRenderersWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled)
            {
                continue;
            }

            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return has;
    }

    /// <summary>合并多个模块的世界包围盒。</summary>
    public static bool TryGetModulesWorldBounds(PlateMapDisplayModule[] modules, out Bounds bounds)
    {
        bounds = new Bounds();
        if (modules == null || modules.Length == 0)
        {
            return false;
        }

        bool has = false;
        for (int i = 0; i < modules.Length; i++)
        {
            PlateMapDisplayModule module = modules[i];
            if (module == null)
            {
                continue;
            }

            Bounds b = module.GetWorldBounds();
            if (!has)
            {
                bounds = b;
                has = true;
            }
            else
            {
                bounds.Encapsulate(b);
            }
        }

        return has;
    }
}
