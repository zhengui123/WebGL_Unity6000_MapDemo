using System;
using System.Collections;
using System.Threading.Tasks;
using LitJson;
using NativeWebSocket;
using UnityEngine;
using WebSocket = NativeWebSocket.WebSocket;
using WebSocketState = NativeWebSocket.WebSocketState;

/// <summary>
/// WebGL中websocket链接失败后，不能继续使用，必须new一个新的
/// </summary>
public class WebSocketController : UnitySingle<WebSocketController>
{
    private string webScoketUrl = "ws://localhost:8090";
    
    public bool isConnected = false;
    public bool isConnecting = false;
        
    WebSocket websocket;

    void Start()
    { 
        Init();
    }

    void Init()
    {

        websocket = new WebSocket(webScoketUrl);

        websocket.OnOpen += OnOpen;

        websocket.OnError += OnError;

        websocket.OnClose += OnClose;
     
        websocket.OnMessage += OnMessage;
        
      

        websocket.Connect();

    }
    void Update()
    {
        //不可缺失，否则编辑器内无法接受信息
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
#endif
    }

    async void SendWebSocketMessage()
    {
        if (websocket.State == WebSocketState.Open)
        {
            // Sending bytes
            await websocket.Send(new byte[] { 10, 20, 30 });

            // Sending plain text
            await websocket.SendText("plain text message");
        }
    }

    Coroutine connectCoroutine;

    #region 回调

    /// <summary>
    /// 链接成功回调
    /// </summary>
    private void OnOpen()
    {
        isConnected = true;
        Debug.Log("websocket链接成功!");
        StopConnectCoroutine();
        websocket.SendText("Unity连接成功");

    }

    /// <summary>
    /// 连接出错回调
    /// </summary>
    private void OnError(string e)
    {
        Debug.Log("websocket链接出错!： " + e);
        isConnected = false;
    }

    /// <summary>
    /// 连接断开
    /// </summary>
    void OnClose(WebSocketCloseCode e)
    {
        
        Debug.Log("websocket链接关闭：" + e);
        isConnected = false;

        //断线触发-重新连接
        Reconnection();
    }
    
    /// <summary>
    /// 接收消息
    /// </summary>
    public void OnMessage(byte[] bytes) 
    {
        var message = System.Text.Encoding.UTF8.GetString(bytes);
        Debug.Log("websocket接收到消息! (" + bytes.Length + " bytes) " + message);

        WebSocketEvent.OnMessage?.Invoke(message);
      
    }
    #endregion


    #region 断线重连检测
    
    //断线触发-重新连接
    public void Reconnection()
    {
        if (!isConnecting)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                StopConnectCoroutine();
                connectCoroutine = StartCoroutine(ConnetWebsocket());

            }
            else if(Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Debug.Log("webgl版本断线重连，重新new");
                Init();
            }
        }
    }

    /// <summary>
    /// 开启循环重连（webgl不适用，webgl连接失败后必须重新new websocket）
    /// </summary>
    /// <returns></returns>
    public IEnumerator ConnetWebsocket()
    {
        isConnecting = true;
        while (!isConnected)
        {
            Debug.Log("准备重连");
            
         
            yield return websocket.Connect() ;
          
            yield return new WaitForSeconds(3);
        }

    }
    
    /// <summary>
    /// 停止重连协程
    /// </summary>
    private void StopConnectCoroutine()
    {
        if(connectCoroutine != null)
            StopCoroutine(connectCoroutine);
        isConnecting = false;
    }

    #endregion


    /// <summary>
    /// 发送json数据
    /// </summary>
    /// <param name="json"></param>
    public void SendWebScoketOnlyJson(WebSocketDataType type, string json)
    {
        WebSocketData data = new WebSocketData();
        data.userId = UserInfo.Instance.userID;
        data.jsonType = type;
        data.json = json;
        string sendText = JsonMapper.ToJson(data);
        websocket.SendText(sendText);

    }

    /// <summary>
    /// 发送json数据
    /// </summary>
    public void SendWebScoketCustomMessage(string sendText)
    {
     
        websocket.SendText(sendText);

    }
    
    /// <summary>
    /// 退出关闭
    /// </summary>
    private async void OnApplicationQuit()
    {
        Debug.Log("Unity程序退出");
        
        await websocket.Close();
    }
}
