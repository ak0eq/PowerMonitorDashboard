# 构建、运行与部署

## 1. 环境要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 1809+ / Windows 11 |
| SDK | .NET 10 SDK（`dotnet --list-sdks` 可见 10.x） |
| 运行时 | .NET Desktop Runtime 10（运行已发布程序时需要） |
| 可选 | ffmpeg、Python 3.11+（仅在重新生成视频/插图时需要） |

> 项目使用 `net10.0-windows` 与 `UseWPF`。若需降级到 .NET 8，修改两个 `csproj` 的 `TargetFramework` 即可，源码不依赖 .NET 10 专有 API。

## 2. 常用命令

```powershell
# 还原
dotnet restore PowerMonitorDashboard.sln

# 构建
dotnet build PowerMonitorDashboard.sln -c Release

# 运行
dotnet run --project src/PowerMonitorDashboard.App -c Release

# 测试
dotnet test PowerMonitorDashboard.sln -c Release

# 发布（依赖框架，体积小）
dotnet publish src/PowerMonitorDashboard.App -c Release -r win-x64 --self-contained false -o artifacts/publish

# 发布（自包含，目标机无需安装运行时）
dotnet publish src/PowerMonitorDashboard.App -c Release -r win-x64 --self-contained true -o artifacts/publish-selfcontained
```

一键脚本：

```powershell
./build.ps1                    # restore + build + test
./build.ps1 -Publish           # 额外发布到 artifacts/publish
./build.ps1 -Configuration Debug -SkipTests
```

## 3. 运行产物

发布目录结构：

```
artifacts/publish/
├─ PowerMonitorDashboard.exe
├─ PowerMonitorDashboard.dll
├─ PowerMonitorDashboard.Core.dll
├─ Assets/
│  ├─ images/app.ico, logo.png, video-poster.png
│  └─ video/safety-training.mp4
└─ …（WPF 依赖的 dll）
```

`Assets` 必须随程序一起复制，视频路径默认为 `Assets/video/safety-training.mp4`（相对于程序目录）。

## 4. 数据目录

| 内容 | 路径 |
| --- | --- |
| 设置 | `%LOCALAPPDATA%\PowerMonitorDashboard\settings.json` |
| 历史快照 | `%LOCALAPPDATA%\PowerMonitorDashboard\history.json` |
| 日志 | `%LOCALAPPDATA%\PowerMonitorDashboard\logs\app-yyyyMMdd.log` |
| 导出 | 默认 `%LOCALAPPDATA%\PowerMonitorDashboard\exports`，实际由对话框决定 |

删除上述目录即可恢复出厂状态。程序不会写入自身安装目录（Program Files 只读时同样可以运行）。

## 5. 配置项

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `SamplingIntervalMs` | 1000 | 采样周期（200–60000 ms） |
| `HistoryCapacity` | 5000 | 内存历史容量 |
| `HistoryDisplayLimit` | 500 | 表格显示上限（50–5000） |
| `AutoStartAcquisition` | true | 启动自动采集 |
| `AutoScrollHistory` | true | 历史自动滚动 |
| `ShowQualityColumn` | true | 显示数据质量列 |
| `AssumedPowerFactor` | 0.95 | 跨字段校验假定的 cosφ |
| `VideoRelativePath` | Assets/video/safety-training.mp4 | 视频路径 |
| `OperatorName` | 值班员 | 值班员名称 |
| `Thresholds.*` | 见数据模型文档 | 告警阈值 |
| `Simulation.*` | 见数据模型文档 | 仿真参数 |

配置损坏或字段非法时，程序会回退到默认值并在日志中记录原因；`Normalize` 会把越界值收敛到合法范围。

## 6. 常见问题

### 视频黑屏或“播放失败”

- 确认 `Assets/video/safety-training.mp4` 存在，且与可执行文件相对路径一致。
- Windows N 版或精简系统可能缺少 Media Foundation 解码器，安装 [Media Feature Pack](https://support.microsoft.com/help/3010081) 后重试。
- 可在“设置”中把视频路径改为本机任意 MP4 文件验证。

### 启动时提示“已在运行”

程序使用命名互斥量实现单实例。若确认没有运行中的实例，注销/重启可释放残留句柄。

### 日志中文乱码

日志为 UTF-8（无 BOM）。使用支持 UTF-8 的编辑器打开；Windows 11 记事本可直接识别。

### 高 DPI 下界面模糊

程序清单声明 PerMonitorV2。若使用远程桌面或缩放切换，重新最大化窗口即可触发重新布局。

### 权限问题

程序以 `asInvoker` 运行，不需要管理员权限。若 `%LOCALAPPDATA%` 被策略限制，启动会提示失败并记录日志。
