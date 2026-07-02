using System.Collections.Generic;
using System.Text;

/// <summary>
/// 将请求头键值对转换为 JSON 对象字符串，用于 POST 提交。
/// </summary>
public static class HttpHeadersJson
{
    /// <summary>将请求头字典序列化为 JSON 对象，例如 {"Satoken":"xxx","X-Tenant-Id":"1"}。</summary>
    public static string ToJsonObject(IReadOnlyDictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return "{}";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append('{');
        bool isFirst = true;

        foreach (KeyValuePair<string, string> pair in headers)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            if (!isFirst)
            {
                builder.Append(',');
            }

            AppendJsonString(builder, pair.Key.Trim());
            builder.Append(':');
            AppendJsonString(builder, pair.Value ?? string.Empty);
            isFirst = false;
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        if (!string.IsNullOrEmpty(value))
        {
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }
        }

        builder.Append('"');
    }
}
