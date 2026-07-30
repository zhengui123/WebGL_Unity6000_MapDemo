using System.Collections;
using System.Collections.Generic;
using LitJson;
using UnityEngine;
/// <summary>
/// websocket调用示例
/// </summary>
public class WebSocketDemo : MonoBehaviour
{
    
    void Start()
    {
    }

    public WebSocketDataType dataType = WebSocketDataType.Method;

    public string methodName => nameof(WebAPI.Instance.GoNext);
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Debug.Log(Application.platform);
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                SendCustomMessage(WebSocketDataType.Txt,"这里是纯文本格式");

            }
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("当前平台" + Application.platform);
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                //WebSocketController.Instance.SendWebScoketOnlyJson(WebSocketDataType.Txt,"这里是Windos版本");
                WebSocketController.Instance.SendWebScoketOnlyJson(dataType,methodName);

            }
            else if(Application.platform == RuntimePlatform.WebGLPlayer)
            {
               SendCustomMessage(WebSocketDataType.Txt,"这里是webgl版本");

            }
        }
    }

    public void SendCustomMessage(WebSocketDataType type, string json)
    {
        WebSocketData data = new WebSocketData();
        data.userId = UserInfo.Instance.userID;
        data.jsonType = type;
        data.json = json;
        string sendText = JsonMapper.ToJson(data);
        WebSocketController.Instance.SendWebScoketCustomMessage(sendText);

    }
}
