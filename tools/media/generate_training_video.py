"""生成教学视频与应用图标资源。

该脚本属于项目的“素材流水线”，可重复执行：
    python tools/media/generate_training_video.py

输出：
    src/PowerMonitorDashboard.App/Assets/video/demo-training.mp4
    src/PowerMonitorDashboard.App/Assets/images/video-poster-demo.png
    src/PowerMonitorDashboard.App/Assets/images/app.ico
    src/PowerMonitorDashboard.App/Assets/images/logo.png
"""

from __future__ import annotations

import math
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.stdout.reconfigure(encoding="utf-8")

WIDTH, HEIGHT = 1280, 720
FPS = 25

ROOT = Path(__file__).resolve().parents[2]
APP_ROOT = ROOT / "src" / "PowerMonitorDashboard.App"
VIDEO_DIR = APP_ROOT / "Assets" / "video"
IMAGE_DIR = APP_ROOT / "Assets" / "images"

FONT_REGULAR = "C:/Windows/Fonts/msyh.ttc"
FONT_BOLD = "C:/Windows/Fonts/msyhbd.ttc"
FONT_MONO = "C:/Windows/Fonts/consola.ttf"

CYAN = (34, 211, 238)
CYAN_DIM = (34, 211, 238, 120)
BLUE = (59, 130, 246)
WHITE = (232, 244, 255)
MUTED = (147, 174, 196)
DARK = (5, 11, 18)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, size)


def mono(size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONT_MONO, size)


def ease_out_cubic(x: float) -> float:
    return 1.0 - pow(1.0 - max(0.0, min(1.0, x)), 3)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def make_background() -> Image.Image:
    """深色科技感背景：垂直渐变 + 网格 + 冷色光晕。"""
    top = np.array([5, 11, 18], dtype=np.float32)
    bottom = np.array([9, 34, 49], dtype=np.float32)
    rows = np.linspace(0.0, 1.0, HEIGHT, dtype=np.float32)[:, None]
    base = top[None, :] * (1 - rows) + bottom[None, :] * rows
    base = np.repeat(base[:, None, :], WIDTH, axis=1)

    # 左上角与右下角的光晕
    yy, xx = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float32)
    glow1 = np.exp(-(((xx - 180) ** 2 + (yy - 90) ** 2) / (2 * 300.0 ** 2)))
    glow2 = np.exp(-(((xx - 1120) ** 2 + (yy - 640) ** 2) / (2 * 340.0 ** 2)))
    base += glow1[:, :, None] * np.array([10, 60, 78], dtype=np.float32)
    base += glow2[:, :, None] * np.array([8, 36, 70], dtype=np.float32)

    image = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB")

    grid = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(grid)
    for x in range(0, WIDTH, 64):
        draw.line([(x, 0), (x, HEIGHT)], fill=(34, 211, 238, 10), width=1)
    for y in range(0, HEIGHT, 64):
        draw.line([(0, y), (WIDTH, y)], fill=(34, 211, 238, 10), width=1)
    image = Image.alpha_composite(image.convert("RGBA"), grid).convert("RGB")
    return image


BACKGROUND = make_background()


@dataclass
class Scene:
    start: float
    duration: float
    chapter: str
    title: str
    bullets: tuple[str, ...]
    icon: str
    intro: bool = False
    outro: bool = False


SCENES: tuple[Scene, ...] = (
    Scene(0.0, 4.5, "课程导览", "电力安全工作规程", ("变电运维现场安全操作示范",), "bolt", intro=True),
    Scene(4.5, 6.5, "第一章", "停电、验电与接地", (
        "断开断路器与隔离开关，确认机械位置",
        "使用合格验电器逐相验电，确认无电压",
        "装设接地线前再次确认设备状态",
    ), "switch"),
    Scene(11.0, 6.5, "第二章", "绝缘工器具检查", (
        "检查绝缘手套气密性与试验合格标签",
        "绝缘杆表面无划伤、无受潮痕迹",
        "每次使用前进行外观检查并登记",
    ), "glove"),
    Scene(17.5, 6.5, "第三章", "高处作业防护", (
        "安全带必须高挂低用并可靠固定",
        "作业前检查构架强度与防坠装置",
        "严禁失去保护攀爬或跨越护栏",
    ), "harness"),
    Scene(24.0, 6.5, "第四章", "工作票与监护制度", (
        "工作票内容与现场设备状态一致",
        "监护人不得兼做其他工作或擅自离岗",
        "变更安全措施须重新履行审批",
    ), "permit"),
    Scene(30.5, 6.5, "第五章", "触电急救与应急响应", (
        "立即切断电源或用绝缘物使触电者脱离",
        "判断意识与呼吸，立即呼救并拨打 120",
        "持续心肺复苏直至专业救援到达",
    ), "firstaid"),
    Scene(37.0, 5.0, "课程小结", "安全第一  预防为主", ("规范操作 · 全程监护 · 快速响应",), "bolt", outro=True),
)

DURATION = SCENES[-1].start + SCENES[-1].duration


def draw_icon(draw: ImageDraw.ImageDraw, kind: str, cx: int, cy: int, scale: float, color) -> None:
    """在指定位置绘制简化的电力安全图标。"""
    w = max(2, int(5 * scale))
    if kind == "bolt":
        points = [(cx + 6 * scale, cy - 90 * scale), (cx - 38 * scale, cy + 12 * scale),
                  (cx - 2 * scale, cy + 12 * scale), (cx - 12 * scale, cy + 90 * scale),
                  (cx + 40 * scale, cy - 22 * scale), (cx + 4 * scale, cy - 22 * scale)]
        draw.polygon(points, fill=color)
    elif kind == "switch":
        draw.rounded_rectangle([cx - 90 * scale, cy - 60 * scale, cx + 90 * scale, cy + 60 * scale],
                               radius=12 * scale, outline=color, width=w)
        draw.line([(cx - 60 * scale, cy + 30 * scale), (cx + 55 * scale, cy - 35 * scale)], fill=color, width=w)
        draw.ellipse([cx - 70 * scale, cy + 20 * scale, cx - 50 * scale, cy + 40 * scale], fill=color)
        draw.ellipse([cx + 45 * scale, cy - 45 * scale, cx + 65 * scale, cy - 25 * scale], fill=color)
        for offset in (-40, 0, 40):
            draw.line([(cx + offset * scale, cy + 60 * scale), (cx + offset * scale, cy + 90 * scale)],
                      fill=(245, 185, 62), width=w)
    elif kind == "glove":
        draw.rounded_rectangle([cx - 55 * scale, cy - 70 * scale, cx + 25 * scale, cy + 60 * scale],
                               radius=26 * scale, outline=color, width=w)
        draw.rounded_rectangle([cx + 12 * scale, cy - 20 * scale, cx + 70 * scale, cy + 34 * scale],
                               radius=18 * scale, outline=color, width=w)
        for i in range(3):
            x = cx - 40 * scale + i * 26 * scale
            draw.line([(x, cy - 70 * scale), (x, cy - 20 * scale)], fill=color, width=w)
    elif kind == "harness":
        draw.arc([cx - 80 * scale, cy - 100 * scale, cx + 80 * scale, cy + 60 * scale], 200, 340, fill=color, width=w)
        draw.line([(cx - 55 * scale, cy - 20 * scale), (cx - 20 * scale, cy + 70 * scale)], fill=color, width=w)
        draw.line([(cx + 55 * scale, cy - 20 * scale), (cx + 20 * scale, cy + 70 * scale)], fill=color, width=w)
        draw.rounded_rectangle([cx - 34 * scale, cy + 30 * scale, cx + 34 * scale, cy + 80 * scale],
                               radius=8 * scale, outline=color, width=w)
        draw.arc([cx + 40 * scale, cy - 60 * scale, cx + 100 * scale, cy], 0, 180, fill=(245, 185, 62), width=w)
    elif kind == "permit":
        draw.rounded_rectangle([cx - 65 * scale, cy - 85 * scale, cx + 65 * scale, cy + 85 * scale],
                               radius=8 * scale, outline=color, width=w)
        for i in range(5):
            y = cy - 50 * scale + i * 26 * scale
            draw.line([(cx - 42 * scale, y), (cx + 42 * scale, y)], fill=(147, 174, 196), width=max(1, int(3 * scale)))
        draw.ellipse([cx + 18 * scale, cy + 28 * scale, cx + 58 * scale, cy + 68 * scale], outline=(245, 185, 62), width=w)
    elif kind == "firstaid":
        draw.rounded_rectangle([cx - 80 * scale, cy - 80 * scale, cx + 80 * scale, cy + 80 * scale],
                               radius=16 * scale, outline=color, width=w)
        draw.rectangle([cx - 14 * scale, cy - 50 * scale, cx + 14 * scale, cy + 50 * scale], fill=(255, 93, 108))
        draw.rectangle([cx - 50 * scale, cy - 14 * scale, cx + 50 * scale, cy + 14 * scale], fill=(255, 93, 108))


def draw_scene(image: Image.Image, scene: Scene, local_t: float) -> Image.Image:
    draw = ImageDraw.Draw(image, "RGBA")
    progress = min(1.0, local_t / max(0.001, scene.duration))
    fade_in = ease_out_cubic(local_t / 0.6)
    fade_out = 1.0 - ease_out_cubic(max(0.0, (local_t - (scene.duration - 0.6)) / 0.6))
    alpha = fade_in * fade_out

    # 顶部与底部信息
    draw.text((64, 44), "电力安全工作规程 · 教学视频", font=font(20), fill=(147, 174, 196, int(220 * alpha)))
    draw.text((WIDTH - 64, 44), "220 kV 中心变电站", font=font(20), fill=(147, 174, 196, int(220 * alpha)),
              anchor="ra")

    # 章节标签
    label = f"{scene.chapter}   {int(progress * 100):02d}%"
    draw.text((64, 150), label, font=font(18), fill=(34, 211, 238, int(230 * alpha)))

    if scene.intro or scene.outro:
        title_font = font(64, bold=True)
        subtitle_font = font(26)
        draw.text((WIDTH // 2, HEIGHT // 2 - 40), scene.title, font=title_font, fill=(232, 244, 255, int(255 * alpha)),
                  anchor="mm")
        draw.text((WIDTH // 2, HEIGHT // 2 + 40), scene.bullets[0], font=subtitle_font,
                  fill=(147, 174, 196, int(240 * alpha)), anchor="mm")
        draw.line([(WIDTH // 2 - 180, HEIGHT // 2 + 86), (WIDTH // 2 + 180, HEIGHT // 2 + 86)],
                  fill=(34, 211, 238, int(160 * alpha)), width=2)
        if scene.outro:
            draw.text((WIDTH // 2, HEIGHT // 2 + 140), "规范操作 · 全程监护 · 快速响应", font=font(22),
                      fill=(245, 185, 62, int(230 * alpha)), anchor="mm")
        # 中心图标
        pulse = 1.0 + 0.04 * math.sin(local_t * 3.2)
        draw_icon(draw, scene.icon, WIDTH // 2, HEIGHT // 2 - 210, 0.55 * pulse, (34, 211, 238, int(230 * alpha)))
    else:
        draw.text((64, 182), scene.title, font=font(46, bold=True), fill=(232, 244, 255, int(255 * alpha)))

        # 要点逐条淡入
        y = 300
        for index, bullet in enumerate(scene.bullets):
            appear = ease_out_cubic((local_t - 0.5 - index * 0.35) / 0.5)
            if appear <= 0:
                continue
            shift = int((1 - appear) * 24)
            draw.ellipse([64, y + 6, 74, y + 16], outline=(34, 211, 238, int(240 * alpha * appear)), width=2)
            draw.text((90 + shift, y), bullet, font=font(24), fill=(232, 244, 255, int(245 * alpha * appear)))
            y += 62

        # 右侧图标面板
        panel = [780, 150, 1200, 570]
        draw.rounded_rectangle(panel, radius=18, fill=(10, 30, 44, int(190 * alpha)),
                               outline=(34, 211, 238, int(90 * alpha)), width=1)
        cx, cy = 990, 360
        for radius in (170, 130):
            draw.ellipse([cx - radius, cy - radius, cx + radius, cy + radius],
                         outline=(34, 211, 238, int(38 * alpha)), width=1)
        draw_icon(draw, scene.icon, cx, cy, 1.15, (34, 211, 238, int(235 * alpha)))

    # 底部进度条
    bar_y = HEIGHT - 46
    draw.line([(64, bar_y), (WIDTH - 64, bar_y)], fill=(30, 62, 82, 220), width=2)
    filled = int((WIDTH - 128) * progress)
    draw.line([(64, bar_y), (64 + filled, bar_y)], fill=(34, 211, 238, 235), width=3)
    draw.text((64, bar_y + 14), f"{scene.chapter}", font=font(15), fill=(100, 128, 154, 220))
    draw.text((WIDTH - 64, bar_y + 14), "安全培训 · 内部资料", font=font(15), fill=(100, 128, 154, 220), anchor="ra")
    return image


def render_frames(frame_dir: Path) -> None:
    total = int(DURATION * FPS)
    for index in range(total):
        t = index / FPS
        scene = next(s for s in SCENES if s.start <= t < s.start + s.duration)
        local = t - scene.start
        image = BACKGROUND.copy()
        image = draw_scene(image, scene, local)

        # 场景切换时短暂压暗
        edge = min(local, scene.duration - local)
        if edge < 0.35:
            shade = Image.new("RGBA", image.size, (0, 0, 0, int(90 * (1 - edge / 0.35))))
            image = Image.alpha_composite(image.convert("RGBA"), shade).convert("RGB")

        image.save(frame_dir / f"{index:05d}.png")

        if index % 100 == 0:
            print(f"  frame {index}/{total}")


def encode_video(frame_dir: Path, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    command = [
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-framerate", str(FPS), "-i", str(frame_dir / "%05d.png"),
        "-f", "lavfi", "-i", "anullsrc=channel_layout=stereo:sample_rate=44100",
        "-shortest", "-c:v", "libx264", "-preset", "medium", "-crf", "21",
        "-pix_fmt", "yuv420p", "-movflags", "+faststart",
        "-c:a", "aac", "-b:a", "96k", str(output),
    ]
    subprocess.run(command, check=True)
    print(f"  视频已生成：{output} ({output.stat().st_size / 1024 / 1024:.1f} MB)")


def render_poster(frame_dir: Path, output: Path) -> None:
    source = frame_dir / f"{int(6.0 * FPS):05d}.png"
    Image.open(source).save(output)
    print(f"  封面已生成：{output}")


def render_icon(output_ico: Path, output_logo: Path) -> None:
    """绘制应用图标：深色圆角底 + 青蓝渐变闪电。"""
    size = 256
    icon = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(icon)

    for y in range(size):
        ratio = y / size
        color = (
            int(lerp(10, 16, ratio)),
            int(lerp(30, 74, ratio)),
            int(lerp(44, 96, ratio)),
            255,
        )
        draw.line([(0, y), (size, y)], fill=color)

    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=56, fill=255)
    icon.putalpha(mask)

    bolt = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bolt_draw = ImageDraw.Draw(bolt)
    points = [(140, 34), (74, 140), (118, 140), (104, 222), (186, 104), (138, 104)]
    bolt_draw.polygon(points, fill=(34, 211, 238, 255))
    bolt = bolt.filter(ImageFilter.GaussianBlur(0.6))
    icon = Image.alpha_composite(icon, bolt)

    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(glow).polygon(points, fill=(34, 211, 238, 90))
    glow = glow.filter(ImageFilter.GaussianBlur(14))
    icon = Image.alpha_composite(icon, glow)

    icon.save(output_logo)
    icon.save(output_ico, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print(f"  图标已生成：{output_ico} / {output_logo}")


def main() -> None:
    IMAGE_DIR.mkdir(parents=True, exist_ok=True)
    VIDEO_DIR.mkdir(parents=True, exist_ok=True)

    frame_dir = Path(tempfile.mkdtemp(prefix="pmd-frames-"))
    try:
        print(f"渲染 {DURATION:.1f} 秒视频到临时目录 {frame_dir}")
        render_frames(frame_dir)
        encode_video(frame_dir, VIDEO_DIR / "demo-training.mp4")
        render_poster(frame_dir, IMAGE_DIR / "video-poster-demo.png")
    finally:
        shutil.rmtree(frame_dir, ignore_errors=True)

    render_icon(IMAGE_DIR / "app.ico", IMAGE_DIR / "logo.png")


if __name__ == "__main__":
    main()
