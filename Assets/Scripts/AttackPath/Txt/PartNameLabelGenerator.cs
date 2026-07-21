using UnityEngine;

/// <summary>
/// 挂在零部件父节点（如 User）上：生成 / 重置 / 删除零部件名称 TextMesh，并统一调节大小与粗细。
/// </summary>
[DisallowMultipleComponent]
public class PartNameLabelGenerator : MonoBehaviour
{
    [Tooltip("要生成标签的子物体；留空则使用全部直接子物体")]
    [SerializeField] private Transform[] _partRoots;

    [Tooltip("额外偏移；默认在包围盒顶面之上再抬高一点")]
    [SerializeField] private Vector3 _labelOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("是否按 Renderer 包围盒顶面自动抬高")]
    [SerializeField] private bool _useBoundsTop = true;

    [Header("文字样式")]
    [Tooltip("TextMesh.characterSize，控制世界空间字号")]
    [SerializeField] private float _characterSize = 0.08f;

    [Tooltip("TextMesh.fontSize")]
    [SerializeField] private int _fontSize = 48;

    [Tooltip("文字粗细（Bold 需字体支持粗体字形）")]
    [SerializeField] private FontStyle _fontStyle = FontStyle.Bold;

    [SerializeField] private Color _color = Color.white;

    /// <summary>先清除已生成内容，再为每个零部件重新挂一份 Billboard 文本。</summary>
    [ContextMenu("生成/重置零部件名称文本")]
    public void GenerateLabels()
    {
        ClearLabelsInternal(log: false);

        Transform[] targets = ResolveTargets();
        int created = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            Transform part = targets[i];
            if (part == null)
            {
                continue;
            }

            PartNameLabelBillboard label = part.gameObject.AddComponent<PartNameLabelBillboard>();
            label.ApplyStyleSettings(_characterSize, _fontSize, _fontStyle, _color);
            label.ApplyOffsetSettings(_labelOffset, _useBoundsTop);
            label.SetupFromOwnerName();
            created++;
        }

        Debug.Log($"[PartNameLabelGenerator] 已重置并生成 {created} 个标签。");
    }

    /// <summary>删除已生成的标签组件与 PartNameLabel 子物体。</summary>
    [ContextMenu("删除零部件名称文本")]
    public void ClearLabels()
    {
        ClearLabelsInternal(log: true);
    }

    /// <summary>批量开关零部件名称文字（仅 PartNameLabel，不动零件）。</summary>
    public void SetAllLabelsVisible(bool visible)
    {
        Transform[] targets = ResolveTargets();
        for (int i = 0; i < targets.Length; i++)
        {
            Transform part = targets[i];
            if (part == null)
            {
                continue;
            }

            PartNameLabelBillboard label = part.GetComponent<PartNameLabelBillboard>();
            if (label == null)
            {
                continue;
            }

            label.SetLabelVisible(visible);
        }
    }

    /// <summary>把当前样式参数应用到已存在的标签（不重新创建）。</summary>
    [ContextMenu("应用文字大小与粗细")]
    public void ApplyStyleToExistingLabels()
    {
        Transform[] targets = ResolveTargets();
        int updated = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            Transform part = targets[i];
            if (part == null)
            {
                continue;
            }

            PartNameLabelBillboard label = part.GetComponent<PartNameLabelBillboard>();
            if (label == null)
            {
                continue;
            }

            // 仅改样式，不重算高度
            label.ApplyStyleSettings(_characterSize, _fontSize, _fontStyle, _color);
            label.RefreshLabelTextOnly();
            updated++;
        }

        Debug.Log($"[PartNameLabelGenerator] 已更新 {updated} 个标签样式。");
    }

    private void ClearLabelsInternal(bool log)
    {
        Transform[] targets = ResolveTargets();
        int removed = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            Transform part = targets[i];
            if (part == null)
            {
                continue;
            }

            PartNameLabelBillboard[] labels = part.GetComponents<PartNameLabelBillboard>();
            for (int j = 0; j < labels.Length; j++)
            {
                if (labels[j] == null)
                {
                    continue;
                }

                labels[j].DestroyLabelObject();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(labels[j]);
                }
                else
#endif
                {
                    Object.Destroy(labels[j]);
                }

                removed++;
            }

            // 兜底：清掉残留的 PartNameLabel 子物体
            Transform leftover = part.Find("PartNameLabel");
            while (leftover != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(leftover.gameObject);
                }
                else
#endif
                {
                    Object.Destroy(leftover.gameObject);
                }

                leftover = part.Find("PartNameLabel");
            }
        }

        if (log)
        {
            Debug.Log($"[PartNameLabelGenerator] 已删除 {removed} 个标签组件。");
        }
    }

    private Transform[] ResolveTargets()
    {
        if (_partRoots != null && _partRoots.Length > 0)
        {
            return _partRoots;
        }

        int childCount = transform.childCount;
        Transform[] children = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            children[i] = transform.GetChild(i);
        }

        return children;
    }
}
