# Top Edge Volume

![Top Edge Volume icon](TopEdgeVolume/Assets/TopEdgeVolume.png)

一个轻量、无窗口的 Windows 托盘工具：把鼠标移到**任意显示器的顶部或底部边缘**，滚动滚轮即可调整系统音量。

[English](#english) · [中文](#中文)

## 中文

### 功能

- 支持多显示器：每一块屏幕的顶部和底部边缘均可触发。
- 向上滚动增大音量，向下滚动减小音量。
- 直接调用 Windows Core Audio，不模拟媒体键，响应迅速。
- 自动跟随默认播放设备：切换扬声器、耳机或蓝牙设备后，无须重启程序。
- 托盘右键菜单可暂停功能、设置触发区高度和单次调整步长。
- 可从托盘菜单启用或关闭开机自动启动。
- 单文件、自包含的 x64 EXE，无需安装 AutoHotkey 或 .NET 运行时。

### 使用方法

1. 从 [Releases](../../releases) 下载 `TopEdgeVolume.exe` 并运行。
2. 将鼠标放到任意显示器顶部或底部的触发区域内（默认 12 像素）。
3. 向上或向下滚动滚轮调节系统音量。
4. 右键系统托盘图标可更改设置或退出程序。

### 托盘菜单

| 设置 | 可选项 | 默认值 |
| --- | --- | --- |
| 顶部触发区域 | 4 / 8 / 12 / 16 像素 | 12 像素 |
| 底部边缘调音量 | 开 / 关 | 开 |
| 滚轮调整步长 | 1 / 2 / 3 / 5 格 | 1 格 |
| 开机自动启动 | 开 / 关 | 关 |

“一格”是当前 Windows 音频设备定义的一次标准音量增减。启用开机自动启动时，程序仅会在当前用户的启动文件夹创建自己的快捷方式。

### 常见问题

**切换耳机或扬声器后还会控制旧设备吗？** 不会。程序监听 Windows 的默认播放设备变更，下一次滚轮会连接到新的默认设备。

**为什么没有主窗口？** 这是后台工具；所有控制均在系统托盘图标的右键菜单中完成。

**会收集数据或联网吗？** 不会。程序完全在本机运行，不包含遥测、网络访问或广告。

### 从源码构建

要求：Windows 10/11 与 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
dotnet publish .\TopEdgeVolume\TopEdgeVolume.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish
```

生成的程序位于 `publish\TopEdgeVolume.exe`。

### 许可证

本项目采用 [MIT License](LICENSE)。

---

## English

### What it does

Top Edge Volume is a small, windowless Windows tray utility. Move the pointer to the top or bottom edge of any monitor and use the mouse wheel to change the system volume.

- Works on the top and bottom edges of every monitor.
- Uses the Windows Core Audio API directly for responsive volume changes.
- Tracks the current default output device after you switch speakers, headphones, or Bluetooth audio.
- Lets you configure the edge height, volume step, and automatic startup from the tray menu.
- Ships as a self-contained x64 executable with no AutoHotkey or .NET runtime installation required.

### Build

Requires Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Run the command in [从源码构建](#从源码构建).

### License

Released under the [MIT License](LICENSE).
