using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Domain;

namespace PowerMonitorDashboard.Infrastructure;

/// <summary>
/// 历史数据快照文件。程序退出时保存最近 <c>capacity</c> 条记录，
/// 下次启动自动恢复，保证大屏重启后仍有趋势上下文。
/// </summary>
public sealed class JsonHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly IAppLogger _logger;

    /// <summary>创建历史存储。</summary>
    public JsonHistoryStore(string path, IAppLogger logger)
    {
        _path = path;
        _logger = logger;
    }

    /// <summary>读取历史数据。</summary>
    public IReadOnlyList<PowerReading> Load(int maxCount)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var json = File.ReadAllText(_path, Encoding.UTF8);
            var snapshot = JsonSerializer.Deserialize<HistorySnapshot>(json, SerializerOptions);
            if (snapshot?.Readings is null || snapshot.Readings.Count == 0)
            {
                return [];
            }

            var readings = snapshot.Readings
                .Where(static r => r is not null)
                .OrderBy(static r => r.Timestamp)
                .TakeLast(Math.Max(1, maxCount))
                .ToArray();

            _logger.Info($"已从磁盘恢复 {readings.Length} 条历史数据。");
            return readings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.Warn("读取历史数据失败，忽略本地快照。", ex);
            return [];
        }
    }

    /// <summary>保存历史数据。</summary>
    public void Save(IReadOnlyList<PowerReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        try
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var snapshot = new HistorySnapshot
            {
                SavedAt = DateTimeOffset.Now,
                Readings = readings.ToList()
            };

            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, _path, overwrite: true);
            _logger.Info($"已保存 {readings.Count} 条历史数据。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("保存历史数据失败。", ex);
        }
    }

    private sealed class HistorySnapshot
    {
        public DateTimeOffset SavedAt { get; set; }

        public List<PowerReading> Readings { get; set; } = [];
    }
}
