using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Configuration;

namespace PowerMonitorDashboard.Infrastructure;

/// <summary>
/// 基于 JSON 的设置存储。写入采用“临时文件 + 原子替换”，
/// 即使写入过程中断电也不会留下半截损坏的配置文件。
/// </summary>
public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly IAppLogger _logger;

    /// <summary>创建设置存储。</summary>
    public JsonSettingsStore(string path, IAppLogger logger)
    {
        _path = path;
        _logger = logger;
    }

    /// <summary>设置文件路径。</summary>
    public string Path => _path;

    /// <summary>读取设置；文件缺失或损坏时返回默认值。</summary>
    public MonitorSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                _logger.Info("未找到配置文件，使用默认设置。");
                return new MonitorSettings();
            }

            var json = File.ReadAllText(_path, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<MonitorSettings>(json, SerializerOptions);
            if (settings is null)
            {
                _logger.Warn("配置文件内容为空，使用默认设置。");
                return new MonitorSettings();
            }

            settings.Normalize();
            var errors = settings.Validate();
            if (errors.Count > 0)
            {
                _logger.Warn("配置文件存在非法项：" + string.Join("；", errors));
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Error("读取配置文件失败，回退到默认设置。", ex);
            return new MonitorSettings();
        }
    }

    /// <summary>保存设置。</summary>
    public void Save(MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, _path, overwrite: true);
            _logger.Info("配置已保存。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("保存配置失败。", ex);
            throw;
        }
    }
}
