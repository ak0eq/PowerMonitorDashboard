using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Visuals;

namespace PowerMonitorDashboard.Converters;

/// <summary>
/// 功率 → 负荷等级文本（高/中/低）的值转换器。
/// 这是作业要求中的 <see cref="IValueConverter"/> 示例：把 0～1000 MW 的连续值
/// 映射为运维人员更关心的定性描述，阈值可在 XAML 中通过属性覆盖。
/// </summary>
public sealed class PowerLevelConverter : IValueConverter
{
    /// <summary>“高”等级阈值，单位 MW。</summary>
    public double HighThreshold { get; set; } = 400d;

    /// <summary>“中”等级阈值，单位 MW。</summary>
    public double LowThreshold { get; set; } = 250d;

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "—";
        }

        if (!double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var power))
        {
            return "—";
        }

        return Classify(power) switch
        {
            PowerBand.High => "高",
            PowerBand.Medium => "中",
            _ => "低"
        };
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("负荷等级只用于展示，不支持反向转换。");

    private PowerBand Classify(double power)
    {
        if (power >= HighThreshold)
        {
            return PowerBand.High;
        }

        return power >= LowThreshold ? PowerBand.Medium : PowerBand.Low;
    }
}

/// <summary>功率 → 负荷等级颜色（高/中/低）。</summary>
public sealed class PowerLevelBrushConverter : IValueConverter
{
    /// <summary>高负荷画刷。</summary>
    public Brush HighBrush { get; set; } = AppBrushes.High;

    /// <summary>中负荷画刷。</summary>
    public Brush MediumBrush { get; set; } = AppBrushes.Medium;

    /// <summary>低负荷画刷。</summary>
    public Brush LowBrush { get; set; } = AppBrushes.Low;

    /// <summary>高负荷阈值。</summary>
    public double HighThreshold { get; set; } = 400d;

    /// <summary>中负荷阈值。</summary>
    public double LowThreshold { get; set; } = 250d;

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return LowBrush;
        }

        if (!double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var power))
        {
            return LowBrush;
        }

        if (power >= HighThreshold)
        {
            return HighBrush;
        }

        return power >= LowThreshold ? MediumBrush : LowBrush;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>运行状态 → 中文文本。</summary>
public sealed class OperatingStatusTextConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is OperatingStatus status
            ? status switch
            {
                OperatingStatus.Critical => "严重",
                OperatingStatus.Attention => "告警",
                _ => "正常"
            }
            : "未知";

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>运行状态 → 颜色。</summary>
public sealed class OperatingStatusBrushConverter : IValueConverter
{
    /// <summary>正常画刷。</summary>
    public Brush NormalBrush { get; set; } = AppBrushes.Success;

    /// <summary>注意画刷。</summary>
    public Brush AttentionBrush { get; set; } = AppBrushes.Warning;

    /// <summary>严重画刷。</summary>
    public Brush CriticalBrush { get; set; } = AppBrushes.Danger;

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is OperatingStatus status
            ? status switch
            {
                OperatingStatus.Critical => CriticalBrush,
                OperatingStatus.Attention => AttentionBrush,
                _ => NormalBrush
            }
            : AttentionBrush;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>告警级别 → 颜色。</summary>
public sealed class AlarmSeverityBrushConverter : IValueConverter
{
    /// <summary>提示画刷。</summary>
    public Brush InfoBrush { get; set; } = AppBrushes.Info;

    /// <summary>告警画刷。</summary>
    public Brush WarningBrush { get; set; } = AppBrushes.Warning;

    /// <summary>严重画刷。</summary>
    public Brush CriticalBrush { get; set; } = AppBrushes.Danger;

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AlarmSeverity severity
            ? severity switch
            {
                AlarmSeverity.Critical => CriticalBrush,
                AlarmSeverity.Warning => WarningBrush,
                _ => InfoBrush
            }
            : InfoBrush;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>布尔值 → 可见性（可反转）。</summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>是否反转。</summary>
    public bool Invert { get; set; }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>布尔值取反。</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>空字符串 → 可见性（用于提示文本的显隐）。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    /// <summary>是否反转（true 表示“非空时可见”）。</summary>
    public bool Invert { get; set; }

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasText = !string.IsNullOrWhiteSpace(value as string);
        if (Invert)
        {
            hasText = !hasText;
        }

        return hasText ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>数量 → 可见性（大于 0 时可见）。</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>比例值 → 百分比文本。</summary>
public sealed class PercentConverter : IValueConverter
{
    /// <summary>输入是否为 0～1 的比例值。</summary>
    public bool FromRatio { get; set; } = true;

    /// <summary>小数位数。</summary>
    public int Digits { get; set; } = 1;

    /// <summary>是否附加百分号。</summary>
    public bool WithSymbol { get; set; } = true;

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "—";
        }

        if (!double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return "—";
        }

        var percent = FromRatio ? number * 100d : number;
        var text = percent.ToString("F" + Digits, CultureInfo.InvariantCulture);
        return WithSymbol ? text + "%" : text;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
