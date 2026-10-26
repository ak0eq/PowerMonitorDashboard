using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PowerMonitorDashboard.Core.Domain;

/// <summary>
/// 轻量级 <see cref="INotifyPropertyChanged"/> 基类。
/// 领域层不依赖任何 UI 框架，因此这里只使用 <see cref="System.ComponentModel"/> 中定义的标准接口。
/// </summary>
public abstract class ObservableEntity : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 设置字段值；值发生变化时触发 <see cref="PropertyChanged"/> 并返回 <c>true</c>。
    /// </summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>触发属性变更通知。</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>一次性触发多个属性的变更通知，用于计算属性联动。</summary>
    protected void OnPropertiesChanged(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            OnPropertyChanged(name);
        }
    }
}
