using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;
        private bool _hasNavigated = false;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var shell = new AppShell();
            var window = new Window(shell);
            
            // Navigate after shell is loaded
            shell.Loaded += OnShellLoaded;
            
            return window;
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
                
                if (walletExists)
                {
                    // Returning user - go to unlock page (direct page replacement)
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
    }
}
