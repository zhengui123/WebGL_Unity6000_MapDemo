using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using UnityEngine;
/// <summary>
/// webSocket数据处理
/// </summary>
public class WebSocketProcessing : MonoBehaviour
{
    public static Queue<string> messageQueue = new Queue<string>();

    public void Awake()
    {
        WebSocketEvent.OnMessage += OnMessage;
        Type.GetType(nameof(WebAPI));
    }

    private void OnMessage(string msg)
    {
        try
        {
            Debug.Log("websocket接收消息--开头");

            WebSocketData data = JsonMapper.ToObject<WebSocketData>(msg);
            Debug.Log("websocket接收消息-head-" + data.head);
            Debug.Log("websocket接收消息-userId-" + data.userId);
            Debug.Log("websocket接收消息-jsonType-" + data.jsonType);

            Debug.Log("websocket接收消息-json-" + data.json);
            Debug.Log("websocket接收消息--结尾");

            if (data.head != "Unity")
            {
                
            }
            if (data.userId != UserInfo.Instance.userID)
            {
                
            }

            switch (data.jsonType)
            {
                case WebSocketDataType.Method:
                    Debug.Log("websocket接受方法调用：" + data.json);

                    Type type = typeof(WebAPI);
                    
                    MethodInfo method = type.GetMethod(data.json);

                    method.Invoke(WebAPI.Instance, null);
                    
                    break;
                case WebSocketDataType.Txt:
                    Debug.Log("websocket接受文本消息：" + data.json);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.Log("websocket接收消息不符合Unity自定义结构--" + e);
        }
    }

    void MethodTest()
    {
        Type type = typeof(WebAPI);
        //t = Type.GetType(className);//通过string类型的className获得相同名称的类
        var obj = type.Assembly.CreateInstance(nameof(WebAPI));//创建获取到的类的实例
        
        //没有参数的方法的调用
        MethodInfo method_1 = type.GetMethod("methodName_1");//通过string类型的methodName获得同名的方法
        method_1.Invoke(obj, null);//调用t类实例obj中的方法"TestStringToMethod_1"，第二个参数没有额外字段直接使用null
        
        //有参数的方法的调用
        object[] parameters = new object[] { "测试" ,this.gameObject};//所有的参数丢进方法一起运行的字段,可以多个
        MethodInfo method_2 = type.GetMethod("methodName_2");
        method_2.Invoke(obj, parameters);//调用t类实例obj中的方法"TestStringToMethod_2"
    }
}
