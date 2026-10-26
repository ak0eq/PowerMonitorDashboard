using System.IO;
using System.Text;
using PowerMonitorDashboard.Core.Abstractions;

namespace PowerMonitorDashboard.Infrastructure;

/// <summary>
/// 按天滚动的简易文件日志。大屏程序通常需要长时间连续运行，
/// 因此日志必须落盘并且不能无限增长：每天一个文件，写入后立即刷新，保证异常退出也不丢日志。
/// </summary>
public sealed class FileLogger : IAppLogger, IDisposable
{
    private readonly string _directory;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private DateTime _currentDate;
    private bool _disposed;

    /// <summary>创建日志器。</summary>
    public FileLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    /// <summary>日志目录。</summary>
    public string DirectoryPath => _directory;

    /// <summary>当前日志文件路径。</summary>
    public string CurrentFile => Path.Combine(_directory, $"app-{DateTime.Now:yyyyMMdd}.log");

    /// <summary>写入信息日志。</summary>
    public void Info(string message) => Log(Core.Abstractions.LogLevel.Info, message);

    /// <summary>写入警告日志。</summary>
    public void Warn(string message, Exception? exception = null) => Log(Core.Abstractions.LogLevel.Warning, message, exception);

    /// <summary>写入错误日志。</summary>
    public void Error(string message, Exception? exception = null) => Log(Core.Abstractions.LogLevel.Error, message, exception);

    /// <inheritdoc />
    public void Log(Core.Abstractions.LogLevel level, string message, Exception? exception = null)
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            try
            {
                EnsureWriter();
                var line = string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{LevelText(level)}] {message}");
                _writer!.WriteLine(line);
                if (exception is not null)
                {
                    _writer.WriteLine(exception);
                }

                _writer.Flush();
            }
            catch (IOException)
            {
                // 日志写入失败不能拖垮主程序，直接忽略本次写入。
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void EnsureWriter()
    {
        var today = DateTime.Today;
        if (_writer is not null && _currentDate == today)
        {
            return;
        }

        _writer?.Dispose();
        _currentDate = today;
        var path = Path.Combine(_directory, $"app-{today:yyyyMMdd}.log");
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    private static string LevelText(Core.Abstractions.LogLevel level) => level switch
    {
        Core.Abstractions.LogLevel.Debug => "DEBUG",
        Core.Abstractions.LogLevel.Info => "INFO ",
        Core.Abstractions.LogLevel.Warning => "WARN ",
        Core.Abstractions.LogLevel.Error => "ERROR",
        _ => "INFO "
    };

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
