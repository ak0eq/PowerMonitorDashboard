using System.Windows;
using System.Windows.Threading;
using PowerMonitorDashboard.Core.Abstractions;

namespace PowerMonitorDashboard;

/// <summary>应用程序入口。</summary>
public partial class App : Application
{
    private AppHost? _host;
    private Mutex? _singleInstance;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例运行：避免两个大屏同时写入同一份历史文件。
        _singleInstance = new Mutex(initiallyOwned: true, "PowerMonitorDashboard.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("电力监控大屏已在运行，请勿重复启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = new AppHost(this);
            _host.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"程序启动失败：{ex.Message}\n\n详细信息已写入日志目录。", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            _host?.Logger?.Error("程序启动失败。", ex);
            Shutdown(1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _host?.Logger?.Error("UI 线程未处理异常。", e.Exception);
        MessageBox.Show($"界面发生异常：{e.Exception.Message}\n\n程序将尝试继续运行，详细信息已写入日志。",
            "异常", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _host?.Logger?.Error("应用域未处理异常。", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _host?.Logger?.Error("未观察的任务异常。", e.Exception);
        e.SetObserved();
    }
}
