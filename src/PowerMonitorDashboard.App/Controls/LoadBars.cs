using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PowerMonitorDashboard.Controls;

/// <summary>
/// 馈线负荷波动柱状图。用 <see cref="DoubleAnimationUsingKeyFrames"/> 驱动 <see cref="Phase"/>，
/// 再由相位映射出 6 根柱子的高度，模拟负荷的周期性波动；
/// <see cref="Intensity"/> 与实时功率联动，功率越大波动幅度越大。
/// </summary>
public sealed class LoadBars : FrameworkElement
{
    /// <summary>动画相位（0～1，循环）。</summary>
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase),
        typeof(double),
        typeof(LoadBars),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>波动强度（0～1），来自实时负荷率。</summary>
    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity),
        typeof(double),
        typeof(LoadBars),
        new FrameworkPropertyMetadata(0.7d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>是否处于采集状态。</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(LoadBars),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>柱状图标签。</summary>
    public static readonly DependencyProperty LabelsProperty = DependencyProperty.Register(
        nameof(Labels),
        typeof(IReadOnlyList<string>),
        typeof(LoadBars),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly double[] BaseHeights = [0.52d, 0.68d, 0.60d, 0.79d, 0.56d, 0.72d];
    private static readonly Brush GridBrush = Frozen("#FF123049");
    private static readonly Brush LabelBrush = Frozen("#FF6B879C");
    private static readonly Typeface Typeface = new("Microsoft YaHei UI");

    /// <summary>创建柱状图并启动关键帧动画。</summary>
    public LoadBars()
    {
        Loaded += (_, _) => StartAnimation();
        Unloaded += (_, _) => BeginAnimation(PhaseProperty, null);
    }

    /// <summary>动画相位。</summary>
    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    /// <summary>波动强度。</summary>
    public double Intensity
    {
        get => (double)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    /// <summary>是否活动。</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>柱子标签。</summary>
    public IReadOnlyList<string>? Labels
    {
        get => (IReadOnlyList<string>?)GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 40d || height < 40d)
        {
            return;
        }

        const double bottomPadding = 18d;
        var plotHeight = height - bottomPadding;
        var accent = Frozen("#FF22D3EE");
        var accentAlt = Frozen("#FF3B82F6");
        var gradient = new LinearGradientBrush(accent.Color, accentAlt.Color, new Point(0, 1), new Point(0, 0));
        gradient.Freeze();

        drawingContext.DrawLine(new Pen(GridBrush, 1d), new Point(0, plotHeight), new Point(width, plotHeight));

        var labels = Labels ?? ["A", "B", "C", "D", "E", "F"];
        var count = Math.Min(BaseHeights.Length, Math.Max(1, labels.Count));
        var slot = width / count;
        var barWidth = Math.Min(26d, slot * 0.46d);
        var intensity = Math.Clamp(Intensity, 0d, 1d);
        var opacity = IsActive ? 1d : 0.35d;

        for (var i = 0; i < count; i++)
        {
            var oscillation = 0.5d + 0.5d * Math.Sin((Phase + i * 0.137d) * Math.PI * 2d);
            var amplitude = 0.55d + 0.45d * intensity;
            var ratio = Math.Clamp(BaseHeights[i] * amplitude * (0.62d + 0.38d * oscillation), 0.08d, 1d);
            var barHeight = Math.Max(4d, plotHeight * ratio);
            var x = slot * i + (slot - barWidth) / 2d;
            var y = plotHeight - barHeight;

            drawingContext.PushOpacity(opacity);
            var body = new Rect(x, y, barWidth, barHeight);
            drawingContext.DrawRoundedRectangle(gradient, null, body, 3d, 3d);
            drawingContext.DrawRectangle(Frozen("#FF9BEBFF"), null, new Rect(x, y, barWidth, 2d));
            drawingContext.Pop();

            var label = CreateText(labels[i], 9.5d, LabelBrush);
            drawingContext.DrawText(label, new Point(x + (barWidth - label.Width) / 2d, plotHeight + 4d));
        }
    }

    private void StartAnimation()
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(6.4d),
            RepeatBehavior = RepeatBehavior.Forever
        };

        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromPercent(0d)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.22d, KeyTime.FromPercent(0.18d), new SineEase { EasingMode = EasingMode.EaseInOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.41d, KeyTime.FromPercent(0.37d), new SineEase { EasingMode = EasingMode.EaseInOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.63d, KeyTime.FromPercent(0.58d), new SineEase { EasingMode = EasingMode.EaseInOut }));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.85d, KeyTime.FromPercent(0.79d), new SineEase { EasingMode = EasingMode.EaseInOut }));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1d, KeyTime.FromPercent(1d)));

        BeginAnimation(PhaseProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

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
}
