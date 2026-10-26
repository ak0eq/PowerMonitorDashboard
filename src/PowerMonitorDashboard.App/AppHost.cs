using System.IO;
using System.Windows;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Configuration;
using PowerMonitorDashboard.Core.Simulation;
using PowerMonitorDashboard.Infrastructure;
using PowerMonitorDashboard.Services;
using PowerMonitorDashboard.ViewModels;
using PowerMonitorDashboard.Views;

namespace PowerMonitorDashboard;

/// <summary>
/// 组合根：负责创建服务、连接数据管线、装配主视图模型与窗口。
/// 所有依赖都在这里显式构造，便于替换实现（例如接入真实 SCADA 数据源）。
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly Application _application;
    private readonly IClock _clock = SystemClock.Instance;
    private readonly AppPaths _paths;

    private FileLogger? _logger;
    private JsonSettingsStore? _settingsStore;
    private JsonHistoryStore? _historyStore;
    private SimulationTelemetrySource? _source;
    private MonitoringService? _service;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private MonitorSettings? _settings;

    /// <summary>创建组合根。</summary>
    public AppHost(Application application)
    {
        _application = application;
        _paths = new AppPaths();
    }

    /// <summary>日志器（供全局异常处理使用）。</summary>
    public IAppLogger? Logger => _logger;

    /// <summary>启动应用。</summary>
    public void Start()
    {
        _logger = new FileLogger(_paths.LogDirectory);
        _logger.Info("==================================================");
        _logger.Info("应用启动。");

        _settingsStore = new JsonSettingsStore(_paths.SettingsFile, _logger);
        _settings = _settingsStore.Load();
        _historyStore = new JsonHistoryStore(_paths.HistoryFile, _logger);

        var simulator = new TelemetrySimulator(_settings.Simulation);
        _source = new SimulationTelemetrySource(
            simulator,
            () => _settings.SamplingIntervalMs,
            _clock,
            _logger);

        _service = new MonitoringService(_settings.Thresholds, _settings.HistoryCapacity, _clock, _logger);
        _source.ReadingProduced += OnReadingProduced;

        var initialHistory = _historyStore.Load(_settings.HistoryCapacity);
        _service.SeedHistory(initialHistory);

        _viewModel = new MainViewModel(
            _service,
            _source,
            _clock,
            _logger,
            new WpfUiDispatcher(_application.Dispatcher),
            new DispatcherScheduler(_application.Dispatcher),
            new FileDialogService(),
            new UserDialogService(),
            _paths,
            _settings,
            _settingsStore,
            _historyStore,
            initialHistory);

        _window = new MainWindow
        {
            DataContext = _viewModel
        };

        _application.MainWindow = _window;
        _window.Show();
        _viewModel.Start();

        _logger.Info($"主窗口已显示，分辨率 {SystemParameters.PrimaryScreenWidth:F0}×{SystemParameters.PrimaryScreenHeight:F0}，窗口 {_window.ActualWidth:F0}×{_window.ActualHeight:F0}。");
        _logger.Info($"视频路径：{_paths.Resolve(_settings.VideoRelativePath)}");
    }

    private void OnReadingProduced(object? sender, Core.Domain.PowerReading reading)
    {
        try
        {
            _service?.Accept(reading);
        }
        catch (Exception ex)
        {
            _logger?.Error("处理实时数据时发生异常。", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (_source is not null)
            {
                _source.ReadingProduced -= OnReadingProduced;
            }

            _viewModel?.Dispose();
            _source?.Dispose();
            _logger?.Info("应用退出。");
        }
        finally
        {
            _logger?.Dispose();
        }
    }
}
