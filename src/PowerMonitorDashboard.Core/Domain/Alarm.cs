using System.Globalization;

namespace PowerMonitorDashboard.Core.Domain;

/// <summary>
/// 一条告警记录。告警由 <see cref="Monitoring.AlarmStateMachine"/> 根据实时数据产生，
/// 具有“活动 → 已确认 → 已清除”的生命周期；工况恢复后自动清除，避免误报长期驻留。
/// </summary>
public sealed class Alarm : ObservableEntity
{
    private AlarmSeverity _severity;
    private AlarmState _state;
    private string _message;
    private double _value;
    private double _threshold;
    private DateTimeOffset _lastUpdatedAt;
    private DateTimeOffset? _acknowledgedAt;
    private DateTimeOffset? _clearedAt;

    /// <summary>创建告警。</summary>
    public Alarm(
        AlarmKind kind,
        AlarmSeverity severity,
        string message,
        double value,
        double threshold,
        DateTimeOffset raisedAt)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        _severity = severity;
        _message = message;
        _value = value;
        _threshold = threshold;
        RaisedAt = raisedAt;
        _lastUpdatedAt = raisedAt;
        _state = AlarmState.Active;
    }

    /// <summary>告警唯一标识。</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>告警类型（去重键）。</summary>
    public AlarmKind Kind { get; init; }

    /// <summary>告警级别。</summary>
    public AlarmSeverity Severity
    {
        get => _severity;
        private set
        {
            if (Set(ref _severity, value))
            {
                OnPropertiesChanged(nameof(SeverityText), nameof(IsCritical));
            }
        }
    }

    /// <summary>生命周期状态。</summary>
    public AlarmState State
    {
        get => _state;
        private set
        {
            if (Set(ref _state, value))
            {
                OnPropertiesChanged(nameof(StateText), nameof(IsActive), nameof(CanAcknowledge), nameof(DurationText));
            }
        }
    }

    /// <summary>告警描述（包含当前值与阈值）。</summary>
    public string Message
    {
        get => _message;
        private set => Set(ref _message, value);
    }

    /// <summary>触发告警时的实时值。</summary>
    public double Value
    {
        get => _value;
        private set => Set(ref _value, value);
    }

    /// <summary>触发告警的阈值。</summary>
    public double Threshold
    {
        get => _threshold;
        private set => Set(ref _threshold, value);
    }

    /// <summary>首次产生时间。</summary>
    public DateTimeOffset RaisedAt { get; }

    /// <summary>最近一次刷新时间。</summary>
    public DateTimeOffset LastUpdatedAt
    {
        get => _lastUpdatedAt;
        private set
        {
            if (Set(ref _lastUpdatedAt, value))
            {
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    /// <summary>确认时间。</summary>
    public DateTimeOffset? AcknowledgedAt
    {
        get => _acknowledgedAt;
        private set => Set(ref _acknowledgedAt, value);
    }

    /// <summary>清除时间。</summary>
    public DateTimeOffset? ClearedAt
    {
        get => _clearedAt;
        private set => Set(ref _clearedAt, value);
    }

    /// <summary>是否为活动（未清除）告警。</summary>
    public bool IsActive => State != AlarmState.Cleared;

    /// <summary>是否可确认。</summary>
    public bool CanAcknowledge => State == AlarmState.Active;

    /// <summary>是否为严重告警。</summary>
    public bool IsCritical => Severity == AlarmSeverity.Critical;

    /// <summary>级别中文名称。</summary>
    public string SeverityText => Severity switch
    {
        AlarmSeverity.Critical => "严重",
        AlarmSeverity.Warning => "告警",
        _ => "提示"
    };

    /// <summary>状态中文名称。</summary>
    public string StateText => State switch
    {
        AlarmState.Active => "未处理",
        AlarmState.Acknowledged => "已确认",
        _ => "已恢复"
    };

    /// <summary>首次产生时间文本。</summary>
    public string RaisedAtText => RaisedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>持续时长文本。</summary>
    public string DurationText
    {
        get
        {
            var end = ClearedAt ?? LastUpdatedAt;
            var span = end - RaisedAt;
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}时{span.Minutes}分"
                : $"{Math.Max(0, (int)span.TotalSeconds)}秒";
        }
    }

    /// <summary>刷新告警的值与描述。</summary>
    internal void Refresh(string message, double value, double threshold, DateTimeOffset now)
    {
        Message = message;
        Value = value;
        Threshold = threshold;
        LastUpdatedAt = now;
    }

    /// <summary>提升严重级别。</summary>
    internal void Escalate(AlarmSeverity severity, string message, double value, double threshold, DateTimeOffset now)
    {
        Severity = severity;
        Message = message;
        Value = value;
        Threshold = threshold;
        LastUpdatedAt = now;
    }

    /// <summary>值班员确认告警。</summary>
    public bool Acknowledge(DateTimeOffset now)
    {
        if (!CanAcknowledge)
        {
            return false;
        }

        State = AlarmState.Acknowledged;
        AcknowledgedAt = now;
        return true;
    }

    /// <summary>工况恢复，清除告警。</summary>
    internal bool Clear(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return false;
        }

        State = AlarmState.Cleared;
        ClearedAt = now;
        LastUpdatedAt = now;
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"[{SeverityText}] {Message}";
}
