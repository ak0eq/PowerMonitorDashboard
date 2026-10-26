using System.Globalization;
using System.Windows.Media;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Monitoring;
using PowerMonitorDashboard.Mvvm;
using PowerMonitorDashboard.Services;
using PowerMonitorDashboard.Visuals;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>拓扑图节点类型。</summary>
public enum TopologyNodeKind
{
    /// <summary>外部电网联络点。</summary>
    Grid,

    /// <summary>发电机组。</summary>
    Generator,

    /// <summary>主变压器。</summary>
    Transformer,

    /// <summary>母线。</summary>
    Busbar,

    /// <summary>变电站。</summary>
    Substation,

    /// <summary>负荷中心。</summary>
    Load
}

/// <summary>电压等级筛选选项。</summary>
/// <param name="Name">显示名称。</param>
/// <param name="Level">电压等级；<c>null</c> 表示全部。</param>
public sealed record VoltageLevelOption(string Name, VoltageLevel? Level)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>图例项。</summary>
/// <param name="Name">名称。</param>
/// <param name="Brush">颜色。</param>
public sealed record TopologyLegendItem(string Name, Brush Brush);

/// <summary>拓扑图节点。</summary>
public sealed class TopologyNode : ObservableObject
{
    private string _loadText = "—";
    private string _statusText = "正常";
    private Brush _borderBrush = AppBrushes.Create("#2B5876");
    private Brush _fillBrush = AppBrushes.Create("#0F2436");
    private double _opacity = 1d;
    private double _borderThickness = 1.4d;
    private bool _isAlert;

    /// <summary>节点标识。</summary>
    public required string Id { get; init; }

    /// <summary>节点名称。</summary>
    public required string Name { get; init; }

    /// <summary>节点类型。</summary>
    public required TopologyNodeKind Kind { get; init; }

    /// <summary>电压等级。</summary>
    public required VoltageLevel Level { get; init; }

    /// <summary>中心点 X 坐标（设计坐标系 1000×540）。</summary>
    public required double X { get; init; }

    /// <summary>中心点 Y 坐标。</summary>
    public required double Y { get; init; }

    /// <summary>宽度。</summary>
    public required double Width { get; init; }

    /// <summary>高度。</summary>
    public required double Height { get; init; }

    /// <summary>设备说明（ToolTip 第二行）。</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Canvas 左边距。</summary>
    public double Left => X - Width / 2d;

    /// <summary>Canvas 上边距。</summary>
    public double Top => Y - Height / 2d;

    /// <summary>电压等级文本。</summary>
    public string LevelText => Level switch
    {
        VoltageLevel.Kv10 => "10 kV",
        VoltageLevel.Kv110 => "110 kV",
        VoltageLevel.Kv220 => "220 kV",
        VoltageLevel.Kv500 => "500 kV",
        _ => Level.ToString()
    };

    /// <summary>类型文本。</summary>
    public string KindText => Kind switch
    {
        TopologyNodeKind.Grid => "区域电网",
        TopologyNodeKind.Generator => "发电机组",
        TopologyNodeKind.Transformer => "主变压器",
        TopologyNodeKind.Busbar => "母线",
        TopologyNodeKind.Substation => "变电站",
        _ => "负荷中心"
    };

    /// <summary>实时负载/电压文本。</summary>
    public string LoadText
    {
        get => _loadText;
        set => SetProperty(ref _loadText, value);
    }

    /// <summary>状态文本。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>边框画刷。</summary>
    public Brush BorderBrush
    {
        get => _borderBrush;
        set => SetProperty(ref _borderBrush, value);
    }

    /// <summary>填充画刷。</summary>
    public Brush FillBrush
    {
        get => _fillBrush;
        set => SetProperty(ref _fillBrush, value);
    }

    /// <summary>不透明度（用于电压等级筛选时弱化非选中设备）。</summary>
    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    /// <summary>边框粗细。</summary>
    public double BorderThickness
    {
        get => _borderThickness;
        set => SetProperty(ref _borderThickness, value);
    }

    /// <summary>是否处于告警状态。</summary>
    public bool IsAlert
    {
        get => _isAlert;
        set => SetProperty(ref _isAlert, value);
    }

    /// <summary>ToolTip 标题。</summary>
    public string TooltipTitle => $"{Name} · {KindText} · {LevelText}";
}

/// <summary>拓扑图连线。</summary>
public sealed class TopologyLink : ObservableObject
{
    private Brush _stroke = AppBrushes.Create("#2B5876");
    private double _opacity = 0.9d;
    private double _thickness = 2d;

    /// <summary>起点节点。</summary>
    public required TopologyNode From { get; init; }

    /// <summary>终点节点。</summary>
    public required TopologyNode To { get; init; }

    /// <summary>电压等级。</summary>
    public required VoltageLevel Level { get; init; }

    /// <summary>起点 X。</summary>
    public double X1 => From.X;

    /// <summary>起点 Y。</summary>
    public double Y1 => From.Y;

    /// <summary>终点 X。</summary>
    public double X2 => To.X;

    /// <summary>终点 Y。</summary>
    public double Y2 => To.Y;

    /// <summary>线宽。</summary>
    public double Thickness
    {
        get => _thickness;
        set => SetProperty(ref _thickness, value);
    }

    /// <summary>透明度。</summary>
    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    /// <summary>线条颜色。</summary>
    public Brush Stroke
    {
        get => _stroke;
        set => SetProperty(ref _stroke, value);
    }
}

/// <summary>
/// 电网单线拓扑图视图模型。设备坐标使用 1000×540 的设计坐标系，
/// 由视图层的 ViewBox 等比缩放，因此在不同分辨率的大屏上都能保持构图比例。
/// </summary>
public sealed class TopologyViewModel : ObservableObject
{
    private static readonly Brush DefaultNodeFill = AppBrushes.Create("#0F2436");
    private static readonly Brush DefaultNodeBorder = AppBrushes.Create("#2B5876");
    private static readonly Brush DimBrush = AppBrushes.Create("#1E3A50");
    private static readonly Brush GridBorder = AppBrushes.Create("#4ADE80");

    private VoltageLevelOption _selectedLevel;
    private PowerReading? _lastReading;
    private StatusEvaluation? _evaluation;

    /// <summary>创建拓扑视图模型。</summary>
    public TopologyViewModel()
    {
        LevelOptions =
        [
            new VoltageLevelOption("全部电压等级", null),
            new VoltageLevelOption("500 kV 联络", VoltageLevel.Kv500),
            new VoltageLevelOption("220 kV 主网", VoltageLevel.Kv220),
            new VoltageLevelOption("110 kV 配网", VoltageLevel.Kv110),
            new VoltageLevelOption("10 kV 机组", VoltageLevel.Kv10)
        ];
        _selectedLevel = LevelOptions[0];

        BuildNodes();
        BuildLinks();
        Recolor();
    }

    /// <summary>节点集合。</summary>
    public IReadOnlyList<TopologyNode> Nodes { get; private set; } = [];

    /// <summary>连线集合。</summary>
    public IReadOnlyList<TopologyLink> Links { get; private set; } = [];

    /// <summary>电压等级选项。</summary>
    public IReadOnlyList<VoltageLevelOption> LevelOptions { get; }

    /// <summary>图例。</summary>
    public IReadOnlyList<TopologyLegendItem> Legend { get; } =
    [
        new("500 kV", AppBrushes.Create("#4ADE80")),
        new("220 kV", AppBrushes.Create("#FFC53D")),
        new("110 kV", AppBrushes.Create("#FF6B6B")),
        new("10 kV", AppBrushes.Create("#7DD3FC"))
    ];

    /// <summary>当前选中的电压等级（null 表示全部）。</summary>
    public VoltageLevelOption SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (value is not null && SetProperty(ref _selectedLevel, value))
            {
                Recolor();
                OnPropertyChanged(nameof(SelectionText));
            }
        }
    }

    /// <summary>当前筛选说明。</summary>
    public string SelectionText => SelectedLevel.Level is null
        ? "显示全部电压等级"
        : $"高亮 {SelectedLevel.Name}";

    /// <summary>应用实时数据。</summary>
    public void Update(ProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Update(result.Reading, result.Status);
    }

    /// <summary>应用一条实时数据与状态评估（启动恢复时使用）。</summary>
    public void Update(PowerReading reading, StatusEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(evaluation);

        _lastReading = reading;
        _evaluation = evaluation;

        var power = reading.PowerMw;
        var voltage = reading.VoltageKv;

        foreach (var node in Nodes)
        {
            switch (node.Kind)
            {
                case TopologyNodeKind.Generator:
                    node.LoadText = string.Create(CultureInfo.InvariantCulture, $"{power / 3d:F1} MW");
                    break;
                case TopologyNodeKind.Busbar:
                    node.LoadText = string.Create(CultureInfo.InvariantCulture, $"{voltage:F1} kV");
                    break;
                case TopologyNodeKind.Substation:
                    node.LoadText = string.Create(CultureInfo.InvariantCulture, $"{power * ShareOf(node.Id):F1} MW");
                    break;
                case TopologyNodeKind.Load:
                    node.LoadText = string.Create(CultureInfo.InvariantCulture, $"{power * ShareOf(node.Id):F1} MW");
                    break;
                case TopologyNodeKind.Transformer:
                    node.LoadText = string.Create(CultureInfo.InvariantCulture, $"{power / 3d:F0} MW");
                    break;
                case TopologyNodeKind.Grid:
                    node.LoadText = string.Create(CultureInfo.InvariantCulture, $"{power * 0.18d:F0} MW");
                    break;
            }

            var isAlert = evaluation.Status == OperatingStatus.Critical
                          && node.Kind is TopologyNodeKind.Busbar or TopologyNodeKind.Substation;
            node.IsAlert = isAlert;
            node.StatusText = isAlert
                ? "严重越限"
                : evaluation.Status == OperatingStatus.Attention && node.Kind == TopologyNodeKind.Busbar
                    ? "越限告警"
                    : "运行正常";
        }

        Recolor();
    }

    private static double ShareOf(string id) => id switch
    {
        "S1" or "L1" => 0.36d,
        "S2" or "L2" => 0.34d,
        "S3" or "L3" => 0.30d,
        _ => 0d
    };

    private void BuildNodes()
    {
        Nodes =
        [
            new TopologyNode { Id = "GRID", Name = "区域电网", Kind = TopologyNodeKind.Grid, Level = VoltageLevel.Kv500, X = 648, Y = 40, Width = 54, Height = 30, Detail = "500 kV 区域联络点，与省网相连" },
            new TopologyNode { Id = "T4", Name = "联络变", Kind = TopologyNodeKind.Transformer, Level = VoltageLevel.Kv500, X = 556, Y = 40, Width = 62, Height = 32, Detail = "500/220 kV 联络变压器，容量 750 MVA" },
            new TopologyNode { Id = "G1", Name = "1号机组", Kind = TopologyNodeKind.Generator, Level = VoltageLevel.Kv10, X = 62, Y = 90, Width = 84, Height = 40, Detail = "300 MW 汽轮发电机组" },
            new TopologyNode { Id = "G2", Name = "2号机组", Kind = TopologyNodeKind.Generator, Level = VoltageLevel.Kv10, X = 62, Y = 165, Width = 84, Height = 40, Detail = "300 MW 汽轮发电机组" },
            new TopologyNode { Id = "G3", Name = "3号机组", Kind = TopologyNodeKind.Generator, Level = VoltageLevel.Kv10, X = 62, Y = 240, Width = 84, Height = 40, Detail = "150 MW 燃气机组" },
            new TopologyNode { Id = "T1", Name = "1号主变", Kind = TopologyNodeKind.Transformer, Level = VoltageLevel.Kv220, X = 196, Y = 90, Width = 58, Height = 34, Detail = "220/10 kV 升压变压器" },
            new TopologyNode { Id = "T2", Name = "2号主变", Kind = TopologyNodeKind.Transformer, Level = VoltageLevel.Kv220, X = 196, Y = 165, Width = 58, Height = 34, Detail = "220/10 kV 升压变压器" },
            new TopologyNode { Id = "T3", Name = "3号主变", Kind = TopologyNodeKind.Transformer, Level = VoltageLevel.Kv220, X = 196, Y = 240, Width = 58, Height = 34, Detail = "220/10 kV 升压变压器" },
            new TopologyNode { Id = "BUS", Name = "220 kV 母线", Kind = TopologyNodeKind.Busbar, Level = VoltageLevel.Kv220, X = 300, Y = 165, Width = 12, Height = 190, Detail = "双母带旁路接线，连接三台主变" },
            new TopologyNode { Id = "S1", Name = "城东变", Kind = TopologyNodeKind.Substation, Level = VoltageLevel.Kv220, X = 430, Y = 90, Width = 42, Height = 42, Detail = "220 kV 变电站，2×180 MVA" },
            new TopologyNode { Id = "S2", Name = "城南变", Kind = TopologyNodeKind.Substation, Level = VoltageLevel.Kv220, X = 430, Y = 165, Width = 42, Height = 42, Detail = "220 kV 变电站，2×150 MVA" },
            new TopologyNode { Id = "S3", Name = "城西变", Kind = TopologyNodeKind.Substation, Level = VoltageLevel.Kv220, X = 430, Y = 240, Width = 42, Height = 42, Detail = "220 kV 变电站，2×120 MVA" },
            new TopologyNode { Id = "L1", Name = "城区负荷", Kind = TopologyNodeKind.Load, Level = VoltageLevel.Kv110, X = 610, Y = 90, Width = 62, Height = 36, Detail = "商业与居民混合负荷" },
            new TopologyNode { Id = "L2", Name = "工业负荷", Kind = TopologyNodeKind.Load, Level = VoltageLevel.Kv110, X = 610, Y = 165, Width = 62, Height = 36, Detail = "工业园区负荷" },
            new TopologyNode { Id = "L3", Name = "郊区负荷", Kind = TopologyNodeKind.Load, Level = VoltageLevel.Kv110, X = 610, Y = 240, Width = 62, Height = 36, Detail = "农业与轻工业负荷" }
        ];
    }
    private void BuildLinks()
    {
        var map = Nodes.ToDictionary(static n => n.Id);
        Links =
        [
            new TopologyLink { From = map["G1"], To = map["T1"], Level = VoltageLevel.Kv10 },
            new TopologyLink { From = map["G2"], To = map["T2"], Level = VoltageLevel.Kv10 },
            new TopologyLink { From = map["G3"], To = map["T3"], Level = VoltageLevel.Kv10 },
            new TopologyLink { From = map["T1"], To = map["BUS"], Level = VoltageLevel.Kv220 },
            new TopologyLink { From = map["T2"], To = map["BUS"], Level = VoltageLevel.Kv220 },
            new TopologyLink { From = map["T3"], To = map["BUS"], Level = VoltageLevel.Kv220 },
            new TopologyLink { From = map["BUS"], To = map["S1"], Level = VoltageLevel.Kv220 },
            new TopologyLink { From = map["BUS"], To = map["S2"], Level = VoltageLevel.Kv220 },
            new TopologyLink { From = map["BUS"], To = map["S3"], Level = VoltageLevel.Kv220 },
            new TopologyLink { From = map["S1"], To = map["L1"], Level = VoltageLevel.Kv110 },
            new TopologyLink { From = map["S2"], To = map["L2"], Level = VoltageLevel.Kv110 },
            new TopologyLink { From = map["S3"], To = map["L3"], Level = VoltageLevel.Kv110 },
            new TopologyLink { From = map["S1"], To = map["T4"], Level = VoltageLevel.Kv500 },
            new TopologyLink { From = map["T4"], To = map["GRID"], Level = VoltageLevel.Kv500 }
        ];
    }

    private void Recolor()
    {
        var filter = SelectedLevel.Level;
        foreach (var node in Nodes)
        {
            var matches = filter is null || node.Level == filter;
            var levelBrush = LevelBrush(node.Level);

            node.Opacity = matches ? 1d : 0.22d;
            node.BorderThickness = matches && filter is not null ? 2.4d : (node.Kind == TopologyNodeKind.Busbar ? 2d : 1.4d);
            node.BorderBrush = !matches
                ? DimBrush
                : filter is null
                    ? DefaultNodeBorder
                    : levelBrush;
            node.FillBrush = node.Kind switch
            {
                TopologyNodeKind.Busbar => AppBrushes.Create("#13293D"),
                TopologyNodeKind.Grid => AppBrushes.Create("#0E2A22"),
                _ => DefaultNodeFill
            };

            if (node.IsAlert)
            {
                node.BorderBrush = AppBrushes.Danger;
                node.BorderThickness = 2.6d;
            }
            else if (node.Kind == TopologyNodeKind.Grid)
            {
                node.BorderBrush = GridBorder;
            }
        }

        foreach (var link in Links)
        {
            var matches = filter is null || link.Level == filter;
            link.Opacity = matches ? (filter is null ? 0.55d : 0.95d) : 0.12d;
            link.Thickness = matches && filter is not null ? 2.8d : 2d;
            link.Stroke = LevelBrush(link.Level);
        }

        if (_evaluation is { } evaluation && evaluation.HasIssues)
        {
            foreach (var link in Links.Where(static l => l.Level == VoltageLevel.Kv220))
            {
                link.Opacity = Math.Max(link.Opacity, 0.75d);
            }
        }
    }

    private static Brush LevelBrush(VoltageLevel level) => level switch
    {
        VoltageLevel.Kv500 => AppBrushes.Create("#4ADE80"),
        VoltageLevel.Kv220 => AppBrushes.Create("#FFC53D"),
        VoltageLevel.Kv110 => AppBrushes.Create("#FF6B6B"),
        _ => AppBrushes.Create("#7DD3FC")
    };
}
