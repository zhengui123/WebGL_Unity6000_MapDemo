using System.Reflection;
using UnityEngine;

/// <summary>
/// Demo 专用辅助：通过反射读写 <see cref="GameManager"/> 内部状态，
/// 避免为测试入口扩展 GameManager 公开 API。
/// </summary>
internal static class GameManagerDemoAccess
{
    private static FieldInfo _currentStateField;
    private static MethodInfo _applySideEffectsMethod;

    /// <summary>读取当前操控级别；manager 为空时视为地球级。</summary>
    public static GameManager.ControlState GetCurrentState(GameManager manager)
    {
        return manager != null ? manager.CurrentState : GameManager.ControlState.EarthLevel;
    }

    /// <summary>
    /// 强制设置操控级别，并同步执行点击开关等副作用（与事件驱动路径一致）。
    /// 用于尚无完整过渡链的状态对齐，或倒播前的状态修正。
    /// </summary>
    public static void ForceState(GameManager manager, GameManager.ControlState state)
    {
        if (manager == null)
        {
            return;
        }

        EnsureReflection();
        _currentStateField?.SetValue(manager, state);
        _applySideEffectsMethod?.Invoke(manager, new object[] { state });
    }

    /// <summary>懒加载反射缓存，仅初始化一次。</summary>
    private static void EnsureReflection()
    {
        if (_currentStateField != null)
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _currentStateField = typeof(GameManager).GetField("_currentState", flags);
        _applySideEffectsMethod = typeof(GameManager).GetMethod("ApplyStateSideEffects", flags);
    }
}
