# AkiLink — Bluetooth Audio Receiver for Windows
# AkiLink — 蓝牙音频接收器

[![Release](https://img.shields.io/github/v/release/AkiroMusic/AkiLink?style=flat-square&label=Latest%20Release&color=6C8CFF)](https://github.com/AkiroMusic/AkiLink/releases/latest)
[![Build](https://img.shields.io/badge/build-passing-22c55e?style=flat-square)](https://github.com/AkiroMusic/AkiLink/actions)
[![Tests](https://img.shields.io/badge/tests-75%20passed-22c55e?style=flat-square)](https://github.com/AkiroMusic/AkiLink/tree/main/tests/AkiLink.Tests)
[![License](https://img.shields.io/badge/license-MIT-6C8CFF?style=flat-square)](LICENSE)

**AkiLink** is a modern WPF desktop application that turns your Windows PC into a high-quality Bluetooth audio receiver. It uses the WinRT `AudioPlaybackConnection` API to receive streaming audio from paired Bluetooth devices (phones, tablets, etc.) with low latency and minimal setup.

**AkiLink** 是一款现代化的 WPF 桌面应用，可以将你的 Windows 电脑变成高品质蓝牙音频接收器。它通过 WinRT `AudioPlaybackConnection` API 接收来自已配对蓝牙设备（手机、平板等）的流式音频，延迟低、设置简单。

> **v1.2.1 — Start with Windows / 开机自动启动**
> A new **Start with Windows** toggle in Settings registers AkiLink in the HKCU auto-start (Run) key — it launches automatically when you sign in to Windows. The setting is a pure user-level registry entry (no elevation needed), applies instantly, and can be disabled from Settings or Task Manager at any time. See the [Version History](#version-history) for details.
> 设置中新增 **开机自动启动** 开关：开启后 AkiLink 会写入用户级注册表自启动项（HKCU Run 键），登录 Windows 时自动启动。该设置是纯用户级注册表项（无需管理员权限），即时生效，随时可在设置或任务管理器中关闭。详见[版本历史](#版本历史)。

## Download
## 下载

| Platform | Package | Notes |
|----------|---------|-------|
| Windows 10/11 x64 | [AkiLink-v1.2.1-win-x64-setup.exe](https://github.com/AkiroMusic/AkiLink/releases/latest/download/AkiLink-v1.2.1-win-x64-setup.exe) | **Recommended** — Inno Setup installer (requires .NET 10 Desktop Runtime) |
| Windows 10/11 x64 | [self-contained.exe](https://github.com/AkiroMusic/AkiLink/releases/latest/download/AkiLink-v1.2.1-win-x64-self-contained.exe) | No runtime required, larger file |
| Windows 10/11 x64 | [self-contained.zip](https://github.com/AkiroMusic/AkiLink/releases/latest/download/AkiLink-v1.2.1-win-x64-self-contained.zip) | Portable — unpack and run |

| 平台 | 安装包 | 说明 |
|----------|---------|-------|
| Windows 10/11 x64 | AkiLink-v1.2.1-win-x64-setup.exe | **推荐** — Inno Setup 安装程序（需 .NET 10 Desktop Runtime） |
| Windows 10/11 x64 | self-contained.exe | 免装运行时，体积更大 |
| Windows 10/11 x64 | self-contained.zip | 便携版 — 解压即用 |

---

## Features
## 功能特性

- **Device Discovery** — Scan for paired Bluetooth audio devices using `AudioPlaybackConnection.GetDeviceSelector()`
- **设备发现** — 使用 `AudioPlaybackConnection.GetDeviceSelector()` 扫描已配对的蓝牙音频设备
- **One-Click Connect / Disconnect** — Establish or tear down audio connections with a single click
- **一键连接/断开** — 单击即可建立或断开音频连接
- **Real-Time Status** — Monitor connection state changes (connected / disconnected / error) as they happen
- **实时状态** — 实时监控连接状态变化（已连接/已断开/错误）
- **System Tray Support** — Minimize to tray on close; audio continues in the background
- **系统托盘** — 关闭窗口时最小化到托盘，音频持续播放
- **Volume Control** — Master volume slider + mute toggle, synced to Windows system audio
- **音量控制** — 主音量滑块 + 静音切换，与 Windows 系统音频同步
- **Audio Codec Settings**
- **音频编码设置**
  - Preferred codec selection (Auto / SBC / AAC / aptX / LDAC)
  - 首选编码选择（自动 / SBC / AAC / aptX / LDAC）
  - Bitrate selection (up to 990 kbps)
  - 比特率选择（最高 990 kbps）
  - Sample rate selection (44100 / 48000 Hz)
  - 采样率选择（44100 / 48000 Hz）
  - Transmission mode (Balanced / Best Quality / Low Latency)
  - 传输模式（均衡 / 最佳音质 / 低延迟）
- **Auto-Reconnect** — Automatically reconnects when the paired device comes back in range
- **自动重连** — 设备回到范围内时自动重新连接
- **Start with Windows** — Optional auto-launch at sign-in (user-level registry entry, toggle in Settings)
- **开机自动启动** — 登录 Windows 时自动启动（用户级注册表项，可在设置中开关）
- **Background Notifications** — Tray balloon alerts on connect and unexpected disconnect (suppressed for user-initiated disconnects)
- **后台通知** — 连接/意外断开时托盘气泡提示（用户主动断开时不提示）
- **Connection History** — Full log of connect/disconnect/error events with delete and clear support
- **连接历史** — 完整的连接/断开/错误事件日志，支持逐条删除和清空
- **Compact Mode** — Minimal overlay UI for always-on-top monitoring
- **精简模式** — 简约浮窗 UI，始终置顶监控
- **Localization** — English and Chinese (中文) UI languages, switchable at runtime
- **界面本地化** — 英文和中文 UI 语言，运行时一键切换
- **Dark Theme** — Modern dark UI with card-style layout throughout
- **深色主题** — 现代化暗色 UI，卡片式布局

## Screenshots
## 截图

*(Coming soon)*
*（即将推出）*

## Architecture
## 架构

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
│   ├── INotificationService.cs      # Desktop notification abstraction
│   ├── LocalizationService.cs       # Runtime language switching
│   ├── SystemTrayService.cs         # System tray icon + notifications
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
│       ├── MainViewModelTests.cs   # 61 tests
│       ├── SettingsServiceTests.cs # 6 tests
│       └── IconGeometriesTests.cs  # 7 tests
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
## 环境要求

- **Windows 10** (build 19041+) or **Windows 11**
- **Windows 10**（build 19041+）或 **Windows 11**
- **.NET 10 SDK** (or later; pinned to `10.0.301` in `global.json`)
- **.NET 10 SDK**（或更新版本；`global.json` 锁定 `10.0.301`）
- Bluetooth adapter supporting audio playback connections
- 支持音频播放连接的蓝牙适配器

## Getting Started
## 快速开始

```bash
# Build
dotnet build

# Run
dotnet run

# Test
dotnet test
```

The app uses `net10.0-windows10.0.19041.0` to access WinRT APIs (`Windows.Devices.Enumeration`, `Windows.Media.Audio`).

应用使用 `net10.0-windows10.0.19041.0` 目标框架以访问 WinRT API（`Windows.Devices.Enumeration`、`Windows.Media.Audio`）。

## Testing
## 测试

75 unit tests covering:

75 个单元测试，涵盖：

- Constructor initialization and service subscription
- 构造函数初始化与服务订阅
- Device scanning states (success, failure, empty results)
- 设备扫描状态（成功、失败、空结果）
- Connection lifecycle (connect, disconnect, auto-reconnect)
- 连接生命周期（连接、断开、自动重连）
- Per-device connection state (connected flag set/cleared on the device that actually holds the connection)
- 每设备连接状态（连接标志在真正持有连接的设备上置位/清除）
- Volume and mute synchronization (guard against infinite loops)
- 音量与静音同步（防止无限循环）
- Settings persistence (loads saved non-default settings without clobbering them at startup)
- 设置持久化（加载已保存的非默认设置，启动时不会覆盖它们）
- Mute/volume changes persist to settings (toggle mute, external volume/mute changes)
- 静音/音量变更持久化到设置（静音切换、外部音量/静音变更）
- Settings coalesced file persistence (background flush, atomic write, corrupt-file fallback)
- 设置合并写盘持久化（后台刷写、原子写入、损坏文件回退）
- Connection history (clear with confirmation, delete entry, HasHistory tracking, detail-row HasDetail)
- 连接历史（带确认的清空、删除单条记录、HasHistory 跟踪、详情行 HasDetail）
- Codec settings propagation
- 编码设置传递
- `CanExecute` logic for connect/disconnect buttons
- 连接/断开按钮的 `CanExecute` 逻辑
- Auto-connect on startup (guard flags, device matching, LastDeviceId persistence)
- 启动自动连接（防护标志、设备匹配、LastDeviceId 持久化）
- Start with Windows (loading the toggle from settings without persisting during load)
- 开机自动启动（从设置加载开关，加载时不写回注册表）
- Background notifications on connect/disconnect (and suppression for user-initiated disconnects)
- 连接/断开的后台通知（以及用户主动断开时的通知抑制）

Run with:

运行方式：

```bash
dotnet test ./tests/AkiLink.Tests
```

## Version History
## 版本历史

- **v1.2.1** — New **Start with Windows** option: a toggle in Settings registers AkiLink in the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key (quoted executable path via `Environment.ProcessPath`, which also works for single-file publish) so the app launches automatically at sign-in. Toggling off removes the registry entry; loading settings never rewrites the registry (respects manual overrides from Task Manager or Registry Editor); registry failures are caught and logged, never crashing the app. 1 new unit test (75 total).
- **v1.2.1** — 新增 **开机自动启动** 选项：设置中的开关会在用户级 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 键下注册 AkiLink（通过 `Environment.ProcessPath` 获取加引号的完整路径，单文件发布同样适用），登录 Windows 时自动启动。关闭开关即删除注册表项；加载设置时绝不重写注册表（尊重用户在任务管理器或注册表编辑器中的手动修改）；注册表访问失败会被捕获并记录日志，绝不导致应用崩溃。新增 1 个单元测试（共 75 个）。
- **v1.2.0** — Removed the live VU meter feature: `AudioLevelMeterService`, `IAudioLevelMeterService`, and `PercentToGridLengthConverter` were deleted (the meter polled CoreAudio `IAudioMeterInformation` at 33 Hz for the app's entire lifetime, needlessly waking the CPU even when idle in the tray — the biggest background-resource consumer). The LEVEL card was removed from the Devices view, the `LevelTitle` locale keys were dropped, and the 6 meter tests were removed (80 → 74 tests). Also: tray icon left-click now shows/restores the window (previously only the context menu or balloon-click did). Plus 9 robustness fixes from the stability audit: **(1)** connect is now guarded by a `SemaphoreSlim` gate and a VM-level `IsConnecting` flag so double-clicks can never launch two overlapping `AudioPlaybackConnection`s; **(2)** `Disconnect()` cancels an in-flight connect instead of racing it; **(3)** the auto-reconnect loop shares the same connect gate, eliminating the manual-vs-auto reconnect race that caused Bluetooth reconnect loops; **(4)** `AudioVolumeService` now implements `IMMNotificationClient` and re-binds the volume endpoint when the OS default playback device changes (headphones plugged in, output switched) without restarting the app; **(5)** `SettingsService.Dispose` now does a bounded wait for the in-flight flush (plus `_disposed` guards in `Save`/`FlushLoopAsync`) so the final settings write can never be lost or hung at exit; **(6)** `Connect()` captures the target device up front so selection changes mid-await can't corrupt the connected-device bookkeeping; **(7)** `FireOnUiThread` no longer runs mutated observable state inline off the UI thread during shutdown — it logs and drops instead; **(8)** `T()` tolerates stray `{` in external error strings (no more `FormatException` losing status updates); **(9)** volume/mute events fire only when the value actually changed (no duplicate notification storms).
- **v1.2.0** — 移除实时 VU 电平表功能：删除 `AudioLevelMeterService`、`IAudioLevelMeterService`、`PercentToGridLengthConverter`（该电平表以 33Hz 持续轮询 CoreAudio `IAudioMeterInformation`，即使空闲在托盘也会不断唤醒 CPU——是后台资源消耗的最大来源）；Devices 视图的 LEVEL 卡片、`LevelTitle` 双语键与 6 个电平表测试一并移除（80 → 74 测试）。另外：托盘图标现在支持左键单击显示/恢复窗口（此前只能通过右键菜单或点击气泡）。另有稳定性审计的 9 项健壮性修复：**(1)** 连接操作由 `SemaphoreSlim` 门控 + VM 层 `IsConnecting` 标志双重防护，双击绝不可能创建两个重叠的 `AudioPlaybackConnection`；**(2)** `Disconnect()` 会取消正在进行的连接而非与之竞态；**(3)** 自动重连循环复用同一连接门控，消除导致蓝牙重连循环的手动/自动连接竞态；**(4)** `AudioVolumeService` 实现 `IMMNotificationClient`，当系统默认播放设备变化（插入耳机、切换输出）时自动重绑音量端点，无需重启应用；**(5)** `SettingsService.Dispose` 现在对进行中的落盘做有界等待（`Save`/`FlushLoopAsync` 增加 `_disposed` 防护），退出时最终设置写入不再可能丢失或挂起；**(6)** `Connect()` 预先捕获目标设备，await 期间切换选中设备不再污染已连接设备记账；**(7)** `FireOnUiThread` 在关闭期间不再于 UI 线程外内联修改可观察状态——改为记录日志并丢弃；**(8)** `T()` 容忍外部错误字符串中的杂散 `{`（不再因 `FormatException` 丢失状态更新）；**(9)** 音量/静音事件仅在值真正变化时触发（不再有重复通知风暴）。
- **v1.1.8** — Three new features: **auto-connect on startup** (a new `AutoConnectOnStartup` toggle in Settings persists `LastDeviceId` after every successful connect, and `TryAutoConnectAsync` — fired after the main window shows — scans, merges into the `Devices` collection, matches on the saved device ID, and connects; best-effort and silently tolerant of failure); **background notifications** (a new `INotificationService` abstraction implemented by `SystemTrayService.ShowBalloonTip`, raising a tray balloon on connect and on unexpected disconnect, while a `_userInitiatedDisconnect` flag suppresses the disconnect toast when the user disconnects deliberately); and a live VU level meter (removed in v1.1.9). 15 new unit tests (80 total at the time).
- **v1.1.8** — 三个新功能：**启动自动连接**（设置中新增 `AutoConnectOnStartup` 开关，每次成功连接后持久化 `LastDeviceId`，主窗口显示后触发 `TryAutoConnectAsync` —— 扫描并合并进 `Devices` 集合、按保存的设备 ID 匹配后自动连接，尽力而为、失败静默容忍）；**后台通知**（新增 `INotificationService` 抽象，由 `SystemTrayService.ShowBalloonTip` 实现，连接与意外断开时弹出托盘气泡，`_userInitiatedDisconnect` 标志在用户主动断开时抑制断开提示）；以及实时 VU 电平表（v1.1.9 已移除）。新增 15 个单元测试（当时共 80 个）。
- **v1.1.7** — "Ethereal Glass" redesign: the UI now follows the Aki Design System — Plus Jakarta Sans for UI text, Fraunces (serif) for card titles, IBM Plex Mono for numeric values (9 static TTFs embedded via pack URIs, name tables repaired so the family loads reliably); a new dark palette (`#0E1016` background, `#171A23` / `#212531` surfaces, `#6C8CFF` accent, `#A78BFA` secondary); cards are 24px-radius with 24px padding, a hairline border plus a recessed 5px inner hairline drawn by the new `CardBorder` control (double-frame look); the sidebar became a Material 3 navigation rail (80px wide, 52px buttons, radius 14, 21px icons, 10px labels) with an accent-tinted active pill; a 40px frosted-glass title bar and a 28px glass status bar; two ambient radial glow gradients (accent top-left, accent-secondary bottom-right) replace the old static star field; and the volume percentage now renders in IBM Plex Mono per the numeric-value rule.
- **v1.1.7** — "Ethereal Glass" 重新设计：界面全面落地 Aki Design System —— 界面文本用 Plus Jakarta Sans、卡片标题用 Fraunces（衬线）、数值用 IBM Plex Mono（9 个静态 TTF 通过 pack URI 内嵌，name 表已修复保证字体族可靠加载）；全新暗色配色（`#0E1016` 背景、`#171A23`/`#212531` 表面、`#6C8CFF` 主色、`#A78BFA` 次色）；卡片为 24px 圆角 + 24px 内边距，发丝边框外加新增 `CardBorder` 控件绘制的 5px 内凹发丝线（双框效果）；侧边栏升级为 Material 3 导航栏（80px 宽、52px 按钮、圆角 14、图标 21px、标签 10px），激活项带主色淡彩药丸；新增 40px 磨砂玻璃标题栏与 28px 玻璃状态栏；两个环境光晕渐变（左上主色、右下次色）取代旧的静态星星背景；音量百分比按数值规则改用 IBM Plex Mono 渲染。
- **v1.1.6** — New app icon with a transparent background: the icon is now a 1254×1254 ARGB PNG and the `.ico` was rebuilt as a proper multi-size set (16/24/32/48/64/128/256) instead of the previous single 16×16 frame, so the exe, taskbar, and system-tray icons render crisply at every DPI. The title-bar icon now also benefits from the alpha channel.
- **v1.1.6** — 全新应用图标，透明背景：图标源改为 1254×1254 ARGB PNG，`.ico` 重新生成为规范的多尺寸集合（16/24/32/48/64/128/256），取代原来只有 16×16 单帧的旧图标，使 exe、任务栏、系统托盘图标在任何 DPI 下都清晰锐利；标题栏图标也受益于 alpha 通道。
- **v1.1.5** — Single-instance guard + UI polish: a named Mutex held for the process lifetime enforces one running instance — launching a second instance restores + foregrounds the existing window and exits, eliminating duplicate processes, duplicate tray icons, and conflicting `AudioPlaybackConnection`s on the same adapter; the header bar (app title + connection status) was removed for a more compact layout; the decorative star background lost its animation (the entire `<Window.Triggers>` storyboard and per-star scale transforms were deleted) and the 8 stars now sit static in the bottom-right at 0.12 opacity; the device list shows a per-device "connected" status row (green dot + label + realtime codec), tracked via a dedicated `_connectedDevice` field so the flag clears correctly even when the selection changes between connect and disconnect (4 new tests); the Quality Guide card was redesigned as a flat informational panel with a left accent rail and a TIP badge (new `QualityGuideBadge` key, EN + ZH); scrollbars were slimmed (6px → 3px, thumb 24px → 12px) and the settings list no longer stretches its content horizontally.
- **v1.1.5** — 单实例守卫 + UI 打磨：通过进程生命周期持有的命名 Mutex 强制单实例运行 —— 再次启动时会恢复并置前已有窗口后退出，杜绝重复进程、重复托盘图标以及同一适配器上冲突的 `AudioPlaybackConnection`；移除头部栏（应用标题 + 连接状态），布局更紧凑；装饰性星星背景去掉动画（整段 `<Window.Triggers>` 故事板与每颗星的缩放变换已删除），8 颗星改为固定在右下角、0.12 透明度静态显示；设备列表新增每设备"已连接"状态行（绿点 + 标签 + 实时编码），通过独立的 `_connectedDevice` 字段追踪，即使连接与断开之间切换了选中设备也能正确清除标志（新增 4 个测试）；音质指南卡片重设计为扁平信息面板（左侧强调色竖条 + TIP 徽章，新增 `QualityGuideBadge` 键，中英双语）；滚动条瘦身（6px → 3px，滑块 24px → 12px），设置列表内容不再水平拉伸。
- **v1.1.4** — Fix 10 audit findings: tray "Quit" no longer leaves a zombie window (real exit via `AllowClose` gate); mute toggles and external volume/mute changes now persist to settings; settings writes are coalesced onto a background thread with atomic file replacement (no more per-slider-delta synchronous disk I/O on the UI thread) plus a synchronous flush on exit; history detail rows render when an error message is present (`HasDetail`); maximize now respects the secondary monitor's work area; CoreAudio COM objects are released on every init failure path (no RCW leaks); `BluetoothAudioService` gained a disposed guard, a defensive try/catch in the state handler, and a bounded dispose-wait timeout; history-clear confirmation is localized (EN + ZH); the Win11 compatibility GUID is documented (Windows 11 shares the Windows 10 GUID). Also wired `IDialogService` into DI and added 15 new unit tests (48 MainViewModel + 6 SettingsService) for the persistence and coalescing behavior.
- **v1.1.4** — 修复 10 项审计问题：托盘"退出"不再残留僵尸窗口（通过 `AllowClose` 门控实现真正退出）；静音切换与外部音量/静音变更现在都会持久化；设置写入合并到后台线程并以原子方式替换文件（拖动滑块不再逐次同步写盘阻塞 UI 线程），退出时同步落盘；历史记录含错误详情时详情行正常渲染（`HasDetail`）；最大化时尊重副显示器工作区；CoreAudio COM 对象在每条初始化失败路径上都会释放（无 RCW 泄漏）；`BluetoothAudioService` 增加已释放保护、状态处理器防御性 try/catch 与有界的 teardown 等待超时；清空历史的确认提示已完成中英文本地化；补充 Windows 11 兼容 GUID 说明（Win11 与 Win10 共用同一 GUID）。同时将 `IDialogService` 接入 DI，并新增 15 个单元测试（48 个 MainViewModel + 6 个 SettingsService）覆盖持久化与合并写盘行为。
- **v1.1.3** — Fix volume slider fighting user input: CoreAudio callbacks now filter out the app's own volume/mute changes via a dedicated `AppEventContext` GUID (dragging the slider is no longer echoed back and snapped); the slider also gets explicit `SmallChange="0.01"` / `LargeChange="0.1"` — WPF defaults both to `1.0`, which with `Maximum=1` made one track click jump straight to 0% or 100%. Track hit-area enlarged 4px → 32px, and the track background is now declared before the Track so the 4px bar renders behind the 16px thumb circle instead of crossing through it. Verified with a COM-level probe, an end-to-end event harness, and a WPF command probe (track click ±10%, keyboard ±1%, zero echo-back).
- **v1.1.3** — 修复音量滑块与用户输入"打架"：CoreAudio 回调现在通过专属 `AppEventContext` GUID 过滤掉应用自身的音量/静音变更（拖动滑块不再被回调回写导致回弹）；滑块同时显式设置 `SmallChange="0.01"` / `LargeChange="0.1"` —— WPF 默认两者均为 `1.0`，在 `Maximum=1` 时一次点击轨道会直接跳到 0% 或 100%。轨道点击区域从 4px 加高到 32px，且轨道背景改为在 Track 之前声明，使 4px 横条渲染在 16px 圆点之后而非穿过圆点。经 COM 层探针、端到端事件夹具、WPF 命令探针三重实证（点击轨道 ±10%、键盘 ±1%、零回声回写）。
- **v1.1.2** — Fix Bluetooth reconnect loops and device corruption: serialized connection teardown (`TrackDispose` / `WaitForPendingDisposeAsync`) so a new `AudioPlaybackConnection` is never created while the previous one is still alive; bounded blocking teardown on shutdown to prevent half-open A2DP links; exponential backoff (5s → 60s cap) in the auto-reconnect loop to avoid the known Windows 11 fastfail from repeated `StartAsync` calls. Verified on real hardware with 10 connect/disconnect cycles — device name integrity preserved, clean shutdown, zero errors in the Windows event log.
- **v1.1.2** — 修复蓝牙重连循环与设备损坏：连接 teardown 串行化（`TrackDispose` / `WaitForPendingDisposeAsync`），确保新的 `AudioPlaybackConnection` 绝不在上一个仍存活时创建；退出时有限阻塞等待 teardown 完成，防止 A2DP 半开链路；自动重连循环加入指数退避（5s → 60s 封顶），避免重复 `StartAsync` 触发的 Windows 11 已知 fastfail 崩溃。真机验证 10 轮连接/断开：设备名完整、退出无残留、Windows 事件日志零错误。
- **v1.1.1** — Fix settings clobbering at startup (partial defaults like `Volume=0` / `Language=en-US` were persisted over saved values) and fix the crash when launching with `isMuted:true`: `IAudioEndpointVolume` reordered to native vtable order, and all CoreAudio COM interfaces now use `[ComImport]` + `[InterfaceType(InterfaceIsIUnknown)]` + `[PreserveSig]` so the CCW callback vtable is built from interface declaration order (fixes native access violations in `audioses.dll` after `OnNotify`, see dotnet/runtime#127512).
- **v1.1.1** — 修复启动时设置被覆盖（`Volume=0` / `Language=en-US` 等部分默认值在启动时覆盖已保存设置），并修复 `isMuted:true` 启动崩溃：`IAudioEndpointVolume` 按本机 vtable 顺序重排，所有 CoreAudio COM 接口改用 `[ComImport]` + `[InterfaceType(InterfaceIsIUnknown)]` + `[PreserveSig]`，使 CCW 回调 vtable 按接口声明顺序构建（修复 `audioses.dll` 在 `OnNotify` 返回后的本机访问冲突，见 dotnet/runtime#127512）。
- **v1.1** — 3-tab sidebar layout, DI container, settings persistence, custom title bar.
- **v1.1** — 三栏侧边栏布局、DI 容器、设置持久化、自定义标题栏。
- **v1.0** — Initial release.
- **v1.0** — 初版发布。

## Tech Stack
## 技术栈

| Layer | Technology |
|-------|-----------|
| UI Framework | WPF (.NET 10) |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Bluetooth API | WinRT `AudioPlaybackConnection` |
| Audio Volume | Windows CoreAudio COM (IMMDeviceEnumerator) |
| System Tray | Hardcodet.NotifyIcon.Wpf |
| Testing | xUnit + Moq |
| Icon Tools | PowerShell System.Drawing (PNG → multi-size ICO) |

| 层 | 技术 |
|---|---|
| UI 框架 | WPF (.NET 10) |
| 架构模式 | MVVM (CommunityToolkit.Mvvm) |
| 蓝牙 API | WinRT `AudioPlaybackConnection` |
| 音频音量 | Windows CoreAudio COM (IMMDeviceEnumerator) |
| 系统托盘 | Hardcodet.NotifyIcon.Wpf |
| 测试框架 | xUnit + Moq |
| 图标工具 | PowerShell System.Drawing (PNG → 多尺寸 ICO) |

## License
## 许可证

MIT

---

*Crafted with discipline. ulw*
*以纪律精心打磨。ulw*
