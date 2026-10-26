using System.Windows;
using System.Windows.Threading;

namespace PowerMonitorDashboard.Infrastructure;

/// <summary>UI 定时器抽象，便于在无界面的单元测试中禁用时钟。</summary>
public interface IUiScheduler
{
    /// <summary>按固定间隔重复执行。</summary>
    IDisposable Schedule(TimeSpan interval, Action action);

    /// <summary>延迟执行一次。</summary>
    IDisposable ScheduleOnce(TimeSpan delay, Action action);
}

/// <summary>基于 <see cref="DispatcherTimer"/> 的实现。</summary>
public sealed class DispatcherScheduler : IUiScheduler
{
    private readonly Dispatcher _dispatcher;

    /// <summary>创建调度器。</summary>
    public DispatcherScheduler(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public IDisposable Schedule(TimeSpan interval, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = interval
        };
        timer.Tick += (_, _) => action();
        timer.Start();
        return new TimerHandle(timer);
    }

    /// <inheritdoc />
    public IDisposable ScheduleOnce(TimeSpan delay, Action action)
    {
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = delay
        };
        timer.Tick += (_, _) =>
        {
            timer?.Stop();
            action();
        };
        timer.Start();
        return new TimerHandle(timer);
    }

    private sealed class TimerHandle : IDisposable
    {
        private DispatcherTimer? _timer;

        public TimerHandle(DispatcherTimer timer) => _timer = timer;

        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}

/// <summary>不执行任何动作的调度器，供单元测试与设计期使用。</summary>
public sealed class NullUiScheduler : IUiScheduler
{
    /// <summary>共享实例。</summary>
    public static NullUiScheduler Instance { get; } = new();

    /// <inheritdoc />
    public IDisposable Schedule(TimeSpan interval, Action action) => EmptyHandle.Instance;

    /// <inheritdoc />
    public IDisposable ScheduleOnce(TimeSpan delay, Action action) => EmptyHandle.Instance;

    private sealed class EmptyHandle : IDisposable
    {
        public static EmptyHandle Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>消息框服务抽象。</summary>
public interface IUserDialogService
{
    /// <summary>确认对话框。</summary>
    bool Confirm(string title, string message);

    /// <summary>错误提示。</summary>
    void ShowError(string title, string message);

    /// <summary>信息提示。</summary>
    void ShowInfo(string title, string message);
}

/// <summary>基于 WPF MessageBox 的实现。</summary>
public sealed class UserDialogService : IUserDialogService
{
    /// <inheritdoc />
    public bool Confirm(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    /// <inheritdoc />
    public void ShowError(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <inheritdoc />
    public void ShowInfo(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
