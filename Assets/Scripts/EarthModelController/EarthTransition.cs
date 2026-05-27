using UnityEngine;
using VolumetricFogAndMist;
using DG.Tweening;

public class EarthTransition : MonoBehaviour
{
    [Header("场景对象")]
    [SerializeField] private GameObject earthObj;
    [SerializeField] private GameObject plateMapObj;
    [SerializeField] private VolumetricFog fogController;

    [Header("相机配置")]
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private Vector3 firstTargetLocalPos = new Vector3(0f, 1200f, 0f);
    [SerializeField] private Vector3 secondTargetLocalPos = new Vector3(0f, 1000f, 0f);
    [SerializeField] private float plateMapCenterDistance = 800f;

    public float goEarthAnimTime = 1;
    public float showFogAnimTime = 1;
    public float showPlateMapAnimTime = 1;

    [Header("雾效配置")]
    [SerializeField] private float fogPeakDensity = 1.25f;

    private Sequence _transitionSequence;

    private void Awake()
    {
        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// 供 UGUI Button 直接绑定：点击后按 2 秒过渡到板块图。
    /// </summary>
    public void TransitionToPlateMap()
    {
        PlayTransition();
    }

    /// <summary>
    /// 外部可自定义第一段相机移动时长（单位：秒）。
    /// </summary>
    public void PlayTransition()
    {
        if (!CanPlayTransition())
        {
            return;
        }

        KillCurrentSequence();
        ResetFogDensity(0f);

        _transitionSequence = DOTween.Sequence();
        _transitionSequence.Append(MoveCameraLocal(firstTargetLocalPos, goEarthAnimTime));
        _transitionSequence.Append(AnimateCameraAndFogIn(secondTargetLocalPos, showFogAnimTime));
        _transitionSequence.AppendCallback(SwitchToPlateMapView);
        _transitionSequence.Append(FadeFogDensity(0f, showPlateMapAnimTime));
    }

    private bool CanPlayTransition()
    {
        return mainCameraTransform != null && fogController != null;
    }

    private void KillCurrentSequence()
    {
        if (_transitionSequence != null && _transitionSequence.IsActive())
        {
            _transitionSequence.Kill();
        }
    }

    private Tween MoveCameraLocal(Vector3 targetLocalPos, float duration)
    {
        Debug.Log("MoveCameraLocal: " + targetLocalPos + " " + duration);
        return mainCameraTransform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.Linear);
    }

    private Tween AnimateCameraAndFogIn(Vector3 targetLocalPos, float duration)
    {
        Sequence sequence = DOTween.Sequence();
        sequence.Join(MoveCameraLocal(targetLocalPos, duration));
        sequence.Join(FadeFogDensity(fogPeakDensity, duration));
        return sequence;
    }

    private Tween FadeFogDensity(float targetDensity, float duration)
    {
        return DOTween.To(
            () => fogController.density,
            value => fogController.density = value,
            targetDensity,
            duration
        ).SetEase(Ease.Linear);
    }

    private void SwitchToPlateMapView()
    {
        if (earthObj != null)
        {
            earthObj.SetActive(false);
        }

        if (plateMapObj != null)
        {
            plateMapObj.SetActive(true);
            CenterPlateMapInView();
        }
    }

    private void CenterPlateMapInView()
    {
        Vector3 viewCenterWorldPos = mainCameraTransform.position + mainCameraTransform.forward * plateMapCenterDistance;
        plateMapObj.transform.position = viewCenterWorldPos;
    }

    private void ResetFogDensity(float densityValue)
    {
        fogController.density = densityValue;
    }

    private void OnDestroy()
    {
        KillCurrentSequence();
    }
}
