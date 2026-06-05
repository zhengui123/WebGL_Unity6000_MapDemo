using System;
using UnityEngine;

/// <summary>
/// 主相机（FogCamera）本地位姿，用于拉近终点配置。
/// </summary>
[Serializable]
public class CityCameraPoseSettings
{
    [Tooltip("主相机相对 CameraPivot 的本地坐标")]
    public Vector3 cameraLocalPosition;

    [Tooltip("主相机相对 CameraPivot 的本地欧拉角")]
    public Vector3 cameraLocalEuler;

    public void CaptureFrom(Transform cameraTransform)
    {
        if (cameraTransform == null)
        {
            return;
        }

        cameraLocalPosition = cameraTransform.localPosition;
        cameraLocalEuler = cameraTransform.localEulerAngles;
    }
}
