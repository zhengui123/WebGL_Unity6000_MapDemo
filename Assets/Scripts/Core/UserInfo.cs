using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 用户数据
/// </summary>
public class UserInfo : UnitySingle<UserInfo>
{
    public string userID;
    public string userName;


    public void Awake()
    {
        userID = Guid.NewGuid().ToString();
        userName = "userName-test" + 001;
    }
}
