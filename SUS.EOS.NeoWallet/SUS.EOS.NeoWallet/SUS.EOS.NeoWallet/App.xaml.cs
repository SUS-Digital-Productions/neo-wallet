#pragma warning disable CS0618 // Application.MainPage obsolete warning

using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Pages;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.EosioSigningRequest.Services;
using System.Runtime.InteropServices;

namespace SUS.EOS.NeoWallet;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISystemTrayService _trayService;
    private bool _hasNavigated = false;
    private Window? _mainWindow;
    private string? _pendingEsrUri; // ESR to process after navigation

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _trayService = serviceProvider.GetRequiredService<ISystemTrayService>();
        
        // Register protocol handlers immediately to override Anchor wallet
        var protocolHandler = serviceProvider.GetRequiredService<IProtocolHandlerService>();
        protocolHandler.RegisterProtocolHandlers();
        
        System.Diagnostics.Trace.WriteLine($"[APP] Protocol handlers registered. esr:// default: {protocolHandler.IsDefaultHandler("esr")}");
        System.Diagnostics.Trace.WriteLine($"[APP] Protocol handlers registered. anchor:// default: {protocolHandler.IsDefaultHandler("anchor")}");
    }
    
    /// <summary>
    /// Process an ESR from external activation (single-instance redirect)
    /// </summary>
    public void ProcessExternalEsr(string esrUri)
    {
        System.Diagnostics.Trace.WriteLine($"[APP] ProcessExternalEsr called: {esrUri}");
        
        // Show window if hidden
        _trayService.ShowMainWindow();
        
        // Check if wallet is unlocked - if so, process directly
        var storageService = _serviceProvider.GetRequiredService<IWalletStorageService>();
        if (storageService.IsUnlocked)
        {
            System.Diagnostics.Trace.WriteLine("[APP] Wallet is unlocked, processing ESR directly");
            _ = HandleEsrLinkAsync(esrUri);
        }
        else
        {
            System.Diagnostics.Trace.WriteLine("[APP] Wallet is locked, storing ESR for after unlock");
            _pendingEsrUri = esrUri;
            // The ESR will be processed after user unlocks
        }
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        
        System.Diagnostics.Trace.WriteLine($"[APP] Deep link received: {uri}");
        
        // Show window if hidden
        _trayService.ShowMainWindow();
        
        // Handle esr:// or anchor:// protocol
        if (uri.Scheme == "esr" || uri.Scheme == "anchor")
        {
            System.Diagnostics.Trace.WriteLine($"[APP] Processing ESR link: {uri}");
            ProcessExternalEsr(uri.ToString());
        }
    }

    private async Task HandleEsrLinkAsync(string esrUri)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[APP] HandleEsrLinkAsync: {esrUri}");
            
            // Wait for app to be ready
            await Task.Delay(500);
            
            System.Diagnostics.Trace.WriteLine($"[APP] MainPage type: {Application.Current?.MainPage?.GetType().Name}");
            
            MainPage? mainPage = null;
            
            // Handle different navigation structures
            if (Application.Current?.MainPage is AppShell shell)
            {
                System.Diagnostics.Trace.WriteLine($"[APP] Found AppShell, CurrentPage: {shell.CurrentPage?.GetType().Name}");
                await shell.GoToAsync("//MainPage");
                await Task.Delay(100);
                mainPage = shell.CurrentPage as MainPage;
            }
            else if (Application.Current?.MainPage is NavigationPage navPage)
            {
                System.Diagnostics.Trace.WriteLine($"[APP] Found NavigationPage, CurrentPage: {navPage.CurrentPage?.GetType().Name}");
                
                // Check if current page is MainPage
                mainPage = navPage.CurrentPage as MainPage;
                
                // If not, check the navigation stack
                if (mainPage == null)
                {
                    foreach (var page in navPage.Navigation.NavigationStack)
                    {
                        System.Diagnostics.Trace.WriteLine($"[APP] Stack page: {page?.GetType().Name}");
                        if (page is MainPage mp)
                        {
                            mainPage = mp;
                            break;
                        }
                    }
                }
            }
            else if (Application.Current?.MainPage is MainPage mp)
            {
                mainPage = mp;
            }
            
            // If we found MainPage, process the ESR
            if (mainPage != null)
            {
                System.Diagnostics.Trace.WriteLine("[APP] Found MainPage, processing ESR");
                await mainPage.HandleDeepLinkEsrAsync(esrUri);
            }
            else
            {
                // Fallback: Get MainPage from DI and call directly
                System.Diagnostics.Trace.WriteLine("[APP] MainPage not found in navigation, trying DI");
                var mainPageFromDI = _serviceProvider.GetService<MainPage>();
                if (mainPageFromDI != null)
                {
                    System.Diagnostics.Trace.WriteLine("[APP] Got MainPage from DI, processing ESR");
                    await mainPageFromDI.HandleDeepLinkEsrAsync(esrUri);
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("[APP] Could not find MainPage - processing ESR directly here");
                    // Last resort: process ESR directly in App
                    await ProcessEsrDirectlyAsync(esrUri);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[APP] Error handling ESR link: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[APP] Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Process ESR directly without going through MainPage
    /// </summary>
    private async Task ProcessEsrDirectlyAsync(string esrUri)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[APP] ProcessEsrDirectlyAsync: {esrUri}");
            
            var esrService = _serviceProvider.GetRequiredService<IEsrService>();
            var esrRequest = await esrService.ParseRequestAsync(esrUri);
            
            System.Diagnostics.Trace.WriteLine($"[APP] ESR parsed. ChainId: {esrRequest.ChainId}");
            
            // Get the signing popup page from DI
            var popup = _serviceProvider.GetRequiredService<EsrSigningPopupPage>();
            
            // Get the current page to show popup on
            Page? currentPage = null;
            if (Application.Current?.MainPage is NavigationPage navPage)
            {
                currentPage = navPage.CurrentPage;
            }
            else
            {
                currentPage = Application.Current?.MainPage;
            }
            
            if (currentPage != null)
            {
                System.Diagnostics.Trace.WriteLine("[APP] Showing ESR signing popup");
                await currentPage.Navigation.PushModalAsync(popup);
                
                // Initialize and show the signing request
                var result = await popup.ShowSigningRequestAsync(esrRequest);
                System.Diagnostics.Trace.WriteLine($"[APP] Signing result: {result.Success}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[APP] Error processing ESR directly: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[APP] Stack trace: {ex.StackTrace}");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell();
        _mainWindow = new Window(shell);
        
        // Configure window for background operation
        ConfigureWindowForBackground(_mainWindow);
        
        // Navigate after shell is loaded
        shell.Loaded += OnShellLoaded;
        
        return _mainWindow;
    }

    private void ConfigureWindowForBackground(Window window)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            System.Diagnostics.Trace.WriteLine("[APP] Background mode not supported on this platform");
            return;
        }
        
        // Get platform-specific window handler via reflection to avoid compile-time dependency
        var platformWindow = window.Handler?.PlatformView;
        if (platformWindow == null) return;
        
        System.Diagnostics.Trace.WriteLine("[APP] Configuring Windows-specific handlers");
        
        try
        {
            // Set window reference for single-instance activation via reflection
            var winuiAppType = Type.GetType("SUS.EOS.NeoWallet.WinUI.App, SUS.EOS.NeoWallet.WinUI");
            if (winuiAppType != null)
            {
                var setMainWindowMethod = winuiAppType.GetMethod("SetMainWindow");
                setMainWindowMethod?.Invoke(null, new[] { platformWindow });
            }
            
            // Set window to tray service
            _trayService.SetNativeWindow(platformWindow);
            
            // Initialize system tray
            _trayService.Initialize();
            
            // Handle window close to minimize to tray instead via reflection
            ConfigureWindowClosingHandler(platformWindow);
            
            System.Diagnostics.Trace.WriteLine("[APP] Background mode configured");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[APP] Error configuring background mode: {ex.Message}");
        }
    }
    
    private void ConfigureWindowClosingHandler(object platformWindow)
    {
        try
        {
            // Use reflection to get AppWindow and subscribe to Closing event
            // Microsoft.UI.Windowing.AppWindow.GetFromWindowId(...)
            
            // Get window handle
            var windowNativeType = Type.GetType("WinRT.Interop.WindowNative, Microsoft.WinUI");
            var getWindowHandleMethod = windowNativeType?.GetMethod("GetWindowHandle");
            var hwnd = getWindowHandleMethod?.Invoke(null, new[] { platformWindow });
            
            if (hwnd == null) return;
            
            // Get WindowId
            var win32InteropType = Type.GetType("Microsoft.UI.Win32Interop, Microsoft.WinUI");
            var getWindowIdMethod = win32InteropType?.GetMethod("GetWindowIdFromWindow");
            var windowId = getWindowIdMethod?.Invoke(null, new[] { hwnd });
            
            if (windowId == null) return;
            
            // Get AppWindow
            var appWindowType = Type.GetType("Microsoft.UI.Windowing.AppWindow, Microsoft.WinUI");
            var getFromWindowIdMethod = appWindowType?.GetMethod("GetFromWindowId");
            var appWindow = getFromWindowIdMethod?.Invoke(null, new[] { windowId });
            
            if (appWindow == null) return;
            
            // Subscribe to Closing event
            var closingEvent = appWindowType?.GetEvent("Closing");
            if (closingEvent != null)
            {
                var handler = new Action<object, object>((sender, args) =>
                {
                    System.Diagnostics.Trace.WriteLine("[APP] Window closing - minimizing to tray");
                    
                    // Set Cancel = true
                    var cancelProperty = args.GetType().GetProperty("Cancel");
                    cancelProperty?.SetValue(args, true);
                    
                    _trayService.HideToTray();
                });
                
                // Create delegate for the event
                var delegateType = closingEvent.EventHandlerType;
                if (delegateType != null)
                {
                    var methodInfo = handler.GetType().GetMethod("Invoke");
                    var typedDelegate = Delegate.CreateDelegate(delegateType, handler.Target, methodInfo!);
                    closingEvent.AddEventHandler(appWindow, typedDelegate);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[APP] Error configuring closing handler: {ex.Message}");
        }
    }

    private async void OnShellLoaded(object? sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[APP] OnShellLoaded called");
        if (_hasNavigated)
        {
            System.Diagnostics.Trace.WriteLine("[APP] Already navigated, skipping");
            return;
        }
        _hasNavigated = true;
        
        System.Diagnostics.Trace.WriteLine("[APP] Dispatching CheckWalletStateAsync");
        // Use dispatcher to ensure we're on UI thread and shell is ready
        await Dispatcher.DispatchAsync(async () =>
        {
            // Small delay to ensure shell is fully initialized
            await Task.Delay(50);
            System.Diagnostics.Trace.WriteLine("[APP] Calling CheckWalletStateAsync");
            await CheckWalletStateAsync();
        });
    }

    private async Task CheckWalletStateAsync()
    {
        System.Diagnostics.Trace.WriteLine("[APP] CheckWalletStateAsync started");
        try
        {
            System.Diagnostics.Trace.WriteLine("[APP] Getting services");
            var storageService = _serviceProvider.GetRequiredService<IWalletStorageService>();
            var networkService = _serviceProvider.GetRequiredService<INetworkService>();
            
            // Initialize networks if needed
            System.Diagnostics.Trace.WriteLine("[APP] Getting networks");
            var networks = await networkService.GetNetworksAsync();
            if (!networks.Any())
            {
                System.Diagnostics.Trace.WriteLine("[APP] Initializing predefined networks");
                await networkService.InitializePredefinedNetworksAsync();
            }
            
            // Check if wallet exists
            System.Diagnostics.Trace.WriteLine("[APP] Checking if wallet exists");
            var walletExists = await storageService.WalletExistsAsync();
            System.Diagnostics.Trace.WriteLine($"[APP] Wallet exists: {walletExists}");
            System.Diagnostics.Trace.WriteLine($"[APP] Wallet unlocked: {storageService.IsUnlocked}");
            
            if (walletExists && storageService.IsUnlocked)
            {
                // Wallet is already unlocked - go directly to MainPage
                System.Diagnostics.Trace.WriteLine("[APP] Wallet already unlocked, going to MainPage");
                var mainPage = _serviceProvider.GetRequiredService<MainPage>();
                var navPage = new NavigationPage(mainPage);
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = navPage;
                }
            }
            else if (walletExists)
            {
                // Returning user - wallet exists but locked, go to unlock page
                System.Diagnostics.Trace.WriteLine("[APP] Creating EnterPasswordPage");
                var enterPasswordPage = _serviceProvider.GetRequiredService<Pages.EnterPasswordPage>();
                var navPage = new NavigationPage(enterPasswordPage);
                System.Diagnostics.Trace.WriteLine("[APP] Setting window page to EnterPasswordPage");
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = navPage;
                }
                System.Diagnostics.Trace.WriteLine("[APP] Window page set to EnterPasswordPage");
            }
            else
            {
                // First time user - go to initialization page (direct page replacement)
                System.Diagnostics.Trace.WriteLine("[APP] Creating InitializePage");
                var initializePage = _serviceProvider.GetRequiredService<Pages.InitializePage>();
                var navPage = new NavigationPage(initializePage);
                System.Diagnostics.Trace.WriteLine("[APP] Setting window page to InitializePage");
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = navPage;
                }
                System.Diagnostics.Trace.WriteLine("[APP] Window page set to InitializePage");
            }
            
            // Check for pending protocol activation (from esr:// or anchor:// link)
            await CheckPendingProtocolActivationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[APP] ERROR in CheckWalletStateAsync: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[APP] StackTrace: {ex.StackTrace}");
            // Default to initialization page on error
            try
            {
                System.Diagnostics.Trace.WriteLine("[APP] Attempting fallback to InitializePage");
                var initializePage = _serviceProvider.GetRequiredService<Pages.InitializePage>();
                var navPage = new NavigationPage(initializePage);
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = navPage;
                }
                System.Diagnostics.Trace.WriteLine("[APP] Fallback completed");
            }
            catch (Exception navEx)
            {
                System.Diagnostics.Trace.WriteLine($"[APP] CRITICAL: Failed to set page: {navEx.Message}");
                System.Diagnostics.Trace.WriteLine($"[APP] StackTrace: {navEx.StackTrace}");
            }
        }
    }
    
    private async Task CheckPendingProtocolActivationAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
            
        try
        {
            // Check if we were launched via protocol activation - use reflection
            var winuiAppType = Type.GetType("SUS.EOS.NeoWallet.WinUI.App, SUS.EOS.NeoWallet.WinUI");
            if (winuiAppType == null) return;
            
            var pendingUriProperty = winuiAppType.GetProperty("PendingProtocolUri");
            var pendingUri = pendingUriProperty?.GetValue(null) as Uri;
            
            if (pendingUri != null)
            {
                System.Diagnostics.Trace.WriteLine($"[APP] Found pending protocol URI: {pendingUri}");
                pendingUriProperty?.SetValue(null, null); // Clear it
                
                // Store for processing after unlock if needed
                _pendingEsrUri = pendingUri.ToString();
                
                // If wallet is already unlocked, process immediately
                var storageService = _serviceProvider.GetRequiredService<IWalletStorageService>();
                if (storageService.IsUnlocked)
                {
                    await HandleEsrLinkAsync(_pendingEsrUri);
                    _pendingEsrUri = null;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[APP] Error checking pending protocol: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Called when wallet is unlocked - process any pending ESR
    /// </summary>
    public async Task OnWalletUnlockedAsync()
    {
        System.Diagnostics.Trace.WriteLine("[APP] OnWalletUnlockedAsync called");
        
        if (!string.IsNullOrEmpty(_pendingEsrUri))
        {
            System.Diagnostics.Trace.WriteLine($"[APP] Processing pending ESR after unlock: {_pendingEsrUri}");
            var esrToProcess = _pendingEsrUri;
            _pendingEsrUri = null;
            await HandleEsrLinkAsync(esrToProcess);
        }
    }
}
