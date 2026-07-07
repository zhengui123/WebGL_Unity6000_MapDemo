// using System.Globalization;
// using System.IO;
// using System.Text.RegularExpressions;
// using CesiumForUnity;
// using Unity.Mathematics;
// using UnityEngine;

// /// <summary>
// /// 本地 3D Tiles 通用接入模板：自动配置 Tileset、地理原点和主相机视角。
// /// </summary>
// [DisallowMultipleComponent]
// public class CesiumLocalTilesTemplate : MonoBehaviour
// {
//     [Header("必填：本地 tileset.json 路径")]
//     [SerializeField] private string _tilesetJsonPath = @"F:/BaiduNetdiskDownload/3dtiles下载/75343/75343/tileset.json";

//     [Header("组件引用（可留空自动查找/创建）")]
//     [SerializeField] private CesiumGeoreference _georeference;
//     [SerializeField] private Cesium3DTileset _tileset;
//     [SerializeField] private Camera _mainCamera;

//     [Header("Tileset 参数")]
//     [SerializeField] private float _maximumScreenSpaceError = 16f;
//     [SerializeField] private long _maximumCachedBytes = 536870912L;

//     [Header("自动化开关")]
//     [SerializeField] private bool _autoSetGeoreferenceOrigin = true;
//     [SerializeField] private bool _autoMoveMainCamera = true;

//     [Header("相机参数")]
//     [SerializeField] private float _cameraDistance = 1200f;
//     [SerializeField] private float _cameraFarClip = 10000000f;
//     [ContextMenuItem("重新设置相机距离", "ResetCameraDistance")]
//     public string test = "fffff";

//     [ContextMenu("应用本地 3D Tiles 模板")]
//     public void ApplyTemplate()
//     {
//         if (!File.Exists(_tilesetJsonPath))
//         {
//             Debug.LogError($"[CesiumLocalTilesTemplate] 未找到 tileset.json: {_tilesetJsonPath}");
//             return;
//         }

//         if (!TryReadBoundsFromTileset(
//                 _tilesetJsonPath,
//                 out double lonMin,
//                 out double lonMax,
//                 out double latMin,
//                 out double latMax,
//                 out double hMin,
//                 out double hMax))
//         {
//             Debug.LogError("[CesiumLocalTilesTemplate] 解析 tileset.json 范围失败。");
//             return;
//         }

//         double lonCenter = (lonMin + lonMax) * 0.5;
//         double latCenter = (latMin + latMax) * 0.5;
//         double hCenter = (hMin + hMax) * 0.5;

//         EnsureGeoreference();
//         EnsureTileset();

//         _tileset.tilesetSource = CesiumDataSource.FromUrl;
//         _tileset.url = _tilesetJsonPath.Replace('\\', '/');
//         _tileset.maximumScreenSpaceError = _maximumScreenSpaceError;
//         _tileset.maximumCachedBytes = _maximumCachedBytes;

//         if (_autoSetGeoreferenceOrigin)
//         {
//             _georeference.SetOriginLongitudeLatitudeHeight(lonCenter, latCenter, hCenter);
//         }

//         if (_autoMoveMainCamera)
//         {
//             EnsureMainCamera();
//             if (_mainCamera != null)
//             {
//                 MoveCameraToModelCenter(lonCenter, latCenter, hCenter);
//             }
//             else
//             {
//                 Debug.LogWarning("[CesiumLocalTilesTemplate] 未找到 MainCamera，跳过相机自动定位。");
//             }
//         }

//         Debug.Log($"[CesiumLocalTilesTemplate] Lon: {lonMin:F6}~{lonMax:F6}, Lat: {latMin:F6}~{latMax:F6}, Height: {hMin:F1}~{hMax:F1}");
//         Debug.Log($"[CesiumLocalTilesTemplate] Center: lon={lonCenter:F6}, lat={latCenter:F6}, h={hCenter:F1}");
//     }

//     private void Start()
//     {
//         ApplyTemplate();
//     }

//     private void EnsureGeoreference()
//     {
//         if (_georeference != null)
//         {
//             return;
//         }

//         _georeference = FindObjectOfType<CesiumGeoreference>();
//         if (_georeference != null)
//         {
//             return;
//         }

//         GameObject go = new GameObject("CesiumGeoreference");
//         _georeference = go.AddComponent<CesiumGeoreference>();
//     }

//     private void EnsureTileset()
//     {
//         if (_tileset != null)
//         {
//             return;
//         }

//         _tileset = FindObjectOfType<Cesium3DTileset>();
//         if (_tileset != null)
//         {
//             return;
//         }

//         GameObject go = new GameObject("Cesium3DTileset");
//         go.transform.SetParent(_georeference.transform, false);
//         _tileset = go.AddComponent<Cesium3DTileset>();
//     }

//     private void EnsureMainCamera()
//     {
//         if (_mainCamera != null)
//         {
//             return;
//         }

//         GameObject camGo = GameObject.FindGameObjectWithTag("MainCamera");
//         if (camGo != null)
//         {
//             _mainCamera = camGo.GetComponent<Camera>();
//         }

//         if (_mainCamera == null)
//         {
//             _mainCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
//         }
//     }

//     private void MoveCameraToModelCenter(double lon, double lat, double h)
//     {
//         GameObject temp = new GameObject("TempTilesetCenterAnchor");
//         temp.transform.SetParent(_georeference.transform, false);

//         CesiumGlobeAnchor anchor = temp.AddComponent<CesiumGlobeAnchor>();
//         anchor.longitudeLatitudeHeight = new double3(lon, lat, h);
//         anchor.Sync();

//         Vector3 target = temp.transform.position;
//         Vector3 up = temp.transform.up.normalized;
//         Vector3 cameraPosition = target - up * Mathf.Max(1f, _cameraDistance);
//         Quaternion cameraRotation = Quaternion.LookRotation(target - cameraPosition, up);

//         _mainCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
//         _mainCamera.farClipPlane = Mathf.Max(_mainCamera.farClipPlane, _cameraFarClip);

//         Destroy(temp);
//     }

//     private static bool TryReadBoundsFromTileset(
//         string tilesetJsonPath,
//         out double lonMin,
//         out double lonMax,
//         out double latMin,
//         out double latMax,
//         out double hMin,
//         out double hMax)
//     {
//         lonMin = lonMax = 0d;
//         latMin = latMax = 0d;
//         hMin = 0d;
//         hMax = 0d;

//         string text = File.ReadAllText(tilesetJsonPath);

//         Match lonMatch = Regex.Match(
//             text,
//             "\"Longitude\"\\s*:\\s*\\{\\s*\"maximum\"\\s*:\\s*([-\\d.]+)\\s*,\\s*\"minimum\"\\s*:\\s*([-\\d.]+)");
//         Match latMatch = Regex.Match(
//             text,
//             "\"Latitude\"\\s*:\\s*\\{\\s*\"maximum\"\\s*:\\s*([-\\d.]+)\\s*,\\s*\"minimum\"\\s*:\\s*([-\\d.]+)");
//         Match hMatch = Regex.Match(
//             text,
//             "\"Height\"\\s*:\\s*\\{\\s*\"maximum\"\\s*:\\s*([-\\d.]+)\\s*,\\s*\"minimum\"\\s*:\\s*([-\\d.]+)");

//         if (!lonMatch.Success || !latMatch.Success)
//         {
//             return false;
//         }

//         lonMax = double.Parse(lonMatch.Groups[1].Value, CultureInfo.InvariantCulture);
//         lonMin = double.Parse(lonMatch.Groups[2].Value, CultureInfo.InvariantCulture);
//         latMax = double.Parse(latMatch.Groups[1].Value, CultureInfo.InvariantCulture);
//         latMin = double.Parse(latMatch.Groups[2].Value, CultureInfo.InvariantCulture);

//         if (hMatch.Success)
//         {
//             hMax = double.Parse(hMatch.Groups[1].Value, CultureInfo.InvariantCulture);
//             hMin = double.Parse(hMatch.Groups[2].Value, CultureInfo.InvariantCulture);
//         }
//         else
//         {
//             hMin = 0d;
//             hMax = 300d;
//         }

//         return true;
//     }
// }
