using PowerMonitorDashboard.Core.Configuration;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Validation;

namespace PowerMonitorDashboard.Core.Tests;

public sealed class VoltageValidationTests
{
    [Theory]
    [InlineData("220.0")]
    [InlineData("0")]
    [InlineData("500")]
    [InlineData(" 220.5 ")]
    [InlineData("220．5")]
    [InlineData("1，5")]
    public void ValidValues_ShouldPass(string text)
        => Assert.Null(ReadingValidator.ValidateVoltage(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("500.1")]
    [InlineData("220.55")]
    [InlineData("1e3")]
    public void InvalidValues_ShouldFail(string? text)
        => Assert.NotNull(ReadingValidator.ValidateVoltage(text));

    [Fact]
    public void TooManyDecimals_ShouldReportSpecificMessage()
    {
        var issue = ReadingValidator.ValidateVoltage("220.55");

        Assert.NotNull(issue);
        Assert.Contains("小数", issue!.Message);
    }

    [Fact]
    public void OutOfRange_ShouldReportRangeMessage()
    {
        var issue = ReadingValidator.ValidateVoltage("501");

        Assert.NotNull(issue);
        Assert.Contains("0～500", issue!.Message);
    }
}

public sealed class CurrentValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("1500")]
    [InlineData("5000")]
    [InlineData("999.9")]
    public void ValidValues_ShouldPass(string text)
        => Assert.Null(ReadingValidator.ValidateCurrent(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("5001")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void InvalidValues_ShouldFail(string? text)
        => Assert.NotNull(ReadingValidator.ValidateCurrent(text));
}

public sealed class FrequencyValidationTests
{
    [Theory]
    [InlineData("47.0")]
    [InlineData("50.00")]
    [InlineData("52")]
    [InlineData("49.98")]
    public void ValidValues_ShouldPass(string text)
        => Assert.Null(ReadingValidator.ValidateFrequency(text));

    [Theory]
    [InlineData("46.9")]
    [InlineData("52.1")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("五十")]
    public void InvalidValues_ShouldFail(string? text)
        => Assert.NotNull(ReadingValidator.ValidateFrequency(text));
}

public sealed class PowerValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("362.5")]
    [InlineData("1000")]
    public void ValidValues_ShouldPass(string? text)
        => Assert.Null(ReadingValidator.ValidatePower(text));

    [Theory]
    [InlineData("-1")]
    [InlineData("1000.1")]
    [InlineData("abc")]
    public void InvalidValues_ShouldFail(string? text)
        => Assert.NotNull(ReadingValidator.ValidatePower(text));
}

public sealed class TimestampValidationTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void PastTimestamp_ShouldPass()
    {
        var text = TestData.Format(Now.AddMinutes(-5));

        Assert.Null(ReadingValidator.ValidateTimestamp(text, Now));
    }

    [Fact]
    public void CurrentTimestamp_ShouldPass()
    {
        Assert.Null(ReadingValidator.ValidateTimestamp(TestData.Format(Now), Now));
    }

    [Fact]
    public void FutureTimestamp_ShouldFail()
    {
        var issue = ReadingValidator.ValidateTimestamp(TestData.Format(Now.AddMinutes(1)), Now);

        Assert.NotNull(issue);
        Assert.Contains("不能晚于当前时间", issue!.Message);
    }

    [Fact]
    public void EmptyTimestamp_ShouldFail()
    {
        var issue = ReadingValidator.ValidateTimestamp(" ", Now);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("2026/10/26 20:00:00")]
    [InlineData("2026-10-26 20:00")]
    [InlineData("2026/10/26 20:00")]
    public void SupportedFormats_ShouldPass(string text)
    {
        var now = new DateTimeOffset(2026, 10, 26, 21, 0, 0, TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));

        Assert.Null(ReadingValidator.ValidateTimestamp(text, now));
    }

    [Fact]
    public void MalformedTimestamp_ShouldFail()
    {
        Assert.NotNull(ReadingValidator.ValidateTimestamp("昨天下午", Now));
        Assert.NotNull(ReadingValidator.ValidateTimestamp("2026-13-01 10:00:00", Now));
    }
}

public sealed class ReadingValidationIntegrationTests
{
    private static ReadingDraft ValidDraft() => new("220.0", "1000", "362", "50.00", TestData.Format(TestData.Now.AddMinutes(-1)));

    [Fact]
    public void ValidDraft_ShouldProduceValidResult()
    {
        var result = ReadingValidator.Validate(ValidDraft(), TestData.Now);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void MultipleErrors_ShouldBeCollectedAtOnce()
    {
        var draft = new ReadingDraft("999", "", "300", "60", TestData.Format(TestData.Now.AddHours(1)));
        var result = ReadingValidator.Validate(draft, TestData.Now);

        Assert.False(result.IsValid);
        Assert.True(result.Issues.Count(i => i.IsError) >= 4);
        Assert.NotEmpty(result.Summary);
    }

    [Fact]
    public void MismatchedPower_ShouldProduceWarningOnly()
    {
        var draft = new ReadingDraft("220.0", "1000", "300", "50.00", TestData.Format(TestData.Now));
        var result = ReadingValidator.Validate(draft, TestData.Now, assumedPowerFactor: 0.95d);

        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings);
        var warning = Assert.Single(result.Issues);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
        Assert.Contains("估算约", warning.Message);
    }

    [Fact]
    public void MatchingPower_ShouldNotWarn()
    {
        var draft = new ReadingDraft("220.0", "1000", "362.0", "50.00", TestData.Format(TestData.Now));
        var result = ReadingValidator.Validate(draft, TestData.Now);

        Assert.Empty(result.Issues);
    }

    [Fact]
    public void EmptyPower_ShouldBeCalculatedFromVoltageAndCurrent()
    {
        var draft = new ReadingDraft("220.0", "1000", string.Empty, "50.00", TestData.Format(TestData.Now.AddMinutes(-1)));

        var ok = ReadingValidator.TryBuildReading(draft, TestData.Now, 0.95d, out var reading, out var result);

        Assert.True(ok);
        Assert.True(result.IsValid);
        Assert.NotNull(reading);
        Assert.Equal(ReadingSource.Manual, reading!.Source);
        Assert.Equal(362d, reading.PowerMw, 0);
        Assert.Equal(Math.Round(reading.VoltageKv, 1), reading.VoltageKv, 6);
    }

    [Fact]
    public void TryBuildReading_ShouldRejectInvalidDraft()
    {
        var draft = new ReadingDraft("abc", "1000", null, "50", TestData.Format(TestData.Now));

        var ok = ReadingValidator.TryBuildReading(draft, TestData.Now, 0.95d, out var reading, out var result);

        Assert.False(ok);
        Assert.Null(reading);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void TryBuildReading_ShouldUseEnteredTimestamp()
    {
        var timestamp = TestData.Now.AddMinutes(-3);
        var draft = new ReadingDraft("220.0", "1000", "362", "50.00", TestData.Format(timestamp));

        Assert.True(ReadingValidator.TryBuildReading(draft, TestData.Now, 0.95d, out var reading, out _));
        Assert.NotNull(reading);
        Assert.Equal(timestamp.ToString("yyyy-MM-dd HH:mm:ss"), reading!.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
    }
}

public sealed class MonitorSettingsTests
{
    [Fact]
    public void Default_ShouldBeValid()
    {
        var settings = new MonitorSettings();

        Assert.Empty(settings.Validate());
    }

    [Fact]
    public void InvalidSamplingInterval_ShouldBeReported()
    {
        var settings = new MonitorSettings { SamplingIntervalMs = 10 };

        Assert.NotEmpty(settings.Validate());
    }

    [Fact]
    public void InvalidThresholdOrdering_ShouldBeReported()
    {
        var settings = new MonitorSettings
        {
            Thresholds = new PowerThresholds
            {
                FrequencyAttentionLowHz = 50.5d,
                FrequencyAttentionHighHz = 49.5d
            }
        };

        Assert.NotEmpty(settings.Validate());
    }

    [Fact]
    public void Normalize_ShouldClampOutOfRangeValues()
    {
        var settings = new MonitorSettings
        {
            SamplingIntervalMs = 1,
            HistoryCapacity = 1,
            HistoryDisplayLimit = 1,
            AssumedPowerFactor = 5d,
            OperatorName = "  "
        };

        settings.Normalize();

        Assert.InRange(settings.SamplingIntervalMs, 200, 60000);
        Assert.InRange(settings.HistoryCapacity, 100, 100000);
        Assert.InRange(settings.HistoryDisplayLimit, 50, 5000);
        Assert.InRange(settings.AssumedPowerFactor, 0.5d, 1d);
        Assert.Equal("值班员", settings.OperatorName);
    }

    [Fact]
    public void Clone_ShouldBeIndependentCopy()
    {
        var settings = new MonitorSettings { OperatorName = "张工" };
        var clone = settings.Clone();

        clone.OperatorName = "李工";
        clone.Thresholds = clone.Thresholds with { CurrentAttentionA = 1000d };

        Assert.Equal("张工", settings.OperatorName);
        Assert.NotEqual(1000d, settings.Thresholds.CurrentAttentionA);
    }
}
