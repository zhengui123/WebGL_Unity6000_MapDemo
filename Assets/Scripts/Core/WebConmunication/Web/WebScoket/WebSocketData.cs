using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebSocketData
{
    public string head = "Unity";
    public string userId;
    public WebSocketDataType jsonType;
    public string json;
}

public enum WebSocketDataType
{
    Method,
    Txt,
    
}

public class WebScoketMethod
{
    public string methodName;
    public string parameter;
}