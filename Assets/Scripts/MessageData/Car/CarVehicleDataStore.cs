using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 车辆态势数据缓存：零部件防护状态 + 攻击链路。再次请求时整份覆盖。
/// </summary>
public sealed class CarVehicleDataStore
{
    private static CarVehicleDataStore _instance;

    public static CarVehicleDataStore Instance => _instance ??= new CarVehicleDataStore();

    public PartProtectionStatusResponse PartProtectionStatus { get; private set; }
    public AttackChainResponse AttackChain { get; private set; }
    public string LastEncryptVin { get; private set; }
    public string LastStartTime { get; private set; }
    public string LastEndTime { get; private set; }
    public bool HasCache { get; private set; }

    public event Action CacheReplaced;

    /// <summary>用新响应覆盖缓存（抛弃旧数据）。</summary>
    public void Replace(
        string encryptVin,
        string startTime,
        string endTime,
        PartProtectionStatusResponse partProtection,
        AttackChainResponse attackChain)
    {
        LastEncryptVin = encryptVin ?? string.Empty;
        LastStartTime = startTime ?? string.Empty;
        LastEndTime = endTime ?? string.Empty;
        PartProtectionStatus = partProtection;
        AttackChain = attackChain;
        HasCache = partProtection != null && attackChain != null;
        CacheReplaced?.Invoke();
    }

    public void Clear()
    {
        LastEncryptVin = string.Empty;
        LastStartTime = string.Empty;
        LastEndTime = string.Empty;
        PartProtectionStatus = null;
        AttackChain = null;
        HasCache = false;
        CacheReplaced?.Invoke();
    }

    /// <summary>取首个未防护零部件；无则返回 null。</summary>
    public PartProtectionStatusPart GetFirstUnprotectedPart()
    {
        PartProtectionStatusPart[] parts = PartProtectionStatus?.data?.unprotectedParts;
        if (parts == null || parts.Length == 0)
        {
            return null;
        }

        return parts[0];
    }

    /// <summary>
    /// 轮播条目：先 unprotectedParts（未防护），再 protectedParts（已防护）。
    /// </summary>
    public List<CarVehiclePartSlide> BuildPartSlides()
    {
        List<CarVehiclePartSlide> slides = new List<CarVehiclePartSlide>();
        AppendSlides(slides, PartProtectionStatus?.data?.unprotectedParts, ProtectionStateType.Unprotected);
        AppendSlides(slides, PartProtectionStatus?.data?.protectedParts, ProtectionStateType.Protected);
        return slides;
    }

    /// <summary>是否存在可绘制的攻击链路边（links 经 IP 映射后至少一条有效）。</summary>
    public bool HasAttackPathEntries()
    {
        return BuildAttackPathEntries().Count > 0;
    }

    /// <summary>从 AttackChainData.nodes 收集全部零件标号（partType 1~5）。</summary>
    public List<int> BuildAttackChainNodePartTypes()
    {
        List<int> types = new List<int>();
        AttackChainNode[] nodes = AttackChain?.data?.nodes;
        if (nodes == null || nodes.Length == 0)
        {
            return types;
        }

        HashSet<int> unique = new HashSet<int>();
        for (int i = 0; i < nodes.Length; i++)
        {
            AttackChainNode node = nodes[i];
            if (node == null || !TryResolveNodePartType(node, out int partType) || !unique.Add(partType))
            {
                continue;
            }

            types.Add(partType);
        }

        return types;
    }

    /// <summary>从 AttackChainData.nodes 收集全部零件显示名（partTypeName，兼容旧逻辑）。</summary>
    public List<string> BuildAttackChainNodePartNames()
    {
        List<string> names = new List<string>();
        AttackChainNode[] nodes = AttackChain?.data?.nodes;
        if (nodes == null || nodes.Length == 0)
        {
            return names;
        }

        HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < nodes.Length; i++)
        {
            AttackChainNode node = nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.partTypeName))
            {
                continue;
            }

            string name = node.partTypeName.Trim();
            if (unique.Add(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// 将攻击链路 links 的 sourceIp / targetIp 转为 GridLine 零件名对。
    /// 优先 nodes.partType → 场景 partRoot.name；失败再回退 partTypeName。
    /// </summary>
    public List<AttackChainPathEntry> BuildAttackPathEntries()
    {
        List<AttackChainPathEntry> entries = new List<AttackChainPathEntry>();
        AttackChainData data = AttackChain?.data;
        if (data?.links == null || data.links.Length == 0)
        {
            return entries;
        }

        Dictionary<string, AttackChainNode> ipToNode = BuildIpToNodeMap(data.nodes);
        for (int i = 0; i < data.links.Length; i++)
        {
            AttackChainLink link = data.links[i];
            if (link == null)
            {
                continue;
            }

            if (!TryResolveGridLineKeyByIp(ipToNode, link.sourceIp, out string startPartName)
                || !TryResolveGridLineKeyByIp(ipToNode, link.targetIp, out string endPartName))
            {
                LogManager.LogFeatureWarning(
                    $"[CarVehicleDataStore] 跳过无法映射 IP 的攻击链路：{link.sourceIp} → {link.targetIp}");
                continue;
            }

            entries.Add(new AttackChainPathEntry(startPartName, endPartName));
        }

        return entries;
    }

    private static Dictionary<string, AttackChainNode> BuildIpToNodeMap(AttackChainNode[] nodes)
    {
        Dictionary<string, AttackChainNode> map = new Dictionary<string, AttackChainNode>(StringComparer.OrdinalIgnoreCase);
        if (nodes == null)
        {
            return map;
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            AttackChainNode node = nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.partsIp))
            {
                continue;
            }

            map[node.partsIp.Trim()] = node;
        }

        return map;
    }

    private static bool TryResolveGridLineKeyByIp(
        Dictionary<string, AttackChainNode> ipToNode,
        string ip,
        out string partName)
    {
        partName = null;
        if (string.IsNullOrWhiteSpace(ip) || ipToNode == null)
        {
            return false;
        }

        if (!ipToNode.TryGetValue(ip.Trim(), out AttackChainNode node) || node == null)
        {
            return false;
        }

        if (TryResolveNodePartType(node, out int partType))
        {
            VehicleToPartTransitionController transition = VehicleToPartTransitionController.Instance;
            if (transition != null
                && transition.TryResolveGridLineKeyByPartType(partType, out string key)
                && !string.IsNullOrEmpty(key))
            {
                partName = key;
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(node.partTypeName))
        {
            partName = node.partTypeName.Trim();
            return true;
        }

        return false;
    }

    private static bool TryResolveNodePartType(AttackChainNode node, out int partType)
    {
        partType = 0;
        if (node == null)
        {
            return false;
        }

        if (PartTypeMatchUtil.TryParsePartType(node.partType, out partType))
        {
            return true;
        }

        // links 上也可能带 partType；nodes 主路径优先
        return false;
    }

    private static void AppendSlides(
        List<CarVehiclePartSlide> slides,
        PartProtectionStatusPart[] parts,
        ProtectionStateType state)
    {
        if (parts == null || parts.Length == 0)
        {
            return;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            PartProtectionStatusPart part = parts[i];
            if (part == null)
            {
                continue;
            }

            int partType = 0;
            PartTypeMatchUtil.TryNormalizePartType(part.partType, out partType);

            string displayName = !string.IsNullOrWhiteSpace(part.partTypeName)
                ? part.partTypeName.Trim()
                : string.Empty;

            // 无显示名且无有效标号时跳过
            if (displayName.Length == 0 && !PartTypeMatchUtil.IsValidPartType(partType))
            {
                continue;
            }

            if (displayName.Length == 0 && PartTypeMatchUtil.IsValidPartType(partType))
            {
                VehicleToPartTransitionController transition = VehicleToPartTransitionController.Instance;
                if (transition != null
                    && transition.TryResolvePartByType(partType, out Transform root)
                    && root != null)
                {
                    displayName = root.name;
                }
                else
                {
                    displayName = partType.ToString();
                }
            }

            slides.Add(new CarVehiclePartSlide(
                partType,
                displayName,
                state,
                BuildPendingEventNames(part)));
        }
    }

    private static List<string> BuildPendingEventNames(PartProtectionStatusPart part)
    {
        List<string> names = new List<string>(MessageListPanel.MaxMessageCount);
        if (part?.pendingEvents == null)
        {
            return names;
        }

        int count = Mathf.Min(part.pendingEvents.Length, MessageListPanel.MaxMessageCount);
        for (int i = 0; i < count; i++)
        {
            PartProtectionPendingEvent evt = part.pendingEvents[i];
            if (evt == null || string.IsNullOrWhiteSpace(evt.eventName))
            {
                continue;
            }

            names.Add(evt.eventName.Trim());
        }

        return names;
    }
}

/// <summary>攻击链路边经 IP 映射后的起点→终点零件名（GridLine 键）。</summary>
public readonly struct AttackChainPathEntry
{
    public readonly string StartPartName;
    public readonly string EndPartName;

    public AttackChainPathEntry(string startPartName, string endPartName)
    {
        StartPartName = startPartName;
        EndPartName = endPartName;
    }
}

/// <summary>车辆零部件轮播一帧数据。</summary>
public readonly struct CarVehiclePartSlide
{
    public readonly int PartType;
    public readonly string PartTypeName;
    public readonly ProtectionStateType ProtectionState;
    public readonly IReadOnlyList<string> EventNames;

    public CarVehiclePartSlide(
        int partType,
        string partTypeName,
        ProtectionStateType protectionState,
        List<string> eventNames)
    {
        PartType = partType;
        PartTypeName = partTypeName;
        ProtectionState = protectionState;
        EventNames = eventNames ?? new List<string>();
    }
}
