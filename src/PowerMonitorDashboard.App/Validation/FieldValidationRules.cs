using System.Globalization;
using System.Windows.Controls;
using PowerMonitorDashboard.Core.Validation;

namespace PowerMonitorDashboard.Validation;

/// <summary>
/// WPF 校验规则基类：把 Core 层的纯函数校验器适配为 <see cref="ValidationRule"/>，
/// 从而在输入过程中即时显示红色边框与错误提示，同时保持业务规则只有一份实现。
/// </summary>
public abstract class FieldValidationRule : ValidationRule
{
    /// <summary>执行校验。</summary>
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var issue = ValidateValue(value as string);
        return issue is null ? ValidationResult.ValidResult : new ValidationResult(false, issue.Message);
    }

    /// <summary>由派生类实现的具体校验。</summary>
    protected abstract ValidationIssue? ValidateValue(string? text);
}

/// <summary>电压校验：0～500 kV，最多一位小数。</summary>
public sealed class VoltageValidationRule : FieldValidationRule
{
    /// <inheritdoc />
    protected override ValidationIssue? ValidateValue(string? text) => ReadingValidator.ValidateVoltage(text);
}

/// <summary>电流校验：0～5000 A，必填。</summary>
public sealed class CurrentValidationRule : FieldValidationRule
{
    /// <inheritdoc />
    protected override ValidationIssue? ValidateValue(string? text) => ReadingValidator.ValidateCurrent(text);
}

/// <summary>频率校验：47.0～52.0 Hz。</summary>
public sealed class FrequencyValidationRule : FieldValidationRule
{
    /// <inheritdoc />
    protected override ValidationIssue? ValidateValue(string? text) => ReadingValidator.ValidateFrequency(text);
}

/// <summary>功率校验：可留空，填写时为 0～1000 MW。</summary>
public sealed class PowerValidationRule : FieldValidationRule
{
    /// <inheritdoc />
    protected override ValidationIssue? ValidateValue(string? text) => ReadingValidator.ValidatePower(text);
}

/// <summary>时间戳校验：格式正确且不得晚于当前时间。</summary>
public sealed class TimestampValidationRule : FieldValidationRule
{
    /// <inheritdoc />
    protected override ValidationIssue? ValidateValue(string? text)
        => ReadingValidator.ValidateTimestamp(text, DateTimeOffset.Now);
}
