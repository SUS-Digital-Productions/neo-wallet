namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// System tray icon service for background operation
/// </summary>
public partial interface ISystemTrayService
{
    /// <summary>
    /// Set the native window handle for platform-specific operations
    /// </summary>
    void SetNativeWindow(object window);

    /// <summary>
    /// Initialize system tray icon
    /// </summary>
    void Initialize();

    /// <summary>
    /// Show the main window
    /// </summary>
    void ShowMainWindow();

    /// <summary>
    /// Hide the main window to tray
    /// </summary>
    void HideToTray();

    /// <summary>
    /// Exit the application completely
    /// </summary>
    void ExitApplication();

    /// <summary>
    /// Show notification balloon
    /// </summary>
    void ShowNotification(string title, string message);
}
