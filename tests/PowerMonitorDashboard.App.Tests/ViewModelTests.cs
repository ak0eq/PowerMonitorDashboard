using System.IO;
using PowerMonitorDashboard.Core.Abstractions;
using PowerMonitorDashboard.Core.Configuration;
using PowerMonitorDashboard.Core.Domain;
using PowerMonitorDashboard.Core.Validation;
using PowerMonitorDashboard.Core.Monitoring;
using PowerMonitorDashboard.Infrastructure;
using PowerMonitorDashboard.Services;
using PowerMonitorDashboard.ViewModels;

namespace PowerMonitorDashboard.App.Tests;

internal static class ResultFactory
{
    public static ProcessingResult Create(
        PowerReading? reading = null,
        double energy = 0d,
        int samples = 1,
        AlarmBatch? alarms = null,
        IReadOnlyList<Alarm>? active = null)
    {
        var thresholds = PowerThresholds.Default;
        var value = reading ?? new PowerReading(
            DateTimeOffset.Now, 220d, 1000d, 362d, 50d, ReadingSource.Simulated, QualityFlag.Good);
        var evaluation = StatusEvaluator.Evaluate(value, thresholds);
        return new ProcessingResult(
            value,
            evaluation,
            alarms ?? AlarmBatch.Empty,
            active ?? [],
            energy,
            samples);
    }
}

public sealed class LiveMetricsViewModelTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 26, 20, 0, 0, TimeSpan.FromHours(8)));

    [Fact]
    public void Update_ShouldRefreshValuesAndSeries()
    {
        var viewModel = new LiveMetricsViewModel(() => PowerThresholds.Default, _clock);
        var reading = new PowerReading(_clock.Now, 220.5d, 1010d, 365.4d, 49.98d, ReadingSource.Simulated, QualityFlag.Good);

        viewModel.Update(ResultFactory.Create(reading, energy: 12.5d, samples: 7));

        Assert.True(viewModel.HasData);
        Assert.Equal(220.5d, viewModel.Reading.VoltageKv);
        Assert.Equal("220.5", viewModel.Reading.VoltageText);
        Assert.Single(viewModel.VoltageSeries);
        Assert.Equal(12.5d, viewModel.DailyEnergyMwh);
        Assert.Equal(7, viewModel.SampleCount);
        Assert.Equal(OperatingStatus.Normal, viewModel.Status);
        Assert.Equal("正常", viewModel.StatusText);
        Assert.False(viewModel.IsFrequencyOutOfBand);
    }

    [Fact]
    public void Update_ShouldFlagOutOfBandFrequency()
    {
        var viewModel = new LiveMetricsViewModel(() => PowerThresholds.Default, _clock);
        var reading = new PowerReading(_clock.Now, 220d, 1000d, 362d, 50.8d, ReadingSource.Simulated, QualityFlag.Good);

        viewModel.Update(ResultFactory.Create(reading));

        Assert.Equal(OperatingStatus.Attention, viewModel.Status);
        Assert.True(viewModel.IsFrequencyOutOfBand);
        Assert.Contains("频率", viewModel.StatusSummary);
    }

    [Fact]
    public void Initialize_ShouldRestoreLastReadingAndTrend()
    {
        var viewModel = new LiveMetricsViewModel(() => PowerThresholds.Default, _clock);
        var history = Enumerable.Range(0, 5)
            .Select(i => new PowerReading(_clock.Now.AddSeconds(i), 220d + i, 1000d, 360d, 50d, ReadingSource.Simulated, QualityFlag.Good))
            .ToArray();
        var evaluation = StatusEvaluator.Evaluate(history[^1], PowerThresholds.Default);

        viewModel.Initialize(history, evaluation);

        Assert.True(viewModel.HasData);
        Assert.Equal(5, viewModel.VoltageSeries.Count);
        Assert.Equal(224d, viewModel.Reading.VoltageKv);
        Assert.Equal(OperatingStatus.Normal, viewModel.Status);
    }

    [Fact]
    public void RefreshThresholds_ShouldUpdateHints()
    {
        var thresholds = PowerThresholds.Default;
        var viewModel = new LiveMetricsViewModel(() => thresholds, _clock);

        thresholds = thresholds with { NominalVoltageKv = 110d };
        viewModel.RefreshThresholds();

        Assert.Equal(104.5d, viewModel.VoltageAttentionLow, 3);
        Assert.Contains("104.5", viewModel.VoltageHint);
    }

    [Fact]
    public void Series_ShouldBeTrimmedToCapacity()
    {
        var viewModel = new LiveMetricsViewModel(() => PowerThresholds.Default, _clock);

        for (var i = 0; i < LiveMetricsViewModel.SeriesCapacity + 20; i++)
        {
            viewModel.Update(ResultFactory.Create());
        }

        Assert.Equal(LiveMetricsViewModel.SeriesCapacity, viewModel.VoltageSeries.Count);
    }
}

public sealed class DataEntryViewModelTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 26, 20, 0, 0, TimeSpan.FromHours(8)));

    private DataEntryViewModel CreateViewModel(Action<ReadingDraft>? onSubmit = null, PowerReading? live = null)
        => new(
            _clock,
            () => PowerThresholds.Default,
            () => 0.95d,
            () => live,
            draft =>
            {
                onSubmit?.Invoke(draft);
                return new DataEntryFeedback("已保存", true);
            });

    [Fact]
    public void Defaults_ShouldBeValidOrWarnOnly()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("220.0", viewModel.VoltageText);
        Assert.Equal("50.00", viewModel.FrequencyText);
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public void InvalidVoltage_ShouldBlockSaving()
    {
        ReadingDraft? submitted = null;
        var viewModel = CreateViewModel(draft => submitted = draft);
        viewModel.VoltageText = "999";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.HasErrors);
        Assert.Null(submitted);
        Assert.True(viewModel.StatusIsError);
    }

    [Fact]
    public void TooManyDecimals_ShouldBlockSaving()
    {
        var viewModel = CreateViewModel();
        viewModel.VoltageText = "220.55";

        Assert.True(viewModel.HasErrors);
        var issue = Assert.Single(viewModel.Issues);
        Assert.Contains("小数", issue.Message);
    }

    [Fact]
    public void FutureTimestamp_ShouldBlockSaving()
    {
        var viewModel = CreateViewModel();
        viewModel.TimestampText = _clock.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");

        Assert.True(viewModel.HasErrors);
        Assert.Contains("不能晚于当前时间", viewModel.ValidationSummary);
    }

    [Fact]
    public void ValidInput_ShouldSubmitAndRefreshTimestamp()
    {
        ReadingDraft? submitted = null;
        var viewModel = CreateViewModel(draft => submitted = draft);
        viewModel.VoltageText = "220.0";
        viewModel.CurrentText = "1000";
        viewModel.PowerText = "362";
        viewModel.FrequencyText = "50.00";
        viewModel.TimestampText = _clock.Now.AddMinutes(-1).ToString("yyyy-MM-dd HH:mm:ss");

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(submitted);
        Assert.Equal("220.0", submitted!.VoltageKv);
        Assert.False(viewModel.StatusIsError);
        Assert.Equal("已保存", viewModel.StatusMessage);
    }

    [Fact]
    public void FillFromLive_ShouldCopyValues()
    {
        var live = new PowerReading(_clock.Now, 221.2d, 1050d, 380.5d, 49.99d, ReadingSource.Simulated, QualityFlag.Good);
        var viewModel = CreateViewModel(live: live);

        viewModel.FillFromLiveCommand.Execute(null);

        Assert.Equal("221.2", viewModel.VoltageText);
        Assert.Equal("1050", viewModel.CurrentText);
        Assert.Equal("380.50", viewModel.PowerText);
        Assert.Equal("49.99", viewModel.FrequencyText);
    }

    [Fact]
    public void UseCurrentTime_ShouldResetTimestamp()
    {
        var viewModel = CreateViewModel();
        viewModel.TimestampText = "2020-01-01 00:00:00";

        viewModel.UseCurrentTimeCommand.Execute(null);

        Assert.StartsWith("2026-10-26", viewModel.TimestampText);
    }
}

public sealed class HistoryViewModelTests
{
    [Fact]
    public void Add_ShouldKeepNewestFirstAndRespectLimit() => StaRunner.Run(() =>
    {
        var viewModel = new HistoryViewModel(() => PowerThresholds.Default) { DisplayLimit = 50 };

        for (var i = 0; i < 55; i++)
        {
            var reading = new PowerReading(
                DateTimeOffset.Now.AddSeconds(i), 220d, 1000d, 100d + i, 50d, ReadingSource.Simulated, QualityFlag.Good);
            viewModel.Add(ResultFactory.Create(reading));
        }

        Assert.Equal(50, viewModel.Rows.Count);
        Assert.Equal(154d, viewModel.Rows[0].PowerMw);
        Assert.Equal(50, viewModel.View.Cast<object>().Count());
    });

    [Fact]
    public void AbnormalFilter_ShouldShowOnlyAbnormalRows() => StaRunner.Run(() =>
    {
        var viewModel = new HistoryViewModel(() => PowerThresholds.Default);
        viewModel.Add(ResultFactory.Create());
        viewModel.Add(ResultFactory.Create(new PowerReading(
            DateTimeOffset.Now, 195d, 1000d, 362d, 50d, ReadingSource.Simulated, QualityFlag.Good)));

        viewModel.ShowOnlyAbnormal = true;

        Assert.Equal(1, viewModel.View.Cast<object>().Count());
        Assert.Equal("仅异常", viewModel.FilterText);
    });

    [Fact]
    public void Load_ShouldEvaluateStatus() => StaRunner.Run(() =>
    {
        var viewModel = new HistoryViewModel(() => PowerThresholds.Default);
        var readings = new[]
        {
            new PowerReading(DateTimeOffset.Now, 220d, 1000d, 362d, 50d, ReadingSource.Simulated, QualityFlag.Good),
            new PowerReading(DateTimeOffset.Now.AddSeconds(1), 195d, 1000d, 300d, 50d, ReadingSource.Simulated, QualityFlag.Good)
        };

        viewModel.Load(readings);

        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Equal(OperatingStatus.Critical, viewModel.Rows[1].Status);
    });
}

public sealed class AlarmPanelViewModelTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 26, 20, 0, 0, TimeSpan.FromHours(8)));

    [Fact]
    public void Apply_ShouldTrackRaisedAlarmsAndCounts()
    {
        var panel = new AlarmPanelViewModel(_clock);
        var alarm = new Alarm(AlarmKind.Overload, AlarmSeverity.Critical, "过载", 520d, 500d, _clock.Now);
        var batch = new AlarmBatch([alarm], [], [], []);

        panel.Apply(ResultFactory.Create(alarms: batch, active: [alarm]));

        Assert.Single(panel.Recent);
        Assert.Equal(1, panel.ActiveCount);
        Assert.Equal(1, panel.CriticalCount);
        Assert.True(panel.HasActive);
        Assert.True(panel.HasCritical);
    }

    [Fact]
    public void AcknowledgeCommand_ShouldUpdateAlarmState()
    {
        var panel = new AlarmPanelViewModel(_clock);
        var alarm = new Alarm(AlarmKind.FrequencyHigh, AlarmSeverity.Warning, "频率高", 50.6d, 50.5d, _clock.Now);
        panel.Apply(ResultFactory.Create(alarms: new AlarmBatch([alarm], [], [], []), active: [alarm]));

        panel.AcknowledgeCommand.Execute(alarm);

        Assert.Equal(AlarmState.Acknowledged, alarm.State);
        Assert.Equal(1, panel.ActiveCount);
    }

    [Fact]
    public void Clear_ShouldResetPanel()
    {
        var panel = new AlarmPanelViewModel(_clock);
        panel.Apply(ResultFactory.Create());

        panel.Clear();

        Assert.Empty(panel.Recent);
        Assert.Equal(0, panel.ActiveCount);
    }
}

public sealed class VideoViewModelTests
{
    private readonly VideoViewModel _viewModel = new(NullLogger.Instance);

    [Fact]
    public void SetSource_ShouldExposeFileNameAndLoadingState()
    {
        _viewModel.SetSource(Path.Combine("Assets", "video", "safety-training.mp4"));

        Assert.Equal("safety-training.mp4", _viewModel.FileName);
        Assert.True(_viewModel.ShowLoading);
        Assert.False(_viewModel.ShowVideo);
    }

    [Fact]
    public void ReportOpened_ShouldSetDurationAndReadyState()
    {
        _viewModel.SetSource("video.mp4");

        _viewModel.ReportOpened(TimeSpan.FromSeconds(42));

        Assert.True(_viewModel.IsReady);
        Assert.Equal(42d, _viewModel.DurationSeconds);
        Assert.Equal("00:42", _viewModel.DurationText);
        Assert.True(_viewModel.ShowVideo);
        Assert.False(_viewModel.ShowLoading);
    }

    [Fact]
    public void ReportPosition_ShouldUpdateProgress()
    {
        _viewModel.ReportOpened(TimeSpan.FromSeconds(42));
        _viewModel.ReportPosition(TimeSpan.FromSeconds(21));

        Assert.Equal("00:21", _viewModel.PositionText);
        Assert.Equal(50d, _viewModel.ProgressPercent, 3);
    }

    [Fact]
    public void SeekTo_ShouldRaiseEventAndClamp()
    {
        _viewModel.ReportOpened(TimeSpan.FromSeconds(42));
        double? seek = null;
        _viewModel.SeekRequested += (_, value) => seek = value;

        _viewModel.SeekTo(1000d);

        Assert.Equal(42d, seek);
    }

    [Fact]
    public void PlayPauseCommand_ShouldRaisePlayThenPause()
    {
        VideoPlaybackAction? action = null;
        _viewModel.PlaybackRequested += (_, value) => action = value;

        _viewModel.PlayPauseCommand.Execute(null);
        Assert.Equal(VideoPlaybackAction.Play, action);

        _viewModel.SetPlaying(true);
        _viewModel.PlayPauseCommand.Execute(null);
        Assert.Equal(VideoPlaybackAction.Pause, action);
    }

    [Fact]
    public void ReportFailed_ShouldShowErrorState()
    {
        _viewModel.SetSource("missing.mp4");

        _viewModel.ReportFailed("文件不存在");

        Assert.True(_viewModel.HasError);
        Assert.False(_viewModel.ShowVideo);
        Assert.Contains("文件不存在", _viewModel.ErrorMessage);
    }

    [Fact]
    public void ReportEnded_ShouldStopPlaying()
    {
        _viewModel.ReportOpened(TimeSpan.FromSeconds(42));
        _viewModel.SetPlaying(true);

        _viewModel.ReportEnded();

        Assert.False(_viewModel.IsPlaying);
        Assert.Equal(42d, _viewModel.PositionSeconds);
    }
}

public sealed class SettingsViewModelTests
{
    [Fact]
    public void TryBuild_ShouldReturnValidSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pmd-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(directory, directory);
            var viewModel = new SettingsViewModel(new MonitorSettings(), new FakeFileDialogService(), paths)
            {
                SamplingIntervalText = "2000",
                OperatorName = "张工"
            };

            var ok = viewModel.TryBuild(out var settings);

            Assert.True(ok);
            Assert.NotNull(settings);
            Assert.Equal(2000, settings!.SamplingIntervalMs);
            Assert.Equal("张工", settings.OperatorName);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void TryBuild_ShouldRejectInvalidValues()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pmd-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(directory, directory);
            var viewModel = new SettingsViewModel(new MonitorSettings(), new FakeFileDialogService(), paths)
            {
                SamplingIntervalText = "10",
                NominalVoltageText = "abc"
            };

            var ok = viewModel.TryBuild(out var settings);

            Assert.False(ok);
            Assert.Null(settings);
            Assert.True(viewModel.HasValidationError);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ResetDefaults_ShouldRestoreDefaultValues()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pmd-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(directory, directory);
            var viewModel = new SettingsViewModel(new MonitorSettings(), new FakeFileDialogService(), paths)
            {
                SamplingIntervalText = "5000"
            };

            viewModel.ResetDefaultsCommand.Execute(null);

            Assert.Equal("1000", viewModel.SamplingIntervalText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
