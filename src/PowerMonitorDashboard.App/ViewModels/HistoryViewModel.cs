using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Monitoring;
using PowerMonitorDashboard.Mvvm;
using PowerMonitorDashboard.Services;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>历史表格中的一行。行对象是不可变快照，因此可以在后台线程安全创建。</summary>
public sealed class ReadingRow
{
    /// <summary>由实时结果创建行。</summary>
    public ReadingRow(PowerReading reading, OperatingStatus status)
    {
        Reading = reading;
        Status = status;
    }

    /// <summary>原始数据。</summary>
    public PowerReading Reading { get; }

    /// <summary>运行状态。</summary>
    public OperatingStatus Status { get; }

    /// <summary>是否异常。</summary>
    public bool IsAbnormal => Status != OperatingStatus.Normal;

    /// <summary>时间文本。</summary>
    public string TimestampText => Reading.TimestampText;

    /// <summary>电压。</summary>
    public double VoltageKv => Reading.VoltageKv;

    /// <summary>电流。</summary>
    public double CurrentA => Reading.CurrentA;

    /// <summary>功率。</summary>
    public double PowerMw => Reading.PowerMw;

    /// <summary>频率。</summary>
    public double FrequencyHz => Reading.FrequencyHz;

    /// <summary>电压文本。</summary>
    public string VoltageText => Reading.VoltageKv.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>电流文本。</summary>
    public string CurrentText => Reading.CurrentA.ToString("F0", CultureInfo.InvariantCulture);

    /// <summary>功率文本。</summary>
    public string PowerText => Reading.PowerMw.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>频率文本。</summary>
    public string FrequencyText => Reading.FrequencyHz.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>数据来源文本。</summary>
    public string SourceText => Reading.SourceText;

    /// <summary>数据质量文本。</summary>
    public string QualityText => Reading.QualityText;

    /// <summary>是否人工补录。</summary>
    public bool IsManual => Reading.IsManual;
}

/// <summary>
/// 历史数据表格视图模型。维护最新优先的行集合、异常筛选、自动滚动与统计信息。
/// </summary>
public sealed class HistoryViewModel : ObservableObject
{
    private readonly ObservableCollection<ReadingRow> _rows = [];
    private readonly ListCollectionView _view;
    private readonly Func<PowerThresholds> _thresholds;

    private bool _showOnlyAbnormal;
    private bool _autoScroll = true;
    private int _displayLimit = 500;

    /// <summary>创建历史视图模型。</summary>
    public HistoryViewModel(Func<PowerThresholds> thresholds)
    {
        _thresholds = thresholds;
        _view = new ListCollectionView(_rows)
        {
            Filter = FilterRow
        };
        _view.SortDescriptions.Add(new SortDescription(nameof(ReadingRow.TimestampText), ListSortDirection.Descending));
    }

    /// <summary>行集合变化（用于通知视图滚动到顶部）。</summary>
    public event EventHandler? RowsAdded;

    /// <summary>未过滤的行集合。</summary>
    public ObservableCollection<ReadingRow> Rows => _rows;

    /// <summary>带筛选/排序的视图。</summary>
    public ICollectionView View => _view;

    /// <summary>是否只显示异常数据。</summary>
    public bool ShowOnlyAbnormal
    {
        get => _showOnlyAbnormal;
        set
        {
            if (SetProperty(ref _showOnlyAbnormal, value))
            {
                _view.Refresh();
                OnPropertiesChanged(nameof(CountText), nameof(FilterText));
            }
        }
    }

    /// <summary>是否自动滚动到最新记录。</summary>
    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    /// <summary>表格最多保留的行数。</summary>
    public int DisplayLimit
    {
        get => _displayLimit;
        set
        {
            var normalized = Math.Clamp(value, 50, 5000);
            if (SetProperty(ref _displayLimit, normalized))
            {
                Trim();
                OnPropertyChanged(nameof(CountText));
            }
        }
    }

    /// <summary>行数说明。</summary>
    public string CountText => string.Create(
        CultureInfo.InvariantCulture,
        $"共 {_rows.Count} 条 · 显示上限 {DisplayLimit}");

    /// <summary>筛选说明。</summary>
    public string FilterText => ShowOnlyAbnormal ? "仅异常" : "全部数据";

    /// <summary>追加一条处理结果。</summary>
    public void Add(ProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Add(new ReadingRow(result.Reading.Clone(), result.Status.Status));
    }

    /// <summary>追加一行。</summary>
    public void Add(ReadingRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _rows.Insert(0, row);
        Trim();
        OnPropertyChanged(nameof(CountText));
        RowsAdded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>批量载入历史数据（按时间正序传入）。</summary>
    public void Load(IEnumerable<PowerReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var thresholds = _thresholds();
        foreach (var reading in readings)
        {
            var status = StatusEvaluator.Evaluate(reading, thresholds).Status;
            _rows.Add(new ReadingRow(reading, status));
        }

        Trim();
        OnPropertyChanged(nameof(CountText));
        RowsAdded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>清空表格。</summary>
    public void Clear()
    {
        _rows.Clear();
        OnPropertyChanged(nameof(CountText));
    }

    /// <summary>导出当前筛选后的数据。</summary>
    public IReadOnlyList<PowerReading> GetVisibleReadings()
        => _view.Cast<ReadingRow>().Select(static r => r.Reading).ToArray();

    private bool FilterRow(object item) => !ShowOnlyAbnormal || (item is ReadingRow row && row.IsAbnormal);

    private void Trim()
    {
        while (_rows.Count > DisplayLimit)
        {
            _rows.RemoveAt(_rows.Count - 1);
        }
    }
}
