using PowerMonitorDashboard.Core.Calculations;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Simulation;

namespace PowerMonitorDashboard.Core.Tests;

public sealed class PowerMathTests
{
    [Fact]
    public void ApparentPower_ShouldUseThreePhaseFormula()
    {
        var apparent = PowerMath.ApparentPowerMva(220d, 1000d);

        Assert.Equal(Math.Sqrt(3) * 220d, apparent, 6);
    }

    [Fact]
    public void CurrentFromPower_ShouldRoundTripThroughPowerFromCurrent()
    {
        const double power = 362d;
        const double voltage = 220d;
        const double powerFactor = 0.95d;

        var current = PowerMath.CurrentFromPower(power, voltage, powerFactor);
        var restored = PowerMath.PowerFromCurrent(voltage, current, powerFactor);

        Assert.Equal(1000d, current, 1);
        Assert.Equal(power, restored, 6);
    }

    [Fact]
    public void CurrentFromPower_ShouldReturnZeroForZeroVoltage()
    {
        Assert.Equal(0d, PowerMath.CurrentFromPower(100d, 0d, 0.95d));
    }

    [Theory]
    [InlineData(362d, 220d, 1000d, 0.95d)]
    [InlineData(0d, 220d, 0d, 0d)]
    [InlineData(500d, 220d, 100d, 1d)]
    public void PowerFactor_ShouldStayWithinPhysicalRange(double power, double voltage, double current, double expectedMinimum)
    {
        var factor = PowerMath.PowerFactor(power, voltage, current);

        Assert.InRange(factor, expectedMinimum, 1d);
    }

    [Fact]
    public void Energy_ShouldIntegrateOverTime()
    {
        Assert.Equal(50d, PowerMath.EnergyMwh(100d, TimeSpan.FromMinutes(30)));
    }
}

public sealed class LoadProfileTests
{
    [Fact]
    public void Factor_ShouldReachEveningPeak()
    {
        var time = new DateTimeOffset(new DateTime(2026, 10, 26, 21, 0, 0, DateTimeKind.Local));

        Assert.Equal(0.97d, LoadProfile.GetFactor(time), 3);
    }

    [Fact]
    public void Factor_ShouldBeLowestAtNight()
    {
        var night = new DateTimeOffset(new DateTime(2026, 10, 26, 4, 0, 0, DateTimeKind.Local));
        var morning = new DateTimeOffset(new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Local));

        Assert.True(LoadProfile.GetFactor(night) < LoadProfile.GetFactor(morning));
    }

    [Fact]
    public void Factor_ShouldStayInNormalizedRangeAcrossDay()
    {
        for (var hour = 0; hour < 24; hour++)
        {
            var time = new DateTimeOffset(new DateTime(2026, 10, 26, hour, 0, 0, DateTimeKind.Local));
            var factor = LoadProfile.GetFactor(time);
            Assert.InRange(factor, 0.45d, 1.0d);
        }
    }
}

public sealed class TelemetrySimulatorTests
{
    [Fact]
    public void SameSeed_ShouldProduceIdenticalSequence()
    {
        var start = TestData.Now;
        var first = new TelemetrySimulator(SimulationOptions.Default, seed: 20261026);
        var second = new TelemetrySimulator(SimulationOptions.Default, seed: 20261026);

        for (var i = 0; i < 120; i++)
        {
            var a = first.Next(start.AddSeconds(i));
            var b = second.Next(start.AddSeconds(i));
            Assert.Equal(a.VoltageKv, b.VoltageKv);
            Assert.Equal(a.CurrentA, b.CurrentA);
            Assert.Equal(a.PowerMw, b.PowerMw);
            Assert.Equal(a.FrequencyHz, b.FrequencyHz);
        }
    }

    [Fact]
    public void Readings_ShouldBePhysicallyConsistent()
    {
        var simulator = new TelemetrySimulator(SimulationOptions.Default, seed: 42);

        for (var i = 0; i < 300; i++)
        {
            var reading = simulator.Next(TestData.Now.AddSeconds(i));

            Assert.InRange(reading.VoltageKv, 150d, 260d);
            Assert.InRange(reading.FrequencyHz, 48.4d, 51.6d);
            Assert.InRange(reading.CurrentA, 0d, 5000d);
            Assert.True(reading.PowerMw > 0d);
            Assert.Equal(reading.VoltageKv, Math.Round(reading.VoltageKv, 1), 6);

            // 视在功率与功率因数应落在合理区间：功率由 U、I、cosφ 推导，三个字段天然自洽。
            Assert.InRange(reading.PowerFactor, 0.88d, 1d);
            var expectedPower = PowerMath.PowerFromCurrent(reading.VoltageKv, reading.CurrentA, reading.PowerFactor);
            Assert.Equal(expectedPower, reading.PowerMw, 2);
        }
    }

    [Fact]
    public void InjectedDisturbance_ShouldProduceOutOfBandFrequency()
    {
        var simulator = new TelemetrySimulator(SimulationOptions.Default, seed: 7);
        simulator.InjectDisturbance(DisturbanceKind.FrequencySurge, magnitude: 1.5d);

        var maximum = 0d;
        for (var i = 0; i < 10; i++)
        {
            maximum = Math.Max(maximum, simulator.Next(TestData.Now.AddSeconds(i)).FrequencyHz);
        }

        Assert.True(maximum > 50.5d, $"注入扰动后频率应越限，实际最大值 {maximum:F2} Hz");
    }

    [Fact]
    public void Readings_ShouldFollowDailyLoadCurve()
    {
        var options = SimulationOptions.Default with { PeakLoadMw = 480d, LoadNoiseRatio = 0.001d };
        var simulator = new TelemetrySimulator(options, seed: 99);
        var evening = new DateTimeOffset(new DateTime(2026, 10, 26, 20, 30, 0, DateTimeKind.Local));

        PowerReading? last = null;
        for (var i = 0; i < 240; i++)
        {
            last = simulator.Next(evening.AddSeconds(i));
        }

        var expected = 480d * LoadProfile.GetFactor(evening);
        Assert.NotNull(last);
        Assert.InRange(last!.PowerMw, expected * 0.75d, expected * 1.2d);
    }
}
