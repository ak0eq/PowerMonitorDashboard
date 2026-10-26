using System.Globalization;
using PowerMonitorDashboard.Core.Calculations;
using PowerMonitorDashboard.Core.Domain;

namespace PowerMonitorDashboard.Core.Validation;

/// <summary>校验结果级别。</summary>
public enum ValidationSeverity
{
    /// <summary>错误：阻止提交。</summary>
    Error = 0,

    /// <summary>警告：允许提交，但提醒值班员复核。</summary>
    Warning = 1
}

/// <summary>单条校验结果。</summary>
/// <param name="Field">字段标识（与视图模型属性同名）。</param>
/// <param name="Message">中文提示。</param>
/// <param name="Severity">级别。</param>
public sealed record ValidationIssue(string Field, string Message, ValidationSeverity Severity = ValidationSeverity.Error)
{
    /// <summary>是否为阻止提交的错误。</summary>
    public bool IsError => Severity == ValidationSeverity.Error;
}

/// <summary>数据录入表单的原始输入。保留字符串形式，才能对“小数位”“空值”等格式问题给出精确提示。</summary>
public sealed record ReadingDraft(
    string? VoltageKv = null,
    string? CurrentA = null,
    string? PowerMw = null,
    string? FrequencyHz = null,
    string? Timestamp = null);

/// <summary>整表校验结果。</summary>
public sealed record ReadingValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    /// <summary>空结果。</summary>
    public static ReadingValidationResult Valid { get; } = new([]);

    /// <summary>是否存在阻止提交的错误。</summary>
    public bool IsValid => Issues.All(static i => !i.IsError);

    /// <summary>是否存在提醒性警告。</summary>
    public bool HasWarnings => Issues.Any(static i => i.Severity == ValidationSeverity.Warning);

    /// <summary>第一条错误。</summary>
    public ValidationIssue? FirstError => Issues.FirstOrDefault(static i => i.IsError);

    /// <summary>供界面直接展示的多行文本。</summary>
    public string Summary => Issues.Count == 0
        ? string.Empty
        : string.Join(Environment.NewLine, Issues.Select(static i => (i.IsError ? "✖ " : "⚠ ") + i.Message));
}

/// <summary>
/// 电力数据录入校验器。所有规则都在 Core 层以纯函数实现，
/// WPF 的 <see cref="System.Windows.Controls.ValidationRule"/> 只是它的适配器，
/// 因此同一套规则既能用于实时红框提示，也能用于保存前的整体校验，还能被单元测试直接覆盖。
/// </summary>
public static class ReadingValidator
{
    /// <summary>电压下限，kV。</summary>
    public const double VoltageMinKv = 0d;

    /// <summary>电压上限，kV。</summary>
    public const double VoltageMaxKv = 500d;

    /// <summary>电流下限，A。</summary>
    public const double CurrentMinA = 0d;

    /// <summary>电流上限，A。</summary>
    public const double CurrentMaxA = 5000d;

    /// <summary>频率下限，Hz。</summary>
    public const double FrequencyMinHz = 47d;

    /// <summary>频率上限，Hz。</summary>
    public const double FrequencyMaxHz = 52d;

    /// <summary>功率上限，MW。</summary>
    public const double PowerMaxMw = 1000d;

    /// <summary>校验电压：0～500 kV，最多一位小数。</summary>
    public static ValidationIssue? ValidateVoltage(string? text)
    {
        var raw = Normalize(text);
        if (string.IsNullOrEmpty(raw))
        {
            return new ValidationIssue(nameof(ReadingDraft.VoltageKv), "电压不能为空。");
        }

        if (!TryParse(raw, out var value))
        {
            return new ValidationIssue(nameof(ReadingDraft.VoltageKv), "电压必须是有效数字。");
        }

        if (CountDecimals(raw) > 1)
        {
            return new ValidationIssue(nameof(ReadingDraft.VoltageKv), "电压最多保留一位小数。");
        }

        if (value < VoltageMinKv || value > VoltageMaxKv)
        {
            return new ValidationIssue(nameof(ReadingDraft.VoltageKv), "电压应在 0～500 kV 之间。");
        }

        return null;
    }

    /// <summary>校验电流：0～5000 A，必填。</summary>
    public static ValidationIssue? ValidateCurrent(string? text)
    {
        var raw = Normalize(text);
        if (string.IsNullOrEmpty(raw))
        {
            return new ValidationIssue(nameof(ReadingDraft.CurrentA), "电流不能为空。");
        }

        if (!TryParse(raw, out var value))
        {
            return new ValidationIssue(nameof(ReadingDraft.CurrentA), "电流必须是有效数字。");
        }

        if (value < CurrentMinA || value > CurrentMaxA)
        {
            return new ValidationIssue(nameof(ReadingDraft.CurrentA), "电流应在 0～5000 A 之间。");
        }

        return null;
    }

    /// <summary>校验频率：47.0～52.0 Hz，必填。</summary>
    public static ValidationIssue? ValidateFrequency(string? text)
    {
        var raw = Normalize(text);
        if (string.IsNullOrEmpty(raw))
        {
            return new ValidationIssue(nameof(ReadingDraft.FrequencyHz), "频率不能为空。");
        }

        if (!TryParse(raw, out var value))
        {
            return new ValidationIssue(nameof(ReadingDraft.FrequencyHz), "频率必须是有效数字。");
        }

        if (value < FrequencyMinHz || value > FrequencyMaxHz)
        {
            return new ValidationIssue(nameof(ReadingDraft.FrequencyHz), "频率应在 47.0～52.0 Hz 之间。");
        }

        return null;
    }

    /// <summary>校验功率：可留空（由 U、I、cosφ 自动计算），填写时需在 0～1000 MW。</summary>
    public static ValidationIssue? ValidatePower(string? text)
    {
        var raw = Normalize(text);
        if (string.IsNullOrEmpty(raw))
        {
            // 留空表示自动计算，不是错误。
            return null;
        }

        if (!TryParse(raw, out var value))
        {
            return new ValidationIssue(nameof(ReadingDraft.PowerMw), "功率必须是有效数字，或留空自动计算。");
        }

        if (value < 0d || value > PowerMaxMw)
        {
            return new ValidationIssue(nameof(ReadingDraft.PowerMw), "功率应在 0～1000 MW 之间。");
        }

        return null;
    }

    /// <summary>校验时间戳：必填、格式正确，且不得晚于当前时间。</summary>
    public static ValidationIssue? ValidateTimestamp(string? text, DateTimeOffset now)
    {
        var raw = Normalize(text);
        if (string.IsNullOrEmpty(raw))
        {
            return new ValidationIssue(nameof(ReadingDraft.Timestamp), "请输入采样时间。");
        }

        if (!TryParseTimestamp(raw, out var timestamp))
        {
            return new ValidationIssue(nameof(ReadingDraft.Timestamp), "时间格式应为 yyyy-MM-dd HH:mm:ss。");
        }

        return timestamp > now
            ? new ValidationIssue(nameof(ReadingDraft.Timestamp), "时间戳不能晚于当前时间。")
            : null;
    }

    /// <summary>执行整表校验（包含跨字段一致性提示）。</summary>
    public static ReadingValidationResult Validate(ReadingDraft draft, DateTimeOffset now, double assumedPowerFactor = 0.95d)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var issues = new List<ValidationIssue>(6);

        AddIf(issues, ValidateVoltage(draft.VoltageKv));
        AddIf(issues, ValidateCurrent(draft.CurrentA));
        AddIf(issues, ValidateFrequency(draft.FrequencyHz));
        AddIf(issues, ValidatePower(draft.PowerMw));
        AddIf(issues, ValidateTimestamp(draft.Timestamp, now));

        var powerIssue = CheckConsistency(draft.VoltageKv, draft.CurrentA, draft.PowerMw, assumedPowerFactor);
        if (powerIssue is not null)
        {
            issues.Add(powerIssue);
        }

        return new ReadingValidationResult(issues);
    }

    /// <summary>
    /// 尝试把表单输入转换为领域对象。
    /// 功率为空时按 P = √3·U·I·cosφ 自动计算；时间戳按本地时区解释。
    /// </summary>
    public static bool TryBuildReading(
        ReadingDraft draft,
        DateTimeOffset now,
        double assumedPowerFactor,
        out PowerReading? reading,
        out ReadingValidationResult result)
    {
        result = Validate(draft, now, assumedPowerFactor);
        reading = null;
        if (!result.IsValid || !TryParseTimestamp(Normalize(draft.Timestamp)!, out var timestamp))
        {
            return false;
        }

        var voltage = ParseRequired(draft.VoltageKv);
        var current = ParseRequired(draft.CurrentA);
        var frequency = ParseRequired(draft.FrequencyHz);
        var power = string.IsNullOrWhiteSpace(draft.PowerMw)
            ? PowerMath.PowerFromCurrent(voltage, current, assumedPowerFactor)
            : ParseRequired(draft.PowerMw);

        reading = new PowerReading(
            timestamp,
            Math.Round(voltage, 1, MidpointRounding.AwayFromZero),
            Math.Round(current, 0, MidpointRounding.AwayFromZero),
            Math.Round(power, 2, MidpointRounding.AwayFromZero),
            Math.Round(frequency, 2, MidpointRounding.AwayFromZero),
            ReadingSource.Manual,
            QualityFlag.Good);

        return true;
    }

    /// <summary>跨字段一致性检查：功率应约等于 √3·U·I·cosφ。</summary>
    private static ValidationIssue? CheckConsistency(string? voltageText, string? currentText, string? powerText, double powerFactor)
    {
        var rawPower = Normalize(powerText);
        if (string.IsNullOrEmpty(rawPower))
        {
            return null;
        }

        if (!TryParse(Normalize(voltageText), out var voltage)
            || !TryParse(Normalize(currentText), out var current)
            || !TryParse(rawPower, out var power))
        {
            return null;
        }

        if (voltage <= 0 || current <= 0)
        {
            return null;
        }

        var expected = PowerMath.PowerFromCurrent(voltage, current, powerFactor);
        var deviation = Math.Abs(power - expected);
        var tolerance = Math.Max(5d, expected * 0.10d);
        if (deviation <= tolerance)
        {
            return null;
        }

        return new ValidationIssue(
            nameof(ReadingDraft.PowerMw),
            string.Create(CultureInfo.InvariantCulture, $"功率与电压、电流不匹配：按 cosφ={powerFactor:F2} 估算约 {expected:F1} MW，请复核。"),
            ValidationSeverity.Warning);
    }

    private static void AddIf(List<ValidationIssue> issues, ValidationIssue? issue)
    {
        if (issue is not null)
        {
            issues.Add(issue);
        }
    }

    private static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim()
            .Replace('．', '.')
            .Replace('，', '.');
    }

    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/M/d H:mm:ss",
        "yyyy/M/d H:mm"
    ];

    private static bool TryParseTimestamp(string text, out DateTimeOffset timestamp)
    {
        if (DateTime.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            timestamp = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
            return true;
        }

        timestamp = default;
        return false;
    }

    private static bool TryParse(string? text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static double ParseRequired(string? text)
        => double.Parse(Normalize(text)!, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int CountDecimals(string text)
    {
        var index = text.IndexOf('.', StringComparison.Ordinal);
        if (index < 0)
        {
            return 0;
        }

        var count = 0;
        for (var i = index + 1; i < text.Length && char.IsDigit(text[i]); i++)
        {
            count++;
        }

        return count;
    }
}
