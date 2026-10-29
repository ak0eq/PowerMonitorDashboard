# 素材与文档流水线

项目中的应用图标、文档插图由脚本生成；教学视频为配套的《电力安全操作规程》短片，随程序一起分发，并由程序循环播放。

## 1. 依赖

```powershell
python -m pip install pillow numpy
# ffmpeg 需在 PATH 中
ffmpeg -version
```

- Python 3.11+
- Pillow（绘图、字体）
- numpy（背景渐变）
- ffmpeg（视频抽帧、编码）

## 2. 教学视频

| 文件 | 说明 |
| --- | --- |
| `src/PowerMonitorDashboard.App/Assets/video/safety-training.mp4` | 97.5 秒、1280×720、30 fps、H.264 + AAC 立体声 |
| `src/PowerMonitorDashboard.App/Assets/images/video-poster.png` | 视频封面（第 22 秒画面，由 ffmpeg 抽取） |
| `src/PowerMonitorDashboard.App/Assets/images/app.ico` | 多尺寸应用图标（脚本生成） |
| `src/PowerMonitorDashboard.App/Assets/images/logo.png` | 256×256 图标源（脚本生成） |

### 2.1 章节结构

视频共 7 个章节，深色科技风画面与监控大屏保持一致：

| 章节 | 内容 |
| --- | --- |
| 01 片头 | 电力安全操作规程 · 变电运维现场作业教学片 |
| 02 进入作业现场 | 安全帽、绝缘鞋、工作服；核对工作票与设备双重名称；确认安全措施 |
| 03 倒闸操作 | 严格执行“两票三制”；操作前核对设备名称与编号；逐项执行、全程监护复诵 |
| 04 验电与装设接地线 | 先验电、后接地；验电器自检；接地线装设牢固并记录；悬挂标示牌、装设遮栏 |
| 05 高处与带电作业 | 安全带高挂低用；保持规定安全距离；使用检验合格的绝缘工器具 |
| 06 应急处置 | 触电急救先断电；立即实施心肺复苏；第一时间汇报调度、保护事故现场 |
| 07 结束页 | 安全第一、预防为主、综合治理 |

### 2.2 播放与循环

播放由 `MediaElement`（`LoadedBehavior=Manual`）与 `VideoViewModel` 共同控制：

- `MediaOpened` 读取时长、设置进度条上限并自动播放；
- 播放结束后若“循环”开关为开（默认），执行 `Stop → Position=0 → Play`，同时保持背景渐变不中断；
- 关闭循环时停在最后一帧，再次点击播放会从头开始；
- 进度条拖动过程中只预览时间，松开鼠标后才真正 Seek，避免频繁跳转造成卡顿；
- 缓冲时显示旋转加载动画，加载失败显示错误信息与“重新加载”。

### 2.3 可选：生成演示视频

`tools/media/generate_training_video.py` 可以离线生成一段与项目风格一致的演示短片（用于没有真实素材时替换 `Assets/video/safety-training.mp4`）。脚本输出到 `demo-training.mp4`，不会覆盖正式教学视频：

```powershell
python tools/media/generate_training_video.py
# 输出：Assets/video/demo-training.mp4、Assets/images/video-poster-demo.png
```

若决定使用演示视频，把设置中的“视频文件路径”指向 `Assets/video/demo-training.mp4` 即可，无需改代码。

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

1. 启动后等待 60 秒，积累趋势数据；
2. 停止并重新播放视频，播放到约第 20 秒；
3. 填入实时值与当前时间，截图主界面；
4. 点击“故障演练”，截图告警状态；
5. 输入非法值并保存，截图校验状态；
6. 打开设置窗口并截图。

截图脚本依赖 `UIAutomationClient` 与 `System.Drawing`，仅用于生成文档素材，不随程序发布。
