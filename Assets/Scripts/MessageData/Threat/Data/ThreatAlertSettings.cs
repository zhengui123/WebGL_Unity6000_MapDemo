/// <summary>
/// 威胁态势告警阈值与流程时长配置。
/// </summary>
public static class ThreatAlertSettings
{
    /// <summary>单省触发告警的事件条数阈值（大于等于）。</summary>
    public const int EventsPerProvinceThreshold = 10;

    /// <summary>同一 Vin 出现次数达到该值则进入车辆大屏（大于等于）。</summary>
    public const int SameVinCountToEnterVehicle = 3;

    /// <summary>国家级别停留秒数。</summary>
    public const float CountryLevelHoldSeconds = 10f;

    /// <summary>省级停留秒数。</summary>
    public const float ProvinceLevelHoldSeconds = 60f;

    /// <summary>车辆级别停留秒数（Vin≥3 下钻后）；可被 Runner Inspector 覆盖。</summary>
    public const float VehicleLevelHoldSeconds = 10f;

    /// <summary>攻击链路级别停留秒数；可被 Runner Inspector 覆盖。</summary>
    public const float AttackPathLevelHoldSeconds = 10f;

    /// <summary>零部件级别单件停留秒数；可被 Runner Inspector 覆盖。</summary>
    public const float PartLevelHoldSeconds = 10f;

    /// <summary>主动退出/打断威胁下钻后的冷却秒数；可被 Runner Inspector 覆盖。</summary>
    public const float InterruptCooldownSeconds = 180f;
}
