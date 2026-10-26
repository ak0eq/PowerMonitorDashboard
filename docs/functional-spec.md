# 功能规格与作业要求对照

## 1. 大屏布局

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ 顶栏：标题 / 数据源状态 / 采样周期 / 告警摘要 / 时钟 / 采集控制 / 设置 / 全屏 │
├───────────────┬───────────────┬───────────────┬───────────────────────────────┤
│ 母线电压 KPI  │ 线路电流 KPI  │ 有功功率 KPI  │ 电网频率 KPI                  │
├───────────────┴───────────────┼───────────────┼───────────────────────────────┤
│ 电网拓扑与潮流（Canvas）      │ 实时趋势      │ 人工数据补录（校验）          │
│                               │ 电压/频率/功率│                               │
├───────────────────────────────┤               │                               │
│ 馈线负荷波动（关键帧动画）    ├───────────────┤                               │
│                               │ 告警中心      │                               │
├───────────────────────────────┴───────────────┴───────────────────────────────┤
│ 安全教学视频（MediaElement）  │ 历史数据表格（DataGrid / 筛选 / 导出）        │
├───────────────────────────────────────────────────────────────────────────────┤
│ 状态栏：最近更新 / 值班员 / 记录数 / 保存时间 / 运行时长 / 版本 / 快捷键      │
└───────────────────────────────────────────────────────────────────────────────┘
```

## 2. 功能清单

### 2.1 常用控件与布局

- 使用 `Grid`、`StackPanel`、`DockPanel`（状态栏/顶栏）、`WrapPanel`（设置页按钮组）等至少四种布局容器组织界面。
- `Button`（顶栏命令、播放控制）、`TextBox`（录入与设置）、`ComboBox`（电压等级筛选）、`Slider`（音量、播放进度）、`ProgressBar`（负荷率指示）、`ListBox`（告警列表）、`DataGrid`（历史数据）、`CheckBox`、`ToolTip` 等控件均有实际交互。
- 事件处理：按钮 `Click`、`ComboBox.SelectionChanged`、`Slider.ValueChanged`、`MediaElement.MediaOpened/MediaFailed/BufferingStarted/Ended`、`Window.PreviewKeyDown`。

### 2.2 二维图形与图像

- `Canvas` 上绘制完整单线图：`Line`（输电线路，虚线流动动画）、`Ellipse`（变电站/母线告警环）、`Rectangle`（发电机组、变压器、母线）、`Path`（设备图标与预警图标）。
- 每个设备带 `ToolTip`，显示名称、类型、电压等级、实时值/负荷、运行状态。
- `ComboBox` 切换电压等级：500 kV 绿色、220 kV 黄色、110 kV 红色、10 kV 蓝色；选中等级高亮、其余淡化。
- 设备图标全部使用矢量 `Geometry`，任意分辨率下清晰。

### 2.3 数据绑定与校验

- `PowerReading` 实现 `INotifyPropertyChanged`，包含电压、电流、功率、频率、时间戳及来源、质量等字段。
- `ObservableCollection<ReadingRow>` 绑定 `DataGrid`，新增记录自动插入首行并滚动到顶部。
- 数据录入表单支持新增数据点，校验规则如下：

| 字段 | 规则 | 提示 |
| --- | --- | --- |
| 电压 | 0～500 kV，小数点后最多一位 | 红框 + ToolTip + 汇总面板 |
| 电流 | 0～5000 A，必填 | 同上 |
| 频率 | 47.0～52.0 Hz | 同上 |
| 时间戳 | `yyyy-MM-dd HH:mm:ss`，不得晚于当前时间 | 同上 |
| 功率 | 0～1000 MW；留空自动计算 | 同上 |
| 跨字段 | `P ≈ √3·U·I·cosφ` 偏差 > 10% 且 > 5 MW | 非阻断警告 |

- 校验失败时控件显示红色边框和感叹号提示，底部汇总全部问题；保存被阻止，并弹出顶部提示条。
- 值转换器：`PowerLevelConverter` 将功率映射为“高/中/低”，`PowerLevelBrushConverter` 映射为红/橙/青三色；运行状态、告警级别各有文本与颜色转换器。

### 2.4 视频与动画

- `MediaElement` 播放 42 秒安全教学视频（H.264/AAC），`LoadedBehavior=Manual` 精确控制。
- 播放 / 暂停 / 停止 / 静音 / 音量滑条 / 进度条拖动跳转 / 时间显示；`MediaOpened` 获取时长并自动播放；`MediaFailed` 显示错误与重试。
- 播放时背景 `ColorAnimation` 缓慢循环渐变；暂停时暂停、停止时复位。
- 馈线负荷柱状图使用 `DoubleAnimationUsingKeyFrames` 循环模拟功率波动，幅度与实时负荷率联动。
- 预警图标：存在活动告警时显示，鼠标移入播放缩放 + 旋转关键帧动画；设备告警环持续脉冲。
- 视频缓冲时显示旋转加载动画，加载完成后自动播放。

### 2.5 交互与工程化

- 数据刷新按钮随机/即时生成一条实时数据；暂停采集时界面保留最后一帧。
- “故障演练”循环注入四类扰动，便于演示告警与动画。
- 设置对话框可在线调整采样周期、历史容量、阈值、视频路径等，确定后立即生效并持久化。
- 历史数据支持“仅异常”筛选、自动滚动、CSV 导出、清空。
- 告警支持单条确认与全部确认，恢复后自动清除。
- 全屏（F11）、退出全屏（Esc）、快捷键（F5 / Ctrl+E / Ctrl+, / Ctrl+Q）、单实例运行、全局异常处理、日志落盘。

## 3. 作业要求逐项对照

| 作业要求 | 实现位置 | 说明 |
| --- | --- | --- |
| 至少四种布局控件 | `MainWindow.xaml` | Grid、StackPanel、DockPanel、WrapPanel |
| 常用控件与事件 | `MainWindow.xaml(.cs)`、`SettingsWindow.xaml` | Button/TextBox/ComboBox/Slider/ProgressBar/ListBox/DataGrid/CheckBox |
| Canvas 拓扑：Line/Ellipse/Rectangle/变压器 | `MainWindow.xaml`（TopologyNodeTemplate）、`TopologyViewModel` | 14 个节点、14 条线路，变压器带文本 |
| 设备图标 + ToolTip | 节点模板 + `ToolTip` | 矢量图标，悬停显示详情 |
| 电压等级切换配色 | `TopologyViewModel.Recolor` + ComboBox | 500/220/110/10 kV 四色 |
| `PowerData` + INotifyPropertyChanged | `PowerReading` | 含时间戳、来源、质量 |
| ObservableCollection 绑定 DataGrid | `HistoryViewModel` | 最新优先、筛选、自动滚动 |
| 数据录入 + 四条 ValidationRule | `DataEntryViewModel`、`FieldValidationRules`、`ReadingValidator` | 红框 + 汇总 + 阻断保存 |
| 值转换器（高/中/低 + 颜色） | `PowerLevelConverter`、`PowerLevelBrushConverter` | 阈值可在 XAML 配置 |
| MediaElement 播放/暂停/停止/静音/音量/进度 | `MainWindow.xaml(.cs)`、`VideoViewModel` | 完整控制栏 |
| 播放时背景 ColorAnimation | `MainWindow.xaml.cs` `CreateBackgroundStoryboard` | 12 秒循环、自动反向 |
| 负荷波动关键帧动画 | `LoadBars` | 6 根柱子、相位差、幅度联动 |
| 预警图标缩放/旋转 + 悬停 | 拓扑卡片右上角 `预警` 图标 | 脉冲 + 鼠标移入抖动 |
| 加载旋转动画 + 自动播放 | 视频缓冲遮罩 + `MediaOpened` | — |
| 深色科技感大屏风格 | `Themes/*.xaml` | 统一色板与控件模板 |
| 数据刷新按钮 | `RefreshCommand` | 立即生成一条 |
| MVVM / RelayCommand | `Mvvm/`、`ViewModels/` | 自实现命令与基类 |
| 思考题 | 见结课作业报告第 4 章 | 设计说明与代码片段 |

## 4. 界面交互细节

- 顶栏状态胶囊实时反映数据源状态（正常 / 已暂停 / 数据延迟）与告警摘要。
- KPI 卡片右下角迷你趋势图展示最近 150 个采样点，无需切换页面。
- 拓扑图选中电压等级时，图例与下拉框同步，界面有轻微透明度过渡。
- 录入表单的“填入实时值”把当前 KPI 数值复制到表单，“现在”刷新时间戳。
- 提示条（Toast）在 5 秒后自动消失，颜色随级别变化（信息 / 成功 / 警告 / 错误）。
- 历史表格最近记录高亮显示人工补录数据（来源列）。
