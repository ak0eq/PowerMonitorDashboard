using System.ComponentModel;
using System.Text.Json;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Export;

namespace PowerMonitorDashboard.Core.Tests;

public sealed class PowerReadingTests
{
    [Fact]
    public void VoltageChange_ShouldNotifyDependentProperties()
    {
        var reading = TestData.Reading();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)reading).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        reading.VoltageKv = 221.5d;

        Assert.Contains(nameof(PowerReading.VoltageKv), changed);
        Assert.Contains(nameof(PowerReading.ApparentPowerMva), changed);
        Assert.Contains(nameof(PowerReading.PowerFactor), changed);
        Assert.Contains(nameof(PowerReading.VoltageText), changed);
    }

    [Fact]
    public void SettingSameValue_ShouldNotNotify()
    {
        var reading = TestData.Reading();
        var count = 0;
        reading.PropertyChanged += (_, _) => count++;

        reading.VoltageKv = 220d;

        Assert.Equal(0, count);
    }

    [Fact]
    public void UpdateFrom_ShouldCopyValuesButKeepIdentity()
    {
        var live = TestData.Reading();
        var id = live.Id;
        var other = new PowerReading(
            TestData.Now.AddMinutes(1), 221.2d, 1050d, 380.5d, 49.98d, ReadingSource.Manual, QualityFlag.Uncertain);

        live.UpdateFrom(other);

        Assert.Equal(id, live.Id);
        Assert.Equal(221.2d, live.VoltageKv);
        Assert.Equal(1050d, live.CurrentA);
        Assert.Equal(380.5d, live.PowerMw);
        Assert.Equal(49.98d, live.FrequencyHz);
        Assert.Equal(ReadingSource.Manual, live.Source);
        Assert.Equal(QualityFlag.Uncertain, live.Quality);
        Assert.Equal(other.Timestamp, live.Timestamp);
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        var reading = TestData.Reading();

        var clone = reading.Clone();
        clone.VoltageKv = 100d;

        Assert.Equal(220d, reading.VoltageKv);
        Assert.Equal(reading.Id, clone.Id);
    }

    [Fact]
    public void ComputedProperties_ShouldMatchPhysics()
    {
        var reading = TestData.Reading(voltage: 220d, current: 1000d, power: 362d);

        Assert.Equal(381.05d, reading.ApparentPowerMva, 2);
        Assert.Equal(0.95d, reading.PowerFactor, 2);
    }

    [Fact]
    public void TextProperties_ShouldUseInvariantCulture()
    {
        var reading = TestData.Reading(voltage: 220.04d, current: 1000.6d, power: 362.36d, frequency: 49.987d);

        Assert.Equal("220.0", reading.VoltageText);
        Assert.Equal("1001", reading.CurrentText);
        Assert.Equal("362.4", reading.PowerText);
        Assert.Equal("49.99", reading.FrequencyText);
    }

    [Fact]
    public void IsStale_ShouldCompareTimestamp()
    {
        var reading = TestData.Reading();

        Assert.True(reading.IsStale(TestData.Now.AddMinutes(5), TimeSpan.FromMinutes(1)));
        Assert.False(reading.IsStale(TestData.Now.AddSeconds(30), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void SourceText_ShouldBeChinese()
    {
        var reading = TestData.Reading(source: ReadingSource.Manual);

        Assert.Equal("人工补录", reading.SourceText);
        Assert.True(reading.IsManual);
    }

    [Fact]
    public void QualityText_ShouldBeChinese()
    {
        var reading = TestData.Reading(quality: QualityFlag.Uncertain);

        Assert.Equal("可疑", reading.QualityText);
    }
}

public sealed class CsvFormatterTests
{
    [Fact]
    public void Readings_ShouldProduceHeaderAndRows()
    {
        var readings = new[]
        {
            TestData.Reading(timestamp: TestData.Now),
            TestData.Reading(voltage: 219.5d, timestamp: TestData.Now.AddSeconds(1))
        };

        var csv = CsvFormatter.ForReadings(readings, PowerThresholds.Default);
        var lines = csv.TrimEnd().Split(Environment.NewLine);

        Assert.Equal(3, lines.Length);
        Assert.Contains("采样时间", lines[0]);
        Assert.Contains("电压(kV)", lines[0]);
        Assert.Contains("220.0", lines[1]);
        Assert.Contains("219.5", lines[2]);
        Assert.Contains("仿真采集", lines[2]);
        Assert.Contains("正常", lines[2]);
    }

    [Fact]
    public void Readings_ShouldUseBandText()
    {
        var csv = CsvFormatter.ForReadings([TestData.Reading(power: 510d)], PowerThresholds.Default);

        Assert.Contains(",高,", csv);
    }

    [Fact]
    public void Alarms_ShouldEscapeCommasAndQuotes()
    {
        var alarm = new Alarm(
            AlarmKind.FrequencyHigh,
            AlarmSeverity.Warning,
            "频率越限, \"请立即处理\"",
            50.62d,
            50.5d,
            TestData.Now);

        var csv = CsvFormatter.ForAlarms([alarm]);
        var dataLine = csv.TrimEnd().Split(Environment.NewLine)[1];

        Assert.Contains("\"频率越限, \"\"请立即处理\"\"\"", dataLine);
        Assert.Contains("告警", dataLine);
    }

    [Fact]
    public void Alarms_ShouldBeOrderedByRaisedTimeDescending()
    {
        var older = new Alarm(AlarmKind.Overload, AlarmSeverity.Warning, "旧告警", 440d, 430d, TestData.Now.AddMinutes(-5));
        var newer = new Alarm(AlarmKind.VoltageLow, AlarmSeverity.Warning, "新告警", 205d, 209d, TestData.Now);

        var csv = CsvFormatter.ForAlarms([older, newer]);
        var lines = csv.TrimEnd().Split(Environment.NewLine);

        Assert.Contains("新告警", lines[1]);
        Assert.Contains("旧告警", lines[2]);
    }
}

public sealed class SettingsSerializationTests
{
    [Fact]
    public void MonitorSettings_ShouldRoundTripThroughJson()
    {
        var settings = new Domain.PowerThresholds
        {
            NominalVoltageKv = 110d,
            CurrentAttentionA = 800d
        };
        var source = new Configuration.MonitorSettings
        {
            SamplingIntervalMs = 2000,
            OperatorName = "张工",
            Thresholds = settings
        };

        var json = JsonSerializer.Serialize(source);
        var restored = JsonSerializer.Deserialize<Configuration.MonitorSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(2000, restored!.SamplingIntervalMs);
        Assert.Equal("张工", restored.OperatorName);
        Assert.Equal(110d, restored.Thresholds.NominalVoltageKv);
        Assert.Equal(800d, restored.Thresholds.CurrentAttentionA);
    }
}
