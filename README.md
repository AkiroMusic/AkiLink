# AkiLink — Bluetooth Audio Receiver for Windows

**AkiLink** is a modern WPF desktop application that turns your Windows PC into a high-quality Bluetooth audio receiver. It uses the WinRT `AudioPlaybackConnection` API to receive streaming audio from paired Bluetooth devices (phones, tablets, etc.) with low latency and minimal setup.

---

## Features

- **Device Discovery** — Scan for paired Bluetooth audio devices using `AudioPlaybackConnection.GetDeviceSelector()`
- **One-Click Connect / Disconnect** — Establish or tear down audio connections with a single click
- **Real-Time Status** — Monitor connection state changes (connected / disconnected / error) as they happen
- **System Tray Support** — Minimize to tray on close; audio continues in the background
- **Volume Control** — Master volume slider + mute toggle, synced to Windows system audio
- **Audio Codec Settings**
  - Preferred codec selection (Auto / SBC / AAC / aptX / LDAC)
  - Bitrate selection (up to 990 kbps)
  - Sample rate selection (44100 / 48000 Hz)
  - Transmission mode (Balanced / Best Quality / Low Latency)
- **Auto-Reconnect** — Automatically reconnects when the paired device comes back in range
- **Connection History** — Full log of connect/disconnect/error events with delete and clear support
- **Compact Mode** — Minimal overlay UI for always-on-top monitoring
- **Localization** — English and Chinese (中文) UI languages, switchable at runtime
- **Dark Theme** — Modern dark UI with card-style layout throughout

## Screenshots

*(Coming soon)*

## Architecture

```
AkiLink/
├── Converters/          # XAML value converters
│   ├── BoolToBrushConverter.cs
│   ├── BoolToOpacityConverter.cs
│   ├── BoolToVisibilityConverter.cs
│   ├── ConnectionEventTypeToTextConverter.cs
│   ├── ConnectionStateToColorConverter.cs
│   ├── InverseBoolConverter.cs
│   ├── PercentToSignalOpacityConverter.cs
│   ├── ViewToActiveBrushConverter.cs
│   └── ViewTypeEqualityConverter.cs
├── Helpers/             # (placeholder for future helpers)
├── Models/              # Data models and enums
│   ├── AudioCodecSettings.cs
│   ├── BluetoothDeviceInfo.cs
│   ├── ConnectionHistoryEntry.cs
│   └── ConnectionQuality.cs
├── Resources/           # Icons and locale strings
│   ├── AkiLink.ico      # App icon (multi-size 16/24/32/48/64/128/256, 32bpp ARGB)
│   ├── AkiLink.png      # Source PNG (1254×1254, transparent background)
│   ├── Locale.en-US.xaml
│   └── Locale.zh-CN.xaml
├── Services/            # Core business logic
│   ├── AudioVolumeService.cs        # Windows CoreAudio COM wrapper
│   ├── BluetoothAudioService.cs     # WinRT AudioPlaybackConnection
│   ├── IAudioVolumeService.cs       # Volume service interface
│   ├── IBluetoothAudioService.cs    # Bluetooth service interface
│   ├── IDialogService.cs            # Dialog abstraction for testing
│   ├── LocalizationService.cs       # Runtime language switching
│   └── SystemTrayService.cs         # System tray icon + context menu
├── Styles/              # XAML resources and control templates
│   ├── Brushes.xaml      # Color palette (dark + light)
│   ├── Controls.xaml     # All control styles
│   └── ModernTheme.xaml  # Theme entry point
├── ViewModels/          # MVVM view models
│   └── MainViewModel.cs
├── Views/               # XAML user controls
│   ├── HistoryPanel.xaml
│   ├── HistoryPanel.xaml.cs
│   ├── SettingsPanel.xaml
│   └── SettingsPanel.xaml.cs
├── tests/               # Unit tests (xUnit + Moq)
│   └── AkiLink.Tests/
│       ├── MainViewModelTests.cs   # 48 tests
│       └── SettingsServiceTests.cs # 6 tests
├── App.xaml             # Application entry + resources
├── App.xaml.cs          # Startup / shutdown orchestration
├── IconGeometries.cs    # SVG path data for sidebar icons
├── MainWindow.xaml      # Main window layout
├── MainWindow.xaml.cs   # Window lifecycle + minimize-to-tray
├── app.manifest         # DPI awareness (PerMonitorV2)
├── global.json          # .NET SDK pinning
├── Directory.Build.props
└── .gitignore
```

## Requirements

- **Windows 10** (build 19041+) or **Windows 11**
- **.NET 10 SDK** (or later; pinned to `10.0.301` in `global.json`)
- Bluetooth adapter supporting audio playback connections

## Getting Started

```bash
# Build
dotnet build

# Run
dotnet run

# Test
dotnet test
```

The app uses `net10.0-windows10.0.19041.0` to access WinRT APIs (`Windows.Devices.Enumeration`, `Windows.Media.Audio`).

## Testing

65 unit tests covering:

- Constructor initialization and service subscription
- Device scanning states (success, failure, empty results)
- Connection lifecycle (connect, disconnect, auto-reconnect)
- Per-device connection state (connected flag set/cleared on the device that actually holds the connection)
- Volume and mute synchronization (guard against infinite loops)
- Settings persistence (loads saved non-default settings without clobbering them at startup)
- Mute/volume changes persist to settings (toggle mute, external volume/mute changes)
- Settings coalesced file persistence (background flush, atomic write, corrupt-file fallback)
- Connection history (clear with confirmation, delete entry, HasHistory tracking, detail-row HasDetail)
- Codec settings propagation
- `CanExecute` logic for connect/disconnect buttons

Run with:

```bash
dotnet test ./tests/AkiLink.Tests
```

## Version History

- **v1.1.7** — "Ethereal Glass" redesign: the UI now follows the Aki Design System — Plus Jakarta Sans for UI text, Fraunces (serif) for card titles, IBM Plex Mono for numeric values (9 static TTFs embedded via pack URIs, name tables repaired so the family loads reliably); a new dark palette (`#0E1016` background, `#171A23` / `#212531` surfaces, `#6C8CFF` accent, `#A78BFA` secondary); cards are 24px-radius with 24px padding, a hairline border plus a recessed 5px inner hairline drawn by the new `CardBorder` control (double-frame look); the sidebar became a Material 3 navigation rail (80px wide, 52px buttons, radius 14, 21px icons, 10px labels) with an accent-tinted active pill; a 40px frosted-glass title bar and a 28px glass status bar; two ambient radial glow gradients (accent top-left, accent-secondary bottom-right) replace the old static star field; and the volume percentage now renders in IBM Plex Mono per the numeric-value rule.
- **v1.1.6** — New app icon with a transparent background: the icon is now a 1254×1254 ARGB PNG and the `.ico` was rebuilt as a proper multi-size set (16/24/32/48/64/128/256) instead of the previous single 16×16 frame, so the exe, taskbar, and system-tray icons render crisply at every DPI. The title-bar icon now also benefits from the alpha channel.
- **v1.1.5** — Single-instance guard + UI polish: a named Mutex held for the process lifetime enforces one running instance — launching a second instance restores + foregrounds the existing window and exits, eliminating duplicate processes, duplicate tray icons, and conflicting `AudioPlaybackConnection`s on the same adapter; the header bar (app title + connection status) was removed for a more compact layout; the decorative star background lost its animation (the entire `<Window.Triggers>` storyboard and per-star scale transforms were deleted) and the 8 stars now sit static in the bottom-right at 0.12 opacity; the device list shows a per-device "connected" status row (green dot + label + realtime codec), tracked via a dedicated `_connectedDevice` field so the flag clears correctly even when the selection changes between connect and disconnect (4 new tests); the Quality Guide card was redesigned as a flat informational panel with a left accent rail and a TIP badge (new `QualityGuideBadge` key, EN + ZH); scrollbars were slimmed (6px → 3px, thumb 24px → 12px) and the settings list no longer stretches its content horizontally.
- **v1.1.4** — Fix 10 audit findings: tray "Quit" no longer leaves a zombie window (real exit via `AllowClose` gate); mute toggles and external volume/mute changes now persist to settings; settings writes are coalesced onto a background thread with atomic file replacement (no more per-slider-delta synchronous disk I/O on the UI thread) plus a synchronous flush on exit; history detail rows render when an error message is present (`HasDetail`); maximize now respects the secondary monitor's work area; CoreAudio COM objects are released on every init failure path (no RCW leaks); `BluetoothAudioService` gained a disposed guard, a defensive try/catch in the state handler, and a bounded dispose-wait timeout; history-clear confirmation is localized (EN + ZH); the Win11 compatibility GUID is documented (Windows 11 shares the Windows 10 GUID). Also wired `IDialogService` into DI and added 15 new unit tests (48 MainViewModel + 6 SettingsService) for the persistence and coalescing behavior.
- **v1.1.3** — Fix volume slider fighting user input: CoreAudio callbacks now filter out the app's own volume/mute changes via a dedicated `AppEventContext` GUID (dragging the slider is no longer echoed back and snapped); the slider also gets explicit `SmallChange="0.01"` / `LargeChange="0.1"` — WPF defaults both to `1.0`, which with `Maximum=1` made one track click jump straight to 0% or 100%. Track hit-area enlarged 4px → 32px, and the track background is now declared before the Track so the 4px bar renders behind the 16px thumb circle instead of crossing through it. Verified with a COM-level probe, an end-to-end event harness, and a WPF command probe (track click ±10%, keyboard ±1%, zero echo-back).
- **v1.1.2** — Fix Bluetooth reconnect loops and device corruption: serialized connection teardown (`TrackDispose` / `WaitForPendingDisposeAsync`) so a new `AudioPlaybackConnection` is never created while the previous one is still alive; bounded blocking teardown on shutdown to prevent half-open A2DP links; exponential backoff (5s → 60s cap) in the auto-reconnect loop to avoid the known Windows 11 fastfail from repeated `StartAsync` calls. Verified on real hardware with 10 connect/disconnect cycles — device name integrity preserved, clean shutdown, zero errors in the Windows event log.
- **v1.1.1** — Fix settings clobbering at startup (partial defaults like `Volume=0` / `Language=en-US` were persisted over saved values) and fix the crash when launching with `isMuted:true`: `IAudioEndpointVolume` reordered to native vtable order, and all CoreAudio COM interfaces now use `[ComImport]` + `[InterfaceType(InterfaceIsIUnknown)]` + `[PreserveSig]` so the CCW callback vtable is built from interface declaration order (fixes native access violations in `audioses.dll` after `OnNotify`, see dotnet/runtime#127512).
- **v1.1** — 3-tab sidebar layout, DI container, settings persistence, custom title bar.
- **v1.0** — Initial release.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI Framework | WPF (.NET 10) |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Bluetooth API | WinRT `AudioPlaybackConnection` |
| Audio Volume | Windows CoreAudio COM (IMMDeviceEnumerator) |
| System Tray | Hardcodet.NotifyIcon.Wpf |
| Testing | xUnit + Moq |
| Icon Tools | PowerShell System.Drawing (PNG → multi-size ICO) |

## License

MIT

---

*Crafted with discipline. ulw*

---

# AkiLink — 蓝牙音频接收器

**AkiLink** 是一款现代化的 WPF 桌面应用，可以将你的 Windows 电脑变成高品质蓝牙音频接收器。它通过 WinRT `AudioPlaybackConnection` API 接收来自已配对蓝牙设备（手机、平板等）的流式音频，延迟低、设置简单。

## 功能特性

- **设备发现** — 使用 `AudioPlaybackConnection.GetDeviceSelector()` 扫描已配对的蓝牙音频设备
- **一键连接/断开** — 单击即可建立或断开音频连接
- **实时状态** — 实时监控连接状态变化（已连接/已断开/错误）
- **系统托盘** — 关闭窗口时最小化到托盘，音频持续播放
- **音量控制** — 主音量滑块 + 静音切换，与 Windows 系统音频同步
- **音频编码设置**
  - 首选编码选择（自动 / SBC / AAC / aptX / LDAC）
  - 比特率选择（最高 990 kbps）
  - 采样率选择（44100 / 48000 Hz）
  - 传输模式（均衡 / 最佳音质 / 低延迟）
- **自动重连** — 设备回到范围内时自动重新连接
- **连接历史** — 完整的连接/断开/错误事件日志，支持逐条删除和清空
- **精简模式** — 简约浮窗 UI，始终置顶监控
- **界面本地化** — 英文和中文 UI 语言，运行时一键切换
- **深色主题** — 现代化暗色 UI，卡片式布局

## 架构

同上 Architecture 部分。

## 环境要求

- **Windows 10**（build 19041+）或 **Windows 11**
- **.NET 10 SDK**（或更新版本；`global.json` 锁定 `10.0.301`）
- 支持音频播放连接的蓝牙适配器

## 快速开始

```bash
# 构建
dotnet build

# 运行
dotnet run

# 测试
dotnet test
```

应用使用 `net10.0-windows10.0.19041.0` 目标框架以直接调用 WinRT API。

## 技术栈

| 层 | 技术 |
|---|---|
| UI 框架 | WPF (.NET 10) |
| 架构模式 | MVVM (CommunityToolkit.Mvvm) |
| 蓝牙 API | WinRT `AudioPlaybackConnection` |
| 音频音量 | Windows CoreAudio COM (IMMDeviceEnumerator) |
| 系统托盘 | Hardcodet.NotifyIcon.Wpf |
| 测试框架 | xUnit + Moq |
| 图标工具 | PowerShell System.Drawing (PNG → 多尺寸 ICO) |

## 版本历史

- **v1.1.7** — "Ethereal Glass" 重新设计：界面全面落地 Aki Design System —— 界面文本用 Plus Jakarta Sans、卡片标题用 Fraunces（衬线）、数值用 IBM Plex Mono（9 个静态 TTF 通过 pack URI 内嵌，name 表已修复保证字体族可靠加载）；全新暗色配色（`#0E1016` 背景、`#171A23`/`#212531` 表面、`#6C8CFF` 主色、`#A78BFA` 次色）；卡片为 24px 圆角 + 24px 内边距，发丝边框外加新增 `CardBorder` 控件绘制的 5px 内凹发丝线（双框效果）；侧边栏升级为 Material 3 导航栏（80px 宽、52px 按钮、圆角 14、图标 21px、标签 10px），激活项带主色淡彩药丸；新增 40px 磨砂玻璃标题栏与 28px 玻璃状态栏；两个环境光晕渐变（左上主色、右下次色）取代旧的静态星星背景；音量百分比按数值规则改用 IBM Plex Mono 渲染。
- **v1.1.6** — 全新应用图标，透明背景：图标源改为 1254×1254 ARGB PNG，`.ico` 重新生成为规范的多尺寸集合（16/24/32/48/64/128/256），取代原来只有 16×16 单帧的旧图标，使 exe、任务栏、系统托盘图标在任何 DPI 下都清晰锐利；标题栏图标也受益于 alpha 通道。
- **v1.1.5** — 单实例守卫 + UI 打磨：通过进程生命周期持有的命名 Mutex 强制单实例运行 —— 再次启动时会恢复并置前已有窗口后退出，杜绝重复进程、重复托盘图标以及同一适配器上冲突的 `AudioPlaybackConnection`；移除头部栏（应用标题 + 连接状态），布局更紧凑；装饰性星星背景去掉动画（整段 `<Window.Triggers>` 故事板与每颗星的缩放变换已删除），8 颗星改为固定在右下角、0.12 透明度静态显示；设备列表新增每设备"已连接"状态行（绿点 + 标签 + 实时编码），通过独立的 `_connectedDevice` 字段追踪，即使连接与断开之间切换了选中设备也能正确清除标志（新增 4 个测试）；音质指南卡片重设计为扁平信息面板（左侧强调色竖条 + TIP 徽章，新增 `QualityGuideBadge` 键，中英双语）；滚动条瘦身（6px → 3px，滑块 24px → 12px），设置列表内容不再水平拉伸。
- **v1.1.4** — 修复 10 项审计问题：托盘"退出"不再残留僵尸窗口（通过 `AllowClose` 门控实现真正退出）；静音切换与外部音量/静音变更现在都会持久化；设置写入合并到后台线程并以原子方式替换文件（拖动滑块不再逐次同步写盘阻塞 UI 线程），退出时同步落盘；历史记录含错误详情时详情行正常渲染（`HasDetail`）；最大化时尊重副显示器工作区；CoreAudio COM 对象在每条初始化失败路径上都会释放（无 RCW 泄漏）；`BluetoothAudioService` 增加已释放保护、状态处理器防御性 try/catch 与有界的 teardown 等待超时；清空历史的确认提示已完成中英文本地化；补充 Windows 11 兼容 GUID 说明（Win11 与 Win10 共用同一 GUID）。同时将 `IDialogService` 接入 DI，并新增 15 个单元测试（48 个 MainViewModel + 6 个 SettingsService）覆盖持久化与合并写盘行为。
- **v1.1.3** — 修复音量滑块与用户输入"打架"：CoreAudio 回调现在通过专属 `AppEventContext` GUID 过滤掉应用自身的音量/静音变更（拖动滑块不再被回调回写导致回弹）；滑块同时显式设置 `SmallChange="0.01"` / `LargeChange="0.1"` —— WPF 默认两者均为 `1.0`，在 `Maximum=1` 时一次点击轨道会直接跳到 0% 或 100%。轨道点击区域从 4px 加高到 32px，且轨道背景改为在 Track 之前声明，使 4px 横条渲染在 16px 圆点之后而非穿过圆点。经 COM 层探针、端到端事件夹具、WPF 命令探针三重实证（点击轨道 ±10%、键盘 ±1%、零回声回写）。
- **v1.1.2** — 修复蓝牙重连循环与设备损坏：连接 teardown 串行化（`TrackDispose` / `WaitForPendingDisposeAsync`），确保新的 `AudioPlaybackConnection` 绝不在上一个仍存活时创建；退出时有限阻塞等待 teardown 完成，防止 A2DP 半开链路；自动重连循环加入指数退避（5s → 60s 封顶），避免重复 `StartAsync` 触发的 Windows 11 已知 fastfail 崩溃。真机验证 10 轮连接/断开：设备名完整、退出无残留、Windows 事件日志零错误。
- **v1.1.1** — 修复启动时设置被覆盖（`Volume=0` / `Language=en-US` 等部分默认值在启动时覆盖已保存设置），并修复 `isMuted:true` 启动崩溃：`IAudioEndpointVolume` 按本机 vtable 顺序重排，所有 CoreAudio COM 接口改用 `[ComImport]` + `[InterfaceType(InterfaceIsIUnknown)]` + `[PreserveSig]`，使 CCW 回调 vtable 按接口声明顺序构建（修复 `audioses.dll` 在 `OnNotify` 返回后的本机访问冲突，见 dotnet/runtime#127512）。
- **v1.1** — 三栏侧边栏布局、DI 容器、设置持久化、自定义标题栏。
- **v1.0** — 初版发布。
