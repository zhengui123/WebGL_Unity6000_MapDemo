using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 演示用「瞬时过渡」作用域：运行时临时将各过渡控制器的时长字段置 0，
/// 在 <see cref="Dispose"/> 时恢复为进入作用域前的内存值（不写回磁盘，不改 Inspector 序列化资源）。
/// </summary>
public sealed class TransitionInstantDurationScope : IDisposable
{
    /// <summary>已修改字段的备份，用于 Dispose 时还原。</summary>
    private readonly List<Entry> _entries = new();

    /// <summary>单条字段备份记录。</summary>
    private struct Entry
    {
        /// <summary>挂载脚本的组件实例。</summary>
        public UnityEngine.Object Target;
        /// <summary>被修改的 float 字段反射信息。</summary>
        public FieldInfo Field;
        /// <summary>进入作用域前的原始时长。</summary>
        public float OriginalValue;
    }

    /// <summary>
    /// 项目中各过渡脚本里与「动画时长」相关的字段名白名单。
    /// 仅匹配此列表内的 float 字段，避免误改无关数值。
    /// </summary>
    private static readonly HashSet<string> DurationFieldNames = new()
    {
        "_transitionDuration",
        "_plateFadeDuration",
        "_gaodeFadeDuration",
        "_zoomDuration",
        "_scanlineDuration",
        "_rawImageHideDuration",
        "_cameraDollyDuration",
        "_hideDuration",
        "_focusDuration",
        "_restoreDuration",
        "_otherModuleFadeDuration",
        "_firstMoveDuration",
        "_secondMoveDuration",
        "_kjDissolveDuration",
        "goEarthAnimTime",
        "showFogAnimTime",
        "showPlateMapAnimTime",
    };

    /// <summary>
    /// 扫描当前已加载场景内所有 <see cref="MonoBehaviour"/>，将白名单时长字段临时设为 0。
    /// 调用方应配合 using 语句，确保跳转结束后自动恢复。
    /// </summary>
    public static TransitionInstantDurationScope ApplyToLoadedScene()
    {
        TransitionInstantDurationScope scope = new TransitionInstantDurationScope();
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            scope.ApplyZeroToBehaviour(behaviours[i]);
        }

        return scope;
    }

    /// <summary>遍历类型继承链，将匹配白名单的 float 字段置 0 并记录原值。</summary>
    private void ApplyZeroToBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null)
        {
            return;
        }

        Type type = behaviour.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 沿继承链向上查找，覆盖派生类与基类中声明的时长字段
        while (type != null && type != typeof(MonoBehaviour))
        {
            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(float) || !DurationFieldNames.Contains(field.Name))
                {
                    continue;
                }

                float original = (float)field.GetValue(behaviour);
                _entries.Add(new Entry
                {
                    Target = behaviour,
                    Field = field,
                    OriginalValue = original,
                });
                field.SetValue(behaviour, 0f);
            }

            type = type.BaseType;
        }
    }

    /// <summary>离开 using 作用域时调用，将所有已修改字段恢复为进入前的运行时值。</summary>
    public void Dispose()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (entry.Target == null || entry.Field == null)
            {
                continue;
            }

            entry.Field.SetValue(entry.Target, entry.OriginalValue);
        }

        _entries.Clear();
    }
}
