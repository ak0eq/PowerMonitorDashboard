using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.History;
using PowerMonitorDashboard.Core.Monitoring;

namespace PowerMonitorDashboard.Services;

/// <summary>一次数据处理的完整结果，供界面一次性刷新所有区域。</summary>
public sealed record ProcessingResult(
    PowerReading Reading,
    StatusEvaluation Status,
    AlarmBatch Alarms,
    IReadOnlyList<Alarm> ActiveAlarms,
    double DailyEnergyMwh,
    int SampleCount);

/// <summary>
/// 监控服务：连接“数据源 → 校验后的历史 → 状态评估 → 告警状态机”的处理管线。
/// 该类与 UI 无关，可在后台线程调用；界面通过 <see cref="ReadingProcessed"/> 事件获取结果。
/// </summary>
public sealed class MonitoringService
{
    private readonly object _gate = new();
    private readonly ReadingHistory _history;
    private readonly AlarmStateMachine _alarms = new();
    private readonly List<Alarm> _alarmLog = [];
    private readonly IClock _clock;
    private readonly IAppLogger _logger;

    private PowerThresholds _thresholds;
    private PowerReading? _lastReading;
    private double _dailyEnergyMwh;
    private DateOnly _energyDate;
    private int _sampleCount;

    /// <summary>创建监控服务。</summary>
    public MonitoringService(PowerThresholds thresholds, int historyCapacity, IClock clock, IAppLogger logger)
    {
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
        _history = new ReadingHistory(historyCapacity);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _energyDate = DateOnly.FromDateTime(clock.Now.LocalDateTime);
    }

    /// <summary>每次数据处理完成后触发（在调用 <see cref="Accept"/> 的线程上）。</summary>
    public event EventHandler<ProcessingResult>? ReadingProcessed;

    /// <summary>当前阈值。</summary>
    public PowerThresholds Thresholds
    {
        get
        {
            lock (_gate)
            {
                return _thresholds;
            }
        }
    }

    /// <summary>最近一条数据。</summary>
    public PowerReading? LastReading
    {
        get
        {
            lock (_gate)
            {
                return _lastReading;
            }
        }
    }

    /// <summary>累计接收的样本数。</summary>
    public int SampleCount
    {
        get
        {
            lock (_gate)
            {
                return _sampleCount;
            }
        }
    }

    /// <summary>历史缓冲区。</summary>
    public ReadingHistory History => _history;

    /// <summary>告警日志快照（含已恢复告警，按时间倒序）。</summary>
    public IReadOnlyList<Alarm> AlarmLog
    {
        get
        {
            lock (_gate)
            {
                return _alarmLog.ToArray();
            }
        }
    }

    /// <summary>更新阈值配置。</summary>
    public void UpdateThresholds(PowerThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        lock (_gate)
        {
            _thresholds = thresholds;
        }

        _logger.Info("监控阈值已更新。");
    }

    /// <summary>接收并处理一条实时数据。</summary>
    public ProcessingResult Accept(PowerReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var now = _clock.Now;
        StatusEvaluation evaluation;
        AlarmBatch batch;
        double energy;
        int samples;

        lock (_gate)
        {
            UpdateEnergy(reading, now);
            evaluation = StatusEvaluator.Evaluate(reading, _thresholds);
            batch = _alarms.Update(reading, _thresholds, now);

            _history.Add(reading);
            _lastReading = reading;
            _sampleCount++;

            foreach (var alarm in batch.Raised)
            {
                _alarmLog.Insert(0, alarm);
                _logger.Warn($"产生告警：{alarm.Message}");
            }

            foreach (var alarm in batch.Escalated)
            {
                _logger.Warn($"告警升级：{alarm.Message}");
            }

            foreach (var alarm in batch.Cleared)
            {
                _logger.Info($"告警恢复：{alarm.Message}");
            }

            if (_alarmLog.Count > 500)
            {
                _alarmLog.RemoveRange(500, _alarmLog.Count - 500);
            }

            energy = _dailyEnergyMwh;
            samples = _sampleCount;
        }

        var result = new ProcessingResult(
            reading,
            evaluation,
            batch,
            _alarms.ActiveAlarms.ToArray(),
            energy,
            samples);

        ReadingProcessed?.Invoke(this, result);
        return result;
    }

    /// <summary>人工补录一条数据。返回处理结果，时间戳非法时返回 <c>null</c>。</summary>
    public ProcessingResult? AcceptManual(PowerReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (reading.Timestamp > _clock.Now)
        {
            _logger.Warn("拒绝人工补录：时间戳晚于当前时间。");
            return null;
        }

        _logger.Info($"人工补录数据：{reading}");
        return Accept(reading);
    }

    /// <summary>清空历史数据。</summary>
    public void ClearHistory()
    {
        _history.Clear();
        lock (_gate)
        {
            _lastReading = null;
            _sampleCount = 0;
        }

        _logger.Info("历史数据已清空。");
    }

    /// <summary>
    /// 从本地快照预置历史缓冲区（仅用于启动恢复，不参与告警与电量累计）。
    /// </summary>
    public void SeedHistory(IEnumerable<PowerReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var list = readings as IReadOnlyList<PowerReading> ?? readings.ToArray();
        if (list.Count == 0)
        {
            return;
        }

        _history.AddRange(list);
        _sampleCount = list.Count;
        _logger.Info($"已预置 {list.Count} 条历史数据。");
    }

    /// <summary>复位全部活动告警。</summary>
    public IReadOnlyList<Alarm> ResetAlarms()
    {
        var cleared = _alarms.Reset(_clock.Now);
        return cleared;
    }

    /// <summary>获取历史快照。</summary>
    public IReadOnlyList<PowerReading> GetHistorySnapshot() => _history.Snapshot();

    /// <summary>获取最近若干条历史数据。</summary>
    public IReadOnlyList<PowerReading> GetRecent(int count) => _history.Recent(count);

    /// <summary>计算最近一段时间内的统计摘要。</summary>
    public HistoryStatistics GetStatistics(TimeSpan window) => _history.Statistics(window, _clock.Now);

    private void UpdateEnergy(PowerReading reading, DateTimeOffset now)
    {
        var date = DateOnly.FromDateTime(now.LocalDateTime);
        if (date != _energyDate)
        {
            _energyDate = date;
            _dailyEnergyMwh = 0d;
        }

        if (_lastReading is not null)
        {
            var seconds = Math.Clamp((reading.Timestamp - _lastReading.Timestamp).TotalSeconds, 0d, 60d);
            var averagePower = (_lastReading.PowerMw + reading.PowerMw) / 2d;
            _dailyEnergyMwh += averagePower * seconds / 3600d;
        }
    }
}
