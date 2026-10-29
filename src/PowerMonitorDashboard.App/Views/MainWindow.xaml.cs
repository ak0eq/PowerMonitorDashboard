using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PowerMonitorDashboard.ViewModels;
using WpfValidation = System.Windows.Controls.Validation;

namespace PowerMonitorDashboard.Views;

/// <summary>
/// 主窗口。视图层只负责“只能由视图完成”的工作：
/// MediaElement 控制、动画编排、全屏切换、窗口级快捷键与表格自动滚动。
/// 业务状态全部由 <see cref="MainViewModel"/> 持有。
/// </summary>
public partial class MainWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly DispatcherTimer _positionTimer;
    private MainViewModel? _viewModel;
    private Storyboard? _backgroundStoryboard;
    private bool _isFullScreen;
    private bool _isSeeking;
    private WindowStyle _restoreStyle = WindowStyle.SingleBorderWindow;
    private WindowState _restoreState = WindowState.Maximized;

    /// <summary>创建主窗口。</summary>
    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => ApplyDarkTitleBar();
        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
        PreviewKeyDown += OnPreviewKeyDown;

        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += OnPositionTick;
    }

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

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.SettingsRequested += OnSettingsRequested;
        _viewModel.FullScreenRequested += OnFullScreenRequested;
        _viewModel.ExitRequested += OnExitRequested;
        _viewModel.Video.PlaybackRequested += OnPlaybackRequested;
        _viewModel.Video.SeekRequested += OnSeekRequested;
        _viewModel.Video.PropertyChanged += OnVideoPropertyChanged;
        _viewModel.History.RowsAdded += OnHistoryRowsAdded;

        LoadMedia();
        _positionTimer.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _positionTimer.Stop();
        _backgroundStoryboard?.Stop(this);
        try
        {
            Media.Close();
        }
        catch (InvalidOperationException)
        {
            // 关闭阶段忽略媒体关闭异常。
        }

        if (_viewModel is null)
        {
            return;
        }

        _viewModel.SettingsRequested -= OnSettingsRequested;
        _viewModel.FullScreenRequested -= OnFullScreenRequested;
        _viewModel.ExitRequested -= OnExitRequested;
        _viewModel.Video.PlaybackRequested -= OnPlaybackRequested;
        _viewModel.Video.SeekRequested -= OnSeekRequested;
        _viewModel.Video.PropertyChanged -= OnVideoPropertyChanged;
        _viewModel.History.RowsAdded -= OnHistoryRowsAdded;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullScreen)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            var viewModel = _viewModel.CreateSettingsViewModel();
            var dialog = new SettingsWindow(viewModel)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.Result is not null)
            {
                _viewModel.ApplySettings(dialog.Result);
            }
        }
        catch (Exception ex)
        {
            _viewModel.ReportError("打开设置窗口失败。", ex);
            MessageBox.Show("打开设置窗口失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnFullScreenRequested(object? sender, EventArgs e) => ToggleFullScreen();

    private void OnExitRequested(object? sender, EventArgs e) => Close();

    private void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _restoreStyle = WindowStyle;
            _restoreState = WindowState;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            Left = 0d;
            Top = 0d;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            _isFullScreen = true;
        }
        else
        {
            Topmost = false;
            WindowStyle = _restoreStyle;
            ResizeMode = ResizeMode.CanResize;
            WindowState = _restoreState;
            _isFullScreen = false;
        }
    }

    // ============================================================
    // 视频控制
    // ============================================================
    private void LoadMedia()
    {
        if (_viewModel is null)
        {
            return;
        }

        var path = _viewModel.Video.SourcePath;
        try
        {
            Media.Close();
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                _viewModel.Video.ReportFailed($"未找到视频文件：{path}。请将教学视频放入 Assets/video 目录，或在设置中指定其他文件。");
                return;
            }

            _viewModel.Video.ReportBuffering(true);
            Media.Source = new Uri(path);
            Media.Volume = _viewModel.Video.Volume;
            Media.Play();
        }
        catch (Exception ex)
        {
            _viewModel.Video.ReportFailed("视频加载失败：" + ex.Message, ex);
        }
    }

    private void OnVideoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoViewModel.SourcePath))
        {
            LoadMedia();
        }
    }

    private void OnPlaybackRequested(object? sender, VideoPlaybackAction action)
    {
        if (_viewModel is null)
        {
            return;
        }

        switch (action)
        {
            case VideoPlaybackAction.Play:
                if (Media.Source is null)
                {
                    LoadMedia();
                    return;
                }

                // 播放结束后再次点击“播放”时从头开始，而不是停留在最后一帧。
                if (Media.NaturalDuration.HasTimeSpan
                    && Media.Position >= Media.NaturalDuration.TimeSpan - TimeSpan.FromMilliseconds(300))
                {
                    Media.Position = TimeSpan.Zero;
                }

                Media.Play();
                _viewModel.Video.SetPlaying(true);
                StartBackgroundAnimation();
                break;

            case VideoPlaybackAction.Pause:
                Media.Pause();
                _viewModel.Video.SetPlaying(false);
                PauseBackgroundAnimation();
                break;

            case VideoPlaybackAction.Stop:
                Media.Stop();
                Media.Position = TimeSpan.Zero;
                _viewModel.Video.SetPlaying(false);
                StopBackgroundAnimation();
                break;

            case VideoPlaybackAction.Reload:
                LoadMedia();
                break;
        }
    }

    private void OnSeekRequested(object? sender, double seconds)
    {
        try
        {
            if (Media.Source is not null)
            {
                Media.Position = TimeSpan.FromSeconds(seconds);
            }
        }
        catch (InvalidOperationException)
        {
            // 视频尚未加载完成时忽略跳转请求。
        }
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var duration = Media.NaturalDuration.HasTimeSpan ? Media.NaturalDuration.TimeSpan : TimeSpan.Zero;
        _viewModel.Video.ReportOpened(duration);
        Media.Volume = _viewModel.Video.Volume;
        Media.Play();
        _viewModel.Video.SetPlaying(true);
        StartBackgroundAnimation();
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var duration = Media.NaturalDuration.HasTimeSpan ? Media.NaturalDuration.TimeSpan : TimeSpan.Zero;
        _viewModel.Video.ReportMediaEnded(Media.Position, duration);

        // 大屏展播默认循环：回到开头继续播放，同时保持背景渐变不中断。
        if (_viewModel.Video.IsLooping)
        {
            Media.Stop();
            Media.Position = TimeSpan.Zero;
            Media.Play();
            _viewModel.Video.ReportLoopRestart();
            StartBackgroundAnimation();
            return;
        }

        _viewModel.Video.ReportEnded();
        StopBackgroundAnimation();
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _viewModel?.Video.ReportFailed("视频加载失败：" + (e.ErrorException?.Message ?? "文件不存在或格式不受支持。"));
        StopBackgroundAnimation();
    }

    private void OnMediaBufferingStarted(object sender, RoutedEventArgs e) => _viewModel?.Video.ReportBuffering(true);

    private void OnMediaBufferingEnded(object sender, RoutedEventArgs e) => _viewModel?.Video.ReportBuffering(false);

    private void OnMediaClick(object sender, MouseButtonEventArgs e) => _viewModel?.Video.PlayPauseCommand.Execute(null);

    private void OnPositionTick(object? sender, EventArgs e)
    {
        if (_viewModel is null || _isSeeking || Media.Source is null)
        {
            return;
        }

        if (Media.NaturalDuration.HasTimeSpan)
        {
            _viewModel.Video.ReportPosition(Media.Position);
        }
    }

    private void OnPositionSliderPreviewMouseDown(object sender, MouseButtonEventArgs e) => _isSeeking = true;

    private void OnPositionSliderPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isSeeking = false;
        _viewModel?.Video.SeekTo(PositionSlider.Value);
    }

    private void OnPositionSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSeeking)
        {
            _viewModel?.Video.ReportPosition(TimeSpan.FromSeconds(e.NewValue));
        }
    }

    // ============================================================
    // 背景渐变动画：播放时缓慢循环，暂停时暂停，停止时复位
    // ============================================================
    private void StartBackgroundAnimation()
    {
        _backgroundStoryboard ??= CreateBackgroundStoryboard();
        _backgroundStoryboard.Begin(this, isControllable: true);
    }

    private void PauseBackgroundAnimation() => _backgroundStoryboard?.Pause(this);

    private void StopBackgroundAnimation()
    {
        _backgroundStoryboard?.Stop(this);
        BackgroundPulse.Opacity = 0d;
    }

    private Storyboard CreateBackgroundStoryboard()
    {
        var storyboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            Duration = new Duration(TimeSpan.FromSeconds(12))
        };

        storyboard.Children.Add(CreateColorAnimation(BgStopA, Color.FromRgb(0x07, 0x1A, 0x28), Color.FromRgb(0x0C, 0x34, 0x44)));
        storyboard.Children.Add(CreateColorAnimation(BgStopB, Color.FromRgb(0x0A, 0x2C, 0x3E), Color.FromRgb(0x10, 0x4A, 0x5A)));
        storyboard.Children.Add(CreateColorAnimation(BgStopC, Color.FromRgb(0x0B, 0x25, 0x36), Color.FromRgb(0x0A, 0x3A, 0x42)));

        var fade = new DoubleAnimation
        {
            From = 0d,
            To = 0.85d,
            Duration = new Duration(TimeSpan.FromSeconds(1.2)),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fade, BackgroundPulse);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(fade);

        return storyboard;
    }

    private static ColorAnimation CreateColorAnimation(GradientStop stop, Color from, Color to)
    {
        var animation = new ColorAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromSeconds(6)),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(animation, stop);
        Storyboard.SetTargetProperty(animation, new PropertyPath(GradientStop.ColorProperty));
        return animation;
    }

    // ============================================================
    // 其它交互
    // ============================================================
    private void OnHistoryRowsAdded(object? sender, EventArgs e)
    {
        if (_viewModel?.History.AutoScroll != true)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (HistoryGrid.Items.Count > 0)
            {
                HistoryGrid.ScrollIntoView(HistoryGrid.Items[0]);
            }
        }, DispatcherPriority.Background);
    }

    private void OnLoadRateChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is ProgressBar bar)
        {
            bar.ToolTip = $"负荷率 {bar.Value:P1}";
        }
    }

    private void OnSaveEntryClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        // ValidationRule 拒绝的输入不会写回视图模型，因此必须检查界面校验状态后再保存。
        var hasError = WpfValidation.GetHasError(VoltageInputBox)
                       || WpfValidation.GetHasError(CurrentInputBox)
                       || WpfValidation.GetHasError(PowerInputBox)
                       || WpfValidation.GetHasError(FrequencyInputBox)
                       || WpfValidation.GetHasError(TimestampInputBox);

        if (hasError)
        {
            _viewModel.Entry.ReportExternalValidationError("存在非法输入，请修正标红字段后再保存。");
            return;
        }

        _viewModel.Entry.SaveCommand.Execute(null);
    }

    private void OnVoltageLevelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 选择电压等级时给拓扑区域一个轻微的透明度过渡，提示配色已切换。
        var animation = new DoubleAnimation
        {
            From = 0.55d,
            To = 1d,
            Duration = new Duration(TimeSpan.FromMilliseconds(320)),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };

        // 动画作用在拓扑卡片所在的容器上，不改变布局。
        if (sender is ComboBox combo && combo.Parent is Panel)
        {
            combo.BeginAnimation(OpacityProperty, animation);
        }
    }
}
