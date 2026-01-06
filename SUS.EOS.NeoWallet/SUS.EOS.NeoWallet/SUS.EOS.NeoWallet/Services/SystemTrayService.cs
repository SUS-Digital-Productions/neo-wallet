using SUS.EOS.NeoWallet.Services.Interfaces;

#if WINDOWS
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using WinRT.Interop;
#endif

namespace SUS.EOS.NeoWallet.Services;

/// <summary>
/// Windows system tray service using Win32 NotifyIcon
/// </summary>
public partial class SystemTrayService : ISystemTrayService
{
#if WINDOWS
    private Microsoft.UI.Xaml.Window? _window;
    private IntPtr _windowHandle;
    private NotifyIconData _notifyIcon;
    private const int WM_APP = 0x8000;
    private const int WM_TRAYICON = WM_APP + 1;
    private bool _isInitialized;

    public void Initialize()
    {
        if (_isInitialized)
            return;

        System.Diagnostics.Trace.WriteLine("[TRAY] Initializing system tray icon");

        if (_window == null)
        {
            System.Diagnostics.Trace.WriteLine("[TRAY] ERROR: Window not set, call SetNativeWindow first");
            return;
        }

        _windowHandle = WindowNative.GetWindowHandle(_window);

        // Create tray icon
        _notifyIcon = new NotifyIconData();
        _notifyIcon.cbSize = Marshal.SizeOf(_notifyIcon);
        _notifyIcon.hWnd = _windowHandle;
        _notifyIcon.uID = 1;
        _notifyIcon.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
        _notifyIcon.uCallbackMessage = WM_TRAYICON;
        
        // Load icon (using default app icon for now)
        _notifyIcon.hIcon = LoadAppIcon();
        
        // Set tooltip
        _notifyIcon.szTip = "NeoWallet - Running in background";

        // Add to system tray
        Shell_NotifyIcon(NIM_ADD, ref _notifyIcon);

        _isInitialized = true;
        System.Diagnostics.Trace.WriteLine("[TRAY] System tray icon created");
    }

    public Microsoft.UI.Xaml.Window? GetNativeWindow() => _window;

    public void SetNativeWindow(object window)
    {
        if (window is Microsoft.UI.Xaml.Window nativeWindow)
        {
            _window = nativeWindow;
            System.Diagnostics.Trace.WriteLine("[TRAY] Native window set");
        }
        else
        {
            System.Diagnostics.Trace.WriteLine($"[TRAY] ERROR: Expected Microsoft.UI.Xaml.Window but got {window?.GetType().FullName}");
        }
    }

    public void ShowMainWindow()
    {
        System.Diagnostics.Trace.WriteLine("[TRAY] ShowMainWindow called");
        if (_window != null)
        {
            // Restore and activate window
            var hwnd = WindowNative.GetWindowHandle(_window);
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            _window.Activate();
        }
    }

    public void HideToTray()
    {
        System.Diagnostics.Trace.WriteLine("[TRAY] HideToTray called");
        if (_window != null)
        {
            var hwnd = WindowNative.GetWindowHandle(_window);
            ShowWindow(hwnd, SW_HIDE);
            
            ShowNotification("NeoWallet", "Running in background. Click tray icon to restore.");
        }
    }

    public void ExitApplication()
    {
        System.Diagnostics.Trace.WriteLine("[TRAY] ExitApplication called");
        
        // Remove tray icon
        if (_isInitialized)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _notifyIcon);
        }

        // Exit app
        Application.Current?.Quit();
    }

    public void ShowNotification(string title, string message)
    {
        if (!_isInitialized)
            return;

        System.Diagnostics.Trace.WriteLine($"[TRAY] Showing notification: {title}");

        _notifyIcon.uFlags = NIF_INFO;
        _notifyIcon.szInfoTitle = title;
        _notifyIcon.szInfo = message;
        _notifyIcon.dwInfoFlags = NIIF_INFO;

        Shell_NotifyIcon(NIM_MODIFY, ref _notifyIcon);
    }

    private IntPtr LoadAppIcon()
    {
        // Use default application icon
        var hInstance = GetModuleHandle(null);
        return LoadIcon(hInstance, new IntPtr(32512)); // IDI_APPLICATION
    }

    // Win32 API constants
    private const int NIF_ICON = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIIF_INFO = 0x00000001;
    private const int SW_HIDE = 0;
    private const int SW_RESTORE = 9;

    // Win32 API structures
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    // Win32 API imports
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

#else
    // Non-Windows stub implementation
    public void SetNativeWindow(object window)
    {
        System.Diagnostics.Trace.WriteLine("[TRAY] SetNativeWindow not supported on this platform");
    }

    public void Initialize()
    {
        System.Diagnostics.Trace.WriteLine("[TRAY] System tray not supported on this platform");
    }

    public void ShowMainWindow()
    {
        // No-op on non-Windows
    }

    public void HideToTray()
    {
        // No-op on non-Windows
    }

    public void ExitApplication()
    {
        Application.Current?.Quit();
    }

    public void ShowNotification(string title, string message)
    {
        System.Diagnostics.Trace.WriteLine($"[TRAY] Notification (unsupported): {title} - {message}");
    }
#endif
}
