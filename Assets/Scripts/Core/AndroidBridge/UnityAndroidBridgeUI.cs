using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时创建三个按钮，分别调用 Android 三种方法。
/// 挂在 AndroidBridge 上，与 AndroidMessage 配合使用。
/// </summary>
[RequireComponent(typeof(AndroidMessage))]
public class UnityAndroidBridgeUI : MonoBehaviour
{
    [SerializeField] private string statusPrefix = "Unity 状态: ";

    private Text statusText;

    private void Start()
    {
        EnsureEventSystem();
        BuildUi();
        if (AndroidMessage.Instance != null)
            AndroidMessage.Instance.OnAndroidMessageReceived += OnAndroidReply;
    }

    private void OnDestroy()
    {
        if (AndroidMessage.Instance != null)
            AndroidMessage.Instance.OnAndroidMessageReceived -= OnAndroidReply;
    }

    private void OnAndroidReply(string msg)
    {
        if (statusText != null)
            statusText.text = statusPrefix + msg;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("UnityBridgeCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = CreatePanel(canvasGo.transform);

        CreateButton(panel, "Unity→Toast", new Vector2(0, 80), () =>
        {
            AndroidMessage.Instance.CallAndroidShowToast("来自 Unity 的 Toast");
            SetStatus("已请求 Android 显示 Toast");
        });

        CreateButton(panel, "Unity→改原生标题", new Vector2(0, 0), () =>
        {
            AndroidMessage.Instance.CallAndroidUpdateNativeTitle("Unity 已更新原生标题 " + System.DateTime.Now.ToString("HH:mm:ss"));
            SetStatus("已请求 Android 更新标题");
        });

        CreateButton(panel, "Unity→同步回传", new Vector2(0, -80), () =>
        {
            string payload = "{\"from\":\"unity\",\"value\":" + Random.Range(1, 100) + "}";
            AndroidMessage.Instance.CallAndroidRequestDataSync(payload);
            SetStatus("已请求 Android 同步并回传");
        });

        var statusGo = new GameObject("StatusText");
        statusGo.transform.SetParent(panel, false);
        statusText = statusGo.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 18;
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.MiddleCenter;
        var rt = statusText.rectTransform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(-20, 60);
        rt.anchoredPosition = new Vector2(0, -160);
        statusText.text = statusPrefix + "等待操作";
    }

    private RectTransform CreatePanel(Transform parent)
    {
        var go = new GameObject("ButtonPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(280, 320);
        rt.anchoredPosition = new Vector2(-20, -20);
        var img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.55f);
        return rt;
    }

    private void CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240, 48);
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.45f, 0.85f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = statusPrefix + msg;
    }
}
