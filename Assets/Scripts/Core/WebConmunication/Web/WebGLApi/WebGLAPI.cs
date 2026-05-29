using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WebGLAPI : MonoBehaviour
{
    public GameObject cube;
    public float rotNum = 1;
    public Text showMessageText;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.A))
        {
            CubeRot(rotNum);
        }
    }

    public void CubeLeft()
    {
        Debug.Log("Unity日志" + "CubeLeft");

        CubeRot(10);
    }
    
    public void CubeRight()
    {
        Debug.Log("Unity日志" + "CubeRight");

        CubeRot(-10);
    }
    public void CubeRotApi(string num)
    {
        num = StringTool.Base64ToString(num);
        Debug.Log("Unity日志" + "CubeRotApi" + num);
        try
        {
            Quaternion rot = cube.transform.localRotation;
            Vector3 euler = rot.eulerAngles;
            euler.y += float.Parse(num);
            Quaternion newRot = Quaternion.Euler(euler);
            cube.transform.localRotation = newRot;
        }
        catch (Exception e)
        {
            Debug.Log("Unity日志出错：" + e);

        }
        
    }
    
    public void CubeRot(float num)
    {

        Quaternion rot = cube.transform.localRotation;
        Vector3 euler = rot.eulerAngles;
        euler.y += num;
        Quaternion newRot = Quaternion.Euler(euler);
        cube.transform.localRotation = newRot;
    }

    public void ShowMessage(string msg)
    {
        msg = StringTool.Base64ToString(msg);

        Debug.Log("Unity日志" + msg);
        showMessageText.text = msg;
    }
    
    
    [Obsolete("Obsolete")]
    public void SendMessageToJavaScript(string message)
    {
        Application.ExternalCall("receiveMessageFromUnity", message);
    }
}
