using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Configuration;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Export;
using PowerMonitorDashboard.Core.Monitoring;
using PowerMonitorDashboard.Core.Simulation;
using PowerMonitorDashboard.Core.Validation;
using PowerMonitorDashboard.Infrastructure;
using PowerMonitorDashboard.Mvvm;
using PowerMonitorDashboard.Services;
using PowerMonitorDashboard.Visuals;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>提示条级别。</summary>
public enum NotificationLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// 主界面视图模型：组合实时指标、拓扑、趋势、历史、告警、录入与视频等子视图模型，
/// 并把采集线程的结果统一分发到 UI 线程。
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly MonitoringService _service;
    private readonly ITelemetrySource _source;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly IUiDispatcher _dispatcher;
    private readonly IUiScheduler _scheduler;
    private readonly IFileDialogService _dialogs;
    private readonly IUserDialogService _userDialogs;
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _settingsStore;
    private readonly JsonHistoryStore _historyStore;
    private readonly IDisposable _clockTick;
    private readonly IDisposable _autoSaveTick;
    private readonly Random _random = new();
    private readonly DateTimeOffset _startedAt;

    private MonitorSettings _settings;
    private IDisposable? _notificationHandle;
    private DateTimeOffset _lastProcessedAt = DateTimeOffset.MinValue;
    private string _clockText = string.Empty;
    private string _dateText = string.Empty;
    private string _statusMessage = "系统就绪，等待采集数据。";
    private string _connectionText = "仿真数据源 · 待机";
    private bool _isConnectionHealthy = true;
    private string _notificationText = string.Empty;
    private bool _isNotificationVisible;
    private NotificationLevel _notificationLevel = NotificationLevel.Info;
    private Brush _notificationBrush = AppBrushes.Accent;
    private string _lastSavedText = "尚未保存";
    private int _disturbanceIndex;

    /// <summary>创建主视图模型。</summary>
    public MainViewModel(
        MonitoringService service,
        ITelemetrySource source,
        IClock clock,
        IAppLogger logger,
        IUiDispatcher dispatcher,
        IUiScheduler scheduler,
        IFileDialogService dialogs,
        IUserDialogService userDialogs,
        AppPaths paths,
        MonitorSettings settings,
        JsonSettingsStore settingsStore,
        JsonHistoryStore historyStore,
        IReadOnlyList<PowerReading> initialHistory)
    {
        _service = service;
        _source = source;
        _clock = clock;
        _logger = logger;
        _dispatcher = dispatcher;
        _scheduler = scheduler;
        _dialogs = dialogs;
        _userDialogs = userDialogs;
        _paths = paths;
        _settings = settings;
        _settingsStore = settingsStore;
        _historyStore = historyStore;
        _startedAt = clock.Now;

        Live = new LiveMetricsViewModel(() => _service.Thresholds, clock);
        Topology = new TopologyViewModel();
        History = new HistoryViewModel(() => _service.Thresholds)
        {
            DisplayLimit = settings.HistoryDisplayLimit,
            AutoScroll = settings.AutoScrollHistory
        };
        Alarms = new AlarmPanelViewModel(clock);
        Video = new VideoViewModel(logger);
        Entry = new DataEntryViewModel(
            clock,
            () => _service.Thresholds,
            () => _settings.AssumedPowerFactor,
            () => Live.HasData ? Live.Reading : null,
            SubmitDraft);

        LoadInitialState(initialHistory);
        Video.SetSource(_paths.Resolve(_settings.VideoRelativePath));

        ToggleAcquisitionCommand = new RelayCommand(ToggleAcquisition);
        RefreshCommand = new RelayCommand(RefreshNow);
        DisturbanceCommand = new RelayCommand(InjectDisturbance);
        OpenSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        ExportHistoryCommand = new RelayCommand(ExportHistory);
        ExportAlarmsCommand = new RelayCommand(ExportAlarms);
        ClearHistoryCommand = new RelayCommand(ClearHistory, () => History.Rows.Count > 0);
        ToggleFullScreenCommand = new RelayCommand(() => FullScreenRequested?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        AboutCommand = new RelayCommand(ShowAbout);

        _clockText = clock.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _dateText = clock.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + WeekdayText(clock.Now.DayOfWeek);

        _service.ReadingProcessed += OnReadingProcessed;
        _source.RunningChanged += OnSourceRunningChanged;
        Entry.Submitted += OnEntrySubmitted;

        _clockTick = _scheduler.Schedule(TimeSpan.FromSeconds(1), OnClockTick);
        _autoSaveTick = _scheduler.Schedule(TimeSpan.FromMinutes(5), () => SaveHistory(notify: false));

        _logger.Info("主视图模型初始化完成。");
    }

    /// <summary>请求打开设置对话框。</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>请求切换全屏。</summary>
    public event EventHandler? FullScreenRequested;

    /// <summary>请求退出程序。</summary>
    public event EventHandler? ExitRequested;

    /// <summary>实时指标。</summary>
    public LiveMetricsViewModel Live { get; }

    /// <summary>拓扑图。</summary>
    public TopologyViewModel Topology { get; }

    /// <summary>历史表格。</summary>
    public HistoryViewModel History { get; }

    /// <summary>告警面板。</summary>
    public AlarmPanelViewModel Alarms { get; }

    /// <summary>数据录入。</summary>
    public DataEntryViewModel Entry { get; }

    /// <summary>视频播放。</summary>
    public VideoViewModel Video { get; }

    /// <summary>系统标题。</summary>
    public string Title => "电力行业可视化大屏监控系统";

    /// <summary>系统副标题。</summary>
    public string Subtitle => "220 kV 中心变电站 · 区域调度实时监控";

    /// <summary>顶部时钟。</summary>
    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    /// <summary>顶部日期。</summary>
    public string DateText
    {
        get => _dateText;
        private set => SetProperty(ref _dateText, value);
    }

    /// <summary>采集按钮文本。</summary>
    public string AcquisitionButtonText => Live.IsAcquiring ? "暂停采集" : "开始采集";

    /// <summary>数据源状态文本。</summary>
    public string ConnectionText
    {
        get => _connectionText;
        private set => SetProperty(ref _connectionText, value);
    }

    /// <summary>数据源是否健康。</summary>
    public bool IsConnectionHealthy
    {
        get => _isConnectionHealthy;
        private set => SetProperty(ref _isConnectionHealthy, value);
    }

    /// <summary>底部状态栏文本。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>最近一次自动保存时间。</summary>
    public string LastSavedText
    {
        get => _lastSavedText;
        private set => SetProperty(ref _lastSavedText, value);
    }

    /// <summary>提示条文本。</summary>
    public string NotificationText
    {
        get => _notificationText;
        private set => SetProperty(ref _notificationText, value);
    }

    /// <summary>提示条是否可见。</summary>
    public bool IsNotificationVisible
    {
        get => _isNotificationVisible;
        private set => SetProperty(ref _isNotificationVisible, value);
    }

    /// <summary>提示条级别。</summary>
    public NotificationLevel NotificationLevel
    {
        get => _notificationLevel;
        private set
        {
            if (SetProperty(ref _notificationLevel, value))
            {
                NotificationBrush = value switch
                {
                    NotificationLevel.Success => AppBrushes.Success,
                    NotificationLevel.Warning => AppBrushes.Warning,
                    NotificationLevel.Error => AppBrushes.Danger,
                    _ => AppBrushes.Accent
                };
            }
        }
    }

    /// <summary>提示条主题色。</summary>
    public Brush NotificationBrush
    {
        get => _notificationBrush;
        private set => SetProperty(ref _notificationBrush, value);
    }

    /// <summary>历史记录数量文本。</summary>
    public string RecordCountText => History.CountText;

    /// <summary>告警摘要文本。</summary>
    public string AlarmSummaryText => Alarms.SummaryText;

    /// <summary>当日电量文本。</summary>
    public string DailyEnergyText => Live.DailyEnergyText;

    /// <summary>值班员名称。</summary>
    public string OperatorName => string.IsNullOrWhiteSpace(_settings.OperatorName) ? "值班员" : _settings.OperatorName;

    /// <summary>采样周期文本。</summary>
    public string SamplingIntervalText => _settings.SamplingIntervalMs.ToString("N0", CultureInfo.InvariantCulture) + " ms";

    /// <summary>是否显示数据质量列。</summary>
    public bool ShowQualityColumn => _settings.ShowQualityColumn;

    /// <summary>版本信息。</summary>
    public string VersionText
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "v1.0.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>快捷键说明。</summary>
    public string ShortcutsText => "F5 刷新 · F11 全屏 · Esc 退出全屏 · Ctrl+, 设置 · Ctrl+E 导出历史 · Ctrl+Q 退出";

    /// <summary>本次运行时长文本。</summary>
    public string UptimeText
    {
        get
        {
            var span = _clock.Now - _startedAt;
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours} 小时 {span.Minutes} 分"
                : $"{span.Minutes} 分 {span.Seconds} 秒";
        }
    }

    /// <summary>暂停/恢复采集命令。</summary>
    public RelayCommand ToggleAcquisitionCommand { get; }

    /// <summary>立即刷新一条数据。</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>注入一次扰动（告警演练）。</summary>
    public RelayCommand DisturbanceCommand { get; }

    /// <summary>打开设置。</summary>
    public RelayCommand OpenSettingsCommand { get; }

    /// <summary>导出历史数据。</summary>
    public RelayCommand ExportHistoryCommand { get; }

    /// <summary>导出告警记录。</summary>
    public RelayCommand ExportAlarmsCommand { get; }

    /// <summary>清空历史数据。</summary>
    public RelayCommand ClearHistoryCommand { get; }

    /// <summary>切换全屏。</summary>
    public RelayCommand ToggleFullScreenCommand { get; }

    /// <summary>退出程序。</summary>
    public RelayCommand ExitCommand { get; }

    /// <summary>关于。</summary>
    public RelayCommand AboutCommand { get; }

    /// <summary>启动采集（若配置允许）。</summary>
    public void Start()
    {
        if (_settings.AutoStartAcquisition)
        {
            _source.Start();
        }

        Live.SetAcquiring(_source.IsRunning);
        OnPropertiesChanged(nameof(AcquisitionButtonText));
        StatusMessage = _source.IsRunning ? "实时监控已启动，仿真数据源正在推送数据。" : "系统就绪，采集处于暂停状态。";
        Notify("监控系统已启动，按 F5 可立即刷新数据。", NotificationLevel.Success);
    }

    /// <summary>创建用于设置对话框的视图模型。</summary>
    public SettingsViewModel CreateSettingsViewModel() => new(_settings.Clone(), _dialogs, _paths);

    /// <summary>应用新的设置。</summary>
    public void ApplySettings(MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _service.UpdateThresholds(settings.Thresholds);
        Live.RefreshThresholds();
        History.DisplayLimit = settings.HistoryDisplayLimit;
        History.AutoScroll = settings.AutoScrollHistory;
        Entry.Revalidate();
        Video.SetSource(_paths.Resolve(settings.VideoRelativePath));

        try
        {
            _settingsStore.Save(settings);
            Notify("设置已保存并立即生效。", NotificationLevel.Success);
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败。", ex);
            Notify("设置已生效，但写入配置文件失败。", NotificationLevel.Error);
        }

        OnPropertiesChanged(
            nameof(OperatorName),
            nameof(SamplingIntervalText),
            nameof(ShowQualityColumn));
    }

    /// <summary>保存历史数据。</summary>
    public void SaveHistory(bool notify)
    {
        var snapshot = _service.GetRecent(_settings.HistoryCapacity);
        _historyStore.Save(snapshot);
        LastSavedText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        if (notify)
        {
            Notify($"已保存 {snapshot.Count} 条历史数据。", NotificationLevel.Success);
        }
    }

    /// <summary>释放资源并停止采集。</summary>
    public void Dispose()
    {
        _service.ReadingProcessed -= OnReadingProcessed;
        _source.RunningChanged -= OnSourceRunningChanged;
        Entry.Submitted -= OnEntrySubmitted;
        _clockTick.Dispose();
        _autoSaveTick.Dispose();
        _notificationHandle?.Dispose();
        _source.Stop();
        SaveHistory(notify: false);
        _logger.Info("主视图模型已释放。");
    }

    private void LoadInitialState(IReadOnlyList<PowerReading> initialHistory)
    {
        if (initialHistory.Count == 0)
        {
            Alarms.Load(_service.AlarmLog);
            return;
        }

        History.Load(initialHistory);

        var last = initialHistory[^1];
        var evaluation = StatusEvaluator.Evaluate(last, _service.Thresholds);
        Live.Initialize(initialHistory, evaluation);
        Topology.Update(last, evaluation);
        Alarms.Load(_service.AlarmLog);

        StatusMessage = $"已恢复 {initialHistory.Count} 条历史数据，最近一条：{last.TimestampText}。";
        Notify($"已从本地恢复 {initialHistory.Count} 条历史数据。", NotificationLevel.Info);
    }

    private void OnReadingProcessed(object? sender, ProcessingResult result)
        => _dispatcher.Post(() => Apply(result));

    private void Apply(ProcessingResult result)
    {
        _lastProcessedAt = _clock.Now;
        IsConnectionHealthy = true;
        ConnectionText = "仿真数据源 · 正常";

        Live.Update(result);
        Topology.Update(result);
        History.Add(result);
        Alarms.Apply(result);
        StatusMessage = $"最近更新 {result.Reading.TimeText} · {result.Status.Summary}";

        foreach (var alarm in result.Alarms.Raised)
        {
            Notify(alarm.Message, alarm.Severity == AlarmSeverity.Critical ? NotificationLevel.Error : NotificationLevel.Warning);
        }

        OnPropertiesChanged(
            nameof(RecordCountText),
            nameof(AlarmSummaryText),
            nameof(DailyEnergyText),
            nameof(StatusMessage));
    }

    private void OnSourceRunningChanged(object? sender, bool running)
        => _dispatcher.Post(() =>
        {
            Live.SetAcquiring(running);
            OnPropertyChanged(nameof(AcquisitionButtonText));
            ConnectionText = running ? "仿真数据源 · 正常" : "仿真数据源 · 已暂停";
            IsConnectionHealthy = running || !_settings.AutoStartAcquisition;
        });

    private void OnEntrySubmitted(object? sender, DataEntryFeedback feedback)
        => Notify(feedback.Message, feedback.Success ? NotificationLevel.Success : NotificationLevel.Error);

    private void OnClockTick()
    {
        var now = _clock.Now;
        ClockText = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        DateText = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + WeekdayText(now.DayOfWeek);
        OnPropertyChanged(nameof(UptimeText));

        if (!_source.IsRunning || _lastProcessedAt == DateTimeOffset.MinValue)
        {
            return;
        }

        var staleAfter = TimeSpan.FromMilliseconds(Math.Max(5000, _settings.SamplingIntervalMs * 4d));
        var stale = now - _lastProcessedAt > staleAfter;
        if (stale)
        {
            IsConnectionHealthy = false;
            ConnectionText = "数据延迟 · 正在重连";
        }
    }

    private void ToggleAcquisition()
    {
        if (_source.IsRunning)
        {
            _source.Stop();
            Notify("采集已暂停，界面保留最后一帧数据。", NotificationLevel.Info);
        }
        else
        {
            _source.Start();
            Notify("采集已恢复。", NotificationLevel.Success);
        }
    }

    private void RefreshNow()
    {
        _source.ProduceNow();
        StatusMessage = "已手动刷新一条实时数据。";
    }

    private void InjectDisturbance()
    {
        var kinds = new[]
        {
            DisturbanceKind.FrequencySurge,
            DisturbanceKind.VoltageSag,
            DisturbanceKind.LoadSpike,
            DisturbanceKind.FrequencyDip
        };
        var kind = kinds[_disturbanceIndex++ % kinds.Length];
        _source.InjectDisturbance(kind);

        var text = kind switch
        {
            DisturbanceKind.FrequencySurge => "已注入频率上扰，注意频率越限告警。",
            DisturbanceKind.FrequencyDip => "已注入频率下扰，注意频率越限告警。",
            DisturbanceKind.VoltageSag => "已注入电压暂降，注意电压越限告警。",
            _ => "已注入负荷冲击，注意功率越限告警。"
        };
        Notify(text, NotificationLevel.Warning);
    }

    private void ExportHistory()
    {
        var rows = History.GetVisibleReadings();
        if (rows.Count == 0)
        {
            Notify("当前没有可导出的历史数据。", NotificationLevel.Warning);
            return;
        }

        var path = _dialogs.PickSaveFile(
            "导出历史数据",
            "CSV 文件|*.csv|所有文件|*.*",
            _paths.CreateExportFileName("history", "csv"));

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            CsvFileWriter.Write(path, CsvFormatter.ForReadings(rows.Reverse(), _service.Thresholds));
            Notify($"已导出 {rows.Count} 条历史数据到 {Path.GetFileName(path)}。", NotificationLevel.Success);
            _logger.Info($"历史数据已导出：{path}");
        }
        catch (Exception ex)
        {
            _logger.Error("导出历史数据失败。", ex);
            _userDialogs.ShowError("导出失败", ex.Message);
        }
    }

    private void ExportAlarms()
    {
        var alarms = _service.AlarmLog;
        if (alarms.Count == 0)
        {
            Notify("当前没有告警记录。", NotificationLevel.Warning);
            return;
        }

        var path = _dialogs.PickSaveFile(
            "导出告警记录",
            "CSV 文件|*.csv|所有文件|*.*",
            _paths.CreateExportFileName("alarms", "csv"));

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            CsvFileWriter.Write(path, CsvFormatter.ForAlarms(alarms));
            Notify($"已导出 {alarms.Count} 条告警记录。", NotificationLevel.Success);
        }
        catch (Exception ex)
        {
            _logger.Error("导出告警记录失败。", ex);
            _userDialogs.ShowError("导出失败", ex.Message);
        }
    }

    private void ClearHistory()
    {
        if (!_userDialogs.Confirm("清空历史数据", "确认清空当前历史数据？该操作不可撤销，但不会影响正在进行的采集。"))
        {
            return;
        }

        _service.ClearHistory();
        History.Clear();
        Notify("历史数据已清空。", NotificationLevel.Info);
        OnPropertiesChanged(nameof(RecordCountText));
    }

    private void ShowAbout()
    {
        _userDialogs.ShowInfo(
            "关于本系统",
            $"{Title}\n版本 {VersionText}\n\nWPF 电力监控可视化大屏教学项目\n数据源：内置电网仿真器（可替换为 Modbus/OPC UA）\n\n快捷键：{ShortcutsText}");
    }

    private DataEntryFeedback SubmitDraft(ReadingDraft draft)
    {
        if (!ReadingValidator.TryBuildReading(draft, _clock.Now, _settings.AssumedPowerFactor, out var reading, out var validation)
            || reading is null)
        {
            return new DataEntryFeedback(validation.FirstError?.Message ?? "数据校验未通过。", false);
        }

        var result = _service.AcceptManual(reading);
        if (result is null)
        {
            return new DataEntryFeedback("时间戳不能晚于当前时间，保存失败。", false);
        }

        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"已补录 {reading.TimeText} 数据：{reading.VoltageKv:F1} kV / {reading.CurrentA:F0} A / {reading.PowerMw:F2} MW");

        if (validation.HasWarnings)
        {
            var warnings = validation.Issues
                .Where(static i => i.Severity == ValidationSeverity.Warning)
                .Select(static i => i.Message);
            message += "；提示：" + string.Join("；", warnings);
        }

        return new DataEntryFeedback(message, true);
    }

    private void Notify(string text, NotificationLevel level)
    {
        NotificationLevel = level;
        NotificationText = text;
        IsNotificationVisible = true;

        _notificationHandle?.Dispose();
        _notificationHandle = _scheduler.ScheduleOnce(TimeSpan.FromSeconds(5), () => IsNotificationVisible = false);
    }

    private static string WeekdayText(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "星期一",
        DayOfWeek.Tuesday => "星期二",
        DayOfWeek.Wednesday => "星期三",
        DayOfWeek.Thursday => "星期四",
        DayOfWeek.Friday => "星期五",
        DayOfWeek.Saturday => "星期六",
        _ => "星期日"
    };
}
