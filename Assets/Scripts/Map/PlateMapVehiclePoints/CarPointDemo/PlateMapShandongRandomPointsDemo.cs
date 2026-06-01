using UnityEngine;

/// <summary>
/// 山东省内测试点位随机生成（Demo）。经 <see cref="PlateMapVehiclePointEvents"/> 按板块名推送。
/// </summary>
[DisallowMultipleComponent]
public class PlateMapShandongRandomPointsDemo : MonoBehaviour
{
    [Header("目标板块")]
    [SerializeField] private string _plateMapName = "sd_map (1)";

    [Header("省界与采样")]
    [SerializeField] private PlateMapShandongProvincePointFilter _provinceFilter = new PlateMapShandongProvincePointFilter();

    [Header("随机生成")]
    [SerializeField] private int _randomGenerateCount = 100;
    [SerializeField] private int _randomSeed;

    [Header("事件")]
    [SerializeField] private bool _logRebuildCompletedAction;
    [SerializeField] private bool _pushViaJsonApi = true;

    private PlateMapVehiclePointEvents Hub => PlateMapVehiclePointEvents.Instance;

    private void OnEnable()
    {
        Hub.RegisterShouldIncludePointAction(_plateMapName, ShouldIncludePointAction);
        if (_logRebuildCompletedAction)
        {
        }
    }

    private void OnDisable()
    {
        PlateMapVehiclePointEvents hub = PlateMapVehiclePointEvents.Instance;
        if (hub == null)
        {
            return;
        }

        hub.UnregisterShouldIncludePointAction(_plateMapName);
    }


    private bool ShouldIncludePointAction(VehicleMapPointData data)
    {
        if (!_provinceFilter.StrictProvinceBoundary)
        {
            return true;
        }

        return _provinceFilter.Contains(data.longitude, data.latitude);
    }


    [ContextMenu("随机生成100个山东省内点位")]
    public void GenerateRandomVehiclePointsInShandongMenu()
    {
        GenerateRandomVehiclePointsInShandong(100);
    }

    public void GenerateRandomVehiclePointsInShandong(int count = -1)
    {
        if (count <= 0)
        {
            count = _randomGenerateCount;
        }

        VehicleMapPointData[] points = PlateMapShandongTestPointGenerator.Generate(
            _plateMapName, _provinceFilter, count, _randomSeed);

        if (points.Length == 0)
        {
            Debug.LogError("[PlateMapShandongRandomPointsDemo] 未生成任何点位。");
            return;
        }

        bool ok = _pushViaJsonApi
            ? Hub.UpdateVehiclePointsFromJson(_plateMapName, VehicleMapPointJson.ToJson(points))
            : Hub.PublishSetVehiclePoints(_plateMapName, points);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        Debug.Log(ok ? $"[PlateMapShandongRandomPointsDemo] 已推送到 {_plateMapName}，{points.Length} 个点。" : "[PlateMapShandongRandomPointsDemo] 推送失败。");
    }
}
