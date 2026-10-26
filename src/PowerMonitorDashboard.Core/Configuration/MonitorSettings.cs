using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Simulation;

namespace PowerMonitorDashboard.Core.Configuration;

/// <summary>
/// 应用配置。该对象会被序列化为 JSON 持久化到
/// <c>%LOCALAPPDATA%\PowerMonitorDashboard\settings.json</c>。
/// </summary>
public sealed class MonitorSettings
{
    /// <summary>采样周期，单位毫秒。</summary>
    public int SamplingIntervalMs { get; set; } = 1000;

    /// <summary>历史缓冲区容量（内存中保留的采样点数量）。</summary>
    public int HistoryCapacity { get; set; } = 5000;

    /// <summary>历史表格最多显示的行数。</summary>
    public int HistoryDisplayLimit { get; set; } = 500;

    /// <summary>启动后是否自动开始采集。</summary>
    public bool AutoStartAcquisition { get; set; } = true;

    /// <summary>历史表格是否自动滚动到最新记录。</summary>
    public bool AutoScrollHistory { get; set; } = true;

    /// <summary>是否显示数据质量列。</summary>
    public bool ShowQualityColumn { get; set; } = true;

    /// <summary>跨字段校验时假定的功率因数。</summary>
    public double AssumedPowerFactor { get; set; } = 0.95d;

    /// <summary>安全教学视频的相对路径（相对于程序目录）。</summary>
    public string VideoRelativePath { get; set; } = Path.Combine("Assets", "video", "safety-training.mp4");

    /// <summary>值班员名称，显示在状态栏与导出文件中。</summary>
    public string OperatorName { get; set; } = "值班员";

    /// <summary>告警阈值。</summary>
    public PowerThresholds Thresholds { get; set; } = new();

    /// <summary>仿真参数。</summary>
    public SimulationOptions Simulation { get; set; } = new();

    /// <summary>创建副本，供“设置”对话框在不影响运行配置的前提下编辑。</summary>
    public MonitorSettings Clone() => new()
    {
        SamplingIntervalMs = SamplingIntervalMs,
        HistoryCapacity = HistoryCapacity,
        HistoryDisplayLimit = HistoryDisplayLimit,
        AutoStartAcquisition = AutoStartAcquisition,
        AutoScrollHistory = AutoScrollHistory,
        ShowQualityColumn = ShowQualityColumn,
        AssumedPowerFactor = AssumedPowerFactor,
        VideoRelativePath = VideoRelativePath,
        OperatorName = OperatorName,
        Thresholds = Thresholds with { },
        Simulation = Simulation with { }
    };

    /// <summary>校验配置，返回中文错误信息。</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (SamplingIntervalMs is < 200 or > 60000)
        {
            errors.Add("采样周期应在 200～60000 毫秒之间。");
        }

        if (HistoryCapacity is < 100 or > 100000)
        {
            errors.Add("历史容量应在 100～100000 条之间。");
        }

        if (HistoryDisplayLimit is < 50 or > 5000)
        {
            errors.Add("历史显示行数应在 50～5000 之间。");
        }

        if (AssumedPowerFactor is <= 0d or > 1d)
        {
            errors.Add("假定功率因数应在 0～1 之间。");
        }

        if (string.IsNullOrWhiteSpace(VideoRelativePath))
        {
            errors.Add("视频路径不能为空。");
        }

        errors.AddRange(Thresholds.Validate());
        errors.AddRange(Simulation.Validate());
        return errors;
    }

    /// <summary>把越界值收敛到合法范围，保证读取到损坏配置时程序仍可启动。</summary>
    public MonitorSettings Normalize()
    {
        SamplingIntervalMs = Math.Clamp(SamplingIntervalMs, 200, 60000);
        HistoryCapacity = Math.Clamp(HistoryCapacity, 100, 100000);
        HistoryDisplayLimit = Math.Clamp(HistoryDisplayLimit, 50, 5000);
        AssumedPowerFactor = Math.Clamp(AssumedPowerFactor, 0.5d, 1d);
        VideoRelativePath = string.IsNullOrWhiteSpace(VideoRelativePath)
            ? Path.Combine("Assets", "video", "safety-training.mp4")
            : VideoRelativePath.Trim();
        OperatorName = string.IsNullOrWhiteSpace(OperatorName) ? "值班员" : OperatorName.Trim();
        return this;
    }
}
