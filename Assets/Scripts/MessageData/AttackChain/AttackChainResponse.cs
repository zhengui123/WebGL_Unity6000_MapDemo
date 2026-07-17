using System;

/// <summary>
/// 车辆态势：攻击链路响应（attackChain）。
/// </summary>
[Serializable]
public class AttackChainResponse
{
    public int code;
    public string msg;
    public AttackChainData data;

    public bool IsSuccess => code == HttpProjectConfig.SuccessResponseCode;
}

/// <summary>攻击链路 data 节点。</summary>
[Serializable]
public class AttackChainData
{
    /// <summary>车辆搭载的全部零部件（平铺图节点）。</summary>
    public AttackChainNode[] nodes;

    /// <summary>攻击链路（有向边，按 partType+sourceIp+targetIp 去重）。</summary>
    public AttackChainLink[] links;

    public int NodeCount => nodes != null ? nodes.Length : 0;
    public int LinkCount => links != null ? links.Length : 0;
}

/// <summary>平铺图零部件节点。</summary>
[Serializable]
public class AttackChainNode
{
    /// <summary>零部件 ID。</summary>
    public int id;

    /// <summary>零部件类型。</summary>
    public string partType;

    /// <summary>零部件类型名称。</summary>
    public string partTypeName;

    /// <summary>零部件型号。</summary>
    public string partsModel;

    /// <summary>零部件编码。</summary>
    public string partsCode;

    /// <summary>部件 IP。</summary>
    public string partsIp;
}

/// <summary>攻击链路边（来源 IP → 目的 IP）。</summary>
[Serializable]
public class AttackChainLink
{
    /// <summary>部件类型。</summary>
    public string partType;

    /// <summary>来源 IP。</summary>
    public string sourceIp;

    /// <summary>目的 IP。</summary>
    public string targetIp;
}
