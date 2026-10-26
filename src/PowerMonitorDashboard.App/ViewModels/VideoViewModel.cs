using System.Globalization;
using System.IO;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Mvvm;

namespace PowerMonitorDashboard.ViewModels;

/// <summary>播放控制请求。</summary>
public enum VideoPlaybackAction
{
    Play,
    Pause,
    Stop,
    Reload
}

/// <summary>
/// 视频播放视图模型。只保存“状态”，真正的媒体操作由视图层执行：
/// MediaElement 是 UI 元素，把它放进 ViewModel 会破坏 MVVM 的可测试性。
/// </summary>
public sealed class VideoViewModel : ObservableObject
{
    private readonly IAppLogger _logger;
    private string _sourcePath = string.Empty;
    private string _fileName = string.Empty;
    private bool _isPlaying;
    private bool _isBuffering;
    private bool _hasError;
    private bool _isReady;
    private bool _isMuted;
    private double _volume = 0.75d;
    private double _positionSeconds;
    private double _durationSeconds;
    private string _errorMessage = string.Empty;

    /// <summary>创建视频视图模型。</summary>
    public VideoViewModel(IAppLogger logger)
    {
        _logger = logger;
        PlayPauseCommand = new RelayCommand(TogglePlayPause);
        StopCommand = new RelayCommand(Stop, () => IsReady && (IsPlaying || PositionSeconds > 0.5d));
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ReloadCommand = new RelayCommand(() => PlaybackRequested?.Invoke(this, VideoPlaybackAction.Reload));
    }

    /// <summary>播放请求事件，由视图订阅并驱动 MediaElement。</summary>
    public event EventHandler<VideoPlaybackAction>? PlaybackRequested;

    /// <summary>拖动进度条请求事件（单位：秒）。</summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>视频绝对路径。</summary>
    public string SourcePath
    {
        get => _sourcePath;
        private set => SetProperty(ref _sourcePath, value);
    }

    /// <summary>视频文件名。</summary>
    public string FileName
    {
        get => _fileName;
        private set => SetProperty(ref _fileName, value);
    }

    /// <summary>是否正在播放。</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertiesChanged(nameof(StateText), nameof(PlayButtonText));
            }
        }
    }

    /// <summary>是否正在缓冲。</summary>
    public bool IsBuffering
    {
        get => _isBuffering;
        private set
        {
            if (SetProperty(ref _isBuffering, value))
            {
                OnPropertiesChanged(nameof(ShowLoading), nameof(StateText));
            }
        }
    }

    /// <summary>是否加载失败。</summary>
    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertiesChanged(nameof(ShowLoading), nameof(StateText), nameof(ShowVideo));
            }
        }
    }

    /// <summary>是否已成功加载。</summary>
    public bool IsReady
    {
        get => _isReady;
        private set
        {
            if (SetProperty(ref _isReady, value))
            {
                OnPropertiesChanged(nameof(ShowLoading), nameof(StateText), nameof(ShowVideo), nameof(CanSeek));
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>是否静音（与 MediaElement 双向绑定）。</summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MuteButtonText));
            }
        }
    }

    /// <summary>音量 0～1（与 MediaElement 双向绑定）。</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, Math.Clamp(value, 0d, 1d)))
            {
                OnPropertyChanged(nameof(VolumeText));
            }
        }
    }

    /// <summary>当前播放位置（秒）。</summary>
    public double PositionSeconds
    {
        get => _positionSeconds;
        private set
        {
            if (SetProperty(ref _positionSeconds, value))
            {
                OnPropertiesChanged(nameof(PositionText), nameof(ProgressPercent));
            }
        }
    }

    /// <summary>视频总时长（秒）。</summary>
    public double DurationSeconds
    {
        get => _durationSeconds;
        private set
        {
            if (SetProperty(ref _durationSeconds, value))
            {
                OnPropertiesChanged(nameof(DurationText), nameof(ProgressPercent));
            }
        }
    }

    /// <summary>错误信息。</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>时间显示：当前 / 总长。</summary>
    public string PositionText => Format(PositionSeconds);

    /// <summary>总时长文本。</summary>
    public string DurationText => Format(DurationSeconds);

    /// <summary>播放进度（0～100）。</summary>
    public double ProgressPercent => DurationSeconds <= 0.001d ? 0d : Math.Clamp(PositionSeconds / DurationSeconds * 100d, 0d, 100d);

    /// <summary>状态文本。</summary>
    public string StateText => HasError
        ? "播放失败"
        : !IsReady && IsBuffering
            ? "视频加载中"
            : IsPlaying
                ? "正在播放"
                : PositionSeconds > 0.5d
                    ? "已暂停"
                    : "待播放";

    /// <summary>播放按钮文本。</summary>
    public string PlayButtonText => IsPlaying ? "暂停" : "播放";

    /// <summary>静音按钮文本。</summary>
    public string MuteButtonText => IsMuted ? "取消静音" : "静音";

    /// <summary>音量文本。</summary>
    public string VolumeText => (Volume * 100d).ToString("F0", CultureInfo.InvariantCulture) + "%";

    /// <summary>是否显示加载动画。</summary>
    public bool ShowLoading => IsBuffering || (!IsReady && !HasError);

    /// <summary>是否显示视频画面。</summary>
    public bool ShowVideo => IsReady && !HasError;

    /// <summary>是否允许拖动进度。</summary>
    public bool CanSeek => IsReady;

    /// <summary>播放/暂停命令。</summary>
    public RelayCommand PlayPauseCommand { get; }

    /// <summary>停止命令。</summary>
    public RelayCommand StopCommand { get; }

    /// <summary>静音命令。</summary>
    public RelayCommand ToggleMuteCommand { get; }

    /// <summary>重新加载命令。</summary>
    public RelayCommand ReloadCommand { get; }

    /// <summary>设置视频源。</summary>
    public void SetSource(string path)
    {
        SourcePath = path;
        FileName = Path.GetFileName(path);
        IsReady = false;
        HasError = false;
        ErrorMessage = string.Empty;
        PositionSeconds = 0d;
        DurationSeconds = 0d;
        IsPlaying = false;
        IsBuffering = true;
        _logger.Info($"视频源设置为：{path}");
    }

    /// <summary>媒体打开成功。</summary>
    public void ReportOpened(TimeSpan duration)
    {
        DurationSeconds = duration.TotalSeconds;
        IsReady = true;
        HasError = false;
        IsBuffering = false;
        ErrorMessage = string.Empty;
        _logger.Info($"视频加载完成，时长 {duration:mm\\:ss}。");
    }

    /// <summary>媒体加载失败。</summary>
    public void ReportFailed(string message, Exception? exception = null)
    {
        HasError = true;
        IsBuffering = false;
        IsReady = false;
        IsPlaying = false;
        ErrorMessage = message;
        _logger.Error("视频加载失败：" + message, exception);
    }

    /// <summary>缓冲状态变化。</summary>
    public void ReportBuffering(bool buffering) => IsBuffering = buffering;

    /// <summary>播放位置更新。</summary>
    public void ReportPosition(TimeSpan position) => PositionSeconds = Math.Max(0d, position.TotalSeconds);

    /// <summary>播放结束。</summary>
    public void ReportEnded()
    {
        IsPlaying = false;
        PositionSeconds = DurationSeconds;
        _logger.Info("视频播放结束。");
    }

    /// <summary>设置播放状态。</summary>
    public void SetPlaying(bool playing)
    {
        IsPlaying = playing;
        if (playing)
        {
            IsBuffering = false;
            HasError = false;
        }

        RelayCommand.RaiseCanExecuteChanged();
    }

    /// <summary>请求跳转到指定位置。</summary>
    public void SeekTo(double seconds)
    {
        var clamped = Math.Clamp(seconds, 0d, Math.Max(0d, DurationSeconds));
        PositionSeconds = clamped;
        SeekRequested?.Invoke(this, clamped);
    }

    private void TogglePlayPause()
    {
        if (HasError)
        {
            PlaybackRequested?.Invoke(this, VideoPlaybackAction.Reload);
            return;
        }

        PlaybackRequested?.Invoke(this, IsPlaying ? VideoPlaybackAction.Pause : VideoPlaybackAction.Play);
    }

    private void Stop()
    {
        PlaybackRequested?.Invoke(this, VideoPlaybackAction.Stop);
        IsPlaying = false;
        PositionSeconds = 0d;
    }

    private void ToggleMute() => IsMuted = !IsMuted;

    private static string Format(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
        {
            return "00:00";
        }

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1d
            ? span.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }
}
