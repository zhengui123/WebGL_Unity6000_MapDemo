using System;
using UnityEngine;

/// <summary>
/// 将后端 JSON 字符串解析为指定数据类（基于 Unity <see cref="JsonUtility"/>）。
/// 数据类需标记 [Serializable]，字段名与 JSON 键一致。
/// </summary>
public static class HttpJsonParser
{
    /// <summary>尝试将 JSON 反序列化为 <typeparamref name="T"/>。</summary>
    public static bool TryParse<T>(string json, out T data, out string errorMessage) where T : class
    {
        data = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "JSON 为空。";
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<T>(json);
        }
        catch (Exception exception)
        {
            errorMessage = $"JSON 解析异常：{exception.Message}";
            return false;
        }

        if (data == null)
        {
            errorMessage = "JSON 反序列化结果为 null。";
            return false;
        }

        return true;
    }

    /// <summary>将对象序列化为 JSON 字符串（用于 POST 请求体）。</summary>
    public static string ToJson<T>(T data, bool prettyPrint = false)
    {
        return JsonUtility.ToJson(data, prettyPrint);
    }
}
