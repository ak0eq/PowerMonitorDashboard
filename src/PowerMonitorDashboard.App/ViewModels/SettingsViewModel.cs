using System.Globalization;
using System.IO;
using PowerMonitorDashboard.Core.Configuration;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Infrastructure;
using PowerMonitorDashboard.Mvvm;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>
/// “设置”对话框视图模型。所有输入先以字符串保存，点击确定时统一解析，
/// 因此可以一次性展示全部非法项，而不是让用户逐个试错。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly AppPaths _paths;

    private string _samplingIntervalText = "1000";
    private string _historyCapacityText = "5000";
    private string _historyDisplayLimitText = "500";
    private string _assumedPowerFactorText = "0.95";
    private bool _autoStart = true;
    private bool _autoScroll = true;
    private bool _showQuality = true;
    private string _operatorName = "值班员";
    private string _videoPath = string.Empty;

    private string _nominalVoltageText = "220";
    private string _voltageAttentionText = "5";
    private string _voltageCriticalText = "10";
    private string _frequencyAttentionLowText = "49.5";
    private string _frequencyAttentionHighText = "50.5";
    private string _frequencyCriticalLowText = "49.0";
    private string _frequencyCriticalHighText = "51.0";
    private string _currentAttentionText = "1350";
    private string _currentCriticalText = "1550";
    private string _powerAttentionText = "430";
    private string _powerCriticalText = "500";
    private string _powerBandHighText = "400";
    private string _powerBandLowText = "250";
    private string _installedCapacityText = "750";

    private string _validationMessage = string.Empty;

    /// <summary>创建设置视图模型。</summary>
    public SettingsViewModel(MonitorSettings settings, IFileDialogService dialogs, AppPaths paths)
    {
        _dialogs = dialogs;
        _paths = paths;
        BrowseVideoCommand = new RelayCommand(BrowseVideo);
        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        Load(settings);
    }

    /// <summary>采样周期文本。</summary>
    public string SamplingIntervalText { get => _samplingIntervalText; set => SetProperty(ref _samplingIntervalText, value); }

    /// <summary>历史容量文本。</summary>
    public string HistoryCapacityText { get => _historyCapacityText; set => SetProperty(ref _historyCapacityText, value); }

    /// <summary>历史显示行数文本。</summary>
    public string HistoryDisplayLimitText { get => _historyDisplayLimitText; set => SetProperty(ref _historyDisplayLimitText, value); }

    /// <summary>假定功率因数文本。</summary>
    public string AssumedPowerFactorText { get => _assumedPowerFactorText; set => SetProperty(ref _assumedPowerFactorText, value); }

    /// <summary>启动时自动采集。</summary>
    public bool AutoStart { get => _autoStart; set => SetProperty(ref _autoStart, value); }

    /// <summary>历史表格自动滚动。</summary>
    public bool AutoScroll { get => _autoScroll; set => SetProperty(ref _autoScroll, value); }

    /// <summary>显示数据质量列。</summary>
    public bool ShowQuality { get => _showQuality; set => SetProperty(ref _showQuality, value); }

    /// <summary>值班员名称。</summary>
    public string OperatorName { get => _operatorName; set => SetProperty(ref _operatorName, value); }

    /// <summary>视频路径。</summary>
    public string VideoPath { get => _videoPath; set => SetProperty(ref _videoPath, value); }

    /// <summary>标称电压文本。</summary>
    public string NominalVoltageText { get => _nominalVoltageText; set => SetProperty(ref _nominalVoltageText, value); }

    /// <summary>电压注意区间百分比文本。</summary>
    public string VoltageAttentionText { get => _voltageAttentionText; set => SetProperty(ref _voltageAttentionText, value); }

    /// <summary>电压严重区间百分比文本。</summary>
    public string VoltageCriticalText { get => _voltageCriticalText; set => SetProperty(ref _voltageCriticalText, value); }

    /// <summary>频率告警下限文本。</summary>
    public string FrequencyAttentionLowText { get => _frequencyAttentionLowText; set => SetProperty(ref _frequencyAttentionLowText, value); }

    /// <summary>频率告警上限文本。</summary>
    public string FrequencyAttentionHighText { get => _frequencyAttentionHighText; set => SetProperty(ref _frequencyAttentionHighText, value); }

    /// <summary>频率严重下限文本。</summary>
    public string FrequencyCriticalLowText { get => _frequencyCriticalLowText; set => SetProperty(ref _frequencyCriticalLowText, value); }

    /// <summary>频率严重上限文本。</summary>
    public string FrequencyCriticalHighText { get => _frequencyCriticalHighText; set => SetProperty(ref _frequencyCriticalHighText, value); }

    /// <summary>电流告警值文本。</summary>
    public string CurrentAttentionText { get => _currentAttentionText; set => SetProperty(ref _currentAttentionText, value); }

    /// <summary>电流严重值文本。</summary>
    public string CurrentCriticalText { get => _currentCriticalText; set => SetProperty(ref _currentCriticalText, value); }

    /// <summary>功率告警值文本。</summary>
    public string PowerAttentionText { get => _powerAttentionText; set => SetProperty(ref _powerAttentionText, value); }

    /// <summary>功率严重值文本。</summary>
    public string PowerCriticalText { get => _powerCriticalText; set => SetProperty(ref _powerCriticalText, value); }

    /// <summary>高负荷阈值文本。</summary>
    public string PowerBandHighText { get => _powerBandHighText; set => SetProperty(ref _powerBandHighText, value); }

    /// <summary>中负荷阈值文本。</summary>
    public string PowerBandLowText { get => _powerBandLowText; set => SetProperty(ref _powerBandLowText, value); }

    /// <summary>装机容量文本。</summary>
    public string InstalledCapacityText { get => _installedCapacityText; set => SetProperty(ref _installedCapacityText, value); }

    /// <summary>校验错误信息。</summary>
    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    /// <summary>是否存在校验错误。</summary>
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

    /// <summary>浏览视频文件命令。</summary>
    public RelayCommand BrowseVideoCommand { get; }

    /// <summary>恢复默认值命令。</summary>
    public RelayCommand ResetDefaultsCommand { get; }

    /// <summary>把配置读入可编辑字段。</summary>
    public void Load(MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SamplingIntervalText = settings.SamplingIntervalMs.ToString(CultureInfo.InvariantCulture);
        HistoryCapacityText = settings.HistoryCapacity.ToString(CultureInfo.InvariantCulture);
        HistoryDisplayLimitText = settings.HistoryDisplayLimit.ToString(CultureInfo.InvariantCulture);
        AssumedPowerFactorText = settings.AssumedPowerFactor.ToString("F2", CultureInfo.InvariantCulture);
        AutoStart = settings.AutoStartAcquisition;
        AutoScroll = settings.AutoScrollHistory;
        ShowQuality = settings.ShowQualityColumn;
        OperatorName = settings.OperatorName;
        VideoPath = settings.VideoRelativePath;

        var t = settings.Thresholds;
        NominalVoltageText = t.NominalVoltageKv.ToString("F1", CultureInfo.InvariantCulture);
        VoltageAttentionText = (t.VoltageAttentionRatio * 100d).ToString("F1", CultureInfo.InvariantCulture);
        VoltageCriticalText = (t.VoltageCriticalRatio * 100d).ToString("F1", CultureInfo.InvariantCulture);
        FrequencyAttentionLowText = t.FrequencyAttentionLowHz.ToString("F2", CultureInfo.InvariantCulture);
        FrequencyAttentionHighText = t.FrequencyAttentionHighHz.ToString("F2", CultureInfo.InvariantCulture);
        FrequencyCriticalLowText = t.FrequencyCriticalLowHz.ToString("F2", CultureInfo.InvariantCulture);
        FrequencyCriticalHighText = t.FrequencyCriticalHighHz.ToString("F2", CultureInfo.InvariantCulture);
        CurrentAttentionText = t.CurrentAttentionA.ToString("F0", CultureInfo.InvariantCulture);
        CurrentCriticalText = t.CurrentCriticalA.ToString("F0", CultureInfo.InvariantCulture);
        PowerAttentionText = t.PowerAttentionMw.ToString("F0", CultureInfo.InvariantCulture);
        PowerCriticalText = t.PowerCriticalMw.ToString("F0", CultureInfo.InvariantCulture);
        PowerBandHighText = t.PowerBandHighMw.ToString("F0", CultureInfo.InvariantCulture);
        PowerBandLowText = t.PowerBandLowMw.ToString("F0", CultureInfo.InvariantCulture);
        InstalledCapacityText = t.InstalledCapacityMw.ToString("F0", CultureInfo.InvariantCulture);
        ValidationMessage = string.Empty;
    }

    /// <summary>把编辑结果转换为配置对象。</summary>
    public bool TryBuild(out MonitorSettings? settings)
    {
        settings = null;
        var errors = new List<string>();

        var sampling = ParseInt(SamplingIntervalText, "采样周期", errors);
        var capacity = ParseInt(HistoryCapacityText, "历史容量", errors);
        var displayLimit = ParseInt(HistoryDisplayLimitText, "历史显示行数", errors);
        var powerFactor = ParseDouble(AssumedPowerFactorText, "假定功率因数", errors);

        var thresholds = new PowerThresholds
        {
            NominalVoltageKv = ParseDouble(NominalVoltageText, "标称电压", errors),
            VoltageAttentionRatio = ParseDouble(VoltageAttentionText, "电压注意区间", errors) / 100d,
            VoltageCriticalRatio = ParseDouble(VoltageCriticalText, "电压严重区间", errors) / 100d,
            FrequencyAttentionLowHz = ParseDouble(FrequencyAttentionLowText, "频率告警下限", errors),
            FrequencyAttentionHighHz = ParseDouble(FrequencyAttentionHighText, "频率告警上限", errors),
            FrequencyCriticalLowHz = ParseDouble(FrequencyCriticalLowText, "频率严重下限", errors),
            FrequencyCriticalHighHz = ParseDouble(FrequencyCriticalHighText, "频率严重上限", errors),
            CurrentAttentionA = ParseDouble(CurrentAttentionText, "电流告警值", errors),
            CurrentCriticalA = ParseDouble(CurrentCriticalText, "电流严重值", errors),
            PowerAttentionMw = ParseDouble(PowerAttentionText, "功率告警值", errors),
            PowerCriticalMw = ParseDouble(PowerCriticalText, "功率严重值", errors),
            PowerBandHighMw = ParseDouble(PowerBandHighText, "高负荷阈值", errors),
            PowerBandLowMw = ParseDouble(PowerBandLowText, "中负荷阈值", errors),
            InstalledCapacityMw = ParseDouble(InstalledCapacityText, "装机容量", errors)
        };

        var candidate = new MonitorSettings
        {
            SamplingIntervalMs = sampling,
            HistoryCapacity = capacity,
            HistoryDisplayLimit = displayLimit,
            AssumedPowerFactor = powerFactor,
            AutoStartAcquisition = AutoStart,
            AutoScrollHistory = AutoScroll,
            ShowQualityColumn = ShowQuality,
            OperatorName = OperatorName?.Trim() ?? string.Empty,
            VideoRelativePath = VideoPath?.Trim() ?? string.Empty,
            Thresholds = thresholds
        };

        errors.AddRange(candidate.Validate());
        if (errors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, errors.Distinct());
            return false;
        }

        ValidationMessage = string.Empty;
        settings = candidate;
        return true;
    }

    private void BrowseVideo()
    {
        var initial = Path.GetDirectoryName(_paths.Resolve(VideoPath)) ?? _paths.BaseDirectory;
        var picked = _dialogs.PickOpenFile("选择安全教学视频", "视频文件|*.mp4;*.wmv;*.avi;*.mov|所有文件|*.*", initial);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            VideoPath = picked;
        }
    }

    private void ResetDefaults()
    {
        Load(new MonitorSettings());
        ValidationMessage = string.Empty;
    }

    private static int ParseInt(string? text, string field, ICollection<string> errors)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{field}必须是整数。");
        return 0;
    }

    private static double ParseDouble(string? text, string field, ICollection<string> errors)
    {
        if (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{field}必须是有效数字。");
        return 0d;
    }
}
