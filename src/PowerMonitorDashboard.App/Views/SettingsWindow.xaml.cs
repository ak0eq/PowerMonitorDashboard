using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PowerMonitorDashboard.Core.Configuration;
using PowerMonitorDashboard.ViewModels;

namespace PowerMonitorDashboard.Views;

/// <summary>系统设置对话框。</summary>
public partial class SettingsWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private readonly SettingsViewModel _viewModel;

    /// <summary>创建设置对话框。</summary>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SourceInitialized += (_, _) => ApplyDarkTitleBar();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }

    /// <summary>用户确认后的配置对象。</summary>
    public MonitorSettings? Result { get; private set; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void ApplyDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryBuild(out var settings) || settings is null)
        {
            return;
        }

        Result = settings;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
