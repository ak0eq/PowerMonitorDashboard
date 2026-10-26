using System.Threading;
using PowerMonitorDashboard.Core.Abstractions;

namespace PowerMonitorDashboard.App.Tests;

/// <summary>在 STA 线程上执行测试体，供依赖 WPF 集合视图的用例使用。</summary>
internal static class StaRunner
{
    public static void Run(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw new InvalidOperationException("STA 测试线程抛出异常。", captured);
        }
    }
}

/// <summary>可控时钟。</summary>
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now) => Now = now;

    public DateTimeOffset Now { get; set; }
}

/// <summary>文件对话框假实现。</summary>
internal sealed class FakeFileDialogService : Infrastructure.IFileDialogService
{
    public string? SavePath { get; set; }

    public string? OpenPath { get; set; }

    public string? PickSaveFile(string title, string filter, string defaultFileName) => SavePath;

    public string? PickOpenFile(string title, string filter, string initialDirectory) => OpenPath;
}

/// <summary>消息框假实现。</summary>
internal sealed class FakeUserDialogService : Infrastructure.IUserDialogService
{
    public bool ConfirmResult { get; set; } = true;

    public bool Confirm(string title, string message) => ConfirmResult;

    public void ShowError(string title, string message)
    {
    }

    public void ShowInfo(string title, string message)
    {
    }
}
