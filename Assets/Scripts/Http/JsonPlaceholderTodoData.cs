using System;
using UnityEngine;

/// <summary>
/// JsonPlaceholder /todos 接口返回的数据结构。
/// 示例：https://jsonplaceholder.typicode.com/todos/1
/// </summary>
[Serializable]
public class JsonPlaceholderTodoData
{
    public int userId;
    public int id;
    public string title;
    public bool completed;
}
