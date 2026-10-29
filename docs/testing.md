# 测试说明

## 1. 测试框架与规模

- 框架：xUnit 2.9.3 + Microsoft.NET.Test.Sdk
- 运行：`dotnet test PowerMonitorDashboard.sln`
- 规模：**158 个测试**（Core 113 + App 45），全部通过，耗时 < 1 s（不含构建）

## 2. 测试项目

| 项目 | 目标框架 | 覆盖内容 |
| --- | --- | --- |
| `PowerMonitorDashboard.Core.Tests` | `net10.0` | 功率换算、负荷曲线、仿真物理自洽、校验规则、状态评估、告警状态机、历史缓冲、CSV、配置序列化 |
| `PowerMonitorDashboard.App.Tests` | `net10.0-windows` | 值转换器、实时指标、录入校验、历史筛选、告警确认、视频状态机（含循环）、设置构建（WPF 集合视图在 STA 线程运行） |

## 3. 覆盖范围

### 3.1 核心层

| 测试类 | 关注点 |
| --- | --- |
| `PowerMathTests` | √3 公式、正反算一致性、功率因数边界、电量积分 |
| `LoadProfileTests` | 晚高峰 / 夜间低谷、全天范围、连续性 |
| `TelemetrySimulatorTests` | 固定种子复现、物理自洽（U/I/P/cosφ）、扰动越限、跟随日负荷曲线 |
| `VoltageValidationTests` | 范围、空值、非数字、一位小数、全角符号 |
| `CurrentValidationTests` | 必填、范围、上下界 |
| `FrequencyValidationTests` | 47–52 Hz、边界、非法文本 |
| `PowerValidationTests` | 可留空、范围 |
| `TimestampValidationTests` | 多种格式、未来时间、非法日期 |
| `ReadingValidationIntegrationTests` | 多错误汇总、跨字段警告、留空自动计算、构建结果 |
| `MonitorSettingsTests` | 默认合法、错误配置、Normalize、Clone 独立性 |
| `StatusEvaluatorTests` | 正常 / 注意 / 严重、四类越限、数据质量降级、原因排序 |
| `AlarmStateMachineTests` | 产生、去重、升级、回差、恢复、确认、复位 |
| `ReadingHistoryTests` | 容量淘汰、快照隔离、Recent、统计窗口、清空 |
| `PowerReadingTests` | 属性通知、依赖属性联动、UpdateFrom、Clone、文本格式 |
| `CsvFormatterTests` | 表头、行数、转义、排序、负荷等级 |
| `SettingsSerializationTests` | JSON 往返 |

### 3.2 应用层

| 测试类 | 关注点 |
| --- | --- |
| `PowerLevelConverterTests` | 高/中/低阈值、空值、非法输入、反向转换异常 |
| `StatusAndPercentConverterTests` | 状态文本、百分比格式、可见性反转、计数可见性 |
| `LiveMetricsViewModelTests` | 指标刷新、越限标记、历史初始化、阈值热更新、序列截断 |
| `DataEntryViewModelTests` | 默认值、电压/小数/未来时间阻断、合法提交、填入实时值、现在按钮 |
| `HistoryViewModelTests` | 最新优先、显示上限、异常筛选、载入状态评估 |
| `AlarmPanelViewModelTests` | 活动计数、严重计数、确认命令、清空 |
| `VideoViewModelTests` | 源设置、时长与就绪、进度、跳转钳制、播放请求、循环开关、循环重启、失败、结束 |
| `SettingsViewModelTests` | 合法构建、非法拒绝、恢复默认 |

## 4. 测试技巧

### 4.1 确定性时钟

核心层通过 `IClock` 注入时间；测试使用 `FixedClock` 或固定 `DateTimeOffset`，避免“未来时间”规则依赖真实时钟。

### 4.2 固定随机种子

`TelemetrySimulator` 接受 `seed`，同一随机种子产生完全相同的序列，因此可以断言两次实例化结果一致。

### 4.3 STA 线程

`HistoryViewModel` 依赖 `ListCollectionView`，测试通过 `StaRunner.Run(...)` 在 STA 线程执行，避免 WPF 集合视图的线程亲和问题。

### 4.4 视图模型替身

`FakeFileDialogService`、`FakeUserDialogService`、`ImmediateUiDispatcher`、`NullUiScheduler` 等测试替身隔离了对话框、调度与 UI 依赖。

## 5. 已知边界

- 未对 XAML 视觉树做 UI 自动化测试；界面通过截图脚本人工核验关键状态。
- `MediaElement` 的真实解码不在单元测试范围内（需要媒体栈），`VideoViewModel` 只测状态机。
- 仿真器测试关注物理自洽与统计特征，不断言具体数值序列（除固定种子一致性外）。

## 6. 最近一次测试结果

```
PowerMonitorDashboard.Core.Tests   通过: 113  失败: 0
PowerMonitorDashboard.App.Tests    通过:  45  失败: 0
合计                               通过: 158  失败: 0
```
