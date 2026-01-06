using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SUS.EOS.NeoWallet.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        private static Microsoft.UI.Xaml.Window? _mainWindow;
        private static Microsoft.UI.Dispatching.DispatcherQueue? _mainDispatcher;
        
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            
            // Store the dispatcher for the main UI thread
            _mainDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            
            // Handle initial protocol activation for esr:// and anchor:// links
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            System.Diagnostics.Trace.WriteLine($"[WINUI] Activation kind: {activatedArgs.Kind}");
            
            if (activatedArgs.Kind == ExtendedActivationKind.Protocol)
            {
                var protocolArgs = activatedArgs.Data as Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs;
                if (protocolArgs != null)
                {
                    System.Diagnostics.Trace.WriteLine($"[WINUI] Protocol activated with URI: {protocolArgs.Uri}");
                    // Store for later processing after app is ready
                    PendingProtocolUri = protocolArgs.Uri;
                }
            }
        }
        
        /// <summary>
        /// URI from protocol activation, to be processed after app is ready
        /// </summary>
        public static Uri? PendingProtocolUri { get; set; }
        
        /// <summary>
        /// Get the main UI thread dispatcher
        /// </summary>
        public static Microsoft.UI.Dispatching.DispatcherQueue? GetMainDispatcher() => _mainDispatcher;
        
        /// <summary>
        /// Set the main window reference for bringing to foreground
        /// </summary>
        public static void SetMainWindow(Microsoft.UI.Xaml.Window window)
        {
            _mainWindow = window;
        }
        
        /// <summary>
        /// Handle protocol activation from another instance (single-instance redirect)
        /// </summary>
        public static void HandleExternalProtocolActivation(Uri uri)
        {
            System.Diagnostics.Trace.WriteLine($"[WINUI] External protocol activation: {uri}");
            
            // Store the URI
            PendingProtocolUri = uri;
            
            // Bring window to foreground
            BringMainWindowToForeground();
            
            // Notify the MAUI app to process the ESR
            NotifyMauiAppOfEsr(uri.ToString());
        }
        
        public static void BringMainWindowToForeground()
        {
            try
            {
                if (_mainWindow != null)
                {
                    // Get the window handle
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                    
                    // Show the window if minimized
                    ShowWindow(hwnd, SW_RESTORE);
                    
                    // Bring to foreground
                    SetForegroundWindow(hwnd);
                    
                    System.Diagnostics.Trace.WriteLine("[WINUI] Window brought to foreground");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[WINUI] Error bringing window to foreground: {ex.Message}");
            }
        }
        
        private static void NotifyMauiAppOfEsr(string esrUri)
        {
            try
            {
                // Access MAUI app through static Application.Current
                if (Microsoft.Maui.Controls.Application.Current is SUS.EOS.NeoWallet.App mauiApp)
                {
                    System.Diagnostics.Trace.WriteLine("[WINUI] Notifying MAUI app of ESR");
                    mauiApp.ProcessExternalEsr(esrUri);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[WINUI] Error notifying MAUI app: {ex.Message}");
            }
        }
        
        // P/Invoke declarations
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_RESTORE = 9;

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
