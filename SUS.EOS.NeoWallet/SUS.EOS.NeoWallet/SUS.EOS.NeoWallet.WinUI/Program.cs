using Microsoft.Windows.AppLifecycle;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Activation;

namespace SUS.EOS.NeoWallet.WinUI;

/// <summary>
/// Custom entry point for single-instance application support
/// </summary>
public static class Program
{
    private const string AppInstanceKey = "NeoWallet-SingleInstance";

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Check if this is the first instance
        var isMainInstance = DecideRedirection();

        if (isMainInstance)
        {
            System.Diagnostics.Trace.WriteLine("[PROGRAM] This is the main instance - starting app");
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        else
        {
            System.Diagnostics.Trace.WriteLine("[PROGRAM] Redirected to existing instance - exiting");
        }
    }

    private static bool DecideRedirection()
    {
        var isMainInstance = true;
        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var mainInstance = AppInstance.FindOrRegisterForKey(AppInstanceKey);

        System.Diagnostics.Trace.WriteLine($"[PROGRAM] Activation kind: {activatedArgs.Kind}");
        System.Diagnostics.Trace.WriteLine($"[PROGRAM] Is registered instance: {mainInstance.IsCurrent}");

        // Extract protocol URI if this is a protocol activation
        Uri? protocolUri = null;
        if (activatedArgs.Kind == ExtendedActivationKind.Protocol)
        {
            var protocolArgs = activatedArgs.Data as IProtocolActivatedEventArgs;
            protocolUri = protocolArgs?.Uri;
            System.Diagnostics.Trace.WriteLine($"[PROGRAM] Protocol URI from args: {protocolUri}");
        }

        if (!mainInstance.IsCurrent)
        {
            // Another instance is already running, redirect to it
            System.Diagnostics.Trace.WriteLine("[PROGRAM] Redirecting activation to existing instance...");
            isMainInstance = false;
            
            // If we have a protocol URI, save it to a temp file so the main instance can read it
            // This is a workaround for the activation kind getting lost during redirect
            if (protocolUri != null)
            {
                try
                {
                    var tempFile = Path.Combine(Path.GetTempPath(), "neowallet_pending_esr.txt");
                    File.WriteAllText(tempFile, protocolUri.ToString());
                    System.Diagnostics.Trace.WriteLine($"[PROGRAM] Saved ESR to temp file: {tempFile}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[PROGRAM] Error saving ESR to temp: {ex.Message}");
                }
            }
            
            RedirectActivationTo(activatedArgs, mainInstance);
        }
        else
        {
            // This is the main instance - register for activation events from other instances
            System.Diagnostics.Trace.WriteLine("[PROGRAM] Registering for activation events");
            mainInstance.Activated += OnActivated;
        }

        return isMainInstance;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        System.Diagnostics.Trace.WriteLine($"[PROGRAM] OnActivated: {args.Kind}");
        System.Diagnostics.Trace.WriteLine($"[PROGRAM] OnActivated Data type: {args.Data?.GetType().FullName}");

        Uri? protocolUri = null;

        // Check for Protocol activation
        if (args.Kind == ExtendedActivationKind.Protocol)
        {
            var protocolArgs = args.Data as IProtocolActivatedEventArgs;
            if (protocolArgs != null)
            {
                protocolUri = protocolArgs.Uri;
                System.Diagnostics.Trace.WriteLine($"[PROGRAM] Protocol activation received: {protocolUri}");
            }
        }
        // For Launch activations, check if protocol data is embedded or in temp file
        else if (args.Kind == ExtendedActivationKind.Launch)
        {
            System.Diagnostics.Trace.WriteLine("[PROGRAM] Launch activation - checking for ESR data");
            
            // Check temp file for ESR URL (workaround for activation kind getting lost)
            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), "neowallet_pending_esr.txt");
                if (File.Exists(tempFile))
                {
                    var esrUrl = File.ReadAllText(tempFile).Trim();
                    File.Delete(tempFile); // Clean up
                    
                    // ESR uses esr: not esr:// - check both formats
                    if (!string.IsNullOrEmpty(esrUrl) && (esrUrl.StartsWith("esr:") || esrUrl.StartsWith("anchor:")))
                    {
                        protocolUri = new Uri(esrUrl);
                        System.Diagnostics.Trace.WriteLine($"[PROGRAM] Found ESR URL in temp file: {protocolUri}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[PROGRAM] Error reading temp file: {ex.Message}");
            }
            
            // Also try extracting from launch arguments
            if (protocolUri == null)
            {
                var launchArgs = args.Data as Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs;
                if (launchArgs != null && !string.IsNullOrEmpty(launchArgs.Arguments))
                {
                    System.Diagnostics.Trace.WriteLine($"[PROGRAM] Launch arguments: {launchArgs.Arguments}");
                    // ESR uses esr: not esr:// - match both formats, also handle quoted strings
                    var match = System.Text.RegularExpressions.Regex.Match(launchArgs.Arguments, @"[""']?(esr:[^\s""']+|anchor:[^\s""']+)");
                    if (match.Success)
                    {
                        var esrUrl = match.Groups[1].Value;
                        protocolUri = new Uri(esrUrl);
                        System.Diagnostics.Trace.WriteLine($"[PROGRAM] Found ESR URL in launch args: {protocolUri}");
                    }
                }
            }
        }

        // Process if we found a protocol URI
        if (protocolUri != null)
        {
            System.Diagnostics.Trace.WriteLine($"[PROGRAM] Processing protocol URI: {protocolUri}");
            App.PendingProtocolUri = protocolUri;
            BringToForegroundAndProcessEsr(protocolUri);
        }
        else
        {
            System.Diagnostics.Trace.WriteLine("[PROGRAM] No protocol URI found in activation");
            // Still bring window to foreground for any activation
            BringWindowToForeground();
        }
    }
    
    private static void BringWindowToForeground()
    {
        try
        {
            System.Diagnostics.Trace.WriteLine("[PROGRAM] BringWindowToForeground called");
            
            // Get dispatcher from the app's main window
            var dispatcher = GetAppDispatcher();
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() =>
                {
                    System.Diagnostics.Trace.WriteLine("[PROGRAM] Executing BringMainWindowToForeground on UI thread");
                    App.BringMainWindowToForeground();
                });
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("[PROGRAM] No dispatcher available");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[PROGRAM] Error bringing window to foreground: {ex.Message}");
        }
    }

    private static void BringToForegroundAndProcessEsr(Uri uri)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[PROGRAM] BringToForegroundAndProcessEsr called with: {uri}");
            
            // Get dispatcher from the app's main window
            var dispatcher = GetAppDispatcher();
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() =>
                {
                    System.Diagnostics.Trace.WriteLine("[PROGRAM] Processing ESR on UI thread");
                    App.HandleExternalProtocolActivation(uri);
                });
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("[PROGRAM] No dispatcher available - calling directly");
                // Try calling directly as fallback
                App.HandleExternalProtocolActivation(uri);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[PROGRAM] Error processing ESR: {ex.Message}");
        }
    }
    
    private static Microsoft.UI.Dispatching.DispatcherQueue? GetAppDispatcher()
    {
        try
        {
            // Try to get from current thread first
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcher != null)
                return dispatcher;
            
            // Try to get from the WinUI app's main window
            if (Microsoft.UI.Xaml.Application.Current is App app)
            {
                // Access the dispatcher through App's static method
                return App.GetMainDispatcher();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[PROGRAM] Error getting dispatcher: {ex.Message}");
        }
        return null;
    }

    // P/Invoke for SetForegroundWindow
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static void RedirectActivationTo(AppActivationArguments args, AppInstance targetInstance)
    {
        // Redirect to the main instance
        targetInstance.RedirectActivationToAsync(args).AsTask().Wait();
    }
}
