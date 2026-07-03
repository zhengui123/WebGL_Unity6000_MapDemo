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
    public const string DefaultPostHost = HttpProjectConfig.ApiHost;
    public const string DefaultPostPath = HttpProjectConfig.WorkOrderDisposalOverviewPath;
    public const string DefaultPostBody = ComprehensiveRegionRequest.DefaultJson;

    private static IReadOnlyList<(string Key, string Value)> DefaultHeaders => HttpProjectConfig.DefaultHeaders;

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

    [Header("二/三级界面")]
    [SerializeField] private GameObject _apiEntryMenuPanel;
    [SerializeField] private Button _customApiEntryButton;
    [SerializeField] private Button _vinLocationEntryButton;
    [SerializeField] private GameObject _formScrollViewRoot;
    [SerializeField] private GameObject _vinLocationScrollViewRoot;
    [SerializeField] private Button _customApiListBackButton;
    [SerializeField] private Button _vinLocationListBackButton;

    [Header("车辆位置接口")]
    [SerializeField] private InputField _vinStartTimeInput;
    [SerializeField] private InputField _vinEndTimeInput;
    [SerializeField] private InputField _vinProvinceInput;
    [SerializeField] private InputField _vinRegionInput;
    [SerializeField] private InputField _vinCountryInput;
    [SerializeField] private Button _vinLocationRequestButton;
    [SerializeField] private Button _vinLocationStopButton;

    private readonly List<HeaderRowEntry> _headerRows = new List<HeaderRowEntry>();
    private string _activeRequestMethod;

    private enum HttpApiTestViewLevel
    {
        EntryMenu = 0,
        CustomApi = 1,
        VinLocation = 2,
    }

    private HttpApiTestViewLevel _currentViewLevel = HttpApiTestViewLevel.EntryMenu;

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

        if (_customApiEntryButton != null)
        {
            _customApiEntryButton.onClick.AddListener(OnCustomApiEntryClicked);
        }

        if (_vinLocationEntryButton != null)
        {
            _vinLocationEntryButton.onClick.AddListener(OnVinLocationEntryClicked);
        }

        if (_customApiListBackButton != null)
        {
            _customApiListBackButton.onClick.AddListener(OnApiListBackClicked);
        }

        if (_vinLocationListBackButton != null)
        {
            _vinLocationListBackButton.onClick.AddListener(OnApiListBackClicked);
        }

        if (_vinLocationRequestButton != null)
        {
            _vinLocationRequestButton.onClick.AddListener(OnVinLocationRequestClicked);
        }

        if (_vinLocationStopButton != null)
        {
            _vinLocationStopButton.onClick.AddListener(OnStopButtonClicked);
        }
    }

    private void OnEnable()
    {
        EnsureThirdLevelViewLayouts();
        EnsureSubPanelReferences();
        ShowApiEntryMenu();
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
        EnsureSubPanelReferences();
        EnsureJsonResultUi();
        EnsureFallbackResponseLabelFitter();
        ApplyDefaultValues();
        InitializeDefaultHeaderRows();
        ShowApiEntryMenu();
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

        if (_customApiEntryButton != null)
        {
            _customApiEntryButton.onClick.RemoveListener(OnCustomApiEntryClicked);
        }

        if (_vinLocationEntryButton != null)
        {
            _vinLocationEntryButton.onClick.RemoveListener(OnVinLocationEntryClicked);
        }

        if (_customApiListBackButton != null)
        {
            _customApiListBackButton.onClick.RemoveListener(OnApiListBackClicked);
        }

        if (_vinLocationListBackButton != null)
        {
            _vinLocationListBackButton.onClick.RemoveListener(OnApiListBackClicked);
        }

        if (_vinLocationRequestButton != null)
        {
            _vinLocationRequestButton.onClick.RemoveListener(OnVinLocationRequestClicked);
        }

        if (_vinLocationStopButton != null)
        {
            _vinLocationStopButton.onClick.RemoveListener(OnStopButtonClicked);
        }
    }

    private void ApplyDefaultValues()
    {
        SetInputText(_getUrlInput, DefaultGetUrl);
        SetInputText(_postHostInput, DefaultPostHost);
        SetInputText(_postPathInput, DefaultPostPath);
        SetInputText(_postBodyInput, DefaultPostBody);
        SetInputText(_vinStartTimeInput, HttpProjectConfig.DefaultQueryStartTime);
        SetInputText(_vinEndTimeInput, BackendDateTimeTool.GetCurrentTimeString());
        SetInputText(_vinProvinceInput, string.Empty);
        SetInputText(_vinRegionInput, string.Empty);
        SetInputText(_vinCountryInput, string.Empty);
        SetJsonResultText("等待请求...");
    }

    private void InitializeDefaultHeaderRows()
    {
        ClearHeaderRows();
        for (int i = 0; i < DefaultHeaders.Count; i++)
        {
            (string key, string value) = DefaultHeaders[i];
            AddHeaderRow(key, value);
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

    private void OnCustomApiEntryClicked()
    {
        ShowCustomApiView();
    }

    private void OnVinLocationEntryClicked()
    {
        ShowVinLocationView();
    }

    private void OnApiListBackClicked()
    {
        ShowApiEntryMenu();
    }

    private void ShowApiEntryMenu()
    {
        _currentViewLevel = HttpApiTestViewLevel.EntryMenu;
        SetPanelActive(_apiEntryMenuPanel, true);
        SetPanelActive(_formScrollViewRoot, false);
        SetPanelActive(_vinLocationScrollViewRoot, false);
    }

    private void ShowCustomApiView()
    {
        _currentViewLevel = HttpApiTestViewLevel.CustomApi;
        SetPanelActive(_apiEntryMenuPanel, false);
        SetPanelActive(_formScrollViewRoot, true);
        SetPanelActive(_vinLocationScrollViewRoot, false);
    }

    private void ShowVinLocationView()
    {
        _currentViewLevel = HttpApiTestViewLevel.VinLocation;
        SetPanelActive(_apiEntryMenuPanel, false);
        SetPanelActive(_formScrollViewRoot, false);
        SetPanelActive(_vinLocationScrollViewRoot, true);
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    private void OnVinLocationRequestClicked()
    {
        string startTime = NormalizeOptionalParam(_vinStartTimeInput?.text);
        string endTime = NormalizeOptionalParam(_vinEndTimeInput?.text);
        if (string.IsNullOrEmpty(endTime))
        {
            endTime = BackendDateTimeTool.GetCurrentTimeString();
        }

        string province = NormalizeOptionalParam(_vinProvinceInput?.text);
        string region = NormalizeOptionalParam(_vinRegionInput?.text);
        string country = NormalizeOptionalParam(_vinCountryInput?.text);

        Dictionary<string, string> headers = HttpProjectConfig.MergeDefaultHeaders();

        _activeRequestMethod = "车辆位置";
        RefreshRequestButtons(true);
        SetJsonResultText(
            "车辆位置 请求中...\n" +
            $"url={HttpProjectConfig.BuildApiUrl(HttpProjectConfig.LatestVinLocationPath)}\n" +
            $"startTime={startTime}\n" +
            $"endTime={endTime}\n" +
            $"province={province}\n" +
            $"region={region}\n" +
            $"country={country}\n" +
            BuildHeadersPreviewText(headers));

        LatestVinLocationApi.RequestLatestVinLocations(
            province,
            region,
            country,
            startTime,
            endTime,
            OnVinLocationRequestCompleted,
            headers);
    }

    private void OnVinLocationRequestCompleted(HttpRequestResult result, LatestVinLocationResponse response)
    {
        _activeRequestMethod = null;
        RefreshRequestButtons(false);

        if (result == null)
        {
            SetJsonResultText("车辆位置 失败：结果为空。");
            return;
        }

        if (result.IsCancelled)
        {
            SetJsonResultText("车辆位置 已停止");
            return;
        }

        if (!result.IsSuccess)
        {
            string errorText = BuildVinLocationResultText(
                "车辆位置 失败",
                0,
                result.StatusCode,
                result.Error,
                result.RawBody);
            SetJsonResultText(errorText, result.RawBody);
            return;
        }

        if (response == null || !response.IsSuccess)
        {
            string bizError = response != null ? $"code={response.code}, msg={response.msg}" : "响应解析失败";
            string errorText = BuildVinLocationResultText(
                "车辆位置 业务失败",
                0,
                result.StatusCode,
                bizError,
                result.RawBody);
            SetJsonResultText(errorText, result.RawBody);
            return;
        }

        int vehicleCount = response.data != null ? response.data.Length : 0;
        string successText = BuildVinLocationResultText(
            "车辆位置 成功",
            vehicleCount,
            result.StatusCode,
            null,
            result.RawBody);
        SetJsonResultText(successText, result.RawBody);
    }

    private static string BuildVinLocationResultText(
        string title,
        int vehicleCount,
        long statusCode,
        string error,
        string rawBody)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine($"接收到车辆：{vehicleCount} 辆");
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

    private static string NormalizeOptionalParam(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string BuildHeadersPreviewText(Dictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return "请求头：(无)";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("请求头：");
        foreach (KeyValuePair<string, string> header in headers)
        {
            builder.AppendLine($"  {header.Key}: {header.Value}");
        }

        return builder.ToString().TrimEnd();
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
        SetJsonResultText($"GET 请求中...\n{url}\n{BuildHeadersPreviewText(headers)}");

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
        string jsonBody = BuildPostJsonBody();

        _activeRequestMethod = "POST";
        RefreshRequestButtons(true);
        SetJsonResultText(
            $"POST 请求中...\n{url}\n\n{BuildHeadersPreviewText(headers)}\n\n提交 JSON：\n{jsonBody}");

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

    /// <summary>读取 POST 请求参数 JSON；输入框留空时使用 <see cref="DefaultPostBody"/>。</summary>
    private string BuildPostJsonBody()
    {
        string customBody = _postBodyInput != null ? _postBodyInput.text.Trim() : string.Empty;
        if (!string.IsNullOrEmpty(customBody))
        {
            return customBody;
        }

        return DefaultPostBody;
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
        Dictionary<string, string> uiHeaders = new Dictionary<string, string>();
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
            uiHeaders[key] = value;
        }

        return HttpProjectConfig.MergeDefaultHeaders(uiHeaders);
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

        if (_vinLocationRequestButton != null)
        {
            _vinLocationRequestButton.interactable = !isRequesting;
        }

        if (_vinLocationStopButton != null)
        {
            _vinLocationStopButton.interactable = isRequesting;
        }

        if (_customApiEntryButton != null)
        {
            _customApiEntryButton.interactable = !isRequesting;
        }

        if (_vinLocationEntryButton != null)
        {
            _vinLocationEntryButton.interactable = !isRequesting;
        }

        if (_customApiListBackButton != null)
        {
            _customApiListBackButton.interactable = !isRequesting;
        }

        if (_vinLocationListBackButton != null)
        {
            _vinLocationListBackButton.interactable = !isRequesting;
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

    private void EnsureThirdLevelViewLayouts()
    {
        EnsureSubPanelReferences();
        ApplyThirdLevelLayoutIfNeeded(_formScrollViewRoot);
        ApplyThirdLevelLayoutIfNeeded(_vinLocationScrollViewRoot);
    }

    private void ApplyThirdLevelLayoutIfNeeded(GameObject scrollViewRoot)
    {
        if (scrollViewRoot == null)
        {
            return;
        }

        RectTransform rect = scrollViewRoot.GetComponent<RectTransform>();
        if (rect == null || HttpApiTestPanelLayout.IsValidRect(rect))
        {
            return;
        }

        HttpApiTestPanelLayout layout = GetComponent<HttpApiTestPanelLayout>();
        if (layout != null)
        {
            layout.ApplyTo(rect);
        }
        else
        {
            HttpApiTestPanelLayout.ApplyDefaultFormScrollViewLayout(rect);
        }
    }

    private void EnsureSubPanelReferences()
    {
        if (_apiEntryMenuPanel == null)
        {
            Transform entry = transform.Find("ApiEntryMenuPanel");
            if (entry != null)
            {
                _apiEntryMenuPanel = entry.gameObject;
            }
        }

        if (_formScrollViewRoot == null)
        {
            Transform formScroll = transform.Find("FormScrollView");
            if (formScroll != null)
            {
                _formScrollViewRoot = formScroll.gameObject;
            }
        }

        if (_vinLocationScrollViewRoot == null)
        {
            Transform vinScroll = transform.Find("VinLocationScrollView");
            if (vinScroll != null)
            {
                _vinLocationScrollViewRoot = vinScroll.gameObject;
            }
        }

        if (_customApiEntryButton == null)
        {
            Transform button = transform.Find("ApiEntryMenuPanel/CustomApiEntryButton");
            if (button == null)
            {
                button = transform.Find("CustomApiEntryButton");
            }

            if (button != null)
            {
                _customApiEntryButton = button.GetComponent<Button>();
            }
        }

        if (_vinLocationEntryButton == null)
        {
            Transform button = transform.Find("ApiEntryMenuPanel/VinLocationEntryButton");
            if (button == null)
            {
                button = transform.Find("VinLocationEntryButton");
            }

            if (button != null)
            {
                _vinLocationEntryButton = button.GetComponent<Button>();
            }
        }

        if (_customApiListBackButton == null && _formScrollViewRoot != null)
        {
            Transform button = _formScrollViewRoot.transform.Find("Viewport/Content/CustomApiListBackButton");
            if (button != null)
            {
                _customApiListBackButton = button.GetComponent<Button>();
            }
        }

        if (_vinLocationListBackButton == null && _vinLocationScrollViewRoot != null)
        {
            Transform button = _vinLocationScrollViewRoot.transform.Find("Viewport/Content/VinLocationListBackButton");
            if (button != null)
            {
                _vinLocationListBackButton = button.GetComponent<Button>();
            }
        }

        if (_formScrollContent == null && _formScrollViewRoot != null)
        {
            Transform content = _formScrollViewRoot.transform.Find("Viewport/Content");
            if (content != null)
            {
                _formScrollContent = content.GetComponent<RectTransform>();
            }
        }
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
