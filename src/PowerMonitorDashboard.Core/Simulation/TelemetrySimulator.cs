using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Abstractions;

namespace PowerMonitorDashboard.Core.Simulation;

/// <summary>仿真数据源的参数。</summary>
public sealed record SimulationOptions
{
    /// <summary>日最大负荷，单位 MW。</summary>
    public double PeakLoadMw { get; init; } = 480d;

    /// <summary>标称电压，单位 kV。</summary>
    public double NominalVoltageKv { get; init; } = 220d;

    /// <summary>标称频率，单位 Hz。</summary>
    public double NominalFrequencyHz { get; init; } = 50d;

    /// <summary>基准功率因数。</summary>
    public double BasePowerFactor { get; init; } = 0.95d;

    /// <summary>负荷随机噪声（相对峰值）。</summary>
    public double LoadNoiseRatio { get; init; } = 0.0035d;

    /// <summary>频率随机噪声，单位 Hz。</summary>
    public double FrequencyNoiseHz { get; init; } = 0.008d;

    /// <summary>电压随机噪声，单位 kV。</summary>
    public double VoltageNoiseKv { get; init; } = 0.22d;

    /// <summary>启动时的初始负荷标幺值。</summary>
    public double InitialLoadFactor { get; init; } = 0.72d;

    /// <summary>默认参数。</summary>
    public static SimulationOptions Default { get; } = new();

    /// <summary>校验参数。</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PeakLoadMw <= 0)
        {
            errors.Add("峰值负荷必须大于 0。");
        }

        if (NominalVoltageKv <= 0)
        {
            errors.Add("标称电压必须大于 0。");
        }

        if (BasePowerFactor is <= 0 or > 1)
        {
            errors.Add("功率因数必须在 0～1 之间。");
        }

        return errors;
    }
}

/// <summary>扰动类型，用于“故障演练”与告警演示。</summary>
public enum DisturbanceKind
{
    None = 0,
    FrequencySurge = 1,
    FrequencyDip = 2,
    VoltageSag = 3,
    LoadSpike = 4
}

/// <summary>
/// 电网运行仿真器：在没有真实 SCADA 数据接入的情况下，产生物理上自洽的实时数据。
/// </summary>
/// <remarks>
/// 数据生成遵循三条约束：
/// <list type="number">
/// <item>负荷跟随典型日曲线平滑变化，并叠加一阶惯性滤波与高斯噪声；</item>
/// <item>频率、电压都存在向额定值回归的“一次调频/调压”特性，而不是纯随机数；</item>
/// <item>电流由功率反算，功率再由 U、I、cosφ 计算，因此三个字段天然满足功率方程，便于演示跨字段校验。</item>
/// </list>
/// </remarks>
public sealed class TelemetrySimulator
{
    private readonly SimulationOptions _options;
    private readonly Random _random;
    private readonly double _phase;

    private double _loadFactor;
    private double _frequencyOffset;
    private double _voltageOffset;
    private DisturbanceKind _disturbance = DisturbanceKind.None;
    private double _disturbanceMagnitude;
    private DateTimeOffset? _lastTimestamp;

    /// <summary>创建仿真器。</summary>
    /// <param name="options">参数。</param>
    /// <param name="seed">随机种子；固定种子可让测试完全可重复。</param>
    public TelemetrySimulator(SimulationOptions? options = null, int? seed = null)
    {
        _options = options ?? SimulationOptions.Default;
        var errors = _options.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join("；", errors), nameof(options));
        }

        _random = seed is null ? new Random() : new Random(seed.Value);
        _loadFactor = Math.Clamp(_options.InitialLoadFactor, 0.3d, 1.05d);
        _phase = _random.NextDouble() * Math.PI * 2d;
    }

    /// <summary>当前参数。</summary>
    public SimulationOptions Options => _options;

    /// <summary>当前是否处于扰动状态。</summary>
    public bool IsDisturbed => _disturbance != DisturbanceKind.None;

    /// <summary>注入一次扰动，用于告警与动画演示。</summary>
    public void InjectDisturbance(DisturbanceKind kind, double magnitude = 1d)
    {
        _disturbance = kind;
        _disturbanceMagnitude = Math.Clamp(magnitude, 0.2d, 2d);
    }

    /// <summary>生成下一条实时数据。</summary>
    /// <param name="timestamp">采样时刻。</param>
    public PowerReading Next(DateTimeOffset timestamp)
    {
        var seconds = _lastTimestamp is null
            ? 1d
            : Math.Clamp((timestamp - _lastTimestamp.Value).TotalSeconds, 0.2d, 5d);
        _lastTimestamp = timestamp;

        var profile = LoadProfile.GetFactor(timestamp);
        var target = Math.Clamp(profile, 0.3d, 1.05d);

        // 一阶惯性：负荷向曲线目标平滑靠拢，时间常数 30 s。
        var alpha = 1d - Math.Exp(-seconds / 30d);
        _loadFactor += (target - _loadFactor) * alpha;
        _loadFactor += NextGaussian() * _options.LoadNoiseRatio * Math.Sqrt(seconds);

        // 频率的一次调频：负荷越高，稳态频率略低；扰动叠加后按时间衰减。
        var loadRatio = _loadFactor / Math.Max(_options.InitialLoadFactor, 0.5d);
        var droop = (0.80d - loadRatio) * 0.06d;
        _frequencyOffset += -_frequencyOffset * (1d - Math.Exp(-seconds / 8d))
                            + NextGaussian() * _options.FrequencyNoiseHz * Math.Sqrt(seconds);

        // 电压随负荷轻微下降（线路压降），并叠加慢漂移。
        _voltageOffset += -_voltageOffset * (1d - Math.Exp(-seconds / 12d))
                          + NextGaussian() * _options.VoltageNoiseKv * Math.Sqrt(seconds);

        var pf = Math.Clamp(_options.BasePowerFactor + 0.006d * Math.Sin(_phase + timestamp.Second * 0.12d), 0.90d, 0.99d);

        var disturbanceFactor = Decay(seconds, 6d);
        var frequency = _options.NominalFrequencyHz + droop + _frequencyOffset;
        var voltage = _options.NominalVoltageKv * (1d + 0.012d * (0.85d - loadRatio)) + _voltageOffset;
        var loadMw = _options.PeakLoadMw * _loadFactor;

        switch (_disturbance)
        {
            case DisturbanceKind.FrequencySurge:
                frequency += 0.62d * _disturbanceMagnitude * disturbanceFactor;
                break;
            case DisturbanceKind.FrequencyDip:
                frequency -= 0.62d * _disturbanceMagnitude * disturbanceFactor;
                break;
            case DisturbanceKind.VoltageSag:
                voltage -= 11d * _disturbanceMagnitude * disturbanceFactor;
                break;
            case DisturbanceKind.LoadSpike:
                loadMw += _options.PeakLoadMw * 0.09d * _disturbanceMagnitude * disturbanceFactor;
                break;
        }

        if (disturbanceFactor < 0.02d)
        {
            _disturbance = DisturbanceKind.None;
        }

        frequency = Math.Clamp(frequency, 48.5d, 51.5d);
        voltage = Math.Clamp(voltage, 150d, 260d);
        loadMw = Math.Clamp(loadMw, 20d, _options.PeakLoadMw * 1.2d);

        var voltageRounded = Math.Round(voltage, 1, MidpointRounding.AwayFromZero);
        var current = Calculations.PowerMath.CurrentFromPower(loadMw, voltageRounded, pf);
        var currentRounded = Math.Round(current, 0, MidpointRounding.AwayFromZero);
        var power = Calculations.PowerMath.PowerFromCurrent(voltageRounded, currentRounded, pf);

        return new PowerReading(
            timestamp,
            voltageRounded,
            currentRounded,
            Math.Round(power, 2, MidpointRounding.AwayFromZero),
            Math.Round(frequency, 2, MidpointRounding.AwayFromZero),
            ReadingSource.Simulated,
            QualityFlag.Good);
    }

    private static double Decay(double seconds, double tau) => Math.Exp(-seconds / tau);

    private double NextGaussian()
    {
        // Box-Muller 变换，避免依赖 Random.NextDouble 的分布假设。
        var u1 = 1d - _random.NextDouble();
        var u2 = _random.NextDouble();
        return Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
    }
}
