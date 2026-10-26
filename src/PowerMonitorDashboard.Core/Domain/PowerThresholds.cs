using System.Globalization;

namespace PowerMonitorDashboard.Core.Domain;

/// <summary>
/// 电力监控阈值配置。所有判据（越限、告警级别、负荷高/中/低）都由这一份配置驱动，
/// 因此可以在“设置”窗口中在线调整而无需修改代码。
/// </summary>
public sealed record PowerThresholds
{
    /// <summary>标称线电压，单位 kV。</summary>
    public double NominalVoltageKv { get; init; } = 220d;

    /// <summary>电压“注意”区间半宽（相对标称值），默认 ±5%。</summary>
    public double VoltageAttentionRatio { get; init; } = 0.05d;

    /// <summary>电压“严重”区间半宽（相对标称值），默认 ±10%。</summary>
    public double VoltageCriticalRatio { get; init; } = 0.10d;

    /// <summary>频率下限告警值，单位 Hz。</summary>
    public double FrequencyAttentionLowHz { get; init; } = 49.5d;

    /// <summary>频率上限告警值，单位 Hz。</summary>
    public double FrequencyAttentionHighHz { get; init; } = 50.5d;

    /// <summary>频率下限严重告警值，单位 Hz。</summary>
    public double FrequencyCriticalLowHz { get; init; } = 49.0d;

    /// <summary>频率上限严重告警值，单位 Hz。</summary>
    public double FrequencyCriticalHighHz { get; init; } = 51.0d;

    /// <summary>电流告警值，单位 A。</summary>
    public double CurrentAttentionA { get; init; } = 1350d;

    /// <summary>电流严重告警值，单位 A。</summary>
    public double CurrentCriticalA { get; init; } = 1550d;

    /// <summary>有功功率告警值，单位 MW。</summary>
    public double PowerAttentionMw { get; init; } = 500d;

    /// <summary>有功功率严重告警值，单位 MW。</summary>
    public double PowerCriticalMw { get; init; } = 540d;

    /// <summary>“高负荷”区间下限，单位 MW。</summary>
    public double PowerBandHighMw { get; init; } = 400d;

    /// <summary>“中负荷”区间下限，单位 MW。</summary>
    public double PowerBandLowMw { get; init; } = 250d;

    /// <summary>装机容量，用于显示负荷率，单位 MW。</summary>
    public double InstalledCapacityMw { get; init; } = 750d;

    /// <summary>默认阈值。</summary>
    public static PowerThresholds Default { get; } = new();

    /// <summary>电压注意下限。</summary>
    public double VoltageAttentionLowKv => NominalVoltageKv * (1d - VoltageAttentionRatio);

    /// <summary>电压注意上限。</summary>
    public double VoltageAttentionHighKv => NominalVoltageKv * (1d + VoltageAttentionRatio);

    /// <summary>电压严重下限。</summary>
    public double VoltageCriticalLowKv => NominalVoltageKv * (1d - VoltageCriticalRatio);

    /// <summary>电压严重上限。</summary>
    public double VoltageCriticalHighKv => NominalVoltageKv * (1d + VoltageCriticalRatio);

    /// <summary>按功率值划分负荷区间。</summary>
    public PowerBand ClassifyPower(double powerMw)
    {
        if (powerMw >= PowerBandHighMw)
        {
            return PowerBand.High;
        }

        return powerMw >= PowerBandLowMw ? PowerBand.Medium : PowerBand.Low;
    }

    /// <summary>负荷率（0~1）。</summary>
    public double LoadRate(double powerMw)
        => InstalledCapacityMw <= 1e-9 ? 0d : Math.Clamp(powerMw / InstalledCapacityMw, 0d, 1.5d);

    /// <summary>校验阈值配置的合法性与先后关系，返回中文错误信息列表。</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (NominalVoltageKv is < 0.1d or > 500d)
        {
            errors.Add("标称电压应在 0.1～500 kV 之间。");
        }

        if (VoltageAttentionRatio <= 0d || VoltageAttentionRatio >= VoltageCriticalRatio)
        {
            errors.Add("电压注意区间必须大于 0 且小于严重区间。");
        }

        if (VoltageCriticalRatio >= 1d)
        {
            errors.Add("电压严重区间必须小于 100%。");
        }

        if (FrequencyAttentionLowHz >= FrequencyAttentionHighHz)
        {
            errors.Add("频率告警下限必须小于告警上限。");
        }

        if (FrequencyCriticalLowHz >= FrequencyAttentionLowHz)
        {
            errors.Add("频率严重下限必须小于告警下限。");
        }

        if (FrequencyCriticalHighHz <= FrequencyAttentionHighHz)
        {
            errors.Add("频率严重上限必须大于告警上限。");
        }

        if (CurrentAttentionA <= 0d || CurrentAttentionA >= CurrentCriticalA)
        {
            errors.Add("电流告警值必须大于 0 且小于严重告警值。");
        }

        if (PowerAttentionMw <= 0d || PowerAttentionMw >= PowerCriticalMw)
        {
            errors.Add("功率告警值必须大于 0 且小于严重告警值。");
        }

        if (PowerBandLowMw <= 0d || PowerBandLowMw >= PowerBandHighMw)
        {
            errors.Add("负荷区间下限必须大于 0 且小于上限。");
        }

        if (InstalledCapacityMw <= 0d)
        {
            errors.Add("装机容量必须大于 0。");
        }

        return errors;
    }

    /// <summary>生成用于界面显示的阈值摘要。</summary>
    public string Describe()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"电压 {VoltageAttentionLowKv:F1}～{VoltageAttentionHighKv:F1} kV · 频率 {FrequencyAttentionLowHz:F1}～{FrequencyAttentionHighHz:F1} Hz · 电流 ≤{CurrentAttentionA:F0} A · 功率 ≤{PowerAttentionMw:F0} MW");
}
