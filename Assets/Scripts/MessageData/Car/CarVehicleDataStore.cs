using System;

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
}
