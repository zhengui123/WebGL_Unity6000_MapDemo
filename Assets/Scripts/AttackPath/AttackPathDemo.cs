using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 攻击路径 Demo：持有路点列表，启用时驱动同物体上的 <see cref="AttackPathController"/> 播放 LineRenderer 连线。
/// </summary>
[DisallowMultipleComponent]
public class AttackPathDemo : MonoBehaviour
{
    [SerializeField] private AttackPathController _attackPathController;
    [FormerlySerializedAs("waypoints")]
    [SerializeField] private List<Transform> _waypoints;
    [SerializeField] private bool _playOnEnable = true;

    private void Awake()
    {
        if (_attackPathController == null)
        {
            _attackPathController = GetComponent<AttackPathController>();
        }
    }

    /// <summary>启用时按配置自动播放。</summary>
    private void OnEnable()
    {
        if (_playOnEnable)
        {
            PlayPath();
        }
    }

    /// <summary>使用本组件 Inspector 中的路点播放攻击路径。</summary>
    public void PlayPath()
    {
        if (_attackPathController == null)
        {
            return;
        }

        _attackPathController.PlayPath(_waypoints);
    }

    /// <summary>使用本组件路点播放，并可覆盖速度、闭合与循环选项。</summary>
    public void PlayPath(float? speed = null, bool? closePath = null, bool? loopAnimation = null)
    {
        if (_attackPathController == null)
        {
            return;
        }

        _attackPathController.PlayPath(_waypoints, speed, closePath, loopAnimation);
    }
}
