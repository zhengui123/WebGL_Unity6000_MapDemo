using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 调度各类型过渡效果（云雾粒子 / 世界扫描波 / 俯冲速度线），非全屏遮盖。
/// </summary>
public class EarthPlateMapTransitionPlayer : MonoBehaviour
{
    [SerializeField] private Camera _viewCamera;

    [SerializeField] private Transform _worldAnchor;

    [SerializeField] private EarthPlateTransitionConfig _config;

    [Tooltip("前半段占比：效果最浓时切换板块")]
    [SerializeField] [Range(0.35f, 0.65f)] private float _coverPhaseEnd = 0.5f;

    private CloudFogPlateTransition _cloudFog = new CloudFogPlateTransition();
    private TechScanPlateTransition _techScan = new TechScanPlateTransition();
    private DiveRevealPlateTransition _diveReveal = new DiveRevealPlateTransition();

    private EarthPlateTransitionEffectBase _activeEffect;
    private Coroutine _playRoutine;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        EnsureConfig();
    }

    private void EnsureConfig()
    {
        if (_config == null)
        {
            _config = Resources.Load<EarthPlateTransitionConfig>("EarthPlateTransitionConfig");
        }

        if (_config == null)
        {
#if UNITY_EDITOR
            _config = UnityEditor.AssetDatabase.LoadAssetAtPath<EarthPlateTransitionConfig>(
                EarthPlateTransitionConfig.DefaultAssetPath);
#endif
        }

        EarthPlateParticleMaterials.SetConfig(_config);

        if (_config == null)
        {
            Debug.LogWarning("[过渡] 未找到 EarthPlateTransitionConfig，请执行 Tools/地图/创建过渡动画资源。");
        }
    }

    public void BindViewCamera(Camera camera)
    {
        if (camera != null)
        {
            _viewCamera = camera;
        }
    }

    public void BindWorldAnchor(Transform anchor)
    {
        if (anchor != null)
        {
            _worldAnchor = anchor;
        }
    }

    private void OnDestroy()
    {
        DisposeAllEffects();
    }

    public void Play(
        EarthPlateMapSwitcher.EarthToPlateTransitionType type,
        float duration,
        Color mainColor,
        Color accentColor,
        Action onMidSwap,
        Action onComplete)
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
        }

        _playRoutine = StartCoroutine(PlayRoutine(type, duration, mainColor, accentColor, onMidSwap, onComplete));
    }

    public void StopImmediate()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _isPlaying = false;
        if (_activeEffect != null)
        {
            _activeEffect.Dispose();
            _activeEffect = null;
        }
    }

    private IEnumerator PlayRoutine(
        EarthPlateMapSwitcher.EarthToPlateTransitionType type,
        float duration,
        Color mainColor,
        Color accentColor,
        Action onMidSwap,
        Action onComplete)
    {
        Camera cam = ResolveCamera();
        if (cam == null)
        {
            onMidSwap?.Invoke();
            onComplete?.Invoke();
            yield break;
        }

        _activeEffect?.Dispose();
        _activeEffect = CreateEffect(type);
        if (_activeEffect == null)
        {
            onMidSwap?.Invoke();
            onComplete?.Invoke();
            yield break;
        }

        EnsureConfig();
        float coverEnd = Mathf.Clamp(_coverPhaseEnd, 0.35f, 0.65f);
        _activeEffect.Setup(cam, _worldAnchor, _config, mainColor, accentColor, coverEnd);
        _activeEffect.Show();
        _isPlaying = true;

        float dur = Mathf.Max(0.1f, duration);
        bool swapped = false;
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / dur);
            _activeEffect.SetProgress(progress);

            if (!swapped && progress >= coverEnd)
            {
                swapped = true;
                onMidSwap?.Invoke();
            }

            yield return null;
        }

        _activeEffect.SetProgress(1f);
        if (!swapped)
        {
            onMidSwap?.Invoke();
        }

        _activeEffect.Dispose();
        _activeEffect = null;
        _isPlaying = false;
        _playRoutine = null;
        onComplete?.Invoke();
    }

    private Camera ResolveCamera()
    {
        if (_viewCamera != null)
        {
            return _viewCamera;
        }

        _viewCamera = Camera.main;
        return _viewCamera;
    }

    private EarthPlateTransitionEffectBase CreateEffect(EarthPlateMapSwitcher.EarthToPlateTransitionType type)
    {
        switch (type)
        {
            case EarthPlateMapSwitcher.EarthToPlateTransitionType.CloudFog:
                return _cloudFog;
            case EarthPlateMapSwitcher.EarthToPlateTransitionType.TechScan:
                return _techScan;
            case EarthPlateMapSwitcher.EarthToPlateTransitionType.DiveReveal:
                return _diveReveal;
            default:
                return _cloudFog;
        }
    }

    private void DisposeAllEffects()
    {
        _cloudFog?.Dispose();
        _techScan?.Dispose();
        _diveReveal?.Dispose();
        _activeEffect = null;
    }
}
