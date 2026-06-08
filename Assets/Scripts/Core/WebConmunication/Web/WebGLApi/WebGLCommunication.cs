using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
/// <summary>
/// 调用web的js方法
/// </summary>
public class WebGLCommunication : MonoBehaviour
{
    #if UNITY_WEBGL && !UNITY_EDITOR
    // 声明外部JavaScript函数
    [DllImport("__Internal")]
    private static extern string GetDataFromHTML(string message);

    [DllImport("__Internal")]
    private static extern void CallUnityFunction(string message, string callbackFunction);

    
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Debug.Log("unity发出消息之前");
            GetDataFromHTML("unity发出消息");
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log("unity发出消息01--之前");
            CallUnityFunction("unity发出消息01--", nameof(CallbackTest));
        }

    }

    public void CallbackTest(string msg) 
    {
        Debug.Log("Unity输出返回结果：" + msg);
    }
#endif
}
