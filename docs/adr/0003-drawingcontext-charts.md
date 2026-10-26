# ADR-0003：自绘趋势图与负荷柱控件

- 状态：已采纳
- 日期：2026-10-26

## 背景

大屏需要三张高频刷新的趋势图与一组负荷波动柱状图。常见方案是引入 LiveCharts、OxyPlot 等第三方库，或使用大量 `Polyline`/`Rectangle` 元素拼装。

## 决策

实现两个轻量控件：

- `TrendChart : FrameworkElement`：在 `OnRender` 中绘制网格、阈值带、折线、渐变面积与末端高亮；
- `LoadBars : FrameworkElement`：用 `DoubleAnimationUsingKeyFrames` 驱动相位，绘制 6 根负荷柱。

依赖属性变化通过 `FrameworkPropertyMetadataOptions.AffectsRender` 触发重绘。

## 结果

- 零第三方依赖，部署体积小，样式完全可控。
- 每秒重绘 4 个控件（3 张趋势图 + 1 组柱状图）开销可忽略。
- 深色主题下无需与第三方库的默认样式斗争。
- 代价：需要自己处理坐标映射、DPI 文本与空数据状态，代码量约 400 行。

## 备选方案

- **LiveCharts2**：功能强，但依赖 SkiaSharp，增加部署复杂度，且自定义阈值带样式受限。
- **XAML 元素拼装**：实现简单，但 150 个点 × 3 条曲线会产生大量视觉元素，刷新时布局开销明显。
