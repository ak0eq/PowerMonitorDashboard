namespace PowerMonitorDashboard.Core.Abstractions;

/// <summary>时钟抽象，便于对“时间戳不能晚于当前时间”等规则进行确定性测试。</summary>
public interface IClock
{
    /// <summary>当前时间（带本地时区偏移）。</summary>
    DateTimeOffset Now { get; }
}

/// <summary>使用系统时间的默认实现。</summary>
public sealed class SystemClock : IClock
{
    /// <summary>共享实例。</summary>
    public static SystemClock Instance { get; } = new();

    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;
}

/// <summary>日志级别。</summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>应用日志抽象，Core 层只依赖该接口，具体落盘由宿主实现。</summary>
public interface IAppLogger
{
    /// <summary>写入一条日志。</summary>
    void Log(LogLevel level, string message, Exception? exception = null);

    /// <summary>信息日志。</summary>
    void Info(string message) => Log(LogLevel.Info, message);

    /// <summary>警告日志。</summary>
    void Warn(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);

    /// <summary>错误日志。</summary>
    void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
}

/// <summary>空日志实现，供单元测试使用。</summary>
public sealed class NullLogger : IAppLogger
{
    /// <summary>共享实例。</summary>
    public static NullLogger Instance { get; } = new();

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        // 单元测试中不输出任何内容。
    }
}
