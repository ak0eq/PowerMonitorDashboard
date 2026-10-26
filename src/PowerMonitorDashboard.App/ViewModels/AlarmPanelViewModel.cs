using System.Collections.ObjectModel;
using System.Globalization;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Mvvm;
using PowerMonitorDashboard.Services;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>
/// 告警面板视图模型。维护“最近告警”列表与活动告警统计，
/// 支持单条确认与一键确认，确认状态会同步到领域对象。
/// </summary>
public sealed class AlarmPanelViewModel : ObservableObject
{
    private const int MaxRows = 120;

    private readonly IClock _clock;
    private Alarm? _selectedAlarm;
    private int _activeCount;
    private int _criticalCount;
    private int _warningCount;

    /// <summary>创建告警面板。</summary>
    public AlarmPanelViewModel(IClock clock)
    {
        _clock = clock;
        AcknowledgeCommand = new RelayCommand<Alarm>(Acknowledge);
        AcknowledgeAllCommand = new RelayCommand(AcknowledgeAll, () => ActiveCount > 0);
    }

    /// <summary>最近告警（含已恢复），按产生时间倒序。</summary>
    public ObservableCollection<Alarm> Recent { get; } = [];

    /// <summary>当前选中的告警。</summary>
    public Alarm? SelectedAlarm
    {
        get => _selectedAlarm;
        set => SetProperty(ref _selectedAlarm, value);
    }

    /// <summary>活动告警数。</summary>
    public int ActiveCount
    {
        get => _activeCount;
        private set
        {
            if (SetProperty(ref _activeCount, value))
            {
                OnPropertiesChanged(nameof(HasActive), nameof(SummaryText), nameof(BadgeText));
            }
        }
    }

    /// <summary>严重告警数。</summary>
    public int CriticalCount
    {
        get => _criticalCount;
        private set
        {
            if (SetProperty(ref _criticalCount, value))
            {
                OnPropertiesChanged(nameof(HasCritical), nameof(SummaryText));
            }
        }
    }

    /// <summary>一般告警数。</summary>
    public int WarningCount
    {
        get => _warningCount;
        private set
        {
            if (SetProperty(ref _warningCount, value))
            {
                OnPropertiesChanged(nameof(SummaryText));
            }
        }
    }

    /// <summary>是否存在活动告警。</summary>
    public bool HasActive => ActiveCount > 0;

    /// <summary>是否存在严重告警。</summary>
    public bool HasCritical => CriticalCount > 0;

    /// <summary>统计摘要。</summary>
    public string SummaryText => ActiveCount == 0
        ? "无活动告警"
        : string.Create(CultureInfo.InvariantCulture, $"{ActiveCount} 条活动 · {CriticalCount} 严重 · {WarningCount} 告警");

    /// <summary>徽标文本。</summary>
    public string BadgeText => ActiveCount.ToString(CultureInfo.InvariantCulture);

    /// <summary>确认单条告警命令。</summary>
    public RelayCommand<Alarm> AcknowledgeCommand { get; }

    /// <summary>确认全部告警命令。</summary>
    public RelayCommand AcknowledgeAllCommand { get; }

    /// <summary>应用一次处理结果。</summary>
    public void Apply(ProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var alarm in result.Alarms.Raised)
        {
            Recent.Insert(0, alarm);
        }

        while (Recent.Count > MaxRows)
        {
            Recent.RemoveAt(Recent.Count - 1);
        }

        Recount(result.ActiveAlarms);
    }

    /// <summary>载入历史告警。</summary>
    public void Load(IReadOnlyList<Alarm> alarms)
    {
        Recent.Clear();
        foreach (var alarm in alarms.OrderByDescending(static a => a.RaisedAt).Take(MaxRows))
        {
            Recent.Add(alarm);
        }

        Recount(alarms.Where(static a => a.IsActive).ToArray());
    }

    /// <summary>清空面板。</summary>
    public void Clear()
    {
        Recent.Clear();
        ActiveCount = 0;
        CriticalCount = 0;
        WarningCount = 0;
    }

    private void Acknowledge(Alarm? alarm)
    {
        if (alarm is null || !alarm.CanAcknowledge)
        {
            return;
        }

        alarm.Acknowledge(_clock.Now);
        Recount(Recent.Where(static a => a.IsActive).ToArray());
    }

    private void AcknowledgeAll()
    {
        foreach (var alarm in Recent.Where(static a => a.CanAcknowledge))
        {
            alarm.Acknowledge(_clock.Now);
        }

        Recount(Recent.Where(static a => a.IsActive).ToArray());
    }

    private void Recount(IReadOnlyList<Alarm> active)
    {
        ActiveCount = active.Count;
        CriticalCount = active.Count(static a => a.Severity == AlarmSeverity.Critical);
        WarningCount = ActiveCount - CriticalCount;
        RelayCommand.RaiseCanExecuteChanged();
    }
}
