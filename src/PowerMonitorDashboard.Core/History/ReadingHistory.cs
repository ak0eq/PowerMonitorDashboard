using PowerMonitorDashboard.Core.Domain;

namespace PowerMonitorDashboard.Core.History;

/// <summary>历史数据的统计摘要。</summary>
public sealed record HistoryStatistics(
    int Count,
    double MinVoltageKv,
    double MaxVoltageKv,
    double AverageVoltageKv,
    double MinFrequencyHz,
    double MaxFrequencyHz,
    double AverageFrequencyHz,
    double MinCurrentA,
    double MaxCurrentA,
    double MinPowerMw,
    double MaxPowerMw,
    double AveragePowerMw)
{
    /// <summary>空集合的统计结果。</summary>
    public static HistoryStatistics Empty { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// 定长历史数据缓冲区。只保留最近 <see cref="Capacity"/> 条记录，
/// 超出容量后自动淘汰最旧的数据，避免长时间运行时内存持续增长。
/// </summary>
public sealed class ReadingHistory
{
    private readonly Queue<PowerReading> _items;
    private readonly object _gate = new();
    private readonly int _capacity;

    /// <summary>创建历史缓冲区。</summary>
    /// <param name="capacity">容量，必须大于 0。</param>
    public ReadingHistory(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "历史缓冲区容量必须大于 0。");
        }

        _capacity = capacity;
        _items = new Queue<PowerReading>(Math.Min(capacity, 1024));
    }

    /// <summary>容量。</summary>
    public int Capacity => _capacity;

    /// <summary>当前记录数。</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>追加一条记录（内部保存快照，避免实时对象被修改后污染历史）。</summary>
    public void Add(PowerReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        lock (_gate)
        {
            _items.Enqueue(reading.Clone());
            while (_items.Count > _capacity)
            {
                _items.Dequeue();
            }
        }
    }

    /// <summary>批量追加。</summary>
    public void AddRange(IEnumerable<PowerReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        foreach (var reading in readings)
        {
            Add(reading);
        }
    }

    /// <summary>获取按时间正序排列的快照。</summary>
    public IReadOnlyList<PowerReading> Snapshot()
    {
        lock (_gate)
        {
            return _items.ToArray();
        }
    }

    /// <summary>获取最近 <paramref name="count"/> 条记录（时间正序）。</summary>
    public IReadOnlyList<PowerReading> Recent(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            if (_items.Count <= count)
            {
                return _items.ToArray();
            }

            return _items.Skip(_items.Count - count).ToArray();
        }
    }

    /// <summary>清空历史。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
        }
    }

    /// <summary>统计最近一段时间的数据。</summary>
    public HistoryStatistics Statistics(TimeSpan window, DateTimeOffset now)
    {
        IReadOnlyList<PowerReading> snapshot;
        lock (_gate)
        {
            snapshot = _items.ToArray();
        }

        var threshold = now - window;
        var rows = snapshot.Where(r => r.Timestamp >= threshold).ToArray();
        if (rows.Length == 0)
        {
            return HistoryStatistics.Empty;
        }

        return new HistoryStatistics(
            rows.Length,
            rows.Min(static r => r.VoltageKv),
            rows.Max(static r => r.VoltageKv),
            rows.Average(static r => r.VoltageKv),
            rows.Min(static r => r.FrequencyHz),
            rows.Max(static r => r.FrequencyHz),
            rows.Average(static r => r.FrequencyHz),
            rows.Min(static r => r.CurrentA),
            rows.Max(static r => r.CurrentA),
            rows.Min(static r => r.PowerMw),
            rows.Max(static r => r.PowerMw),
            rows.Average(static r => r.PowerMw));
    }
}
