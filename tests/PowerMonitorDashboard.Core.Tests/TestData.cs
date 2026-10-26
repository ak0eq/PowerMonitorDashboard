using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Domain;

namespace PowerMonitorDashboard.Core.Tests;

/// <summary>测试用固定时钟。</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now) => Now = now;

    public DateTimeOffset Now { get; set; }

    public void Advance(TimeSpan delta) => Now += delta;
}

/// <summary>测试数据构造工具。</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now =
        new(new DateTime(2026, 10, 26, 20, 0, 0, DateTimeKind.Local));

    public static PowerReading Reading(
        double voltage = 220d,
        double current = 1000d,
        double power = 362d,
        double frequency = 50d,
        DateTimeOffset? timestamp = null,
        ReadingSource source = ReadingSource.Simulated,
        QualityFlag quality = QualityFlag.Good)
        => new(timestamp ?? Now, voltage, current, power, frequency, source, quality);

    public static string Format(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss");
}
