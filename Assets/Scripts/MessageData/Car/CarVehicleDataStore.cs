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
            if (part == null || string.IsNullOrWhiteSpace(part.partTypeName))
            {
                continue;
            }

            slides.Add(new CarVehiclePartSlide(
                part.partTypeName.Trim(),
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

/// <summary>车辆零部件轮播一帧数据。</summary>
public readonly struct CarVehiclePartSlide
{
    public readonly string PartTypeName;
    public readonly ProtectionStateType ProtectionState;
    public readonly IReadOnlyList<string> EventNames;

    public CarVehiclePartSlide(
        string partTypeName,
        ProtectionStateType protectionState,
        List<string> eventNames)
    {
        PartTypeName = partTypeName;
        ProtectionState = protectionState;
        EventNames = eventNames ?? new List<string>();
    }
}
