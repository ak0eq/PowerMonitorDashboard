using System.Globalization;
using System.IO;
using PowerMonitorDashboard.Core.Abstractions;

namespace PowerMonitorDashboard.Infrastructure;

/// <summary>
/// 应用运行时路径。可写数据统一放在 <c>%LOCALAPPDATA%\PowerMonitorDashboard</c>，
/// 程序目录保持只读，符合 Windows 桌面应用的数据隔离规范。
/// </summary>
public sealed class AppPaths
{
    /// <summary>创建路径服务。</summary>
    /// <param name="baseDirectory">程序目录，默认为 <see cref="AppContext.BaseDirectory"/>。</param>
    /// <param name="dataDirectory">数据目录，默认为本地应用数据目录。</param>
    public AppPaths(string? baseDirectory = null, string? dataDirectory = null)
    {
        BaseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerMonitorDashboard");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(ExportDirectory);
    }

    /// <summary>程序目录。</summary>
    public string BaseDirectory { get; }

    /// <summary>数据目录。</summary>
    public string DataDirectory { get; }

    /// <summary>设置文件路径。</summary>
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>历史数据文件路径。</summary>
    public string HistoryFile => Path.Combine(DataDirectory, "history.json");

    /// <summary>日志目录。</summary>
    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>导出目录。</summary>
    public string ExportDirectory => Path.Combine(DataDirectory, "exports");

    /// <summary>把相对路径解析为程序目录下的绝对路径。</summary>
    public string Resolve(string relativePath)
        => Path.IsPathRooted(relativePath) ? relativePath : Path.GetFullPath(Path.Combine(BaseDirectory, relativePath));

    /// <summary>生成带时间戳的默认导出文件名。</summary>
    public string CreateExportFileName(string prefix, string extension)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
}
