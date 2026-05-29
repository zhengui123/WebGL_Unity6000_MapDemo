using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 地球（Cesium）与 sd_map 板块模型显示切换；监听 MapController.OnArrivedChina 显示板块，板块模式下相机拉远则还原地球。
/// </summary>
[DisallowMultipleComponent]
public class EarthPlateMapSwitcher : MonoBehaviour
{
    public enum MapDisplayType
    {
        Earth = 0,
        Plate = 1
    }

    /// <summary>地球 → 板块 过渡动画类型（Inspector 下拉选择）。</summary>
    public enum EarthToPlateTransitionType
    {
        [InspectorName("云雾过渡（穿云粒子）")]
        CloudFog = 0,

        [InspectorName("科技扫描（世界扫描波）")]
        TechScan = 1,

        [InspectorName("轨道俯冲（俯冲+速度线）")]
        DiveReveal = 2
    }

    [Header("引用（可留空，按名称查找）")]
    [SerializeField] private MapController _mapController;

    [SerializeField] private CameraController _cameraZoomController;

    [Tooltip("地球分支根节点，如 MapParentX（含 CesiumGeoreference）")]
    [SerializeField] private Transform _earthRoot;

    [Tooltip("板块模型根节点，如 sd_map (1)")]
    [SerializeField] private Transform _plateMapRoot;

    [SerializeField] private Camera _viewCamera;

    [Header("查找备用名称")]
    [SerializeField] private string _earthRootName = "MapParentX";
    [SerializeField] private string _plateMapRootName = "sd_map (1)";

    [Header("板块视图")]
    [Tooltip("板块中心沿相机前方向与相机的距离；≤0 时用当前相机局部 Y")]
    [SerializeField] private float _plateViewDistance = 0f;

    [Tooltip("顶面法线朝模型局部 -Y，需旋转使顶面朝向相机")]
    [SerializeField] private bool _alignPlateTopToCamera = true;

    [Header("相机拉远还原地球")]
    [Tooltip("当前为板块显示时，相机局部 Y 大于该值则调用 ShowEarthHidePlate（与 Test 缩放一致）")]
    [SerializeField] private float _restoreEarthMaxCameraLocalY = 1200f;

    [Header("地球 → 板块过渡动画")]
    [SerializeField] private bool _useEarthToPlateTransition = true;

    [SerializeField] private EarthToPlateTransitionType _earthToPlateTransition = EarthToPlateTransitionType.CloudFog;

    [Tooltip("总时长：前半盖住地球，遮罩满屏时切板块，后半遮罩消失露出板块")]
    [SerializeField] private float _transitionDuration = 1.6f;

    [SerializeField] private Color _transitionMainColor = new Color(0.75f, 0.88f, 1f, 0.95f);

    [SerializeField] private Color _transitionAccentColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Header("初始状态")]
    [SerializeField] private bool _startWithEarthVisible = true;
    [SerializeField] private bool _startWithPlateHidden = true;

    /// <summary>当前显示的模型类型。</summary>
    public MapDisplayType CurrentDisplayType { get; private set; } = MapDisplayType.Earth;

    /// <summary>相机拉远还原地球后触发。</summary>
    public event Action OnRestoredEarthByCameraDistance;

    private PlateTransformSnapshot _plateSnapshot;
    private EarthPlateMapTransitionPlayer _transitionPlayer;
    private Coroutine _switchRoutine;

    private struct PlateTransformSnapshot
    {
        public Transform Parent;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    private void Awake()
    {
        ResolveReferences();
        _transitionPlayer = GetComponent<EarthPlateMapTransitionPlayer>();
        if (_transitionPlayer == null)
        {
            _transitionPlayer = gameObject.AddComponent<EarthPlateMapTransitionPlayer>();
        }

        _transitionPlayer.BindViewCamera(_viewCamera);
        _transitionPlayer.BindWorldAnchor(_earthRoot != null ? _earthRoot : transform);

        CachePlateSnapshot();

        if (_startWithPlateHidden && _plateMapRoot != null)
        {
            _plateMapRoot.gameObject.SetActive(false);
        }

        if (_startWithEarthVisible)
        {
            ShowEarthHidePlate();
        }
        else
        {
            SwitchToPlateMap();
        }
    }

    private void OnEnable()
    {
        if (_mapController != null)
        {
            _mapController.OnArrivedChina += HandleArrivedChina;
        }
    }

    private void OnDisable()
    {
        if (_mapController != null)
        {
            _mapController.OnArrivedChina -= HandleArrivedChina;
        }
    }

    private void LateUpdate()
    {
        if (_transitionPlayer != null && _transitionPlayer.IsPlaying)
        {
            return;
        }

        CheckRestoreEarthWhenCameraZoomedOut();
    }

    private void HandleArrivedChina(double longitude, double latitude)
    {
        SwitchToPlateMap();
        Debug.Log(
            $"[EarthPlateMapSwitcher] 到达中国 ({longitude:F2}, {latitude:F2})，切换板块（过渡: {_earthToPlateTransition}）。");
    }

    /// <summary>
    /// 板块显示时，相机局部 Y 超过阈值则还原地球。
    /// </summary>
    private void CheckRestoreEarthWhenCameraZoomedOut()
    {
        if (CurrentDisplayType != MapDisplayType.Plate)
        {
            return;
        }

        float cameraLocalY = GetCameraLocalDistanceY();
        if (cameraLocalY <= _restoreEarthMaxCameraLocalY)
        {
            return;
        }

        ShowEarthHidePlate();
        Debug.Log(
            $"[EarthPlateMapSwitcher] 相机距离 {cameraLocalY:F0} 大于 {_restoreEarthMaxCameraLocalY:F0}，已还原地球显示。");
        OnRestoredEarthByCameraDistance?.Invoke();
    }

    /// <summary>
    /// 隐藏地球并显示板块（可带过渡动画）。
    /// </summary>
    public void SwitchToPlateMap()
    {
        if (_plateMapRoot == null)
        {
            Debug.LogWarning("[EarthPlateMapSwitcher] 未指定板块地图根节点。");
            return;
        }

        if (_switchRoutine != null)
        {
            StopCoroutine(_switchRoutine);
            _switchRoutine = null;
        }

        if (!_useEarthToPlateTransition || _transitionPlayer == null)
        {
            ApplyPlateMapVisible();
            return;
        }

        _switchRoutine = StartCoroutine(SwitchToPlateMapWithTransitionRoutine());
    }

    private IEnumerator SwitchToPlateMapWithTransitionRoutine()
    {
        bool finished = false;
        _transitionPlayer.Play(
            _earthToPlateTransition,
            _transitionDuration,
            _transitionMainColor,
            _transitionAccentColor,
            ApplyPlateMapVisible,
            () => finished = true);

        while (!finished)
        {
            yield return null;
        }

        _switchRoutine = null;
    }

    /// <summary>
    /// 立即完成地球隐藏与板块显示（过渡中点回调）。
    /// </summary>
    private void ApplyPlateMapVisible()
    {
        if (_earthRoot != null)
        {
            _earthRoot.gameObject.SetActive(false);
        }

        if (_plateMapRoot != null)
        {
            _plateMapRoot.gameObject.SetActive(true);
            CenterPlateOnScreen();
        }

        CurrentDisplayType = MapDisplayType.Plate;
    }

    /// <summary>
    /// 显示地球，隐藏板块并恢复板块变换。
    /// </summary>
    public void ShowEarthHidePlate()
    {
        if (_transitionPlayer != null)
        {
            _transitionPlayer.StopImmediate();
        }

        if (_switchRoutine != null)
        {
            StopCoroutine(_switchRoutine);
            _switchRoutine = null;
        }

        if (_plateMapRoot != null)
        {
            RestorePlateSnapshot();
            _plateMapRoot.gameObject.SetActive(false);
        }

        if (_earthRoot != null)
        {
            _earthRoot.gameObject.SetActive(true);
        }

        CurrentDisplayType = MapDisplayType.Earth;
    }

    private float GetCameraLocalDistanceY()
    {
        if (_cameraZoomController != null)
        {
            return _cameraZoomController.CurrentCameraLocalY;
        }

        if (_mapController != null)
        {
            return _mapController.GetCameraLocalDistanceY();
        }

        return float.MaxValue;
    }

    private void ResolveReferences()
    {
        if (_mapController == null)
        {
            _mapController = FindObjectOfType<MapController>();
        }

        if (_cameraZoomController == null)
        {
            _cameraZoomController = FindObjectOfType<CameraController>();
        }

        if (_earthRoot == null && !string.IsNullOrEmpty(_earthRootName))
        {
            GameObject earthGo = GameObject.Find(_earthRootName);
            if (earthGo != null)
            {
                _earthRoot = earthGo.transform;
            }
        }

        if (_plateMapRoot == null && !string.IsNullOrEmpty(_plateMapRootName))
        {
            GameObject plateGo = GameObject.Find(_plateMapRootName);
            if (plateGo != null)
            {
                _plateMapRoot = plateGo.transform;
            }
        }

        if (_viewCamera == null)
        {
            _viewCamera = Camera.main;
        }
    }

    private void CachePlateSnapshot()
    {
        if (_plateMapRoot == null)
        {
            return;
        }

        _plateSnapshot = new PlateTransformSnapshot
        {
            Parent = _plateMapRoot.parent,
            LocalPosition = _plateMapRoot.localPosition,
            LocalRotation = _plateMapRoot.localRotation,
            LocalScale = _plateMapRoot.localScale
        };
    }

    private void RestorePlateSnapshot()
    {
        if (_plateMapRoot == null)
        {
            return;
        }

        if (_plateSnapshot.Parent != null)
        {
            _plateMapRoot.SetParent(_plateSnapshot.Parent, false);
        }

        _plateMapRoot.localPosition = _plateSnapshot.LocalPosition;
        _plateMapRoot.localRotation = _plateSnapshot.LocalRotation;
        _plateMapRoot.localScale = _plateSnapshot.LocalScale;
    }

    private void CenterPlateOnScreen()
    {
        if (_plateMapRoot == null || _viewCamera == null)
        {
            return;
        }

        float distance = ResolvePlateViewDistance();
        Vector3 screenCenterWorld = GetScreenCenterWorldPoint(_viewCamera, distance);

        if (_alignPlateTopToCamera)
        {
            Vector3 toCamera = (_viewCamera.transform.position - screenCenterWorld).normalized;
            if (toCamera.sqrMagnitude > 1e-8f)
            {
                _plateMapRoot.rotation = Quaternion.FromToRotation(Vector3.down, toCamera);
            }
        }

        Bounds bounds = CalculateWorldBounds(_plateMapRoot);
        if (bounds.size.sqrMagnitude > 1e-8f)
        {
            _plateMapRoot.position += screenCenterWorld - bounds.center;
        }
        else
        {
            _plateMapRoot.position = screenCenterWorld;
        }
    }

    private float ResolvePlateViewDistance()
    {
        if (_plateViewDistance > 0f)
        {
            return _plateViewDistance;
        }

        float cameraY = GetCameraLocalDistanceY();
        if (cameraY < float.MaxValue * 0.5f)
        {
            return cameraY;
        }

        return 1200f;
    }

    private static Vector3 GetScreenCenterWorldPoint(Camera camera, float distance)
    {
        Transform camTransform = camera.transform;
        return camTransform.position + camTransform.forward * distance;
    }

    private static Bounds CalculateWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return bounds;
    }
}
