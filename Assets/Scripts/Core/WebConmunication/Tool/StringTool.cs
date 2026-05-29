using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StringTool : UnitySingle<StringTool>
{
    /// <summary>
    /// Base64转字符串
    /// </summary>
    public static string Base64ToString(string base64)
    {
      
        byte[]myByte = System.Convert.FromBase64String(base64);
        string txt = System.Text.Encoding.UTF8.GetString(myByte);
        Debug.Log("Base64转字符串结果：" + txt);
        return txt;
    }
    
    /// <summary>
    /// 字符串转Base64 
    /// </summary>
    public static string StringToBase64(string txt)
    {
        byte[] myByte = System.Text.Encoding.UTF8.GetBytes(txt);
        string base64 = System.Convert.ToBase64String(myByte);
        Debug.Log("字符串转base64结果：" + base64);

        return base64;
    }

    /// <summary>
    /// base64转换图片
    /// </summary>
    /// <param name="base64"></param>
    /// <returns></returns>
    public static Texture2D Base64ToTexture2D(string base64)
    {
        byte[] myByte = System.Convert.FromBase64String(base64);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(myByte);
        return tex;
    }
    
}
