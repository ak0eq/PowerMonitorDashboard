using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Simulation;

namespace PowerMonitorDashboard.Services;

/// <summary>
/// 实时数据源抽象。当前实现为内置仿真源；接入真实 SCADA 时，
/// 只需实现 Modbus/OPC UA 版本并替换组合根中的注册，界面与监控管线无需改动。
/// </summary>
public interface ITelemetrySource : IDisposable
{
    /// <summary>是否正在采集。</summary>
    bool IsRunning { get; }

    /// <summary>产生一条新数据。</summary>
    event EventHandler<PowerReading>? ReadingProduced;

    /// <summary>运行状态变化。</summary>
    event EventHandler<bool>? RunningChanged;

    /// <summary>开始采集。</summary>
    void Start();

    /// <summary>暂停采集。</summary>
    void Stop();

    /// <summary>立即产生一条数据（不受暂停状态影响）。</summary>
    void ProduceNow();

    /// <summary>注入一次扰动，用于告警演练。</summary>
    /// <param name="kind">扰动类型。</param>
    /// <param name="magnitude">扰动幅度倍数。</param>
    void InjectDisturbance(DisturbanceKind kind, double magnitude = 1d);
}

/// <summary>
/// 仿真数据源：后台按设定周期生成数据，并保证任何时刻只有一个采集循环。
/// </summary>
public sealed class SimulationTelemetrySource : ITelemetrySource
{
    private readonly TelemetrySimulator _simulator;
    private readonly Func<int> _intervalMilliseconds;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly object _simulatorGate = new();

    private CancellationTokenSource? _cancellation;
    private Task? _loop;

    /// <summary>创建仿真数据源。</summary>
    /// <param name="simulator">仿真器。</param>
    /// <param name="intervalMilliseconds">采样周期提供器（每次循环重新读取，支持在线修改）。</param>
    /// <param name="clock">时钟。</param>
    /// <param name="logger">日志。</param>
    public SimulationTelemetrySource(
        TelemetrySimulator simulator,
        Func<int> intervalMilliseconds,
        IClock clock,
        IAppLogger logger)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _intervalMilliseconds = intervalMilliseconds ?? throw new ArgumentNullException(nameof(intervalMilliseconds));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsRunning { get; private set; }

    /// <inheritdoc />
    public event EventHandler<PowerReading>? ReadingProduced;

    /// <inheritdoc />
    public event EventHandler<bool>? RunningChanged;

    /// <inheritdoc />
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        IsRunning = true;
        _loop = Task.Run(() => RunAsync(_cancellation.Token));
        RunningChanged?.Invoke(this, true);
        _logger.Info("采集已启动。");
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        _cancellation?.Cancel();
        RunningChanged?.Invoke(this, false);
        _logger.Info("采集已暂停。");
    }

    /// <inheritdoc />
    public void ProduceNow()
    {
        ProduceOne();
    }

    /// <inheritdoc />
    public void InjectDisturbance(DisturbanceKind kind, double magnitude = 1d)
    {
        lock (_simulatorGate)
        {
            _simulator.InjectDisturbance(kind, magnitude);
        }

        _logger.Info($"已注入扰动：{kind}（幅度 {magnitude:F1}）。");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var interval = Math.Clamp(_intervalMilliseconds(), 200, 60000);
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                ProduceOne();
            }
            catch (Exception ex)
            {
                _logger.Error("采集循环发生异常。", ex);
                await Task.Delay(2000, token).ConfigureAwait(false);
            }
        }
    }

    private void ProduceOne()
    {
        PowerReading reading;
        lock (_simulatorGate)
        {
            reading = _simulator.Next(_clock.Now);
        }

        ReadingProduced?.Invoke(this, reading);
    }
}
