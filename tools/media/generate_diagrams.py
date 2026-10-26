"""生成技术文档中的架构图与数据流图。

用法：
    python tools/media/generate_diagrams.py

输出：
    docs/assets/architecture.png
    docs/assets/data-flow.png
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "assets"
OUT.mkdir(parents=True, exist_ok=True)

FONT = "C:/Windows/Fonts/msyh.ttc"
FONT_BOLD = "C:/Windows/Fonts/msyhbd.ttc"

BG = (247, 249, 252)
INK = (29, 41, 57)
MUTED = (100, 116, 139)
LINE = (148, 163, 184)

LAYERS = {
    "ui": ((37, 99, 235), (239, 246, 255)),
    "app": ((8, 145, 178), (236, 254, 255)),
    "core": ((124, 58, 237), (245, 243, 255)),
    "res": ((217, 119, 6), (255, 251, 235)),
}


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONT_BOLD if bold else FONT, size)


def rounded(draw: ImageDraw.ImageDraw, box, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def text(draw, xy, value, size=18, color=INK, bold=False, anchor="la"):
    draw.text(xy, value, font=font(size, bold), fill=color, anchor=anchor)


def arrow(draw, start, end, color=LINE, width=2, dashed=False):
    x1, y1 = start
    x2, y2 = end
    if dashed:
        length = math.hypot(x2 - x1, y2 - y1)
        step = 12
        count = max(1, int(length // step))
        for i in range(count):
            t0 = i / count
            t1 = min(1.0, (i + 0.55) / count)
            draw.line(
                [(x1 + (x2 - x1) * t0, y1 + (y2 - y1) * t0),
                 (x1 + (x2 - x1) * t1, y1 + (y2 - y1) * t1)],
                fill=color, width=width)
    else:
        draw.line([start, end], fill=color, width=width)

    angle = math.atan2(y2 - y1, x2 - x1)
    size = 9
    left = (x2 - size * math.cos(angle - 0.5), y2 - size * math.sin(angle - 0.5))
    right = (x2 - size * math.cos(angle + 0.5), y2 - size * math.sin(angle + 0.5))
    draw.polygon([end, left, right], fill=color)


def draw_architecture() -> None:
    width, height = 1600, 940
    image = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(image)

    text(draw, (60, 44), "PowerMonitorDashboard 分层架构", 34, INK, bold=True)
    text(draw, (60, 90), "表现层只负责展示与交互，业务规则集中在与 UI 无关的核心层，应用层负责装配与调度。",
         18, MUTED)

    bands = [
        ("表现层  WPF", "ui", 140, 312, [
            ("MainWindow", "SettingsWindow"),
            ("ViewModels", "MVVM"),
            ("Converters", "ValidationRule"),
            ("TrendChart", "LoadBars"),
            ("Themes", "资源字典"),
        ]),
        ("应用层  装配与调度", "app", 372, 544, [
            ("AppHost", "组合根"),
            ("MonitoringService", "处理管线"),
            ("Simulation", "TelemetrySource"),
            ("Infrastructure", "JSON/日志/调度"),
        ]),
        ("核心层  领域与规则", "core", 604, 776, [
            ("Domain", "领域模型"),
            ("Validation", "校验规则"),
            ("Monitoring", "状态/告警"),
            ("History", "Export"),
            ("Simulation", "仿真器"),
        ]),
    ]

    for name, key, top, bottom, boxes in bands:
        accent, tint = LAYERS[key]
        rounded(draw, (60, top, 1240, bottom), 16, tint, (226, 232, 240), 1)
        rounded(draw, (60, top, 76, bottom), 8, accent)
        text(draw, (96, top + 18), name, 22, accent, bold=True)

        count = len(boxes)
        gap = 14
        left = 300
        right = 1220
        box_width = (right - left - gap * (count - 1)) / count
        for index, (title, subtitle) in enumerate(boxes):
            x = left + index * (box_width + gap)
            rounded(draw, (x, top + 64, x + box_width, top + 152), 10, (255, 255, 255), (203, 213, 225))
            rounded(draw, (x, top + 64, x + box_width, top + 70), 3, accent)
            text(draw, (x + box_width / 2, top + 92), title, 16, INK, bold=True, anchor="ma")
            text(draw, (x + box_width / 2, top + 118), subtitle, 14, MUTED, anchor="ma")

    for x in (520, 880):
        arrow(draw, (x, 312), (x, 368), (100, 116, 139), 2)
        arrow(draw, (x + 16, 368), (x + 16, 312), (100, 116, 139), 2)
        arrow(draw, (x, 544), (x, 600), (100, 116, 139), 2)
        arrow(draw, (x + 16, 600), (x + 16, 544), (100, 116, 139), 2)

    accent, tint = LAYERS["res"]
    rounded(draw, (1300, 140, 1540, 776), 16, tint, (226, 232, 240))
    rounded(draw, (1300, 140, 1316, 776), 8, accent)
    text(draw, (1336, 168), "本地资源", 22, accent, bold=True)
    resources = ["教学视频 MP4", "settings.json", "history.json", "logs/*.log", "exports/*.csv"]
    for index, item in enumerate(resources):
        y = 226 + index * 100
        rounded(draw, (1336, y, 1516, y + 58), 8, (255, 255, 255), (203, 213, 225))
        text(draw, (1350, y + 18), item, 15, INK)
    arrow(draw, (1246, 458), (1294, 458), accent, 2, dashed=True)
    text(draw, (1270, 428), "读写", 13, MUTED, anchor="ma")

    text(draw, (60, 828), "依赖方向：表现层 → 应用层 → 核心层；核心层通过事件/返回值向上反馈，不反向引用 UI。",
         17, MUTED)
    text(draw, (60, 860), "跨线程边界：采集在后台 Task 中运行，结果经 IUiDispatcher 切回 UI 线程后更新绑定。",
         17, MUTED)
    text(draw, (60, 892), "可替换点：ITelemetrySource 允许用 Modbus / OPC UA 实现替换内置仿真源，其余代码不变。",
         17, MUTED)

    image.save(OUT / "architecture.png", optimize=True)
    print("wrote", OUT / "architecture.png")


def draw_data_flow() -> None:
    width, height = 1600, 960
    image = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(image)

    text(draw, (60, 44), "实时数据处理时序", 34, INK, bold=True)
    text(draw, (60, 90), "从仿真数据生成到界面刷新的完整链路，以及线程边界与命令回传。", 18, MUTED)

    stages = [
        ("①", "TelemetrySimulator", "按典型日负荷曲线、一次调频特性生成物理自洽的一帧数据", "核心层 · 纯函数，可注入种子完全复现"),
        ("②", "SimulationTelemetrySource", "后台 Task 按采样周期循环，触发 ReadingProduced 事件", "应用层 · 采集循环（后台线程）"),
        ("③", "MonitoringService", "状态评估 → 告警状态机 → 历史缓冲 → 电量累计", "应用层 · 串行处理，线程安全"),
        ("④", "MainViewModel", "经 IUiDispatcher 切回 UI 线程，刷新指标/拓扑/趋势/表格", "表现层 · 处理 ReadingProcessed 结果"),
        ("⑤", "WPF Views", "数据绑定驱动界面刷新，命令回传实现交互", "表现层 · 声明式绑定 + MVVM 命令"),
    ]

    left = 330
    width_box = 780
    top = 150
    step = 150
    for index, (number, title, description, note) in enumerate(stages):
        y = top + index * step
        accent = (37, 99, 235) if index >= 3 else (8, 145, 178)
        rounded(draw, (left, y, left + width_box, y + 98), 12, (255, 255, 255), (203, 213, 225))
        rounded(draw, (left, y, left + width_box, y + 6), 3, accent)
        draw.ellipse([left + 20, y + 28, left + 62, y + 70], fill=accent)
        text(draw, (left + 41, y + 37), number, 20, (255, 255, 255), bold=True, anchor="ma")
        text(draw, (left + 80, y + 20), title, 20, INK, bold=True)
        text(draw, (left + 80, y + 50), description, 15, (51, 65, 85))
        text(draw, (left + 80, y + 74), note, 13, MUTED)

        if index < len(stages) - 1:
            arrow(draw, (left + width_box / 2, y + 100), (left + width_box / 2, y + step - 6), (100, 116, 139), 2)

    # 线程边界
    rounded(draw, (70, 150, 210, 548), 12, (236, 254, 255), (165, 243, 252))
    text(draw, (140, 172), "后台线程", 18, (8, 145, 178), bold=True, anchor="ma")
    text(draw, (140, 208), "Task.Run", 14, (8, 145, 178), anchor="ma")
    text(draw, (140, 230), "Periodic", 14, (8, 145, 178), anchor="ma")
    rounded(draw, (70, 600, 210, 848), 12, (239, 246, 255), (191, 219, 254))
    text(draw, (140, 622), "UI 线程", 18, (37, 99, 235), bold=True, anchor="ma")
    text(draw, (140, 658), "数据绑定", 14, (37, 99, 235), anchor="ma")
    text(draw, (140, 680), "命令/动画", 14, (37, 99, 235), anchor="ma")

    rounded(draw, (70, 876, 1530, 930), 10, (255, 255, 255), (226, 232, 240))
    text(draw, (90, 894), "线程模型：①②③ 运行在后台采集线程；④ 由 IUiDispatcher 调度到 UI 线程；⑤ 及命令回传全程在 UI 线程。",
         16, MUTED)

    image.save(OUT / "data-flow.png", optimize=True)
    print("wrote", OUT / "data-flow.png")


if __name__ == "__main__":
    draw_architecture()
    draw_data_flow()
