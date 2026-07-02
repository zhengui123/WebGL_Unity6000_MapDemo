using UnityEngine;

/// <summary>
/// 演示 <see cref="HttpService"/> GET 请求并解析 JSON。
/// 测试接口：<see cref="DefaultTodoUrl"/>
/// </summary>
[DisallowMultipleComponent]
public class HttpServiceDemo : MonoBehaviour
{
    private const string DefaultTodoUrl = "https://jsonplaceholder.typicode.com/todos/1";

    [Header("请求配置")]
    [SerializeField] private string _url = DefaultTodoUrl;
    [SerializeField] private bool _requestOnStart = true;

    [Header("快捷键")]
    [SerializeField] private KeyCode _requestKey = KeyCode.H;
    [SerializeField] private KeyCode _stopKey = KeyCode.S;

    private void Start()
    {
        if (_requestOnStart)
        {
            RequestTodo();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_requestKey))
        {
            RequestTodo();
        }

        if (Input.GetKeyDown(_stopKey))
        {
            StopRequest();
        }
    }

    [ContextMenu("停止当前请求")]
    public void StopRequest()
    {
        HttpService.Instance.StopCurrentRequest();
    }

    [ContextMenu("请求 JsonPlaceholder Todo")]
    public void RequestTodo()
    {
        Debug.Log($"[HttpServiceDemo] 开始请求：{_url}");

        HttpService.Instance.Get<JsonPlaceholderTodoData>(_url, OnTodoResponse);
    }

    private void OnTodoResponse(HttpRequestResult result, JsonPlaceholderTodoData data)
    {
        if (result.IsCancelled)
        {
            Debug.Log("[HttpServiceDemo] 请求已停止。");
            return;
        }

        if (!result.IsSuccess)
        {
            Debug.LogError(
                $"[HttpServiceDemo] 请求失败：{result.Error}，状态码={result.StatusCode}，响应={result.RawBody}");
            return;
        }

        if (data == null)
        {
            Debug.LogError($"[HttpServiceDemo] 解析失败，原始响应：{result.RawBody}");
            return;
        }

        Debug.Log(
            $"[HttpServiceDemo] 解析成功 → userId={data.userId}, id={data.id}, " +
            $"title=\"{data.title}\", completed={data.completed}");
    }
}
