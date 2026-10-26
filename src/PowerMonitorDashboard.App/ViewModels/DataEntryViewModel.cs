using System.ComponentModel;
using System.Globalization;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Validation;
using PowerMonitorDashboard.Mvvm;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>人工补录的提交结果。</summary>
/// <param name="Message">提示文本。</param>
/// <param name="Success">是否成功。</param>
public sealed record DataEntryFeedback(string Message, bool Success);

/// <summary>
/// 数据录入视图模型。表单同时提供三层校验：
/// <list type="number">
/// <item>XAML 中绑定的自定义 <c>ValidationRule</c>：逐字段实时红框提示；</item>
/// <item>本视图模型的整体校验：保存前汇总所有问题并展示警告；</item>
/// <item>Core 层 <see cref="ReadingValidator"/>：与业务规则同源的最终防线。</item>
/// </list>
/// </summary>
public sealed class DataEntryViewModel : ObservableObject
{
    private readonly IClock _clock;
    private readonly Func<PowerThresholds> _thresholds;
    private readonly Func<double> _assumedPowerFactor;
    private readonly Func<PowerReading?> _liveReading;
    private readonly Func<ReadingDraft, DataEntryFeedback> _submit;

    private string _voltageText = "220.0";
    private string _currentText = "0";
    private string _powerText = string.Empty;
    private string _frequencyText = "50.00";
    private string _timestampText = string.Empty;
    private IReadOnlyList<ValidationIssue> _issues = [];
    private string _validationSummary = string.Empty;
    private bool _hasErrors;
    private bool _hasWarnings;
    private string _statusMessage = string.Empty;
    private bool _statusIsError = true;

    /// <summary>创建录入视图模型。</summary>
    public DataEntryViewModel(
        IClock clock,
        Func<PowerThresholds> thresholds,
        Func<double> assumedPowerFactor,
        Func<PowerReading?> liveReading,
        Func<ReadingDraft, DataEntryFeedback> submit)
    {
        _clock = clock;
        _thresholds = thresholds;
        _assumedPowerFactor = assumedPowerFactor;
        _liveReading = liveReading;
        _submit = submit;
        _timestampText = clock.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(Reset);
        FillFromLiveCommand = new RelayCommand(FillFromLive);
        UseCurrentTimeCommand = new RelayCommand(UseCurrentTime);

        PropertyChanged += OnPropertyChangedInternal;
        Validate();
    }

    /// <summary>提交完成事件（用于主界面弹出提示）。</summary>
    public event EventHandler<DataEntryFeedback>? Submitted;

    /// <summary>电压输入。</summary>
    public string VoltageText
    {
        get => _voltageText;
        set => SetProperty(ref _voltageText, value);
    }

    /// <summary>电流输入。</summary>
    public string CurrentText
    {
        get => _currentText;
        set => SetProperty(ref _currentText, value);
    }

    /// <summary>功率输入（留空自动计算）。</summary>
    public string PowerText
    {
        get => _powerText;
        set => SetProperty(ref _powerText, value);
    }

    /// <summary>频率输入。</summary>
    public string FrequencyText
    {
        get => _frequencyText;
        set => SetProperty(ref _frequencyText, value);
    }

    /// <summary>采样时间戳，格式 yyyy-MM-dd HH:mm:ss。</summary>
    public string TimestampText
    {
        get => _timestampText;
        set => SetProperty(ref _timestampText, value);
    }

    /// <summary>当前校验问题集合。</summary>
    public IReadOnlyList<ValidationIssue> Issues
    {
        get => _issues;
        private set
        {
            if (SetProperty(ref _issues, value))
            {
                OnPropertiesChanged(nameof(HasIssues), nameof(IssueCount));
            }
        }
    }

    /// <summary>是否存在校验问题。</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>问题数量。</summary>
    public int IssueCount => Issues.Count;

    /// <summary>校验摘要文本。</summary>
    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    /// <summary>是否存在错误。</summary>
    public bool HasErrors
    {
        get => _hasErrors;
        private set => SetProperty(ref _hasErrors, value);
    }

    /// <summary>是否存在警告。</summary>
    public bool HasWarnings
    {
        get => _hasWarnings;
        private set => SetProperty(ref _hasWarnings, value);
    }

    /// <summary>最近一次操作反馈。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>反馈是否为错误。</summary>
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    /// <summary>保存命令。</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>重置命令。</summary>
    public RelayCommand ResetCommand { get; }

    /// <summary>以实时值填充命令。</summary>
    public RelayCommand FillFromLiveCommand { get; }

    /// <summary>把时间戳设为当前时刻。</summary>
    public RelayCommand UseCurrentTimeCommand { get; }

    /// <summary>使用当前实时值填充表单。</summary>
    public void FillFromLive()
    {
        var reading = _liveReading();
        if (reading is null)
        {
            return;
        }

        VoltageText = reading.VoltageKv.ToString("F1", CultureInfo.InvariantCulture);
        CurrentText = reading.CurrentA.ToString("F0", CultureInfo.InvariantCulture);
        PowerText = reading.PowerMw.ToString("F2", CultureInfo.InvariantCulture);
        FrequencyText = reading.FrequencyHz.ToString("F2", CultureInfo.InvariantCulture);
        TimestampText = _clock.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        StatusMessage = "已填入当前实时值，可直接保存或修改。";
        StatusIsError = false;
    }

    /// <summary>重置表单。</summary>
    public void Reset()
    {
        VoltageText = "220.0";
        CurrentText = "0";
        PowerText = string.Empty;
        FrequencyText = "50.00";
        TimestampText = _clock.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        StatusMessage = "功率留空时将按 P = √3·U·I·cosφ 自动计算。";
        StatusIsError = false;
        Validate();
    }

    /// <summary>把时间戳设为当前时刻。</summary>
    public void UseCurrentTime()
    {
        TimestampText = _clock.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>重新执行校验（阈值等外部配置变化时调用）。</summary>
    public void Revalidate() => Validate();

    private void Save()
    {
        Validate();
        if (HasErrors)
        {
            StatusMessage = "存在校验错误，无法保存。请修正标红字段后重试。";
            StatusIsError = true;
            return;
        }

        var feedback = _submit(BuildDraft());
        StatusMessage = feedback.Message;
        StatusIsError = !feedback.Success;
        Submitted?.Invoke(this, feedback);

        if (feedback.Success)
        {
            // 保存成功后刷新时间戳，数值保留，方便连续录入。
            TimestampText = _clock.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    private void Validate()
    {
        var result = ReadingValidator.Validate(BuildDraft(), _clock.Now, _assumedPowerFactor());
        Issues = result.Issues;
        ValidationSummary = result.Summary;
        HasErrors = !result.IsValid;
        HasWarnings = result.HasWarnings;
    }

    private ReadingDraft BuildDraft()
        => new(VoltageText, CurrentText, PowerText, FrequencyText, TimestampText);

    private void OnPropertyChangedInternal(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VoltageText)
            or nameof(CurrentText)
            or nameof(PowerText)
            or nameof(FrequencyText)
            or nameof(TimestampText))
        {
            Validate();
        }
    }
}
