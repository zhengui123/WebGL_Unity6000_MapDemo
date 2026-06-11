using System;
using UnityEngine;

/// <summary>
/// 拉近终点：通过场景中的 Marker Transform 定义主相机与车辆父物体的目标位姿。
/// </summary>
[Serializable]
public class CityCameraPoseSettings
{
    [Tooltip("目标主相机位姿参考（运行时读取其世界位姿并换算为 CameraPivot 本地空间）")]
    public Transform targetCameraTransform;

    [Tooltip("目标车辆父物体位姿参考（运行时读取其世界坐标与旋转）")]
    public Transform targetVehicleTransform;

    /// <summary>将 Marker 同步为当前主相机与车辆父物体的世界位姿（便于在 Inspector 中录制）。</summary>
    public void SyncMarkersFrom(Transform cameraTransform, Transform vehicleParentTransform)
    {
        if (targetCameraTransform != null && cameraTransform != null)
        {
            targetCameraTransform.SetPositionAndRotation(
                cameraTransform.position,
                cameraTransform.rotation);
        }

        if (targetVehicleTransform != null && vehicleParentTransform != null)
        {
            targetVehicleTransform.SetPositionAndRotation(
                vehicleParentTransform.position,
                vehicleParentTransform.rotation);
        }
    }

    public bool IsValid()
    {
        return targetCameraTransform != null && targetVehicleTransform != null;
    }
}
