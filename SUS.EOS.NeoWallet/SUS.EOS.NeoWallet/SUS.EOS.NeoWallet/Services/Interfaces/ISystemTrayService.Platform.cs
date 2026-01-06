#if WINDOWS
using Microsoft.UI.Xaml;
using Windows.UI.Notifications;
#endif

namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Platform-specific system tray implementation
/// </summary>
public partial interface ISystemTrayService
{
#if WINDOWS
    /// <summary>
    /// Get the WinUI Window instance
    /// </summary>
    Microsoft.UI.Xaml.Window? GetNativeWindow();

    /// <summary>
    /// Set the WinUI Window instance
    /// </summary>
    void SetNativeWindow(Microsoft.UI.Xaml.Window window);
#endif
}
