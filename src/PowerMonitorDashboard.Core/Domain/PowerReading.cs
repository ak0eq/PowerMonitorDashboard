using System.Globalization;
using PowerMonitorDashboard.Core.Calculations;

namespace PowerMonitorDashboard.Core.Domain;

/// <summary>
/// 电力实时数据点，同时是数据绑定与历史记录使用的领域模型。
/// </summary>
/// <remarks>
/// 该类型实现 <see cref="System.ComponentModel.INotifyPropertyChanged"/>：
/// 采集线程每产生一个新数据点，界面上的 KPI 卡片、趋势曲线与拓扑标注都会自动刷新，
/// 而不需要视图层手写刷新代码。历史表格中保存的是 <see cref="Clone"/> 出来的快照，
/// 因此“实时对象”被就地更新不会污染历史记录。
/// </remarks>
public sealed class PowerReading : ObservableEntity
{
    private DateTimeOffset _timestamp;
    private double _voltageKv;
    private double _currentA;
    private double _powerMw;
    private double _frequencyHz;
    private ReadingSource _source;
    private QualityFlag _quality;

    /// <summary>创建空数据点（设计期/反序列化使用）。</summary>
    public PowerReading()
    {
    }

    /// <summary>创建并初始化一个数据点。</summary>
    public PowerReading(
        DateTimeOffset timestamp,
        double voltageKv,
        double currentA,
        double powerMw,
        double frequencyHz,
        ReadingSource source = ReadingSource.Simulated,
        QualityFlag quality = QualityFlag.Good,
        Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        _timestamp = timestamp;
        _voltageKv = voltageKv;
        _currentA = currentA;
        _powerMw = powerMw;
        _frequencyHz = frequencyHz;
        _source = source;
        _quality = quality;
    }

    /// <summary>数据点唯一标识。</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>采样时间（带时区偏移）。</summary>
    public DateTimeOffset Timestamp
    {
        get => _timestamp;
        set
        {
            if (Set(ref _timestamp, value))
            {
                OnPropertiesChanged(nameof(LocalTime), nameof(DateText), nameof(TimeText), nameof(TimestampText));
            }
        }
    }

    /// <summary>线电压，单位 kV。</summary>
    public double VoltageKv
    {
        get => _voltageKv;
        set
        {
            if (Set(ref _voltageKv, value))
            {
                OnPropertiesChanged(nameof(ApparentPowerMva), nameof(PowerFactor), nameof(VoltageText));
            }
        }
    }

    /// <summary>线电流，单位 A。</summary>
    public double CurrentA
    {
        get => _currentA;
        set
        {
            if (Set(ref _currentA, value))
            {
                OnPropertiesChanged(nameof(ApparentPowerMva), nameof(PowerFactor), nameof(CurrentText));
            }
        }
    }

    /// <summary>有功功率，单位 MW。</summary>
    public double PowerMw
    {
        get => _powerMw;
        set
        {
            if (Set(ref _powerMw, value))
            {
                OnPropertiesChanged(nameof(PowerFactor), nameof(PowerText));
            }
        }
    }

    /// <summary>电网频率，单位 Hz。</summary>
    public double FrequencyHz
    {
        get => _frequencyHz;
        set
        {
            if (Set(ref _frequencyHz, value))
            {
                OnPropertyChanged(nameof(FrequencyText));
            }
        }
    }

    /// <summary>数据来源。</summary>
    public ReadingSource Source
    {
        get => _source;
        set
        {
            if (Set(ref _source, value))
            {
                OnPropertiesChanged(nameof(SourceText), nameof(IsManual));
            }
        }
    }

    /// <summary>数据质量。</summary>
    public QualityFlag Quality
    {
        get => _quality;
        set
        {
            if (Set(ref _quality, value))
            {
                OnPropertyChanged(nameof(QualityText));
            }
        }
    }

    /// <summary>本地时区时间。</summary>
    public DateTime LocalTime => Timestamp.ToLocalTime().DateTime;

    /// <summary>视在功率 S = √3·U·I，单位 MVA。</summary>
    public double ApparentPowerMva => PowerMath.ApparentPowerMva(VoltageKv, CurrentA);

    /// <summary>功率因数 cosφ = P / S（无量纲，0~1）。</summary>
    public double PowerFactor => PowerMath.PowerFactor(PowerMw, VoltageKv, CurrentA);

    /// <summary>是否为人工补录数据。</summary>
    public bool IsManual => Source == ReadingSource.Manual;

    /// <summary>日期文本，例如 “2026-10-26”。</summary>
    public string DateText => LocalTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>时间文本，例如 “19:46:57”。</summary>
    public string TimeText => LocalTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>完整时间戳文本。</summary>
    public string TimestampText => LocalTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>格式化后的电压文本。</summary>
    public string VoltageText => VoltageKv.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>格式化后的电流文本。</summary>
    public string CurrentText => CurrentA.ToString("F0", CultureInfo.InvariantCulture);

    /// <summary>格式化后的功率文本。</summary>
    public string PowerText => PowerMw.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>格式化后的频率文本。</summary>
    public string FrequencyText => FrequencyHz.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>数据来源中文名称。</summary>
    public string SourceText => Source switch
    {
        ReadingSource.Simulated => "仿真采集",
        ReadingSource.Manual => "人工补录",
        ReadingSource.Imported => "文件导入",
        ReadingSource.Replayed => "历史回放",
        _ => "未知"
    };

    /// <summary>数据质量中文名称。</summary>
    public string QualityText => Quality switch
    {
        QualityFlag.Good => "有效",
        QualityFlag.Uncertain => "可疑",
        QualityFlag.Bad => "无效",
        _ => "未知"
    };

    /// <summary>使用另一个数据点的值就地更新当前对象（保留 Id，供实时卡片绑定）。</summary>
    public void UpdateFrom(PowerReading other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // 先更新数值，再更新时间与来源，避免界面出现半更新的中间状态。
        VoltageKv = other.VoltageKv;
        CurrentA = other.CurrentA;
        PowerMw = other.PowerMw;
        FrequencyHz = other.FrequencyHz;
        Quality = other.Quality;
        Source = other.Source;
        Timestamp = other.Timestamp;
    }

    /// <summary>创建深拷贝快照。</summary>
    public PowerReading Clone() => new()
    {
        Id = Id,
        Timestamp = Timestamp,
        VoltageKv = VoltageKv,
        CurrentA = CurrentA,
        PowerMw = PowerMw,
        FrequencyHz = FrequencyHz,
        Source = Source,
        Quality = Quality
    };

    /// <summary>判断数据相对 <paramref name="now"/> 是否已过期。</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan tolerance) => now - Timestamp > tolerance;

    /// <inheritdoc />
    public override string ToString()
        => $"{TimestampText} U={VoltageText}kV I={CurrentText}A P={PowerText}MW f={FrequencyText}Hz";
}
