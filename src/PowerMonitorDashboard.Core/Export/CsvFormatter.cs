using System.Globalization;
using System.Text;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Monitoring;

namespace PowerMonitorDashboard.Core.Export;

/// <summary>
/// CSV 导出格式器。生成带中文表头的 UTF-8 文本，字段中出现逗号、引号或换行时按 RFC 4180 转义。
/// 这里只负责生成字符串，真正的文件写入由宿主层完成，方便单元测试。
/// </summary>
public static class CsvFormatter
{
    /// <summary>导出历史数据。</summary>
    public static string ForReadings(IEnumerable<PowerReading> readings, PowerThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(thresholds);

        var builder = new StringBuilder();
        builder.AppendLine("序号,采样时间,电压(kV),电流(A),有功功率(MW),频率(Hz),视在功率(MVA),功率因数,负荷等级,运行状态,数据来源,数据质量");

        var index = 0;
        foreach (var reading in readings)
        {
            index++;
            var evaluation = StatusEvaluator.Evaluate(reading, thresholds);
            var fields = new[]
            {
                index.ToString(CultureInfo.InvariantCulture),
                reading.TimestampText,
                reading.VoltageKv.ToString("F1", CultureInfo.InvariantCulture),
                reading.CurrentA.ToString("F0", CultureInfo.InvariantCulture),
                reading.PowerMw.ToString("F2", CultureInfo.InvariantCulture),
                reading.FrequencyHz.ToString("F2", CultureInfo.InvariantCulture),
                reading.ApparentPowerMva.ToString("F2", CultureInfo.InvariantCulture),
                reading.PowerFactor.ToString("F3", CultureInfo.InvariantCulture),
                BandText(thresholds.ClassifyPower(reading.PowerMw)),
                StatusText(evaluation.Status),
                reading.SourceText,
                reading.QualityText
            };

            builder.AppendLine(string.Join(',', fields.Select(Escape)));
        }

        return builder.ToString();
    }

    /// <summary>导出告警记录。</summary>
    public static string ForAlarms(IEnumerable<Alarm> alarms)
    {
        ArgumentNullException.ThrowIfNull(alarms);

        var builder = new StringBuilder();
        builder.AppendLine("序号,产生时间,级别,类型,描述,状态,确认时间,清除时间");

        var index = 0;
        foreach (var alarm in alarms.OrderByDescending(static a => a.RaisedAt))
        {
            index++;
            var fields = new[]
            {
                index.ToString(CultureInfo.InvariantCulture),
                alarm.RaisedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                alarm.SeverityText,
                alarm.Kind.ToString(),
                alarm.Message,
                alarm.StateText,
                alarm.AcknowledgedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty,
                alarm.ClearedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty
            };

            builder.AppendLine(string.Join(',', fields.Select(Escape)));
        }

        return builder.ToString();
    }

    private static string StatusText(OperatingStatus status) => status switch
    {
        OperatingStatus.Critical => "严重",
        OperatingStatus.Attention => "告警",
        _ => "正常"
    };

    private static string BandText(PowerBand band) => band switch
    {
        PowerBand.High => "高",
        PowerBand.Medium => "中",
        _ => "低"
    };

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
