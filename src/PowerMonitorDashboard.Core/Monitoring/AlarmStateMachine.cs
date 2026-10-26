using System.Globalization;
using PowerMonitorDashboard.Core.Domain;

namespace PowerMonitorDashboard.Core.Monitoring;

/// <summary>一次告警评估产生的变更集合。</summary>
/// <param name="Raised">本次新产生的告警。</param>
/// <param name="Escalated">本次由告警升级为严重的告警。</param>
/// <param name="Updated">数值/描述被刷新的活动告警。</param>
/// <param name="Cleared">本次因工况恢复而清除的告警。</param>
public sealed record AlarmBatch(
    IReadOnlyList<Alarm> Raised,
    IReadOnlyList<Alarm> Escalated,
    IReadOnlyList<Alarm> Updated,
    IReadOnlyList<Alarm> Cleared)
{
    /// <summary>空批次。</summary>
    public static AlarmBatch Empty { get; } = new([], [], [], []);

    /// <summary>是否存在任何变化。</summary>
    public bool HasChanges => Raised.Count > 0 || Escalated.Count > 0 || Cleared.Count > 0;
}

/// <summary>
/// 告警状态机：把连续的实时数据转换为“产生 / 升级 / 刷新 / 清除”四种事件。
/// </summary>
/// <remarks>
/// 关键设计是<b>回差（hysteresis）</b>：越限后必须回到阈值以内一段距离才清除，
/// 否则数据在阈值附近抖动会产生大量“产生—清除”告警风暴。
/// 该类型不是线程安全的，由监控服务串行调用。
/// </remarks>
public sealed class AlarmStateMachine
{
    private readonly Dictionary<AlarmKind, Alarm> _active = new();

    /// <summary>当前活动告警（未清除）。</summary>
    public IReadOnlyCollection<Alarm> ActiveAlarms => _active.Values;

    /// <summary>活动告警数量。</summary>
    public int ActiveCount => _active.Count;

    /// <summary>是否存在严重告警。</summary>
    public bool HasCritical => _active.Values.Any(static a => a.Severity == AlarmSeverity.Critical);

    /// <summary>
    /// 使用一条实时数据刷新告警状态。
    /// </summary>
    public AlarmBatch Update(PowerReading reading, PowerThresholds thresholds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(thresholds);

        var raised = new List<Alarm>();
        var escalated = new List<Alarm>();
        var updated = new List<Alarm>();
        var cleared = new List<Alarm>();

        // 频率：49.5~50.5 Hz 为正常区间，回差 0.05 Hz。
        ApplyUpper(AlarmKind.FrequencyHigh, reading.FrequencyHz, thresholds.FrequencyAttentionHighHz, thresholds.FrequencyCriticalHighHz, 0.05, "频率", "Hz", now, raised, escalated, updated, cleared);
        ApplyLower(AlarmKind.FrequencyLow, reading.FrequencyHz, thresholds.FrequencyAttentionLowHz, thresholds.FrequencyCriticalLowHz, 0.05, "频率", "Hz", now, raised, escalated, updated, cleared);

        // 电压：回差取标称电压的 0.5%。
        var voltageHysteresis = thresholds.NominalVoltageKv * 0.005;
        ApplyUpper(AlarmKind.VoltageHigh, reading.VoltageKv, thresholds.VoltageAttentionHighKv, thresholds.VoltageCriticalHighKv, voltageHysteresis, "电压", "kV", now, raised, escalated, updated, cleared);
        ApplyLower(AlarmKind.VoltageLow, reading.VoltageKv, thresholds.VoltageAttentionLowKv, thresholds.VoltageCriticalLowKv, voltageHysteresis, "电压", "kV", now, raised, escalated, updated, cleared);

        // 电流、功率：回差取告警值的 1%。
        ApplyUpper(AlarmKind.OverCurrent, reading.CurrentA, thresholds.CurrentAttentionA, thresholds.CurrentCriticalA, thresholds.CurrentAttentionA * 0.01, "电流", "A", now, raised, escalated, updated, cleared);
        ApplyUpper(AlarmKind.Overload, reading.PowerMw, thresholds.PowerAttentionMw, thresholds.PowerCriticalMw, thresholds.PowerAttentionMw * 0.01, "有功功率", "MW", now, raised, escalated, updated, cleared);

        return new AlarmBatch(raised, escalated, updated, cleared);
    }

    /// <summary>按告警标识确认告警。</summary>
    public bool Acknowledge(Guid alarmId, DateTimeOffset now)
    {
        var alarm = _active.Values.FirstOrDefault(a => a.Id == alarmId);
        return alarm is not null && alarm.Acknowledge(now);
    }

    /// <summary>确认指定告警对象。</summary>
    public bool Acknowledge(Alarm alarm, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        return alarm.Acknowledge(now);
    }

    /// <summary>清除全部活动告警（例如停止采集后复位）。</summary>
    public IReadOnlyList<Alarm> Reset(DateTimeOffset now)
    {
        var cleared = new List<Alarm>(_active.Count);
        foreach (var alarm in _active.Values)
        {
            if (alarm.Clear(now))
            {
                cleared.Add(alarm);
            }
        }

        _active.Clear();
        return cleared;
    }

    private void ApplyUpper(
        AlarmKind kind,
        double value,
        double warning,
        double critical,
        double hysteresis,
        string name,
        string unit,
        DateTimeOffset now,
        List<Alarm> raised,
        List<Alarm> escalated,
        List<Alarm> updated,
        List<Alarm> cleared)
    {
        if (_active.TryGetValue(kind, out var alarm))
        {
            if (value <= warning - hysteresis)
            {
                alarm.Clear(now);
                _active.Remove(kind);
                cleared.Add(alarm);
                return;
            }

            var severity = value > critical ? AlarmSeverity.Critical : AlarmSeverity.Warning;
            var message = BuildMessage(name, value, unit, severity == AlarmSeverity.Critical ? critical : warning, upper: true, severity);
            if (severity > alarm.Severity)
            {
                alarm.Escalate(severity, message, value, severity == AlarmSeverity.Critical ? critical : warning, now);
                escalated.Add(alarm);
            }
            else
            {
                alarm.Refresh(message, value, severity == AlarmSeverity.Critical ? critical : warning, now);
                updated.Add(alarm);
            }

            return;
        }

        if (value <= warning)
        {
            return;
        }

        var newSeverity = value > critical ? AlarmSeverity.Critical : AlarmSeverity.Warning;
        var threshold = newSeverity == AlarmSeverity.Critical ? critical : warning;
        var newAlarm = new Alarm(kind, newSeverity, BuildMessage(name, value, unit, threshold, upper: true, newSeverity), value, threshold, now);
        _active[kind] = newAlarm;
        raised.Add(newAlarm);
    }

    private void ApplyLower(
        AlarmKind kind,
        double value,
        double warning,
        double critical,
        double hysteresis,
        string name,
        string unit,
        DateTimeOffset now,
        List<Alarm> raised,
        List<Alarm> escalated,
        List<Alarm> updated,
        List<Alarm> cleared)
    {
        if (_active.TryGetValue(kind, out var alarm))
        {
            if (value >= warning + hysteresis)
            {
                alarm.Clear(now);
                _active.Remove(kind);
                cleared.Add(alarm);
                return;
            }

            var severity = value < critical ? AlarmSeverity.Critical : AlarmSeverity.Warning;
            var threshold = severity == AlarmSeverity.Critical ? critical : warning;
            var message = BuildMessage(name, value, unit, threshold, upper: false, severity);
            if (severity > alarm.Severity)
            {
                alarm.Escalate(severity, message, value, threshold, now);
                escalated.Add(alarm);
            }
            else
            {
                alarm.Refresh(message, value, threshold, now);
                updated.Add(alarm);
            }

            return;
        }

        if (value >= warning)
        {
            return;
        }

        var newSeverity = value < critical ? AlarmSeverity.Critical : AlarmSeverity.Warning;
        var newThreshold = newSeverity == AlarmSeverity.Critical ? critical : warning;
        var newAlarm = new Alarm(kind, newSeverity, BuildMessage(name, value, unit, newThreshold, upper: false, newSeverity), value, newThreshold, now);
        _active[kind] = newAlarm;
        raised.Add(newAlarm);
    }

    private static string BuildMessage(string name, double value, string unit, double threshold, bool upper, AlarmSeverity severity)
    {
        var level = severity == AlarmSeverity.Critical ? "严重" : string.Empty;
        var direction = upper ? "高于" : "低于";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{name}{level}越限：{value:F2} {unit}，{direction}阈值 {threshold:F2} {unit}");
    }
}
