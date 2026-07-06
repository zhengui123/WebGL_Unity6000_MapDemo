using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 车辆安全事态消息面板：标题、防护状态、异常事件列表（最多 5 条）。
/// 挂载于 Canvas/CarPanel/CarImg，子物体命名：TitleText、StateText、Icon、MessageText。
/// </summary>
[DisallowMultipleComponent]
public class MessageListPanel : MonoBehaviour
{
    public const int MaxMessageCount = 5;

    [Header("文本")]
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _stateText;

    [Header("异常事件（留空则自动查找子物体 MessageText）")]
    [SerializeField] private Text[] _messageTexts;

    [Header("防护状态图例（贴图未就绪时可留空）")]
    [SerializeField] private Image _stateIcon;
    [SerializeField] private Sprite _protectedIcon;
    [SerializeField] private Sprite _unprotectedIcon;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    /// <summary>
    /// 设置面板内容。
    /// </summary>
    /// <param name="title">标题，写入 TitleText。</param>
    /// <param name="protectionState">防护状态，写入 StateText 并刷新 Icon。</param>
    /// <param name="abnormalEvents">异常事件列表，超过 5 条仅显示前 5 条。</param>
    public void SetMessageList(string title, ProtectionStateType protectionState, IList<string> abnormalEvents)
    {
        EnsureReferences();

        if (_titleText != null)
        {
            _titleText.text = title ?? string.Empty;
        }

        if (_stateText != null)
        {
            _stateText.text = ProtectionStateTypeExtensions.ToDisplayText(protectionState);
        }

        ApplyStateIcon(protectionState);
        ApplyMessageTexts(abnormalEvents);
    }

    /// <summary>运行时替换防护状态图例（贴图资源就绪后调用）。</summary>
    public void SetStateIcons(Sprite protectedIcon, Sprite unprotectedIcon)
    {
        _protectedIcon = protectedIcon;
        _unprotectedIcon = unprotectedIcon;
    }

    private void EnsureReferences()
    {
        if (_titleText == null)
        {
            _titleText = FindChildText("TitleText");
        }

        if (_stateText == null)
        {
            _stateText = FindChildText("StateText");
        }

        if (_stateIcon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
            {
                _stateIcon = iconTransform.GetComponent<Image>();
            }
        }

        if (_messageTexts == null || _messageTexts.Length == 0)
        {
            _messageTexts = CollectMessageTexts();
        }
    }

    private Text FindChildText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private Text[] CollectMessageTexts()
    {
        List<Text> results = new List<Text>(MaxMessageCount);
        CollectMessageTextsRecursive(transform, results);
        results.Sort(CompareMessageTextSiblingIndex);
        return results.ToArray();
    }

    private static void CollectMessageTextsRecursive(Transform parent, List<Text> results)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (IsMessageTextNode(child.name))
            {
                Text text = child.GetComponent<Text>();
                if (text != null)
                {
                    results.Add(text);
                }
            }

            if (child.childCount > 0)
            {
                CollectMessageTextsRecursive(child, results);
            }
        }
    }

    private static bool IsMessageTextNode(string nodeName)
    {
        return nodeName == "MessageText" || nodeName.StartsWith("MessageText ");
    }

    private static int CompareMessageTextSiblingIndex(Text left, Text right)
    {
        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }

    private void ApplyStateIcon(ProtectionStateType protectionState)
    {
        if (_stateIcon == null)
        {
            return;
        }

        Sprite sprite = protectionState == ProtectionStateType.Protected
            ? _protectedIcon
            : _unprotectedIcon;

        if (sprite != null)
        {
            _stateIcon.sprite = sprite;
            _stateIcon.enabled = true;
            return;
        }

        // 贴图未配置时保留 Image 节点，仅隐藏显示。
        _stateIcon.enabled = false;
    }

    private void ApplyMessageTexts(IList<string> abnormalEvents)
    {
        if (_messageTexts == null || _messageTexts.Length == 0)
        {
            return;
        }

        int eventCount = abnormalEvents != null ? abnormalEvents.Count : 0;
        int displayCount = Mathf.Min(eventCount, MaxMessageCount);

        if (_messageTexts.Length == 1)
        {
            ApplySingleMessageText(_messageTexts[0], abnormalEvents, displayCount);
            return;
        }

        for (int i = 0; i < _messageTexts.Length; i++)
        {
            Text messageText = _messageTexts[i];
            if (messageText == null)
            {
                continue;
            }

            bool visible = i < displayCount;
            messageText.gameObject.SetActive(visible);
            if (!visible)
            {
                messageText.text = string.Empty;
                continue;
            }

            messageText.text = abnormalEvents[i] ?? string.Empty;
        }
    }

    private static void ApplySingleMessageText(Text messageText, IList<string> abnormalEvents, int displayCount)
    {
        if (messageText == null)
        {
            return;
        }

        messageText.gameObject.SetActive(true);
        if (displayCount <= 0)
        {
            messageText.text = string.Empty;
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < displayCount; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.Append(abnormalEvents[i] ?? string.Empty);
        }

        messageText.text = builder.ToString();
    }
}

/// <summary>防护状态类型。</summary>
public enum ProtectionStateType
{
    /// <summary>已防护</summary>
    Protected = 0,

    /// <summary>未防护</summary>
    Unprotected = 1,
}

/// <summary><see cref="ProtectionStateType"/> 显示文案扩展。</summary>
public static class ProtectionStateTypeExtensions
{
    public static string ToDisplayText(this ProtectionStateType state)
    {
        switch (state)
        {
            case ProtectionStateType.Protected:
                return "已防护";
            case ProtectionStateType.Unprotected:
                return "未防护";
            default:
                return string.Empty;
        }
    }
}
