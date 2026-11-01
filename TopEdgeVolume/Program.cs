using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TopEdgeVolume;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        NativeMethods.TryEnablePerMonitorDpiAwareness();
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notificationIcon;
    private readonly SystemVolumeController _volumeController = new();
    private readonly TopEdgeMouseHook _mouseHook;
    private readonly List<ToolStripMenuItem> _edgeHeightItems = [];
    private readonly List<ToolStripMenuItem> _volumeStepItems = [];
    private int _edgeHeightPixels = 12;
    private int _volumeStepsPerWheel = 1;
    private bool _hasShownActivationHint;
    private bool _hasShownErrorHint;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        var enabledItem = new ToolStripMenuItem("启用顶部滚轮调音量")
        {
            CheckOnClick = true,
            Checked = true
        };
        var bottomEnabledItem = new ToolStripMenuItem("启用底部边缘调音量")
        {
            CheckOnClick = true,
            Checked = true
        };

        var edgeHeightMenu = new ToolStripMenuItem("顶部触发区域");
        foreach (var height in new[] { 4, 8, 12, 16 })
        {
            var item = new ToolStripMenuItem($"{height} 像素")
            {
                Tag = height,
                Checked = height == _edgeHeightPixels
            };
            item.Click += (_, _) => SetEdgeHeight(height);
            _edgeHeightItems.Add(item);
            edgeHeightMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(enabledItem);
        menu.Items.Add(bottomEnabledItem);
        menu.Items.Add(edgeHeightMenu);
        menu.Items.Add(CreateVolumeStepMenu());
        var startupItem = new ToolStripMenuItem("开机自动启动")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabledForCurrentExecutable()
        };
        startupItem.Click += (_, _) => SetStartupRegistration(startupItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitThread()));

        _notificationIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "屏幕边缘滚轮调音量",
            ContextMenuStrip = menu,
            Visible = true
        };

        _mouseHook = new TopEdgeMouseHook(
            () => enabledItem.Checked,
            () => bottomEnabledItem.Checked,
            () => Volatile.Read(ref _edgeHeightPixels),
            () => Volatile.Read(ref _volumeStepsPerWheel),
            _volumeController.Change,
            ShowActivationHint,
            ShowErrorHint);

        try
        {
            _mouseHook.Start();
            _volumeController.WarmUp();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"无法启动边缘滚轮调音量。\n\n{exception.Message}",
                "边缘滚轮调音量",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            ExitThread();
        }
    }

    protected override void ExitThreadCore()
    {
        _notificationIcon.Visible = false;
        _mouseHook.Dispose();
        _volumeController.Dispose();
        _notificationIcon.Dispose();
        base.ExitThreadCore();
    }

    private void SetEdgeHeight(int height)
    {
        Volatile.Write(ref _edgeHeightPixels, height);
        foreach (var item in _edgeHeightItems)
        {
            item.Checked = item.Tag is int itemHeight && itemHeight == height;
        }
    }

    private ToolStripMenuItem CreateVolumeStepMenu()
    {
        var menu = new ToolStripMenuItem("滚轮调整步长");
        foreach (var steps in new[] { 1, 2, 3, 5 })
        {
            var item = new ToolStripMenuItem($"{steps} 格")
            {
                Tag = steps,
                Checked = steps == _volumeStepsPerWheel
            };
            item.Click += (_, _) => SetVolumeSteps(steps);
            _volumeStepItems.Add(item);
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    private void SetStartupRegistration(ToolStripMenuItem startupItem)
    {
        try
        {
            if (startupItem.Checked)
            {
                StartupRegistration.EnableForCurrentExecutable();
            }
            else
            {
                StartupRegistration.Disable();
            }
        }
        catch (Exception exception)
        {
            startupItem.Checked = StartupRegistration.IsEnabledForCurrentExecutable();
            ShowErrorHint(exception);
        }
    }

    private void SetVolumeSteps(int steps)
    {
        Volatile.Write(ref _volumeStepsPerWheel, steps);
        foreach (var item in _volumeStepItems)
        {
            item.Checked = item.Tag is int itemSteps && itemSteps == steps;
        }
    }

    private void ShowActivationHint()
    {
        if (_hasShownActivationHint)
        {
            return;
        }

        _hasShownActivationHint = true;
        _notificationIcon.ShowBalloonTip(
            1500,
            "边缘滚轮调音量",
            "已识别屏幕边缘滚轮，正在调节系统音量。",
            ToolTipIcon.Info);
    }

    private void ShowErrorHint(Exception exception)
    {
        if (_hasShownErrorHint)
        {
            return;
        }

        _hasShownErrorHint = true;
        _notificationIcon.ShowBalloonTip(
            3000,
            "边缘滚轮调音量",
            $"调音量时出现错误：{exception.GetType().Name}",
            ToolTipIcon.Error);
    }
}

internal static class StartupRegistration
{
    private const string ShortcutFileName = "TopEdgeVolume 自动启动.lnk";

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        ShortcutFileName);

    public static bool IsEnabledForCurrentExecutable()
    {
        try
        {
            var targetPath = TryGetTargetPath();
            return targetPath is not null
                   && string.Equals(
                       Path.GetFullPath(targetPath),
                       Path.GetFullPath(Application.ExecutablePath),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void EnableForCurrentExecutable()
    {
        EnsureExistingShortcutBelongsToThisApp();
        UseShortcut(shortcut =>
        {
            shortcut.TargetPath = Application.ExecutablePath;
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.Description = "边缘滚轮调音量";
            shortcut.Save();
            return true;
        });
    }

    public static void Disable()
    {
        if (!File.Exists(ShortcutPath))
        {
            return;
        }

        EnsureExistingShortcutBelongsToThisApp();
        File.Delete(ShortcutPath);
    }

    private static string? TryGetTargetPath()
    {
        if (!File.Exists(ShortcutPath))
        {
            return null;
        }

        return UseShortcut(shortcut => (string?)shortcut.TargetPath);
    }

    private static void EnsureExistingShortcutBelongsToThisApp()
    {
        var targetPath = TryGetTargetPath();
        if (targetPath is not null
            && !string.Equals(
                Path.GetFileName(targetPath),
                "TopEdgeVolume.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("启动文件夹中存在同名但不属于本程序的快捷方式，未作修改。");
        }
    }

    private static T UseShortcut<T>(Func<dynamic, T> action)
    {
        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                            ?? throw new InvalidOperationException("无法创建 Windows 快捷方式服务。");
            shell = Activator.CreateInstance(shellType)
                    ?? throw new InvalidOperationException("无法创建 Windows 快捷方式服务。");
            shortcut = shell.CreateShortcut(ShortcutPath);
            return action(shortcut);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}

internal sealed class TopEdgeMouseHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const nint WmMouseWheel = 0x020A;
    private readonly Func<bool> _isTopEdgeEnabled;
    private readonly Func<bool> _isBottomEdgeEnabled;
    private readonly Func<int> _edgeHeightPixels;
    private readonly Func<int> _volumeStepsPerWheel;
    private readonly Action<bool, int> _changeVolume;
    private readonly Action _onTriggered;
    private readonly Action<Exception> _onError;
    private readonly NativeMethods.LowLevelMouseProc _hookProcedure;
    private nint _hookHandle;

    public TopEdgeMouseHook(
        Func<bool> isTopEdgeEnabled,
        Func<bool> isBottomEdgeEnabled,
        Func<int> edgeHeightPixels,
        Func<int> volumeStepsPerWheel,
        Action<bool, int> changeVolume,
        Action onTriggered,
        Action<Exception> onError)
    {
        _isTopEdgeEnabled = isTopEdgeEnabled;
        _isBottomEdgeEnabled = isBottomEdgeEnabled;
        _edgeHeightPixels = edgeHeightPixels;
        _volumeStepsPerWheel = volumeStepsPerWheel;
        _changeVolume = changeVolume;
        _onTriggered = onTriggered;
        _onError = onError;
        _hookProcedure = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != nint.Zero)
        {
            return;
        }

        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(WhMouseLl, _hookProcedure, moduleHandle, 0);
        if (_hookHandle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Dispose()
    {
        if (_hookHandle == nint.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = nint.Zero;
        GC.SuppressFinalize(this);
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        try
        {
            var topEdgeEnabled = _isTopEdgeEnabled();
            var bottomEdgeEnabled = _isBottomEdgeEnabled();
            if (code >= 0 && wParam == WmMouseWheel && (topEdgeEnabled || bottomEdgeEnabled))
            {
                var mouse = Marshal.PtrToStructure<NativeMethods.MsllHookStruct>(lParam);
                if (NativeMethods.IsAtVolumeEdge(
                        mouse.Point,
                        _edgeHeightPixels(),
                        topEdgeEnabled,
                        bottomEdgeEnabled))
                {
                    var wheelDelta = unchecked((short)(mouse.MouseData >> 16));
                    if (wheelDelta != 0)
                    {
                        _changeVolume(wheelDelta > 0, _volumeStepsPerWheel());
                        _onTriggered();
                        return 1; // 调音量时不再把滚轮事件交给前台程序。
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _onError(exception);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }
}

internal sealed class SystemVolumeController : IDisposable
{
    private static Guid EventContext = Guid.Empty;
    private readonly object _sync = new();
    private IAudioEndpointVolume? _endpointVolume;
    private IMMDeviceEnumerator? _enumerator;
    private DefaultEndpointNotificationClient? _notificationClient;
    private bool _defaultRenderDeviceChanged;
    private bool _isDisposed;

    /// <summary>预先建立到默认播放设备的连接，避免第一次滚轮时才初始化 COM。</summary>
    public void WarmUp()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            EnsureEndpoint();
        }
    }

    public void Change(bool increase, int steps)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            try
            {
                ChangeCore(increase, steps);
            }
            catch (COMException)
            {
                // 输出设备在运行中被拔出时，刷新一次连接后重试。
                ReleaseEndpoint();
                Volatile.Write(ref _defaultRenderDeviceChanged, false);
                ChangeCore(increase, steps);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (_enumerator is not null && _notificationClient is not null)
            {
                _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
            }

            _notificationClient = null;
            ReleaseEndpoint();
            ReleaseComObject(_enumerator);
            _enumerator = null;
        }
    }

    private void ChangeCore(bool increase, int steps)
    {
        var endpoint = EnsureEndpoint();
        for (var i = 0; i < steps; i++)
        {
            var result = increase
                ? endpoint.VolumeStepUp(ref EventContext)
                : endpoint.VolumeStepDown(ref EventContext);
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private IAudioEndpointVolume EnsureEndpoint()
    {
        EnsureMonitoring();
        if (Volatile.Read(ref _defaultRenderDeviceChanged))
        {
            ReleaseEndpoint();
            Volatile.Write(ref _defaultRenderDeviceChanged, false);
        }

        if (_endpointVolume is not null)
        {
            return _endpointVolume;
        }

        IMMDevice? device = null;
        object? endpointObject = null;
        try
        {
            Marshal.ThrowExceptionForHR(_enumerator!.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device));

            var interfaceId = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(
                ref interfaceId,
                ClsCtx.InprocServer,
                nint.Zero,
                out endpointObject));

            _endpointVolume = (IAudioEndpointVolume)endpointObject;
            endpointObject = null;
            return _endpointVolume;
        }
        finally
        {
            ReleaseComObject(endpointObject);
            ReleaseComObject(device);
        }
    }

    private void EnsureMonitoring()
    {
        if (_enumerator is not null)
        {
            return;
        }

        IMMDeviceEnumerator? enumerator = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            var notificationClient = new DefaultEndpointNotificationClient(HandleDefaultDeviceChanged);
            Marshal.ThrowExceptionForHR(enumerator.RegisterEndpointNotificationCallback(notificationClient));
            _enumerator = enumerator;
            _notificationClient = notificationClient;
            enumerator = null;
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    private void HandleDefaultDeviceChanged(EDataFlow dataFlow, ERole role)
    {
        if (dataFlow == EDataFlow.Render)
        {
            // 此回调可能发生在 COM 的其他线程；只设置标志，下一次滚轮在 UI 线程中重新连接设备。
            Volatile.Write(ref _defaultRenderDeviceChanged, true);
        }
    }

    private void ReleaseEndpoint()
    {
        ReleaseComObject(_endpointVolume);
        _endpointVolume = null;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SystemVolumeController));
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}

internal enum EDataFlow
{
    Render,
    Capture,
    All
}

internal enum ERole
{
    Console,
    Multimedia,
    Communications
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;
}

[ComVisible(true)]
[ComImport]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, uint newState);
    int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    int OnDefaultDeviceChanged(EDataFlow dataFlow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);
    int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}

[ComVisible(true)]
internal sealed class DefaultEndpointNotificationClient : IMMNotificationClient
{
    private readonly Action<EDataFlow, ERole> _onDefaultDeviceChanged;

    public DefaultEndpointNotificationClient(Action<EDataFlow, ERole> onDefaultDeviceChanged)
    {
        _onDefaultDeviceChanged = onDefaultDeviceChanged;
    }

    public int OnDeviceStateChanged(string deviceId, uint newState) => 0;
    public int OnDeviceAdded(string deviceId) => 0;
    public int OnDeviceRemoved(string deviceId) => 0;

    public int OnDefaultDeviceChanged(EDataFlow dataFlow, ERole role, string defaultDeviceId)
    {
        try
        {
            _onDefaultDeviceChanged(dataFlow, role);
        }
        catch
        {
            // 不让 COM 通知回调中的异常传播回 Windows 音频服务。
        }

        return 0;
    }

    public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
}

[Flags]
internal enum ClsCtx : uint
{
    InprocServer = 0x1
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out nint devices);
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    int RegisterEndpointNotificationCallback(IMMNotificationClient client);
    int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid interfaceId, ClsCtx classContext, nint activationParameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    int OpenPropertyStore(int accessMode, out nint properties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int GetState(out uint state);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(nint client);
    int UnregisterControlChangeNotify(nint client);
    int GetChannelCount(out uint channelCount);
    int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
    int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    int GetMasterVolumeLevel(out float levelDb);
    int GetMasterVolumeLevelScalar(out float level);
    int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
    int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
    int GetChannelVolumeLevel(uint channel, out float levelDb);
    int GetChannelVolumeLevelScalar(uint channel, out float level);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    int GetVolumeStepInfo(out uint step, out uint stepCount);
    int VolumeStepUp(ref Guid eventContext);
    int VolumeStepDown(ref Guid eventContext);
    int QueryHardwareSupport(out uint hardwareSupportMask);
    int GetVolumeRange(out float volumeMinDb, out float volumeMaxDb, out float volumeIncrementDb);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject;

internal static class NativeMethods
{
    private const uint MonitorDefaultToNearest = 2;
    private static readonly nint DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    internal delegate nint LowLevelMouseProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MsllHookStruct
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    internal static bool IsAtVolumeEdge(
        Point point,
        int edgeHeightPixels,
        bool topEdgeEnabled,
        bool bottomEdgeEnabled)
    {
        var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        if (monitor == nint.Zero)
        {
            return false;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var edgeHeight = Math.Max(1, edgeHeightPixels);
        var atTopEdge = point.Y >= info.Monitor.Top
                        && point.Y < info.Monitor.Top + edgeHeight;
        var atBottomEdge = point.Y >= info.Monitor.Bottom - edgeHeight
                           && point.Y < info.Monitor.Bottom;
        return (topEdgeEnabled && atTopEdge) || (bottomEdgeEnabled && atBottomEdge);
    }

    internal static void TryEnablePerMonitorDpiAwareness()
    {
        SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWindowsHookEx(
        int idHook,
        LowLevelMouseProc callback,
        nint moduleHandle,
        uint threadId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hookHandle, int code, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    internal static extern void KeybdEvent(byte virtualKey, byte scanCode, uint flags, nuint extraInfo);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint value);
}
