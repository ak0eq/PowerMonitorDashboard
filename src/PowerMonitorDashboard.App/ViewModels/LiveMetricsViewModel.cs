using System.Globalization;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Monitoring;
using PowerMonitorDashboard.Mvvm;
using PowerMonitorDashboard.Services;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>
/// 实时指标视图模型：驱动顶部四张 KPI 卡片与三块趋势图。
/// 数据对象 <see cref="Reading"/> 始终是同一个实例，采集线程的新值通过
/// <see cref="PowerReading.UpdateFrom"/> 就地写入，因此界面绑定无需重建。
/// </summary>
public sealed class LiveMetricsViewModel : ObservableObject
{
    /// <summary>趋势缓冲区容量（约 2.5 分钟）。</summary>
    public const int SeriesCapacity = 150;

    private readonly Queue<double> _voltage = new(SeriesCapacity);
    private readonly Queue<double> _current = new(SeriesCapacity);
    private readonly Queue<double> _power = new(SeriesCapacity);
    private readonly Queue<double> _frequency = new(SeriesCapacity);
    private readonly Func<PowerThresholds> _thresholds;
    private readonly IClock _clock;

    private double[] _voltageSeries = [];
    private double[] _currentSeries = [];
    private double[] _powerSeries = [];
    private double[] _frequencySeries = [];
    private OperatingStatus _status = OperatingStatus.Normal;
    private string _statusSummary = "等待采集数据";
    private bool _hasData;
    private double _dailyEnergyMwh;
    private double _loadRate;
    private int _sampleCount;
    private string _lastUpdateText = "—";
    private bool _isAcquiring;
    private bool _isFrequencyOutOfBand;
    private bool _isVoltageOutOfBand;
    private double _powerFactor = 1d;

    /// <summary>创建实时指标视图模型。</summary>
    public LiveMetricsViewModel(Func<PowerThresholds> thresholds, IClock clock)
    {
        _thresholds = thresholds;
        _clock = clock;
        RefreshThresholdTexts();
    }

    /// <summary>实时数据对象（绑定源）。</summary>
    public PowerReading Reading { get; } = new();

    /// <summary>综合运行状态。</summary>
    public OperatingStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>状态中文名称。</summary>
    public string StatusText => Status switch
    {
        OperatingStatus.Critical => "严重",
        OperatingStatus.Attention => "告警",
        _ => "正常"
    };

    /// <summary>状态说明（首条异常原因或“运行正常”）。</summary>
    public string StatusSummary
    {
        get => _statusSummary;
        private set => SetProperty(ref _statusSummary, value);
    }

    /// <summary>是否已收到数据。</summary>
    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    /// <summary>是否正在采集。</summary>
    public bool IsAcquiring
    {
        get => _isAcquiring;
        private set
        {
            if (SetProperty(ref _isAcquiring, value))
            {
                OnPropertyChanged(nameof(AcquisitionText));
            }
        }
    }

    /// <summary>采集状态文本。</summary>
    public string AcquisitionText => IsAcquiring ? "实时采集中" : "采集已暂停";

    /// <summary>当日累计电量，单位 MWh。</summary>
    public double DailyEnergyMwh
    {
        get => _dailyEnergyMwh;
        private set
        {
            if (SetProperty(ref _dailyEnergyMwh, value))
            {
                OnPropertyChanged(nameof(DailyEnergyText));
            }
        }
    }

    /// <summary>当日累计电量文本。</summary>
    public string DailyEnergyText => DailyEnergyMwh.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>负荷率（0～1）。</summary>
    public double LoadRate
    {
        get => _loadRate;
        private set
        {
            if (SetProperty(ref _loadRate, value))
            {
                OnPropertyChanged(nameof(LoadRateText));
            }
        }
    }

    /// <summary>负荷率文本。</summary>
    public string LoadRateText => (LoadRate * 100d).ToString("F1", CultureInfo.InvariantCulture) + "%";

    /// <summary>功率因数。</summary>
    public double PowerFactor
    {
        get => _powerFactor;
        private set
        {
            if (SetProperty(ref _powerFactor, value))
            {
                OnPropertyChanged(nameof(PowerFactorText));
            }
        }
    }

    /// <summary>功率因数文本。</summary>
    public string PowerFactorText => PowerFactor.ToString("F3", CultureInfo.InvariantCulture);

    /// <summary>样本数量。</summary>
    public int SampleCount
    {
        get => _sampleCount;
        private set
        {
            if (SetProperty(ref _sampleCount, value))
            {
                OnPropertyChanged(nameof(SampleCountText));
            }
        }
    }

    /// <summary>样本数量文本。</summary>
    public string SampleCountText => SampleCount.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>最近刷新时间文本。</summary>
    public string LastUpdateText
    {
        get => _lastUpdateText;
        private set => SetProperty(ref _lastUpdateText, value);
    }

    /// <summary>频率是否越限。</summary>
    public bool IsFrequencyOutOfBand
    {
        get => _isFrequencyOutOfBand;
        private set => SetProperty(ref _isFrequencyOutOfBand, value);
    }

    /// <summary>电压是否越限。</summary>
    public bool IsVoltageOutOfBand
    {
        get => _isVoltageOutOfBand;
        private set => SetProperty(ref _isVoltageOutOfBand, value);
    }

    /// <summary>电压趋势数据。</summary>
    public IReadOnlyList<double> VoltageSeries => _voltageSeries;

    /// <summary>电流趋势数据。</summary>
    public IReadOnlyList<double> CurrentSeries => _currentSeries;

    /// <summary>功率趋势数据。</summary>
    public IReadOnlyList<double> PowerSeries => _powerSeries;

    /// <summary>频率趋势数据。</summary>
    public IReadOnlyList<double> FrequencySeries => _frequencySeries;

    /// <summary>电压轴上限。</summary>
    public double VoltageAxisMax { get; private set; } = 250d;

    /// <summary>电压轴下限。</summary>
    public double VoltageAxisMin { get; private set; } = 190d;

    /// <summary>频率轴上限。</summary>
    public double FrequencyAxisMax { get; private set; } = 51.6d;

    /// <summary>频率轴下限。</summary>
    public double FrequencyAxisMin { get; private set; } = 48.4d;

    /// <summary>功率轴上限。</summary>
    public double PowerAxisMax { get; private set; } = 600d;

    /// <summary>功率轴下限。</summary>
    public double PowerAxisMin { get; private set; }

    /// <summary>电压正常区间下限。</summary>
    public double VoltageAttentionLow { get; private set; }

    /// <summary>电压正常区间上限。</summary>
    public double VoltageAttentionHigh { get; private set; }

    /// <summary>频率正常区间下限。</summary>
    public double FrequencyAttentionLow { get; private set; }

    /// <summary>频率正常区间上限。</summary>
    public double FrequencyAttentionHigh { get; private set; }

    /// <summary>功率告警值。</summary>
    public double PowerAttention { get; private set; }

    /// <summary>电压区间说明。</summary>
    public string VoltageHint { get; private set; } = string.Empty;

    /// <summary>频率区间说明。</summary>
    public string FrequencyHint { get; private set; } = string.Empty;

    /// <summary>功率区间说明。</summary>
    public string PowerHint { get; private set; } = string.Empty;

    /// <summary>用历史数据初始化趋势与当前值。</summary>
    public void Initialize(IReadOnlyList<PowerReading> history, StatusEvaluation? evaluation)
    {
        foreach (var item in history.TakeLast(SeriesCapacity))
        {
            Push(item);
        }

        UpdateSeriesSnapshots();

        if (history.Count > 0)
        {
            Reading.UpdateFrom(history[^1]);
            HasData = true;
            LastUpdateText = Reading.LocalTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            SampleCount = history.Count;
            PowerFactor = Reading.PowerFactor;
            LoadRate = _thresholds().LoadRate(Reading.PowerMw);
            if (evaluation is not null)
            {
                Status = evaluation.Status;
                StatusSummary = evaluation.Summary;
                IsFrequencyOutOfBand = evaluation.IsFrequencyOutOfBand;
                IsVoltageOutOfBand = evaluation.IsVoltageOutOfBand;
            }
        }
    }

    /// <summary>应用一条新的处理结果。</summary>
    public void Update(ProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Reading.UpdateFrom(result.Reading);
        Status = result.Status.Status;
        StatusSummary = result.Status.Summary;
        IsFrequencyOutOfBand = result.Status.IsFrequencyOutOfBand;
        IsVoltageOutOfBand = result.Status.IsVoltageOutOfBand;
        DailyEnergyMwh = result.DailyEnergyMwh;
        LoadRate = _thresholds().LoadRate(result.Reading.PowerMw);
        PowerFactor = result.Reading.PowerFactor;
        SampleCount = result.SampleCount;
        LastUpdateText = result.Reading.LocalTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        HasData = true;

        Push(result.Reading);
        UpdateSeriesSnapshots();

        OnPropertiesChanged(
            nameof(VoltageSeries), nameof(CurrentSeries), nameof(PowerSeries), nameof(FrequencySeries));
    }

    /// <summary>设置采集开关状态。</summary>
    public void SetAcquiring(bool isAcquiring) => IsAcquiring = isAcquiring;

    /// <summary>阈值变更后刷新图表参考线与提示文本。</summary>
    public void RefreshThresholds()
    {
        RefreshThresholdTexts();
        OnPropertiesChanged(
            nameof(VoltageAttentionLow), nameof(VoltageAttentionHigh),
            nameof(FrequencyAttentionLow), nameof(FrequencyAttentionHigh),
            nameof(PowerAttention),
            nameof(VoltageHint), nameof(FrequencyHint), nameof(PowerHint));
    }

    private void RefreshThresholdTexts()
    {
        var thresholds = _thresholds();
        VoltageAttentionLow = thresholds.VoltageAttentionLowKv;
        VoltageAttentionHigh = thresholds.VoltageAttentionHighKv;
        FrequencyAttentionLow = thresholds.FrequencyAttentionLowHz;
        FrequencyAttentionHigh = thresholds.FrequencyAttentionHighHz;
        PowerAttention = thresholds.PowerAttentionMw;
        VoltageAxisMin = Math.Min(190d, thresholds.VoltageCriticalLowKv - 5d);
        VoltageAxisMax = Math.Max(250d, thresholds.VoltageCriticalHighKv + 5d);
        FrequencyAxisMin = Math.Min(48.4d, thresholds.FrequencyCriticalLowHz - 0.3d);
        FrequencyAxisMax = Math.Max(51.6d, thresholds.FrequencyCriticalHighHz + 0.3d);
        PowerAxisMax = Math.Max(600d, thresholds.PowerCriticalMw * 1.2d);

        VoltageHint = string.Create(CultureInfo.InvariantCulture, $"正常 {thresholds.VoltageAttentionLowKv:F1}～{thresholds.VoltageAttentionHighKv:F1} kV");
        FrequencyHint = string.Create(CultureInfo.InvariantCulture, $"正常 {thresholds.FrequencyAttentionLowHz:F2}～{thresholds.FrequencyAttentionHighHz:F2} Hz");
        PowerHint = string.Create(CultureInfo.InvariantCulture, $"告警 {thresholds.PowerAttentionMw:F0} MW");
    }

    private void Push(PowerReading reading)
    {
        Enqueue(_voltage, reading.VoltageKv);
        Enqueue(_current, reading.CurrentA);
        Enqueue(_power, reading.PowerMw);
        Enqueue(_frequency, reading.FrequencyHz);
    }

    private static void Enqueue(Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > SeriesCapacity)
        {
            queue.Dequeue();
        }
    }

    private void UpdateSeriesSnapshots()
    {
        _voltageSeries = _voltage.ToArray();
        _currentSeries = _current.ToArray();
        _powerSeries = _power.ToArray();
        _frequencySeries = _frequency.ToArray();
    }
}
