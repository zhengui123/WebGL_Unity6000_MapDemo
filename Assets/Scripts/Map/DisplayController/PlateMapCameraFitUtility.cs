using UnityEngine;

/// <summary>
/// 按世界包围盒与相机 FOV，估算使内容占视口指定比例时的观察距离 / 相机局部 Y。
/// 约定：局部 Y 越大越远（与 CameraController Min/MaxZoomY 一致）。
/// </summary>
public static class PlateMapCameraFitUtility
{
    /// <summary>
    /// 计算观察距离：使 XZ 外包围最长边与 fill 视口框在<strong>水平地图面</strong>上的对应边重合。
    /// （绿框是视锥与水平面交线，不能用「垂直于视线截面」的 tan 公式。）
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

        return ComputeViewDistanceToFitBoundsOnMapPlane(
            camera,
            worldBounds,
            worldBounds.center,
            camera.transform.rotation,
            viewportFillRatio);
    }

    /// <summary>
    /// 在指定瞄准点与相机朝向下，按水平面绿框反算视距：
    /// 红框 AABB 在绿框坐标系下投影更长的一边，与绿框对应边重合。
    /// </summary>
    public static float ComputeViewDistanceToFitBoundsOnMapPlane(
        Camera camera,
        Bounds worldBounds,
        Vector3 lookAtWorld,
        Quaternion cameraWorldRot,
        float viewportFillRatio)
    {
        if (camera == null)
        {
            return 0f;
        }

        Vector3 forward = cameraWorldRot * Vector3.forward;
        if (forward.sqrMagnitude < 1e-8f)
        {
            return 1f;
        }

        forward.Normalize();

        float sizeX = Mathf.Max(worldBounds.size.x, 0.01f);
        float sizeZ = Mathf.Max(worldBounds.size.z, 0.01f);
        float halfX = 0.5f * sizeX;
        float halfZ = 0.5f * sizeZ;

        float fill = Mathf.Clamp(viewportFillRatio, 0.05f, 3f);
        float halfFovV = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float tanV = Mathf.Tan(halfFovV);
        float tanH = tanV * Mathf.Max(0.01f, camera.aspect);

        // 种子距离（用 XZ 较长边）；再在水平面按「绿框坐标系下红框最长投影」迭代
        float seedLongest = Mathf.Max(sizeX, sizeZ);
        float distance = Mathf.Max(
            0.5f * seedLongest / Mathf.Max(Mathf.Min(tanH, tanV) * fill, 1e-4f),
            10f);

        const int refinePasses = 4;
        for (int pass = 0; pass < refinePasses; pass++)
        {
            Vector3 camPos = lookAtWorld - forward * distance;
            if (!TryGetViewportFillRectOnPlaneYAtPose(
                    camPos,
                    cameraWorldRot,
                    camera.fieldOfView,
                    camera.aspect,
                    lookAtWorld.y,
                    fill,
                    out Vector3 bl,
                    out Vector3 br,
                    out Vector3 tr,
                    out Vector3 tl))
            {
                break;
            }

            // 梯形绿框：对边中点距
            float greenW = Vector3.Distance((bl + tl) * 0.5f, (br + tr) * 0.5f);
            float greenH = Vector3.Distance((bl + br) * 0.5f, (tl + tr) * 0.5f);
            if (greenW < 1e-3f || greenH < 1e-3f)
            {
                break;
            }

            Vector3 greenRight = ((br + tr) * 0.5f - (bl + tl) * 0.5f) / greenW;
            Vector3 greenUp = ((tl + tr) * 0.5f - (bl + br) * 0.5f) / greenH;
            // 水平面内单位化，去掉数值噪声
            greenRight.y = 0f;
            greenUp.y = 0f;
            if (greenRight.sqrMagnitude < 1e-8f || greenUp.sqrMagnitude < 1e-8f)
            {
                break;
            }

            greenRight.Normalize();
            greenUp.Normalize();

            // 红框 AABB 在绿框宽/高向上的投影跨度（外包围在视口坐标系下的边长）
            float redAlongW = 2f * (Mathf.Abs(greenRight.x) * halfX + Mathf.Abs(greenRight.z) * halfZ);
            float redAlongH = 2f * (Mathf.Abs(greenUp.x) * halfX + Mathf.Abs(greenUp.z) * halfZ);
            redAlongW = Mathf.Max(redAlongW, 0.01f);
            redAlongH = Mathf.Max(redAlongH, 0.01f);

            // 取红框在视口系下更长的那条，对齐绿框对应边
            float scale;
            if (redAlongW >= redAlongH)
            {
                scale = redAlongW / Mathf.Max(greenW, 1e-4f);
            }
            else
            {
                scale = redAlongH / Mathf.Max(greenH, 1e-4f);
            }

            distance *= scale;
            if (Mathf.Abs(scale - 1f) < 0.005f)
            {
                break;
            }
        }

        return Mathf.Max(distance, 1f);
    }

    /// <summary>
    /// 虚拟机位下，将居中 fill 视口矩形四角射线打到水平面 Y。
    /// </summary>
    public static bool TryGetViewportFillRectOnPlaneYAtPose(
        Vector3 cameraWorldPos,
        Quaternion cameraWorldRot,
        float fieldOfViewDegrees,
        float aspect,
        float planeY,
        float viewportFillRatio,
        out Vector3 bottomLeft,
        out Vector3 bottomRight,
        out Vector3 topRight,
        out Vector3 topLeft)
    {
        bottomLeft = bottomRight = topRight = topLeft = default;
        float fill = Mathf.Clamp(viewportFillRatio, 0.05f, 3f);
        float margin = (1f - fill) * 0.5f;
        float u0 = margin;
        float u1 = 1f - margin;
        float v0 = margin;
        float v1 = 1f - margin;

        return TryViewportRayToPlaneY(cameraWorldPos, cameraWorldRot, fieldOfViewDegrees, aspect, u0, v0, planeY, out bottomLeft) &&
               TryViewportRayToPlaneY(cameraWorldPos, cameraWorldRot, fieldOfViewDegrees, aspect, u1, v0, planeY, out bottomRight) &&
               TryViewportRayToPlaneY(cameraWorldPos, cameraWorldRot, fieldOfViewDegrees, aspect, u1, v1, planeY, out topRight) &&
               TryViewportRayToPlaneY(cameraWorldPos, cameraWorldRot, fieldOfViewDegrees, aspect, u0, v1, planeY, out topLeft);
    }

    private static bool TryViewportRayToPlaneY(
        Vector3 cameraWorldPos,
        Quaternion cameraWorldRot,
        float fieldOfViewDegrees,
        float aspect,
        float viewportX,
        float viewportY,
        float planeY,
        out Vector3 point)
    {
        float halfFovV = fieldOfViewDegrees * 0.5f * Mathf.Deg2Rad;
        float tanV = Mathf.Tan(halfFovV);
        float tanH = tanV * Mathf.Max(0.01f, aspect);
        Vector3 dirLocal = new Vector3(
            (viewportX - 0.5f) * 2f * tanH,
            (viewportY - 0.5f) * 2f * tanV,
            1f);
        Vector3 dirWorld = cameraWorldRot * dirLocal;
        if (dirWorld.sqrMagnitude < 1e-8f)
        {
            point = default;
            return false;
        }

        dirWorld.Normalize();
        return TryRayToPlaneY(new Ray(cameraWorldPos, dirWorld), planeY, out point);
    }

    private static bool TryRayToPlaneY(Ray ray, float planeY, out Vector3 point)
    {
        point = default;
        if (Mathf.Abs(ray.direction.y) < 1e-5f)
        {
            return false;
        }

        float t = (planeY - ray.origin.y) / ray.direction.y;
        if (t <= 0.01f)
        {
            return false;
        }

        point = ray.origin + ray.direction * t;
        return true;
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
    /// 在「相机朝向 lookAt、沿视线拉开 viewDistance」的前提下，
    /// 用迭代把视距抬到能装下整省 AABB（解决仅用 FOV 估算仍过近的问题）。
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
        return ComputeCameraLocalYVerified(
            camera,
            cameraRig,
            cameraTransform,
            worldBounds,
            worldBounds.center,
            viewportFillRatio,
            minLocalY,
            maxLocalY,
            distanceToLocalYScale,
            out viewDistanceAlongForward);
    }

    /// <summary>
    /// 同上；相机瞄准 <paramref name="lookAtWorld"/>，仍用真实 <paramref name="worldBounds"/> 做 8 角入框校验。
    /// </summary>
    public static float ComputeCameraLocalYVerified(
        Camera camera,
        Transform cameraRig,
        Transform cameraTransform,
        Bounds worldBounds,
        Vector3 lookAtWorld,
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

        float depth = Mathf.Max(viewDistanceAlongForward, baseLocalY);
        float localY = baseLocalY;
        const int maxIter = 12;
        for (int iter = 0; iter < maxIter; iter++)
        {
            Vector3 camWorld = lookAtWorld - forward * depth;
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

#if UNITY_EDITOR
    /// <summary>画闭合折线（Handles 可设线宽，Game 视图需开启 Gizmos）。</summary>
    public static void DrawPolylineGizmo(Color color, float lineWidth, params Vector3[] points)
    {
        if (points == null || points.Length < 2)
        {
            return;
        }

        UnityEditor.Handles.color = color;
        float width = Mathf.Max(1f, lineWidth);
        UnityEditor.Handles.DrawAAPolyLine(width, points);
    }

    /// <summary>在水平面画 XZ 矩形线框（世界 Y 固定）。</summary>
    public static void DrawXZRectangleGizmo(
        Color color,
        Vector3 center,
        float sizeX,
        float sizeZ,
        float lineWidth = 4f)
    {
        float halfX = Mathf.Max(sizeX, 0.01f) * 0.5f;
        float halfZ = Mathf.Max(sizeZ, 0.01f) * 0.5f;
        float y = center.y;
        Vector3 p0 = new Vector3(center.x - halfX, y, center.z - halfZ);
        Vector3 p1 = new Vector3(center.x + halfX, y, center.z - halfZ);
        Vector3 p2 = new Vector3(center.x + halfX, y, center.z + halfZ);
        Vector3 p3 = new Vector3(center.x - halfX, y, center.z + halfZ);

        DrawPolylineGizmo(color, lineWidth, p0, p1, p2, p3, p0);
    }

    /// <summary>
    /// 画板块 XZ 外包围（红）与相机 fill 视口框在板块高度上的投影（绿）。
    /// </summary>
    public static void DrawProvinceFitGizmos(
        Camera camera,
        Bounds plateBounds,
        Vector3 lookAtWorld,
        float viewportFillRatio,
        float lineWidth = 5f)
    {
        if (camera == null)
        {
            return;
        }

        Vector3 redCenter = new Vector3(plateBounds.center.x, lookAtWorld.y, plateBounds.center.z);
        DrawXZRectangleGizmo(Color.red, redCenter, plateBounds.size.x, plateBounds.size.z, lineWidth);

        if (!TryGetViewportFillRectOnPlaneY(
                camera,
                lookAtWorld.y,
                viewportFillRatio,
                out Vector3 bl,
                out Vector3 br,
                out Vector3 tr,
                out Vector3 tl))
        {
            return;
        }

        DrawPolylineGizmo(Color.green, lineWidth, bl, br, tr, tl, bl);
    }

    /// <summary>将居中 fill 视口矩形四角射线打到水平面 Y，得到世界四边形。</summary>
    public static bool TryGetViewportFillRectOnPlaneY(
        Camera camera,
        float planeY,
        float viewportFillRatio,
        out Vector3 bottomLeft,
        out Vector3 bottomRight,
        out Vector3 topRight,
        out Vector3 topLeft)
    {
        bottomLeft = bottomRight = topRight = topLeft = default;
        if (camera == null)
        {
            return false;
        }

        float fill = Mathf.Clamp(viewportFillRatio, 0.05f, 3f);
        float margin = (1f - fill) * 0.5f;
        float u0 = margin;
        float u1 = 1f - margin;
        float v0 = margin;
        float v1 = 1f - margin;

        if (!TryRayToPlaneY(camera.ViewportPointToRay(new Vector3(u0, v0, 0f)), planeY, out bottomLeft) ||
            !TryRayToPlaneY(camera.ViewportPointToRay(new Vector3(u1, v0, 0f)), planeY, out bottomRight) ||
            !TryRayToPlaneY(camera.ViewportPointToRay(new Vector3(u1, v1, 0f)), planeY, out topRight) ||
            !TryRayToPlaneY(camera.ViewportPointToRay(new Vector3(u0, v1, 0f)), planeY, out topLeft))
        {
            return false;
        }

        return true;
    }
#endif
}
