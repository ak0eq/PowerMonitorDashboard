namespace PowerMonitorDashboard.Core.Simulation;

/// <summary>
/// 典型日负荷曲线。数值为标幺值（0~1），峰谷差接近 50%，
/// 既符合电力系统“夜间低谷、早晚双峰”的规律，也能让大屏在较短时间内展示负荷的明显变化。
/// </summary>
public static class LoadProfile
{
    // (小时, 标幺负荷) 控制点，采用余弦插值获得平滑曲线。
    private static readonly (double Hour, double Factor)[] ControlPoints =
    [
        (0.0, 0.55),
        (4.0, 0.48),
        (6.0, 0.55),
        (8.0, 0.72),
        (10.0, 0.86),
        (12.0, 0.80),
        (14.0, 0.74),
        (17.0, 0.83),
        (19.0, 0.94),
        (21.0, 0.97),
        (22.5, 0.80),
        (24.0, 0.55)
    ];

    /// <summary>获取指定时刻的负荷标幺值。</summary>
    public static double GetFactor(DateTimeOffset time)
    {
        var local = time.ToLocalTime();
        var hour = local.Hour + local.Minute / 60d + local.Second / 3600d;

        for (var i = 0; i < ControlPoints.Length - 1; i++)
        {
            var (h0, f0) = ControlPoints[i];
            var (h1, f1) = ControlPoints[i + 1];
            if (hour < h0 || hour > h1)
            {
                continue;
            }

            var t = (hour - h0) / (h1 - h0);
            var smooth = (1d - Math.Cos(t * Math.PI)) / 2d;
            return f0 + (f1 - f0) * smooth;
        }

        return ControlPoints[^1].Factor;
    }

    /// <summary>获取指定时刻的负荷标幺值（按 <see cref="DateTime"/> 重载）。</summary>
    public static double GetFactor(DateTime time) => GetFactor(new DateTimeOffset(time));
}
