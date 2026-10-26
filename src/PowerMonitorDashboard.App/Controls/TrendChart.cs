using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PowerMonitorDashboard.Controls;

/// <summary>
/// 轻量级趋势图控件。直接使用 <see cref="DrawingContext"/> 绘制，
/// 相比通用图表库占用更少内存、刷新更平滑，并且天然支持深色大屏配色。
/// </summary>
public sealed class TrendChart : FrameworkElement
{
    /// <summary>数据序列。</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<double>),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>纵轴最小值。</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>纵轴最大值。</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>正常区间下限（绘制参考线）。</summary>
    public static readonly DependencyProperty ThresholdLowProperty = DependencyProperty.Register(
        nameof(ThresholdLow),
        typeof(double),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>正常区间上限。</summary>
    public static readonly DependencyProperty ThresholdHighProperty = DependencyProperty.Register(
        nameof(ThresholdHigh),
        typeof(double),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>线条颜色。</summary>
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(Brushes.Cyan, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>是否绘制坐标轴文字。</summary>
    public static readonly DependencyProperty ShowAxisProperty = DependencyProperty.Register(
        nameof(ShowAxis),
        typeof(bool),
        typeof(TrendChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>数值格式。</summary>
    public static readonly DependencyProperty ValueFormatProperty = DependencyProperty.Register(
        nameof(ValueFormat),
        typeof(string),
        typeof(TrendChart),
        new FrameworkPropertyMetadata("F1", FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>无数据时的提示文本。</summary>
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText),
        typeof(string),
        typeof(TrendChart),
        new FrameworkPropertyMetadata("等待数据…", FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface Typeface = new("Microsoft YaHei UI");
    private static readonly Brush GridBrush = Frozen("#FF123049");
    private static readonly Brush MutedBrush = Frozen("#FF647F97");
    private static readonly Brush BandBrush = Frozen("#1222D3EE");
    private static readonly Pen GridPen = CreatePen(GridBrush, 1d);
    private static readonly Pen ThresholdPen = CreateDashedPen(Frozen("#66F5B93E"), 1d, 4d, 4d);

    /// <summary>数据序列。</summary>
    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>纵轴最小值。</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>纵轴最大值。</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>正常区间下限。</summary>
    public double ThresholdLow
    {
        get => (double)GetValue(ThresholdLowProperty);
        set => SetValue(ThresholdLowProperty, value);
    }

    /// <summary>正常区间上限。</summary>
    public double ThresholdHigh
    {
        get => (double)GetValue(ThresholdHighProperty);
        set => SetValue(ThresholdHighProperty, value);
    }

    /// <summary>线条颜色。</summary>
    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>是否绘制坐标轴文字。</summary>
    public bool ShowAxis
    {
        get => (bool)GetValue(ShowAxisProperty);
        set => SetValue(ShowAxisProperty, value);
    }

    /// <summary>数值格式。</summary>
    public string ValueFormat
    {
        get => (string)GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    /// <summary>无数据提示。</summary>
    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 12d || height < 12d)
        {
            return;
        }

        var left = ShowAxis ? 44d : 2d;
        var right = ShowAxis ? 10d : 2d;
        var top = 8d;
        var bottom = ShowAxis ? 8d : 4d;
        var plotWidth = Math.Max(1d, width - left - right);
        var plotHeight = Math.Max(1d, height - top - bottom);

        var min = Minimum;
        var max = Maximum;
        if (double.IsNaN(min) || double.IsNaN(max) || max - min <= 1e-9)
        {
            max = min + 1d;
        }

        var range = max - min;
        double MapY(double value) => top + plotHeight - Math.Clamp((value - min) / range, 0d, 1d) * plotHeight;

        // 网格与刻度
        var gridCount = 3;
        for (var i = 0; i < gridCount; i++)
        {
            var ratio = i / (double)(gridCount - 1);
            var y = top + plotHeight * ratio;
            drawingContext.DrawLine(GridPen, new Point(left, y), new Point(left + plotWidth, y));

            if (ShowAxis)
            {
                var labelValue = max - range * ratio;
                var text = FormatValue(labelValue);
                var formatted = CreateText(text, 9.5d, MutedBrush);
                drawingContext.DrawText(formatted, new Point(left - formatted.Width - 6d, y - formatted.Height / 2d));
            }
        }

        // 正常区间
        var hasLow = !double.IsNaN(ThresholdLow);
        var hasHigh = !double.IsNaN(ThresholdHigh);
        if (hasLow && hasHigh)
        {
            var yHigh = MapY(ThresholdHigh);
            var yLow = MapY(ThresholdLow);
            drawingContext.DrawRectangle(BandBrush, null, new Rect(left, yHigh, plotWidth, Math.Max(0d, yLow - yHigh)));
            drawingContext.DrawLine(ThresholdPen, new Point(left, yHigh), new Point(left + plotWidth, yHigh));
            drawingContext.DrawLine(ThresholdPen, new Point(left, yLow), new Point(left + plotWidth, yLow));
        }

        var values = Values;
        if (values is null || values.Count < 2)
        {
            var empty = CreateText(EmptyText, 11d, MutedBrush);
            drawingContext.DrawText(empty, new Point(left + (plotWidth - empty.Width) / 2d, top + (plotHeight - empty.Height) / 2d));
            return;
        }

        var step = values.Count <= 1 ? plotWidth : plotWidth / (values.Count - 1);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(left, MapY(values[0])), isFilled: true, isClosed: false);
            for (var i = 1; i < values.Count; i++)
            {
                context.LineTo(new Point(left + step * i, MapY(values[i])), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();

        var lineColor = (LineBrush as SolidColorBrush)?.Color ?? Colors.Cyan;
        var areaBrush = new LinearGradientBrush(
            Color.FromArgb(72, lineColor.R, lineColor.G, lineColor.B),
            Color.FromArgb(0, lineColor.R, lineColor.G, lineColor.B),
            new Point(0, 0),
            new Point(0, 1));
        areaBrush.Freeze();

        var areaGeometry = new StreamGeometry();
        using (var context = areaGeometry.Open())
        {
            context.BeginFigure(new Point(left, top + plotHeight), isFilled: true, isClosed: true);
            for (var i = 0; i < values.Count; i++)
            {
                context.LineTo(new Point(left + step * i, MapY(values[i])), isStroked: false, isSmoothJoin: false);
            }

            context.LineTo(new Point(left + plotWidth, top + plotHeight), isStroked: false, isSmoothJoin: false);
        }

        areaGeometry.Freeze();
        drawingContext.DrawGeometry(areaBrush, null, areaGeometry);

        var linePen = new Pen(LineBrush, 1.8d)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        linePen.Freeze();
        drawingContext.DrawGeometry(null, linePen, geometry);

        // 末端高亮
        var lastX = left + step * (values.Count - 1);
        var lastY = MapY(values[^1]);
        var glowBrush = new SolidColorBrush(Color.FromArgb(90, lineColor.R, lineColor.G, lineColor.B));
        glowBrush.Freeze();
        drawingContext.DrawEllipse(glowBrush, null, new Point(lastX, lastY), 7d, 7d);
        drawingContext.DrawEllipse(LineBrush, null, new Point(lastX, lastY), 2.6d, 2.6d);
    }

    private string FormatValue(double value)
        => value.ToString(ValueFormat, CultureInfo.InvariantCulture);

    private FormattedText CreateText(string text, double size, Brush brush)
        => new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            Typeface,
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static SolidColorBrush Frozen(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static Pen CreateDashedPen(Brush brush, double thickness, double dash, double gap)
    {
        var pen = new Pen(brush, thickness)
        {
            DashStyle = new DashStyle([dash, gap], 0)
        };
        pen.Freeze();
        return pen;
    }
}
