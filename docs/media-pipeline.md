# 素材生成流水线

项目中的教学视频、应用图标与文档插图都由脚本生成，可重复执行、无需手工修图。

## 1. 依赖

```powershell
python -m pip install pillow numpy
# ffmpeg 需在 PATH 中
ffmpeg -version
```

- Python 3.11+
- Pillow（绘图、字体）
- numpy（背景渐变）
- ffmpeg（H.264 编码）

## 2. 教学视频

```powershell
python tools/media/generate_training_video.py
```

输出：

| 文件 | 说明 |
| --- | --- |
| `src/PowerMonitorDashboard.App/Assets/video/safety-training.mp4` | 42 秒、1280×720、25 fps、H.264 + AAC |
| `src/PowerMonitorDashboard.App/Assets/images/video-poster.png` | 视频封面（第 6 秒画面） |
| `src/PowerMonitorDashboard.App/Assets/images/app.ico` | 多尺寸应用图标 |
| `src/PowerMonitorDashboard.App/Assets/images/logo.png` | 256×256 图标源 |

### 2.1 分镜

| 时间 | 章节 | 内容 |
| --- | --- | --- |
| 0–4.5 s | 课程导览 | 标题“电力安全工作规程” |
| 4.5–11 s | 第一章 | 停电、验电与接地 |
| 11–17.5 s | 第二章 | 绝缘工器具检查 |
| 17.5–24 s | 第三章 | 高处作业防护 |
| 24–30.5 s | 第四章 | 工作票与监护制度 |
| 30.5–37 s | 第五章 | 触电急救与应急响应 |
| 37–42 s | 课程小结 | 安全第一、预防为主 |

画面由 PIL 逐帧绘制：深色渐变背景、网格、章节标题、要点逐条淡入、矢量图标、进度条与场景淡入淡出。编码参数：

```
ffmpeg -framerate 25 -i frames/%05d.png -f lavfi -i anullsrc \
  -shortest -c:v libx264 -preset medium -crf 21 -pix_fmt yuv420p \
  -movflags +faststart -c:a aac -b:a 96k safety-training.mp4
```

- `yuv420p` 保证 Windows Media Foundation 兼容；
- 附带静音 AAC 音轨，避免部分播放器对纯视频流的兼容问题；
- `+faststart` 便于 `MediaElement` 立即起播。

## 3. 文档插图

```powershell
python tools/media/generate_diagrams.py
```

输出：

| 文件 | 用途 |
| --- | --- |
| `docs/assets/architecture.png` | 分层架构图 |
| `docs/assets/data-flow.png` | 实时数据处理时序图 |

插图采用浅色背景，适合打印与 Word 文档；图中文字与实现保持一致。

## 4. 界面截图

界面截图通过 UI Automation 驱动真实程序生成（非设计稿），包括：

1. 启动后等待 60 秒，积累趋势与视频进度；
2. 重新播放视频、填入实时值；
3. 截图主界面；
4. 点击“故障演练”，截图告警状态；
5. 输入非法值并保存，截图校验状态；
6. 打开设置窗口并截图。

截图脚本依赖 `UIAutomationClient` 与 `System.Drawing`，仅用于生成文档素材，不随程序发布。
