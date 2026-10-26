using System.Globalization;
using System.Windows;
using PowerMonitorDashboard.Converters;
using PowerMonitorDashboard.Core.Domain;

namespace PowerMonitorDashboard.App.Tests;

public sealed class PowerLevelConverterTests
{
    private readonly PowerLevelConverter _converter = new() { HighThreshold = 400d, LowThreshold = 250d };

    [Theory]
    [InlineData(500d, "高")]
    [InlineData(400d, "高")]
    [InlineData(399.9d, "中")]
    [InlineData(250d, "中")]
    [InlineData(120d, "低")]
    public void Convert_ShouldMapPowerToBandText(double power, string expected)
    {
        var result = _converter.Convert(power, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_ShouldHandleNullAndInvalidInput()
    {
        Assert.Equal("—", _converter.Convert(null!, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("—", _converter.Convert("abc", typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_ShouldThrow()
    {
        Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack("高", typeof(double), null, CultureInfo.InvariantCulture));
    }
}

public sealed class StatusAndPercentConverterTests
{
    [Theory]
    [InlineData(OperatingStatus.Normal, "正常")]
    [InlineData(OperatingStatus.Attention, "告警")]
    [InlineData(OperatingStatus.Critical, "严重")]
    public void StatusText_ShouldMapToChinese(OperatingStatus status, string expected)
    {
        var converter = new OperatingStatusTextConverter();

        Assert.Equal(expected, converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PercentConverter_ShouldFormatRatio()
    {
        var converter = new PercentConverter { FromRatio = true, Digits = 1 };

        Assert.Equal("82.5%", converter.Convert(0.825d, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PercentConverter_ShouldHandleNull()
    {
        var converter = new PercentConverter();

        Assert.Equal("—", converter.Convert(null!, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BooleanToVisibility_ShouldInvert()
    {
        var converter = new BooleanToVisibilityConverter { Invert = true };

        Assert.Equal(Visibility.Collapsed, converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, converter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CountToVisibility_ShouldCollapseZero()
    {
        var converter = new CountToVisibilityConverter();

        Assert.Equal(Visibility.Collapsed, converter.Convert(0, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, converter.Convert(3, typeof(Visibility), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void StringToVisibility_ShouldRespectInvert()
    {
        var converter = new StringToVisibilityConverter { Invert = true };

        Assert.Equal(Visibility.Collapsed, converter.Convert("有内容", typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, converter.Convert("  ", typeof(Visibility), null, CultureInfo.InvariantCulture));
    }
}
