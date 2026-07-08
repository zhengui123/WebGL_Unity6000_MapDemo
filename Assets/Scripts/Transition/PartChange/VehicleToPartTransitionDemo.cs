using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 键盘测试车辆 → 零件过渡：正播按名称列表轮播，倒播还原上次正播的零件。
/// 默认 P 正播、O 倒播。
/// </summary>
[DisallowMultipleComponent]
public class VehicleToPartTransitionDemo : MonoBehaviour
{
    [SerializeField] private VehicleToPartTransitionController _controller;

    [Header("零件 ID 列表（正播每次取下一个；留空则正播不传 ID）")]
    [SerializeField] private List<string> _partIds = new List<string>();

    [Header("按键")]
    [SerializeField] private KeyCode _playKey = KeyCode.P;
    [SerializeField] private KeyCode _reverseKey = KeyCode.O;

    private int _nextPartIdIndex;

    private void Update()
    {
        VehicleToPartTransitionController controller = ResolveController();
        if (controller == null)
        {
            return;
        }

        if (Input.GetKeyDown(_playKey))
        {
            string partId = GetNextPartIdForForward();
            bool started = string.IsNullOrEmpty(partId)
                ? controller.PlayTransition()
                : controller.PlayTransition(partId);

            if (started)
            {
                Debug.Log($"[VehicleToPartDemo] 正播：{(string.IsNullOrEmpty(partId) ? "列表默认第一项" : partId)}");
            }
        }
        else if (Input.GetKeyDown(_reverseKey))
        {
            bool started = controller.PlayTransitionReverse();
            if (started)
            {
                Debug.Log($"[VehicleToPartDemo] 倒播：{controller.LastPartName}");
            }
        }
    }

    /// <summary>按列表顺序返回下一个零件 ID；列表为空时返回 null。</summary>
    private string GetNextPartIdForForward()
    {
        if (_partIds == null || _partIds.Count == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < _partIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(_partIds[i]))
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        for (int attempt = 0; attempt < _partIds.Count; attempt++)
        {
            int index = (_nextPartIdIndex + attempt) % _partIds.Count;
            string candidate = _partIds[index];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            _nextPartIdIndex = (index + 1) % _partIds.Count;
            return candidate.Trim();
        }

        return null;
    }

    private VehicleToPartTransitionController ResolveController()
    {
        if (_controller != null)
        {
            return _controller;
        }

        return VehicleToPartTransitionController.Instance;
    }
}
