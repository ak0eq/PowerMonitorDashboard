using System.Globalization;
using PowerMonitorDashboard.Core.Domain;

namespace PowerMonitorDashboard.Core.Monitoring;

/// <summary>一次状态评估的结果。</summary>
/// <param name="Status">综合运行状态。</param>
/// <param name="Reasons">按严重程度排序的原因说明；正常时为空集合。</param>
public sealed record StatusEvaluation(OperatingStatus Status, IReadOnlyList<string> Reasons)
{
    /// <summary>用于界面显示的简要结论。</summary>
    public string Summary => Reasons.Count == 0 ? "运行正常" : Reasons[0];

    /// <summary>是否存在异常项。</summary>
    public bool HasIssues => Status != OperatingStatus.Normal;

    /// <summary>是否频率越限（用于预警指示灯）。</summary>
    public bool IsFrequencyOutOfBand { get; init; }

    /// <summary>是否电压越限。</summary>
    public bool IsVoltageOutOfBand { get; init; }

    /// <summary>是否电流越限。</summary>
    public bool IsCurrentOutOfBand { get; init; }

    /// <summary>是否功率越限。</summary>
    public bool IsPowerOutOfBand { get; init; }
}

/// <summary>
/// 依据 <see cref="PowerThresholds"/> 评估一组实时数据的运行状态。
/// 该类型无状态、线程安全，可被采集线程与测试代码直接复用。
/// </summary>
public static class StatusEvaluator
{
    /// <summary>评估实时数据。</summary>
    public static StatusEvaluation Evaluate(PowerReading reading, PowerThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(thresholds);

        var critical = new List<string>();
        var attention = new List<string>();
        var status = OperatingStatus.Normal;

        // ---- 频率 ----
        var frequencyOut = false;
        if (reading.FrequencyHz < thresholds.FrequencyCriticalLowHz)
        {
            critical.Add(Format("频率严重偏低", reading.FrequencyHz, "Hz", thresholds.FrequencyCriticalLowHz, "Hz"));
            frequencyOut = true;
        }
        else if (reading.FrequencyHz > thresholds.FrequencyCriticalHighHz)
        {
            critical.Add(Format("频率严重偏高", reading.FrequencyHz, "Hz", thresholds.FrequencyCriticalHighHz, "Hz"));
            frequencyOut = true;
        }
        else if (reading.FrequencyHz < thresholds.FrequencyAttentionLowHz)
        {
            attention.Add(Format("频率偏低", reading.FrequencyHz, "Hz", thresholds.FrequencyAttentionLowHz, "Hz"));
            frequencyOut = true;
        }
        else if (reading.FrequencyHz > thresholds.FrequencyAttentionHighHz)
        {
            attention.Add(Format("频率偏高", reading.FrequencyHz, "Hz", thresholds.FrequencyAttentionHighHz, "Hz"));
            frequencyOut = true;
        }

        // ---- 电压 ----
        var voltageOut = false;
        if (reading.VoltageKv < thresholds.VoltageCriticalLowKv)
        {
            critical.Add(Format("电压严重偏低", reading.VoltageKv, "kV", thresholds.VoltageCriticalLowKv, "kV"));
            voltageOut = true;
        }
        else if (reading.VoltageKv > thresholds.VoltageCriticalHighKv)
        {
            critical.Add(Format("电压严重偏高", reading.VoltageKv, "kV", thresholds.VoltageCriticalHighKv, "kV"));
            voltageOut = true;
        }
        else if (reading.VoltageKv < thresholds.VoltageAttentionLowKv)
        {
            attention.Add(Format("电压偏低", reading.VoltageKv, "kV", thresholds.VoltageAttentionLowKv, "kV"));
            voltageOut = true;
        }
        else if (reading.VoltageKv > thresholds.VoltageAttentionHighKv)
        {
            attention.Add(Format("电压偏高", reading.VoltageKv, "kV", thresholds.VoltageAttentionHighKv, "kV"));
            voltageOut = true;
        }

        // ---- 电流 ----
        var currentOut = false;
        if (reading.CurrentA > thresholds.CurrentCriticalA)
        {
            critical.Add(Format("电流严重越限", reading.CurrentA, "A", thresholds.CurrentCriticalA, "A"));
            currentOut = true;
        }
        else if (reading.CurrentA > thresholds.CurrentAttentionA)
        {
            attention.Add(Format("电流越限", reading.CurrentA, "A", thresholds.CurrentAttentionA, "A"));
            currentOut = true;
        }

        // ---- 有功功率 ----
        var powerOut = false;
        if (reading.PowerMw > thresholds.PowerCriticalMw)
        {
            critical.Add(Format("有功功率严重越限", reading.PowerMw, "MW", thresholds.PowerCriticalMw, "MW"));
            powerOut = true;
        }
        else if (reading.PowerMw > thresholds.PowerAttentionMw)
        {
            attention.Add(Format("有功功率越限", reading.PowerMw, "MW", thresholds.PowerAttentionMw, "MW"));
            powerOut = true;
        }

        // ---- 数据质量 ----
        if (reading.Quality == QualityFlag.Bad)
        {
            attention.Add("采集数据质量无效");
        }

        if (critical.Count > 0)
        {
            status = OperatingStatus.Critical;
        }
        else if (attention.Count > 0)
        {
            status = OperatingStatus.Attention;
        }

        var reasons = new List<string>(critical.Count + attention.Count);
        reasons.AddRange(critical);
        reasons.AddRange(attention);

        return new StatusEvaluation(status, reasons)
        {
            IsFrequencyOutOfBand = frequencyOut,
            IsVoltageOutOfBand = voltageOut,
            IsCurrentOutOfBand = currentOut,
            IsPowerOutOfBand = powerOut
        };
    }

    private static string Format(string name, double value, string unit, double limit, string limitUnit)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{name}：{value:F2} {unit}（限值 {limit:F2} {limitUnit}）");
}
