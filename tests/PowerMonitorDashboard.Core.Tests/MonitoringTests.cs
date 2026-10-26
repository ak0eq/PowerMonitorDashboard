using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.History;
using PowerMonitorDashboard.Core.Monitoring;

namespace PowerMonitorDashboard.Core.Tests;

public sealed class StatusEvaluatorTests
{
    private static readonly PowerThresholds Thresholds = PowerThresholds.Default;

    [Fact]
    public void NormalReading_ShouldBeNormal()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(), Thresholds);

        Assert.Equal(OperatingStatus.Normal, evaluation.Status);
        Assert.Empty(evaluation.Reasons);
        Assert.False(evaluation.HasIssues);
        Assert.Equal("运行正常", evaluation.Summary);
    }

    [Fact]
    public void SlightlyHighFrequency_ShouldBeAttention()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(frequency: 50.62d), Thresholds);

        Assert.Equal(OperatingStatus.Attention, evaluation.Status);
        Assert.True(evaluation.IsFrequencyOutOfBand);
        Assert.Contains("频率偏高", evaluation.Summary);
    }

    [Fact]
    public void VeryHighFrequency_ShouldBeCritical()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(frequency: 51.05d), Thresholds);

        Assert.Equal(OperatingStatus.Critical, evaluation.Status);
        Assert.Contains("严重偏高", evaluation.Summary);
    }

    [Fact]
    public void LowVoltage_ShouldBeCriticalBelowTenPercent()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(voltage: 195d), Thresholds);

        Assert.Equal(OperatingStatus.Critical, evaluation.Status);
        Assert.True(evaluation.IsVoltageOutOfBand);
    }

    [Fact]
    public void SlightlyLowVoltage_ShouldBeAttention()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(voltage: 205d), Thresholds);

        Assert.Equal(OperatingStatus.Attention, evaluation.Status);
        Assert.Contains("电压偏低", evaluation.Summary);
    }

    [Fact]
    public void HighCurrent_ShouldBeAttention()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(current: 1400d), Thresholds);

        Assert.Equal(OperatingStatus.Attention, evaluation.Status);
        Assert.True(evaluation.IsCurrentOutOfBand);
    }

    [Fact]
    public void Overload_ShouldBeCritical()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(power: 510d), Thresholds);

        Assert.Equal(OperatingStatus.Critical, evaluation.Status);
        Assert.True(evaluation.IsPowerOutOfBand);
    }

    [Fact]
    public void BadQuality_ShouldDegradeToAttention()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(quality: QualityFlag.Bad), Thresholds);

        Assert.Equal(OperatingStatus.Attention, evaluation.Status);
        Assert.Contains("数据质量", evaluation.Summary);
    }

    [Fact]
    public void MultipleIssues_ShouldPutCriticalReasonsFirst()
    {
        var evaluation = StatusEvaluator.Evaluate(TestData.Reading(voltage: 195d, frequency: 50.6d), Thresholds);

        Assert.Equal(OperatingStatus.Critical, evaluation.Status);
        Assert.True(evaluation.Reasons.Count >= 2);
        Assert.Contains("电压严重偏低", evaluation.Reasons[0]);
    }
}

public sealed class AlarmStateMachineTests
{
    private static readonly PowerThresholds Thresholds = PowerThresholds.Default;

    [Fact]
    public void CrossingLimit_ShouldRaiseAlarm()
    {
        var machine = new AlarmStateMachine();

        var batch = machine.Update(TestData.Reading(frequency: 50.62d), Thresholds, TestData.Now);

        var alarm = Assert.Single(batch.Raised);
        Assert.Equal(AlarmKind.FrequencyHigh, alarm.Kind);
        Assert.Equal(AlarmSeverity.Warning, alarm.Severity);
        Assert.Contains("50.62", alarm.Message);
        Assert.Equal(1, machine.ActiveCount);
    }

    [Fact]
    public void StayingAboveLimit_ShouldNotDuplicateAlarm()
    {
        var machine = new AlarmStateMachine();
        machine.Update(TestData.Reading(frequency: 50.62d), Thresholds, TestData.Now);

        var batch = machine.Update(TestData.Reading(frequency: 50.70d), Thresholds, TestData.Now.AddSeconds(1));

        Assert.Empty(batch.Raised);
        Assert.Single(machine.ActiveAlarms);
    }

    [Fact]
    public void Escalation_ShouldUpgradeSeverity()
    {
        var machine = new AlarmStateMachine();
        machine.Update(TestData.Reading(frequency: 50.62d), Thresholds, TestData.Now);

        var batch = machine.Update(TestData.Reading(frequency: 51.2d), Thresholds, TestData.Now.AddSeconds(1));

        Assert.Empty(batch.Raised);
        var alarm = Assert.Single(batch.Escalated);
        Assert.Equal(AlarmSeverity.Critical, alarm.Severity);
        Assert.True(alarm.IsCritical);
    }

    [Fact]
    public void Hysteresis_ShouldPreventFlappingNearLimit()
    {
        var machine = new AlarmStateMachine();
        machine.Update(TestData.Reading(frequency: 50.62d), Thresholds, TestData.Now);

        var stillActive = machine.Update(TestData.Reading(frequency: 50.46d), Thresholds, TestData.Now.AddSeconds(1));
        Assert.Empty(stillActive.Cleared);
        Assert.Equal(1, machine.ActiveCount);

        var cleared = machine.Update(TestData.Reading(frequency: 50.42d), Thresholds, TestData.Now.AddSeconds(2));
        Assert.Single(cleared.Cleared);
        Assert.Equal(0, machine.ActiveCount);
    }

    [Fact]
    public void Recovery_ShouldClearAlarmAndSetState()
    {
        var machine = new AlarmStateMachine();
        machine.Update(TestData.Reading(frequency: 50.62d), Thresholds, TestData.Now);
        var alarm = machine.ActiveAlarms.Single();

        var batch = machine.Update(TestData.Reading(frequency: 50d), Thresholds, TestData.Now.AddSeconds(5));

        Assert.Single(batch.Cleared);
        Assert.Equal(AlarmState.Cleared, alarm.State);
        Assert.NotNull(alarm.ClearedAt);
        Assert.False(alarm.IsActive);
    }

    [Fact]
    public void Acknowledge_ShouldMarkAlarmAsAcknowledged()
    {
        var machine = new AlarmStateMachine();
        machine.Update(TestData.Reading(power: 440d), Thresholds, TestData.Now);
        var alarm = machine.ActiveAlarms.Single();

        Assert.True(machine.Acknowledge(alarm.Id, TestData.Now.AddSeconds(2)));
        Assert.Equal(AlarmState.Acknowledged, alarm.State);
        Assert.False(alarm.CanAcknowledge);
        Assert.NotNull(alarm.AcknowledgedAt);
    }

    [Fact]
    public void LowFrequencyAndOverload_ShouldRaiseTwoAlarms()
    {
        var machine = new AlarmStateMachine();

        var batch = machine.Update(
            TestData.Reading(frequency: 49.3d, power: 520d, current: 1600d),
            Thresholds,
            TestData.Now);

        Assert.Equal(3, batch.Raised.Count);
        Assert.Equal(3, machine.ActiveCount);
        Assert.True(machine.HasCritical);
    }

    [Fact]
    public void Reset_ShouldClearAllActiveAlarms()
    {
        var machine = new AlarmStateMachine();
        machine.Update(TestData.Reading(frequency: 49.3d, power: 520d), Thresholds, TestData.Now);

        var cleared = machine.Reset(TestData.Now.AddSeconds(1));

        Assert.Equal(2, cleared.Count);
        Assert.Equal(0, machine.ActiveCount);
        Assert.All(cleared, alarm => Assert.Equal(AlarmState.Cleared, alarm.State));
    }
}

public sealed class ReadingHistoryTests
{
    [Fact]
    public void Add_ShouldTrimOldestWhenCapacityExceeded()
    {
        var history = new ReadingHistory(3);
        for (var i = 0; i < 5; i++)
        {
            history.Add(TestData.Reading(timestamp: TestData.Now.AddSeconds(i)));
        }

        var snapshot = history.Snapshot();

        Assert.Equal(3, snapshot.Count);
        Assert.Equal(TestData.Now.AddSeconds(2), snapshot[0].Timestamp);
        Assert.Equal(TestData.Now.AddSeconds(4), snapshot[^1].Timestamp);
    }

    [Fact]
    public void Snapshot_ShouldBeIndependentFromSourceObject()
    {
        var history = new ReadingHistory(5);
        var reading = TestData.Reading();
        history.Add(reading);

        reading.VoltageKv = 999;

        Assert.Equal(220d, history.Snapshot()[0].VoltageKv);
    }

    [Fact]
    public void Recent_ShouldReturnLastItems()
    {
        var history = new ReadingHistory(10);
        for (var i = 0; i < 6; i++)
        {
            history.Add(TestData.Reading(timestamp: TestData.Now.AddSeconds(i)));
        }

        var recent = history.Recent(2);

        Assert.Equal(2, recent.Count);
        Assert.Equal(TestData.Now.AddSeconds(5), recent[^1].Timestamp);
    }

    [Fact]
    public void Statistics_ShouldRespectTimeWindow()
    {
        var history = new ReadingHistory(10);
        history.Add(TestData.Reading(voltage: 200d, timestamp: TestData.Now.AddMinutes(-30)));
        history.Add(TestData.Reading(voltage: 210d, timestamp: TestData.Now.AddMinutes(-2)));
        history.Add(TestData.Reading(voltage: 220d, timestamp: TestData.Now));

        var stats = history.Statistics(TimeSpan.FromMinutes(5), TestData.Now);

        Assert.Equal(2, stats.Count);
        Assert.Equal(210d, stats.MinVoltageKv);
        Assert.Equal(220d, stats.MaxVoltageKv);
        Assert.Equal(215d, stats.AverageVoltageKv, 3);
    }

    [Fact]
    public void Clear_ShouldRemoveAllItems()
    {
        var history = new ReadingHistory(5);
        history.Add(TestData.Reading());

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.Empty(history.Snapshot());
    }

    [Fact]
    public void InvalidCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReadingHistory(0));
    }
}
