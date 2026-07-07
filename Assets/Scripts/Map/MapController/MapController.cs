// using System;
// using CesiumForUnity;
// using Unity.Mathematics;
// using UnityEngine;

// /// <summary>
// /// 挂在 MapRoot：拖拽与双击仅旋转 MapRoot 世界朝向；双击对准完成后再通知 Test 缩放。
// /// </summary>
// public class MapController : MonoBehaviour
// {
//     [Header("地球旋转根节点（留空则为本物体）")]
//     [SerializeField] private Transform _mapRoot;

//     [Header("拾取射线用的相机（留空则 Camera.main）")]
//     [SerializeField] private Camera _pickCamera;

//     [Header("地理参考（留空则运行时查找）")]
//     [SerializeField] private CesiumGeoreference _georeference;

//     [Header("旋转（世界空间，相对当前相机朝向）")]
//     [Tooltip("鼠标每移动 1 像素对应的角度（度）")]
//     [SerializeField] private float _degreesPerPixel = 0.12f;

//     [SerializeField] private float _rotateSmoothTime = 0.08f;

//     [Tooltip("勾选后左右拖拽方向与默认相反")]
//     [SerializeField] private bool _invertHorizontalRotation = true;

//     [Tooltip("勾选后上下拖拽方向与默认相反")]
//     [SerializeField] private bool _invertVerticalRotation = true;

//     [Header("地图点击 / 双击")]
//     [SerializeField] private float _clickMaxDragPixels = 8f;
//     [SerializeField] private float _doubleClickInterval = 0.35f;
//     [SerializeField] private float _raycastMaxDistance = 1e9f;
//     [SerializeField] private bool _logLongitudeLatitudeOnClick = true;

//     [Tooltip("双击对准迭代次数（仅旋转 MapRoot）")]
//     [SerializeField] private int _centerAlignIterations = 3;

//     [Header("双击拉近")]
//     [SerializeField] private CameraController _cameraZoomController;

//     [Header("屏幕中心 / 中国区域")]
//     [SerializeField] private bool _enableChinaRegionCheck = true;

//     [Tooltip("屏幕中心经纬度在中国范围内，且相机局部 Y 小于等于该值时触发（与 Test 缩放一致）")]
//     [SerializeField] private float _chinaTriggerMaxCameraLocalY = 1200f;

//     [SerializeField] private double _chinaLongitudeMin = 73.0;
//     [SerializeField] private double _chinaLongitudeMax = 135.0;
//     [SerializeField] private double _chinaLatitudeMin = 18.0;
//     [SerializeField] private double _chinaLatitudeMax = 54.0;

//     [SerializeField] private bool _logScreenCenterLongitudeLatitude;

//     /// <summary>双击旋转对准完成后触发，参数为对准后该点的世界坐标（供 Test 做拉近）。</summary>
//     public event Action<Vector3> OnDoubleClickWorldPoint;

//     /// <summary>屏幕中心进入中国范围且相机足够近时触发（参数：经度、纬度，度）。由 EarthPlateMapSwitcher 等监听并切换模型。</summary>
//     public event Action<double, double> OnArrivedChina;

//     /// <summary>MapRoot 启动时的世界旋转，作为偏移四元数的基准。</summary>
//     private Quaternion _baseRootWorldRotation;

//     /// <summary>相对基准的累积世界旋转偏移。</summary>
//     private Quaternion _targetWorldOffset = Quaternion.identity;

//     /// <summary>用于平滑显示的世界旋转偏移。</summary>
//     private Quaternion _displayWorldOffset = Quaternion.identity;

//     private Vector3 _lastMousePosition;
//     private Vector3 _mouseDownPosition;
//     private float _lastMapClickTime = -999f;
//     private Vector2 _lastMapClickScreenPos;

//     private bool _hasPendingZoom;
//     private Vector3 _pendingCenterGeoLocal;
//     private const float RotationSettledAngleDeg = 0.5f;

//     private bool _wasInsideChinaTriggerZone;

//     /// <summary>
//     /// 初始化 MapRoot、相机、地理参考，并记录基准世界旋转。
//     /// </summary>
//     private void Awake()
//     {
//         if (_mapRoot == null)
//         {
//             _mapRoot = transform;
//         }

//         if (_pickCamera == null)
//         {
//             _pickCamera = Camera.main;
//         }

//         if (_georeference == null)
//         {
//             _georeference = FindObjectOfType<CesiumGeoreference>();
//         }

//         if (_cameraZoomController == null)
//         {
//             _cameraZoomController = FindObjectOfType<CameraController>();
//         }

//         _baseRootWorldRotation = _mapRoot.rotation;
//         ApplyMapRootWorldOffset(_displayWorldOffset);
//     }

//     /// <summary>
//     /// 启动时获取地心坐标、输出日志，并将地心与 MapRoot 枢轴（父物体中心）对齐。
//     /// </summary>
//     private void Start()
//     {
//         if (!TryGetEarthCenterWorldPosition(out Vector3 earthCenterWorld, out Vector3 geoLocal))
//         {
//             return;
//         }

//         Debug.Log(
//             $"[地球中心] 对齐前 世界坐标 {earthCenterWorld} | CesiumGeoreference 局部 {geoLocal}");

//         AlignEarthCenterToMapRootCenter(earthCenterWorld);
//     }

//     /// <summary>
//     /// 每帧处理拖拽旋转、地图点击/双击，并平滑应用 MapRoot 旋转。
//     /// </summary>
//     private void LateUpdate()
//     {
//         HandleRotateInput();
//         HandleMapClickAndDoubleClick();
//         ApplySmoothRotation();
//         CheckScreenCenterChinaRegion();
//     }

//     /// <summary>
//     /// 根据鼠标增量，绕相机 up/right 累积 MapRoot 的世界旋转偏移。
//     /// </summary>
//     private void HandleRotateInput()
//     {
//         if (_mapRoot == null || _pickCamera == null)
//         {
//             return;
//         }

//         if (Input.GetMouseButtonDown(0))
//         {
//             _lastMousePosition = Input.mousePosition;
//             _mouseDownPosition = Input.mousePosition;
//         }

//         if (!Input.GetMouseButton(0))
//         {
//             return;
//         }

//         Vector3 delta = Input.mousePosition - _lastMousePosition;
//         _lastMousePosition = Input.mousePosition;

//         if (delta.sqrMagnitude < 1e-10f)
//         {
//             return;
//         }

//         ApplyCameraRelativeDelta(delta.x * _degreesPerPixel, -delta.y * _degreesPerPixel);
//     }

//     /// <summary>
//     /// 按与拖拽相同的顺序（pitch × yaw）叠加相机相对旋转到目标偏移。
//     /// </summary>
//     /// <param name="yawDeg">绕相机 up 的角度（度）。</param>
//     /// <param name="pitchDeg">绕相机 right 的角度（度）。</param>
//     private void ApplyCameraRelativeDelta(float yawDeg, float pitchDeg)
//     {
//         Transform cam = _pickCamera.transform;
//         float horizontalSign = _invertHorizontalRotation ? -1f : 1f;
//         float verticalSign = _invertVerticalRotation ? -1f : 1f;

//         Quaternion deltaYaw = Quaternion.AngleAxis(yawDeg * horizontalSign, cam.up);
//         Quaternion deltaPitch = Quaternion.AngleAxis(pitchDeg * verticalSign, cam.right);

//         _targetWorldOffset = deltaPitch * deltaYaw * _targetWorldOffset;
//     }

//     /// <summary>
//     /// 将偏移四元数应用到 MapRoot 世界旋转（不改变 position）。
//     /// </summary>
//     /// <param name="worldOffset">相对启动基准的旋转偏移。</param>
//     private void ApplyMapRootWorldOffset(Quaternion worldOffset)
//     {
//         if (_mapRoot == null)
//         {
//             return;
//         }

//         _mapRoot.rotation = worldOffset * _baseRootWorldRotation;
//     }

//     /// <summary>
//     /// 对 MapRoot 偏移做 Slerp 平滑。
//     /// </summary>
//     private void ApplySmoothRotation()
//     {
//         if (_mapRoot == null)
//         {
//             return;
//         }

//         float smooth = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1e-4f, _rotateSmoothTime));
//         _displayWorldOffset = Quaternion.Slerp(_displayWorldOffset, _targetWorldOffset, smooth);
//         ApplyMapRootWorldOffset(_displayWorldOffset);
//         TryFirePendingZoomAfterRotation();
//     }

//     /// <summary>
//     /// 左键抬起且移动很小时：射线拾取；双击则按经纬度旋转 MapRoot 使该点对准相机正前方。
//     /// </summary>
//     private void HandleMapClickAndDoubleClick()
//     {
//         if (!Input.GetMouseButtonUp(0))
//         {
//             return;
//         }

//         if (Vector2.Distance(Input.mousePosition, _mouseDownPosition) > _clickMaxDragPixels)
//         {
//             return;
//         }

//         if (_pickCamera == null || _georeference == null)
//         {
//             return;
//         }

//         Ray ray = _pickCamera.ScreenPointToRay(Input.mousePosition);
//         if (!Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
//         {
//             return;
//         }

//         if (!TryWorldPointToLongitudeLatitudeHeight(hit.point, out double longitude, out double latitude, out double height))
//         {
//             return;
//         }

//         if (_logLongitudeLatitudeOnClick)
//         {
//             Debug.Log($"[地图拾取] 经度 {longitude:F6}°, 纬度 {latitude:F6}°, 椭球高 {height:F2} m（WGS84）");
//         }

//         Vector2 screenPos = Input.mousePosition;
//         bool isDoubleClick = Time.time - _lastMapClickTime <= _doubleClickInterval
//                              && (screenPos - _lastMapClickScreenPos).sqrMagnitude <= 25f;

//         _lastMapClickTime = Time.time;
//         _lastMapClickScreenPos = screenPos;

//         if (!isDoubleClick)
//         {
//             return;
//         }

//         BeginCenterThenZoom(longitude, latitude, height);
//     }

//     /// <summary>
//     /// 开始双击对准：只更新旋转目标，旋转平滑结束后再通知 Test 缩放。
//     /// </summary>
//     private void BeginCenterThenZoom(double longitude, double latitude, double height)
//     {
//         if (!TryLongitudeLatitudeHeightToGeoreferenceLocal(longitude, latitude, height, out Vector3 geoLocal))
//         {
//             return;
//         }

//         _pendingCenterGeoLocal = geoLocal;
//         _hasPendingZoom = true;
//         CenterLongitudeLatitudeOnCameraForward(geoLocal);
//     }

//     /// <summary>
//     /// 固定地理参考局部坐标中的地表点，仅旋转 MapRoot，使该点落在相机正前方；不修改 position。
//     /// 在相机局部空间用 X/Y 偏差角对准（不使用世界坐标 Z），绕世界 Y、X 轴旋转。
//     /// </summary>
//     /// <param name="geoLocal">地表点在 CesiumGeoreference 下的局部坐标。</param>
//     private void CenterLongitudeLatitudeOnCameraForward(Vector3 geoLocal)
//     {
//         if (_mapRoot == null || _pickCamera == null || _georeference == null)
//         {
//             _hasPendingZoom = false;
//             return;
//         }

//         Transform cam = _pickCamera.transform;
//         int iterations = Mathf.Max(1, _centerAlignIterations);

//         for (int i = 0; i < iterations; i++)
//         {
//             Vector3 worldPoint = _georeference.transform.TransformPoint(geoLocal);
//             Vector3 toPoint = worldPoint - cam.position;
//             Vector3 localDir = cam.InverseTransformDirection(toPoint);

//             // 视线深度在相机局部 Z；世界坐标 Z 不参与对准角计算
//             if (localDir.z < 1e-6f)
//             {
//                 break;
//             }

//             float yawDeg = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
//             float pitchDeg = -Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
//             if (Mathf.Abs(yawDeg) <= 0.05f && Mathf.Abs(pitchDeg) <= 0.05f)
//             {
//                 break;
//             }

//             ApplyWorldXYAxisRotationDelta(yawDeg, pitchDeg);
//         }
//     }

//     /// <summary>
//     /// 双击对准：先绕世界 Y（水平），再绕世界 X（俯仰），顺序与拖拽 pitch × yaw 一致。
//     /// </summary>
//     private void ApplyWorldXYAxisRotationDelta(float yawDeg, float pitchDeg)
//     {
//         Quaternion deltaYaw = Quaternion.AngleAxis(yawDeg, Vector3.up);
//         Quaternion deltaPitch = Quaternion.AngleAxis(pitchDeg, Vector3.right);
//         _targetWorldOffset = deltaPitch * deltaYaw * _targetWorldOffset;
//     }

//     /// <summary>
//     /// 旋转已对准目标后触发待执行的相机缩放事件。
//     /// </summary>
//     private void TryFirePendingZoomAfterRotation()
//     {
//         if (!_hasPendingZoom || _georeference == null)
//         {
//             return;
//         }

//         if (Quaternion.Angle(_displayWorldOffset, _targetWorldOffset) > RotationSettledAngleDeg)
//         {
//             return;
//         }

//         _hasPendingZoom = false;
//         Vector3 worldPoint = _georeference.transform.TransformPoint(_pendingCenterGeoLocal);
//         OnDoubleClickWorldPoint?.Invoke(worldPoint);
//     }

//     /// <summary>
//     /// 地心在 ECEF 中为 (0,0,0)，经 CesiumGeoreference 转为局部与世界坐标。
//     /// </summary>
//     private bool TryGetEarthCenterWorldPosition(out Vector3 worldPosition, out Vector3 geoLocal)
//     {
//         worldPosition = Vector3.zero;
//         geoLocal = Vector3.zero;
//         if (_georeference == null)
//         {
//             Debug.LogWarning("[MapController] 未找到 CesiumGeoreference，无法获取地球中心坐标。");
//             return false;
//         }

//         double3 unityInGeoFrame = _georeference.TransformEarthCenteredEarthFixedPositionToUnity(double3.zero);
//         geoLocal = new Vector3(
//             (float)unityInGeoFrame.x,
//             (float)unityInGeoFrame.y,
//             (float)unityInGeoFrame.z);
//         worldPosition = _georeference.transform.TransformPoint(geoLocal);
//         return true;
//     }

//     /// <summary>
//     /// 平移地球层级，使地心世界坐标与 MapRoot（父物体）枢轴重合，便于绕地心旋转。
//     /// </summary>
//     private void AlignEarthCenterToMapRootCenter(Vector3 earthCenterWorld)
//     {
//         if (_mapRoot == null || _georeference == null)
//         {
//             return;
//         }

//         Vector3 targetCenter = _mapRoot.position;
//         Vector3 deltaWorld = targetCenter - earthCenterWorld;

//         // CesiumGeoreference 挂在 MapParentY 下，平移其父节点即可整体挪动地球而不改 MapRoot.position
//         Transform earthBranch = _georeference.transform.parent != null
//             ? _georeference.transform.parent
//             : _georeference.transform;
//         earthBranch.position += deltaWorld;

//         if (!TryGetEarthCenterWorldPosition(out Vector3 afterWorld, out Vector3 afterGeoLocal))
//         {
//             return;
//         }

//         Debug.Log(
//             $"[地球中心] 已对准 MapRoot 枢轴 {targetCenter} | 对齐后世界 {afterWorld} | 局部 {afterGeoLocal}");
//     }

//     /// <summary>
//     /// 将 WGS84 经纬高转换为地理参考 Transform 下的局部坐标。
//     /// </summary>
//     private bool TryLongitudeLatitudeHeightToGeoreferenceLocal(
//         double longitude,
//         double latitude,
//         double height,
//         out Vector3 geoLocal)
//     {
//         geoLocal = Vector3.zero;
//         if (_georeference == null)
//         {
//             return false;
//         }

//         double3 llh = new double3(longitude, latitude, height);
//         double3 ecef = _georeference.ellipsoid.LongitudeLatitudeHeightToCenteredFixed(llh);
//         double3 unityInGeoFrame = _georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
//         geoLocal = new Vector3((float)unityInGeoFrame.x, (float)unityInGeoFrame.y, (float)unityInGeoFrame.z);
//         return true;
//     }

//     /// <summary>
//     /// 将 Unity 世界坐标转换为 WGS84 经纬度（度）与椭球高（米）。
//     /// </summary>
//     private bool TryWorldPointToLongitudeLatitudeHeight(
//         Vector3 worldPoint,
//         out double longitude,
//         out double latitude,
//         out double height)
//     {
//         longitude = latitude = height = 0;
//         if (_georeference == null)
//         {
//             return false;
//         }

//         Vector3 localInGeo = _georeference.transform.InverseTransformPoint(worldPoint);
//         double3 ecef = _georeference.TransformUnityPositionToEarthCenteredEarthFixed(
//             new double3(localInGeo.x, localInGeo.y, localInGeo.z));
//         double3 llh = _georeference.ellipsoid.CenteredFixedToLongitudeLatitudeHeight(ecef);
//         longitude = llh.x;
//         latitude = llh.y;
//         height = llh.z;
//         return true;
//     }

//     /// <summary>
//     /// 获取屏幕中心射线命中地表点的 WGS84 经纬度（度）与椭球高（米）。
//     /// </summary>
//     public bool TryGetScreenCenterLongitudeLatitude(
//         out double longitude,
//         out double latitude,
//         out double height)
//     {
//         longitude = latitude = height = 0;
//         if (_pickCamera == null)
//         {
//             return false;
//         }

//         Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
//         Ray ray = _pickCamera.ScreenPointToRay(screenCenter);
//         if (!Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
//         {
//             return false;
//         }

//         return TryWorldPointToLongitudeLatitudeHeight(hit.point, out longitude, out latitude, out height);
//     }

//     /// <summary>
//     /// 判断经纬度是否在中国范围包围盒内（WGS84，度）。
//     /// </summary>
//     public bool IsInsideChinaLongitudeLatitude(double longitude, double latitude)
//     {
//         return longitude >= _chinaLongitudeMin && longitude <= _chinaLongitudeMax
//                && latitude >= _chinaLatitudeMin && latitude <= _chinaLatitudeMax;
//     }

//     /// <summary>
//     /// 当前相机局部 Y 距离（来自 Test，无 Test 时返回 float.MaxValue）。
//     /// </summary>
//     public float GetCameraLocalDistanceY()
//     {
//         return _cameraZoomController != null
//             ? _cameraZoomController.CurrentCameraLocalY
//             : float.MaxValue;
//     }

//     /// <summary>
//     /// 屏幕中心在中国范围内且相机足够近时触发 OnArrivedChina（模型切换由 EarthPlateMapSwitcher 处理）。
//     /// </summary>
//     private void CheckScreenCenterChinaRegion()
//     {
//         if (!_enableChinaRegionCheck)
//         {
//             return;
//         }

//         if (!TryGetScreenCenterLongitudeLatitude(out double longitude, out double latitude, out _))
//         {
//             _wasInsideChinaTriggerZone = false;
//             return;
//         }

//         if (_logScreenCenterLongitudeLatitude)
//         {
//             Debug.Log($"[屏幕中心] 经度 {longitude:F4}°, 纬度 {latitude:F4}°");
//         }

//         bool inChina = IsInsideChinaLongitudeLatitude(longitude, latitude);
//         bool closeEnough = GetCameraLocalDistanceY() <= _chinaTriggerMaxCameraLocalY;
//         bool insideTriggerZone = inChina && closeEnough;

//         if (insideTriggerZone && !_wasInsideChinaTriggerZone)
//         {
//             _wasInsideChinaTriggerZone = true;
//             Debug.Log("到达中国");
//             OnArrivedChina?.Invoke(longitude, latitude);
//         }
//         else if (!insideTriggerZone)
//         {
//             _wasInsideChinaTriggerZone = false;
//         }
//     }
// }
