namespace PowerMonitorDashboard.Core.Calculations;

/// <summary>
/// 三相交流电的基本换算。所有公式均以“线电压 kV / 线电流 A / 三相有功 MW”为单位。
/// </summary>
/// <remarks>
/// S(MVA) = √3 · U(kV) · I(A) / 1000
/// P(MW)  = S(MVA) · cosφ
/// 因此 I(A) = P(MW) · 1000 / (√3 · U(kV) · cosφ)。
/// 仿真采集器与人工录入的跨字段校验共用这里的实现，保证全系统只有一套公式。
/// </remarks>
public static class PowerMath
{
    /// <summary>√3 常量。</summary>
    public const double Sqrt3 = 1.7320508075688772;

    /// <summary>计算视在功率 S，单位 MVA。</summary>
    public static double ApparentPowerMva(double voltageKv, double currentA)
        => Sqrt3 * voltageKv * currentA / 1000d;

    /// <summary>由功率、电压、功率因数反算电流，单位 A。</summary>
    public static double CurrentFromPower(double powerMw, double voltageKv, double powerFactor)
    {
        var denominator = Sqrt3 * voltageKv * powerFactor;
        return denominator <= 1e-9 ? 0d : powerMw * 1000d / denominator;
    }

    /// <summary>由电压、电流、功率因数计算有功功率，单位 MW。</summary>
    public static double PowerFromCurrent(double voltageKv, double currentA, double powerFactor)
        => ApparentPowerMva(voltageKv, currentA) * powerFactor;

    /// <summary>计算功率因数 cosφ，结果被限制在 0~1。</summary>
    public static double PowerFactor(double powerMw, double voltageKv, double currentA)
    {
        var apparent = ApparentPowerMva(voltageKv, currentA);
        if (apparent <= 1e-9)
        {
            return 0d;
        }

        return Math.Clamp(powerMw / apparent, 0d, 1d);
    }

    /// <summary>按梯形法累计电量，单位 MWh。</summary>
    public static double EnergyMwh(double powerMw, TimeSpan duration)
        => powerMw * duration.TotalHours;

    /// <summary>把 <paramref name="value"/> 四舍五入到指定小数位。</summary>
    public static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
}
