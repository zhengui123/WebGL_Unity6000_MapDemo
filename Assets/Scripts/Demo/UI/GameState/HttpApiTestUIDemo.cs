using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo：HTTP GET / POST 接口调用测试（可配置地址与自定义请求头）。
/// </summary>
[DisallowMultipleComponent]
public class HttpApiTestUIDemo : MonoBehaviour
{
    public const string DefaultGetUrl = "https://jsonplaceholder.typicode.com/todos/1";
    public const string DefaultPostHost = "10.60.16.96:38000";
    public const string DefaultPostPath = "/business/bigScreen/comprehensivePosture/workOrderDisposalOverview";
    public const string DefaultPostBody = "";

    private static readonly (string Key, string Value)[] DefaultHeaders =
    {
        ("Satoken", "r5aP7flTO3wHSf9MHxEwAZ35GdSxDM4cu89axMdKKLOxZtfXBQQRgjLI1oRTOicc"),
        ("X-Tenant-Id", "1"),
        ("Sys-Lang", "zh-CN"),
    };

    [Header("GET")]
    [SerializeField] private InputField _getUrlInput;
    [SerializeField] private Button _getButton;

    [Header("POST")]
    [SerializeField] private InputField _postHostInput;
    [SerializeField] private InputField _postPathInput;
    [SerializeField] private InputField _postBodyInput;
    [SerializeField] private RectTransform _headerRowsContainer;
    [SerializeField] private Button _addHeaderButton;
    [SerializeField] private Button _postButton;
    [SerializeField] private Button _stopButton;
    [SerializeField] private GameObject _headerRowTemplate;

    [Header("响应")]
    [SerializeField] private GameObject _jsonResultBarRoot;
    [SerializeField] private Text _jsonResultText;
    [SerializeField] private ScrollRect _jsonResultScroll;
    [SerializeField] private RectTransform _jsonResultContent;
    [SerializeField] private GameObject _jsonCopyBarRoot;
    [SerializeField] private InputField _jsonCopyInputField;
    [SerializeField] private Text _fallbackResponseLabel;
    [SerializeField] private RectTransform _formScrollContent;
    [SerializeField] private float _formScrollBaseHeight;
    [SerializeField] private Button _backButton;
    [SerializeField] private DemoGameStateUINavigator _navigator;

    private readonly List<HeaderRowEntry> _headerRows = new List<HeaderRowEntry>();
    private string _activeRequestMethod;

    private const int HeaderInputFontSize = 10;
    private const float ResponseLabelInitialHeight = 120f;

    private class HeaderRowEntry
    {
        public GameObject Root;
        public Toggle EnabledToggle;
        public InputField KeyInput;
        public InputField ValueInput;
        public Button DeleteButton;
    }

    private void Awake()
    {
        if (_getButton != null)
        {
            _getButton.onClick.AddListener(OnGetButtonClicked);
        }

        if (_postButton != null)
        {
            _postButton.onClick.AddListener(OnPostButtonClicked);
        }

        if (_stopButton != null)
        {
            _stopButton.onClick.AddListener(OnStopButtonClicked);
        }

        if (_addHeaderButton != null)
        {
            _addHeaderButton.onClick.AddListener(OnAddHeaderButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnEnable()
    {
        EnsureJsonResultUi();
        if (_jsonResultBarRoot != null)
        {
            _jsonResultBarRoot.SetActive(true);
        }

        if (_jsonCopyBarRoot != null)
        {
            _jsonCopyBarRoot.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (_jsonResultBarRoot != null)
        {
            _jsonResultBarRoot.SetActive(false);
        }

        if (_jsonCopyBarRoot != null)
        {
            _jsonCopyBarRoot.SetActive(false);
        }
    }

    private void Start()
    {
        EnsureJsonResultUi();
        EnsureFallbackResponseLabelFitter();
        ApplyDefaultValues();
        InitializeDefaultHeaderRows();
        RefreshRequestButtons(HttpService.Instance != null && HttpService.Instance.IsRequestInProgress);
    }

    private void OnDestroy()
    {
        if (_getButton != null)
        {
            _getButton.onClick.RemoveListener(OnGetButtonClicked);
        }

        if (_postButton != null)
        {
            _postButton.onClick.RemoveListener(OnPostButtonClicked);
        }

        if (_stopButton != null)
        {
            _stopButton.onClick.RemoveListener(OnStopButtonClicked);
        }

        if (_addHeaderButton != null)
        {
            _addHeaderButton.onClick.RemoveListener(OnAddHeaderButtonClicked);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }
    }

    private void ApplyDefaultValues()
    {
        SetInputText(_getUrlInput, DefaultGetUrl);
        SetInputText(_postHostInput, DefaultPostHost);
        SetInputText(_postPathInput, DefaultPostPath);
        SetInputText(_postBodyInput, DefaultPostBody);
        SetJsonResultText("等待请求...");
    }

    private void InitializeDefaultHeaderRows()
    {
        ClearHeaderRows();
        for (int i = 0; i < DefaultHeaders.Length; i++)
        {
            AddHeaderRow(DefaultHeaders[i].Key, DefaultHeaders[i].Value);
        }
    }

    private void OnAddHeaderButtonClicked()
    {
        AddHeaderRow(string.Empty, string.Empty);
    }

    private void AddHeaderRow(string key, string value)
    {
        if (_headerRowsContainer == null)
        {
            return;
        }

        HeaderRowEntry row = CreateHeaderRowEntry();
        SetInputText(row.KeyInput, key);
        SetInputText(row.ValueInput, value);
        if (row.EnabledToggle != null)
        {
            row.EnabledToggle.isOn = true;
        }

        _headerRows.Add(row);
    }

    private HeaderRowEntry CreateHeaderRowEntry()
    {
        if (_headerRowTemplate == null)
        {
            return CreateFallbackHeaderRowEntry();
        }

        GameObject rowGo = Instantiate(_headerRowTemplate, _headerRowsContainer);
        rowGo.name = $"HeaderRow_{_headerRowsContainer.childCount}";
        rowGo.SetActive(true);

        HeaderRowEntry row = new HeaderRowEntry
        {
            Root = rowGo,
            EnabledToggle = rowGo.transform.Find("EnableToggle")?.GetComponent<Toggle>(),
            KeyInput = rowGo.transform.Find("KeyInput")?.GetComponent<InputField>(),
            ValueInput = rowGo.transform.Find("ValueInput")?.GetComponent<InputField>(),
            DeleteButton = rowGo.transform.Find("DeleteButton")?.GetComponent<Button>(),
        };

        if (row.DeleteButton != null)
        {
            row.DeleteButton.onClick.AddListener(() => RemoveHeaderRow(row));
        }

        ApplyHeaderInputFontSize(row.KeyInput);
        ApplyHeaderInputFontSize(row.ValueInput);
        return row;
    }

    private HeaderRowEntry CreateFallbackHeaderRowEntry()
    {
        GameObject rowGo = new GameObject($"HeaderRow_{_headerRows.Count}", typeof(RectTransform));
        rowGo.transform.SetParent(_headerRowsContainer, false);

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 32f);

        InputField template = _postHostInput != null ? _postHostInput : GetComponentInChildren<InputField>(true);
        InputField keyInput = CloneHeaderInput(template, rowGo.transform, "KeyInput", 28f, 84f);
        InputField valueInput = CloneHeaderInput(template, rowGo.transform, "ValueInput", 118f, 150f);

        HeaderRowEntry row = new HeaderRowEntry
        {
            Root = rowGo,
            EnabledToggle = null,
            KeyInput = keyInput,
            ValueInput = valueInput,
        };
        row.DeleteButton = CreateDeleteButton(rowGo.transform, () => RemoveHeaderRow(row));
        return row;
    }

    private void RemoveHeaderRow(HeaderRowEntry row)
    {
        if (row == null)
        {
            return;
        }

        if (row.DeleteButton != null)
        {
            row.DeleteButton.onClick.RemoveAllListeners();
        }

        _headerRows.Remove(row);
        if (row.Root != null)
        {
            Destroy(row.Root);
        }
    }

    private static Button CreateDeleteButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonGo = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(40f, 28f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textGo.GetComponent<Text>();
        label.text = "删除";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.4f, 0.75f, 1f);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 12;
        label.raycastTarget = false;

        Button button = buttonGo.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        return button;
    }

    private void OnGetButtonClicked()
    {
        string url = _getUrlInput != null ? _getUrlInput.text.Trim() : DefaultGetUrl;
        if (string.IsNullOrEmpty(url))
        {
            SetJsonResultText("GET 失败：URL 为空。");
            return;
        }

        Dictionary<string, string> headers = CollectHeaders();
        _activeRequestMethod = "GET";
        RefreshRequestButtons(true);
        SetJsonResultText($"GET 请求中...\n{url}");

        HttpService.Instance.Get(url, result => OnRequestCompleted("GET", result), headers);
    }

    private void OnPostButtonClicked()
    {
        if (!TryBuildPostUrl(out string url, out string buildError))
        {
            SetJsonResultText($"POST 失败：{buildError}");
            return;
        }

        Dictionary<string, string> headers = CollectHeaders();
        string jsonBody = BuildPostJsonBody(headers);

        _activeRequestMethod = "POST";
        RefreshRequestButtons(true);
        SetJsonResultText($"POST 请求中...\n{url}\n\n提交 JSON：\n{jsonBody}");

        HttpService.Instance.Post(url, jsonBody, result => OnRequestCompleted("POST", result), headers);
    }

    private void OnStopButtonClicked()
    {
        string method = _activeRequestMethod;
        if (HttpService.Instance.IsRequestInProgress)
        {
            HttpService.Instance.StopCurrentRequest();
        }

        _activeRequestMethod = null;
        RefreshRequestButtons(false);

        if (!string.IsNullOrEmpty(method))
        {
            SetJsonResultText($"{method} 已停止");
            Debug.Log($"[HttpApiTestUIDemo] {method} 已停止。");
        }
    }

    private string BuildPostJsonBody(Dictionary<string, string> headers)
    {
        string customBody = _postBodyInput != null ? _postBodyInput.text.Trim() : string.Empty;
        if (!string.IsNullOrEmpty(customBody))
        {
            return customBody;
        }

        return HttpHeadersJson.ToJsonObject(headers);
    }

    private void OnRequestCompleted(string method, HttpRequestResult result)
    {
        _activeRequestMethod = null;
        RefreshRequestButtons(false);

        if (result == null)
        {
            SetJsonResultText($"{method} 失败：结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            SetJsonResultText($"{method} 已停止");
            Debug.Log($"[HttpApiTestUIDemo] {method} 已停止。");
            return;
        }

        if (!result.IsSuccess)
        {
            string errorText = BuildResultDisplayText(
                $"{method} 失败",
                result.StatusCode,
                result.Error,
                result.RawBody);
            SetJsonResultText(errorText, result.RawBody);
            Debug.LogError($"[HttpApiTestUIDemo] {method} 失败：{result.Error}\n{result.RawBody}");
            return;
        }

        string successText = BuildResultDisplayText(
            $"{method} 成功",
            result.StatusCode,
            null,
            result.RawBody);
        SetJsonResultText(successText, result.RawBody);
        Debug.Log($"[HttpApiTestUIDemo] {method} 成功，状态码={result.StatusCode}");

        if (method == "GET")
        {
            TryLogParsedGetResponse(result.RawBody);
        }
    }

    private static string BuildResultDisplayText(string title, long statusCode, string error, string rawBody)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine($"状态码：{statusCode}");
        if (!string.IsNullOrEmpty(error))
        {
            builder.AppendLine($"错误：{error}");
        }

        builder.AppendLine();
        builder.AppendLine("JSON：");
        builder.Append(string.IsNullOrEmpty(rawBody) ? "(空)" : rawBody);

        return builder.ToString();
    }

    private static void TryLogParsedGetResponse(string rawBody)
    {
        if (!HttpJsonParser.TryParse(rawBody, out JsonPlaceholderTodoData data, out string parseError))
        {
            Debug.LogWarning($"[HttpApiTestUIDemo] GET 响应未能解析为 JsonPlaceholderTodoData：{parseError}");
            return;
        }

        Debug.Log(
            $"[HttpApiTestUIDemo] GET 解析 → userId={data.userId}, id={data.id}, " +
            $"title=\"{data.title}\", completed={data.completed}");
    }

    private bool TryBuildPostUrl(out string url, out string errorMessage)
    {
        url = null;
        errorMessage = null;

        string host = _postHostInput != null ? _postHostInput.text.Trim() : DefaultPostHost;
        string path = _postPathInput != null ? _postPathInput.text.Trim() : DefaultPostPath;

        if (string.IsNullOrEmpty(host))
        {
            errorMessage = "POST 主机地址为空。";
            return false;
        }

        if (string.IsNullOrEmpty(path))
        {
            errorMessage = "POST 路径为空。";
            return false;
        }

        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        url = host.Contains("://") ? $"{host}{path}" : $"http://{host}{path}";
        return true;
    }

    private Dictionary<string, string> CollectHeaders()
    {
        Dictionary<string, string> headers = new Dictionary<string, string>();
        for (int i = 0; i < _headerRows.Count; i++)
        {
            HeaderRowEntry row = _headerRows[i];
            if (row.EnabledToggle != null && !row.EnabledToggle.isOn)
            {
                continue;
            }

            if (row.KeyInput == null)
            {
                continue;
            }

            string key = row.KeyInput.text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            string value = row.ValueInput != null ? row.ValueInput.text : string.Empty;
            headers[key] = value;
        }

        return headers;
    }

    private static InputField CloneHeaderInput(InputField template, Transform parent, string name, float x, float width)
    {
        if (template == null)
        {
            GameObject fallback = new GameObject(name, typeof(RectTransform));
            fallback.transform.SetParent(parent, false);
            return fallback.AddComponent<InputField>();
        }

        InputField clone = Instantiate(template, parent);
        clone.name = name;
        clone.text = string.Empty;

        RectTransform rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 28f);

        if (clone.placeholder is Text placeholder)
        {
            placeholder.text = name == "KeyInput" ? "请求头" : "内容";
        }

        ApplyHeaderInputFontSize(clone);
        return clone;
    }

    private static void ApplyHeaderInputFontSize(InputField inputField)
    {
        if (inputField == null)
        {
            return;
        }

        if (inputField.textComponent != null)
        {
            inputField.textComponent.fontSize = HeaderInputFontSize;
        }

        if (inputField.placeholder is Text placeholderText)
        {
            placeholderText.fontSize = HeaderInputFontSize;
        }
    }

    private void ClearHeaderRows()
    {
        for (int i = _headerRows.Count - 1; i >= 0; i--)
        {
            RemoveHeaderRow(_headerRows[i]);
        }

        if (_headerRowsContainer == null)
        {
            return;
        }

        for (int i = _headerRowsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = _headerRowsContainer.GetChild(i);
            if (_headerRowTemplate != null && child.gameObject == _headerRowTemplate)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void RefreshRequestButtons(bool isRequesting)
    {
        if (_getButton != null)
        {
            _getButton.interactable = !isRequesting;
        }

        if (_postButton != null)
        {
            _postButton.interactable = !isRequesting;
        }

        if (_stopButton != null)
        {
            _stopButton.interactable = isRequesting;
        }

        if (_addHeaderButton != null)
        {
            _addHeaderButton.interactable = !isRequesting;
        }
    }

    private void SetJsonResultText(string text, string rawJson = null)
    {
        EnsureJsonResultUi();

        string display = text ?? string.Empty;
        if (!string.IsNullOrEmpty(rawJson))
        {
            display = AppendRawJsonSection(display, rawJson);
        }

        if (_fallbackResponseLabel != null)
        {
            _fallbackResponseLabel.text = display;
            RefreshFallbackResponseLayout();
        }

        if (_jsonResultText != null)
        {
            _jsonResultText.text = display;
            RefreshJsonResultLayout();
        }

        if (_jsonCopyInputField != null)
        {
            _jsonCopyInputField.text = display;
            _jsonCopyInputField.MoveTextEnd(false);
        }
        else if (_jsonResultText == null && _fallbackResponseLabel == null)
        {
            Debug.Log($"[HttpApiTestUIDemo] 响应结果:\n{display}");
        }
    }

    private static string AppendRawJsonSection(string display, string rawJson)
    {
        if (string.IsNullOrEmpty(rawJson))
        {
            return display;
        }

        if (!string.IsNullOrEmpty(display) && display.Contains(rawJson))
        {
            return display;
        }

        StringBuilder builder = new StringBuilder(display ?? string.Empty);
        if (builder.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
        }

        builder.AppendLine("JSON：");
        builder.Append(rawJson);
        return builder.ToString();
    }

    private void EnsureJsonResultUi()
    {
        Transform uiRoot = transform.parent;
        if (uiRoot == null)
        {
            return;
        }

        if (_jsonResultBarRoot == null)
        {
            Transform barTransform = uiRoot.Find("HttpJsonResultBar");
            if (barTransform != null)
            {
                _jsonResultBarRoot = barTransform.gameObject;
            }
        }

        if (_jsonResultScroll == null && _jsonResultBarRoot != null)
        {
            Transform scrollTransform = _jsonResultBarRoot.transform.Find("JsonResultScrollView");
            _jsonResultScroll = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;
        }

        if (_jsonResultContent == null && _jsonResultScroll != null)
        {
            _jsonResultContent = _jsonResultScroll.content;
        }

        if (_jsonResultText == null && _jsonResultContent != null)
        {
            Transform textTransform = _jsonResultContent.Find("JsonResultText");
            _jsonResultText = textTransform != null ? textTransform.GetComponent<Text>() : null;
        }

        if (_jsonCopyBarRoot == null)
        {
            Transform copyBar = uiRoot.Find("HttpJsonCopyBar");
            if (copyBar != null)
            {
                _jsonCopyBarRoot = copyBar.gameObject;
            }
        }

        if (_jsonCopyInputField == null && _jsonCopyBarRoot != null)
        {
            Transform inputTransform = _jsonCopyBarRoot.transform.Find("JsonCopyInputField");
            if (inputTransform != null)
            {
                _jsonCopyInputField = inputTransform.GetComponent<InputField>();
            }
        }

        if (_fallbackResponseLabel == null)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == "ResponseLabel")
                {
                    _fallbackResponseLabel = texts[i];
                    break;
                }
            }
        }

        if (_formScrollContent == null)
        {
            Transform formScroll = transform.Find("FormScrollView");
            if (formScroll != null)
            {
                Transform viewport = formScroll.Find("Viewport");
                if (viewport != null)
                {
                    Transform content = viewport.Find("Content");
                    if (content != null)
                    {
                        _formScrollContent = content.GetComponent<RectTransform>();
                    }
                }
            }
        }

        if (_formScrollBaseHeight <= 0f && _formScrollContent != null)
        {
            _formScrollBaseHeight = _formScrollContent.sizeDelta.y;
        }
    }

    private void EnsureFallbackResponseLabelFitter()
    {
        if (_fallbackResponseLabel == null)
        {
            return;
        }

        if (_fallbackResponseLabel.GetComponent<ContentSizeFitter>() == null)
        {
            ContentSizeFitter fitter = _fallbackResponseLabel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        _fallbackResponseLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        _fallbackResponseLabel.verticalOverflow = VerticalWrapMode.Overflow;
        _fallbackResponseLabel.alignment = TextAnchor.UpperLeft;
    }

    private void RefreshFallbackResponseLayout()
    {
        if (_fallbackResponseLabel == null)
        {
            return;
        }

        RectTransform textRect = _fallbackResponseLabel.rectTransform;
        float labelWidth = textRect.parent is RectTransform parentRect
            ? parentRect.rect.width
            : PanelWidthFallback();
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(100f, labelWidth));

        float preferredHeight = LayoutUtility.GetPreferredHeight(textRect);
        if (preferredHeight < ResponseLabelInitialHeight * 0.25f)
        {
            preferredHeight = _fallbackResponseLabel.preferredHeight;
        }

        textRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(32f, preferredHeight));

        if (_formScrollContent == null)
        {
            return;
        }

        float baseHeight = _formScrollBaseHeight > 0f
            ? _formScrollBaseHeight
            : _formScrollContent.sizeDelta.y;
        float responseHeight = Mathf.Max(ResponseLabelInitialHeight, preferredHeight + 8f);
        float scrollHeight = baseHeight - ResponseLabelInitialHeight + responseHeight;
        _formScrollContent.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(baseHeight, scrollHeight));
        LayoutRebuilder.ForceRebuildLayoutImmediate(_formScrollContent);
    }

    private static float PanelWidthFallback()
    {
        return 336f;
    }

    private void RefreshJsonResultLayout()
    {
        if (_jsonResultText == null)
        {
            return;
        }

        RectTransform textRect = _jsonResultText.rectTransform;
        float contentWidth = ResolveJsonViewportWidth();
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(100f, contentWidth - 16f));

        float preferredHeight = LayoutUtility.GetPreferredHeight(textRect);
        if (preferredHeight < 32f)
        {
            preferredHeight = _jsonResultText.preferredHeight;
        }

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(32f, preferredHeight));

        if (_jsonResultContent != null)
        {
            _jsonResultContent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(32f, preferredHeight + 8f));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_jsonResultContent);
        }

        if (_jsonResultScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            _jsonResultScroll.verticalNormalizedPosition = 1f;
        }
    }

    private float ResolveJsonViewportWidth()
    {
        if (_jsonResultScroll != null && _jsonResultScroll.viewport != null)
        {
            return _jsonResultScroll.viewport.rect.width;
        }

        if (_jsonResultContent != null)
        {
            return _jsonResultContent.rect.width;
        }

        return 800f;
    }

    private static void SetInputText(InputField input, string text)
    {
        if (input != null)
        {
            input.text = text;
        }
    }

    private void OnBackButtonClicked()
    {
        DemoGameStateUINavigator navigator = ResolveNavigator();
        if (navigator == null)
        {
            Debug.LogWarning("[HttpApiTestUIDemo] 未找到 DemoGameStateUINavigator。");
            return;
        }

        navigator.ShowMenu();
    }

    private DemoGameStateUINavigator ResolveNavigator()
    {
        if (_navigator != null)
        {
            return _navigator;
        }

        return GetComponentInParent<DemoGameStateUINavigator>();
    }
}
