using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace NeoWallet.Backend.Services;

/// <summary>
/// Manages a system tray icon that keeps the backend alive when the browser window is closed.
/// Provides Open, and Exit context menu items.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _trayIcon;
    private Thread? _uiThread;
    private readonly ManualResetEventSlim _ready = new();
    private bool _disposed;

    /// <summary>Fired when the user clicks "Open" or double-clicks the tray icon.</summary>
    public event Action? OpenRequested;

    /// <summary>Fired when the user clicks "Exit" — the app should shut down.</summary>
    public event Action? ExitRequested;

    /// <summary>
    /// Starts the tray icon on a dedicated STA thread (required by WinForms).
    /// </summary>
    public void Start()
    {
        _uiThread = new Thread(RunMessageLoop)
        {
            Name = "TrayIcon",
            IsBackground = true,
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();

        // Wait until the icon is created before returning.
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    private void RunMessageLoop()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open Neo Wallet", null, (_, _) => OpenRequested?.Invoke());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) =>
        {
            ExitRequested?.Invoke();
            Application.ExitThread();
        });

        _trayIcon = new NotifyIcon
        {
            Text = "Neo Wallet",
            Icon = LoadEmbeddedIcon(),
            ContextMenuStrip = contextMenu,
            Visible = true,
        };

        _trayIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();

        Trace.WriteLine("[TRAY] System tray icon started");
        _ready.Set();

        Application.Run(); // Blocks until Application.ExitThread()
    }

    /// <summary>
    /// Show a balloon notification in the system tray.
    /// </summary>
    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_trayIcon is null) return;

        try
        {
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = text;
            _trayIcon.BalloonTipIcon = icon;
            _trayIcon.ShowBalloonTip(3000);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TRAY] Failed to show notification: {ex.Message}");
        }
    }

    private static Icon LoadEmbeddedIcon()
    {
        // Try to extract icon from the running exe itself
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null) return icon;
            }
            catch { /* fall through */ }
        }

        // Fallback: use a default system icon
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _ready.Dispose();
    }
}
