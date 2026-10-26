namespace PowerMonitorDashboard.Core.Domain;

/// <summary>
/// 电压等级。数值即标称线电压（kV），便于直接参与计算与显示。
/// </summary>
public enum VoltageLevel
{
    /// <summary>10 kV 发电机出口电压。</summary>
    Kv10 = 10,

    /// <summary>110 kV 配电电压。</summary>
    Kv110 = 110,

    /// <summary>220 kV 主网电压。</summary>
    Kv220 = 220,

    /// <summary>500 kV 联络电压。</summary>
    Kv500 = 500
}

/// <summary>数据来源。</summary>
public enum ReadingSource
{
    /// <summary>由内置仿真采集器产生。</summary>
    Simulated = 0,

    /// <summary>由值班人员在“数据录入”界面手工补录。</summary>
    Manual = 1,

    /// <summary>由外部文件导入。</summary>
    Imported = 2,

    /// <summary>历史回放。</summary>
    Replayed = 3
}

/// <summary>数据质量标志，参考工业采集系统中的 Quality 语义。</summary>
public enum QualityFlag
{
    /// <summary>数据可信。</summary>
    Good = 0,

    /// <summary>数据可疑（例如采集延迟、量程临界）。</summary>
    Uncertain = 1,

    /// <summary>数据不可用。</summary>
    Bad = 2
}

/// <summary>负荷区间，用于“高/中/低”值转换与配色。</summary>
public enum PowerBand
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>设备/系统运行状态。</summary>
public enum OperatingStatus
{
    Normal = 0,
    Attention = 1,
    Critical = 2
}

/// <summary>告警级别。</summary>
public enum AlarmSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>告警类型，同时作为告警去重的标识。</summary>
public enum AlarmKind
{
    FrequencyLow,
    FrequencyHigh,
    VoltageLow,
    VoltageHigh,
    OverCurrent,
    Overload,
    DataStale,
    SourceFault
}

/// <summary>告警生命周期状态。</summary>
public enum AlarmState
{
    /// <summary>活动，尚未处理。</summary>
    Active = 0,

    /// <summary>值班员已确认，但工况仍未恢复。</summary>
    Acknowledged = 1,

    /// <summary>工况已恢复，告警自动清除。</summary>
    Cleared = 2
}
