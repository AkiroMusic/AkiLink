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
│   ├── AkiLink.ico      # App icon (256×256 → 16×16)
│   ├── AkiLink.png      # Source PNG
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
│       └── MainViewModelTests.cs   # 38 tests
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

38 unit tests covering:

- Constructor initialization and service subscription
- Device scanning states (success, failure, empty results)
- Connection lifecycle (connect, disconnect, auto-reconnect)
- Volume and mute synchronization (guard against infinite loops)
- Connection history (clear with confirmation, delete entry, HasHistory tracking)
- Codec settings propagation
- `CanExecute` logic for connect/disconnect buttons

Run with:

```bash
dotnet test ./tests/AkiLink.Tests
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI Framework | WPF (.NET 10) |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Bluetooth API | WinRT `AudioPlaybackConnection` |
| Audio Volume | Windows CoreAudio COM (IMMDeviceEnumerator) |
| System Tray | Hardcodet.NotifyIcon.Wpf |
| Testing | xUnit + Moq |
| Icon Tools | ImageMagick (PNG → ICO) |

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
| 图标工具 | ImageMagick (PNG → ICO) |
