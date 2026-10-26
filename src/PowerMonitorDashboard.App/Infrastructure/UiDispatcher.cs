using System.Windows;

namespace PowerMonitorDashboard.Infrastructure;

/// <summary>UI 线程调度抽象。采集线程通过它把结果送回界面，单元测试可替换为同步实现。</summary>
public interface IUiDispatcher
{
    /// <summary>把动作排队到 UI 线程执行。</summary>
    void Post(Action action);
}

/// <summary>基于 <see cref="Application.Current"/> 调度器的实现。</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    /// <summary>创建调度器包装。</summary>
    public WpfUiDispatcher(System.Windows.Threading.Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
        }
        else
        {
            _dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}

/// <summary>同步执行动作的调度器，供单元测试使用。</summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action) => action();
}
