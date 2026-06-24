#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="ControlStateHierarchyTransitionController"/> 的 Inspector 扩展：提供 Play 模式下手动触发开局跳转的按钮。
/// </summary>
[CustomEditor(typeof(ControlStateHierarchyTransitionController))]
public class ControlStateHierarchyTransitionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);

        ControlStateHierarchyTransitionController controller =
            (ControlStateHierarchyTransitionController)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("立即应用开局状态（Play 模式）", GUILayout.Height(28f)))
            {
                controller.ApplyStartStateNow();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "勾选 Apply On Play 后进入 Play 将自动跳转；或在 Play 模式下点击上方按钮手动触发。过渡时长仅在跳转期间临时为 0，不会修改各控制器 Inspector 配置。",
                MessageType.Info);
        }
    }
}
#endif
