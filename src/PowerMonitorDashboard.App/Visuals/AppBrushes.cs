using System.Windows.Media;

namespace PowerMonitorDashboard.Visuals;

/// <summary>
/// 冻结画刷工厂。WPF 中被冻结的 Freezable 可以跨线程使用，
/// 且渲染时无需变更通知，能显著降低大屏高频刷新时的开销。
/// </summary>
public static class AppBrushes
{
    /// <summary>主强调色（青色）。</summary>
    public static readonly SolidColorBrush Accent = Create("#22D3EE");

    /// <summary>次强调色（蓝色）。</summary>
    public static readonly SolidColorBrush AccentSecondary = Create("#3B82F6");

    /// <summary>正常状态色。</summary>
    public static readonly SolidColorBrush Success = Create("#2DD4A7");

    /// <summary>告警状态色。</summary>
    public static readonly SolidColorBrush Warning = Create("#F5B93E");

    /// <summary>严重状态色。</summary>
    public static readonly SolidColorBrush Danger = Create("#FF5D6C");

    /// <summary>主文本色。</summary>
    public static readonly SolidColorBrush TextPrimary = Create("#E8F4FF");

    /// <summary>次文本色。</summary>
    public static readonly SolidColorBrush TextSecondary = Create("#93AEC4");

    /// <summary>弱文本色。</summary>
    public static readonly SolidColorBrush TextMuted = Create("#5F7A90");

    /// <summary>高负荷颜色。</summary>
    public static readonly SolidColorBrush High = Create("#FF6B6B");

    /// <summary>中负荷颜色。</summary>
    public static readonly SolidColorBrush Medium = Create("#FFB648");

    /// <summary>低负荷颜色。</summary>
    public static readonly SolidColorBrush Low = Create("#35D0BA");

    /// <summary>信息色。</summary>
    public static readonly SolidColorBrush Info = Create("#7DD3FC");

    /// <summary>透明画刷。</summary>
    public static readonly SolidColorBrush Transparent = Create("#00000000");

    /// <summary>按十六进制颜色创建冻结的实心画刷。</summary>
    public static SolidColorBrush Create(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>返回带指定透明度的画刷副本。</summary>
    public static SolidColorBrush WithOpacity(SolidColorBrush source, byte alpha)
    {
        var color = source.Color;
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
