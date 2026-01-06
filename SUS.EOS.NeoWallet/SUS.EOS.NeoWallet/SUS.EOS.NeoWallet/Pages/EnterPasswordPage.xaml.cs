using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

public partial class EnterPasswordPage : ContentPage
{
    private readonly IWalletStorageService _storageService;
    private readonly INetworkService _networkService;
    private readonly IServiceProvider _serviceProvider;
    private int _failedAttempts = 0;

    public EnterPasswordPage(IWalletStorageService storageService, INetworkService networkService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _storageService = storageService;
        _networkService = networkService;
        _serviceProvider = serviceProvider;
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ErrorLabel.Text = "Password cannot be empty.";
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        LoginButton.IsEnabled = false;

        try
        {
            // Attempt to unlock the wallet
            var unlocked = await _storageService.UnlockWalletAsync(PasswordEntry.Text);

            if (unlocked)
            {
                _failedAttempts = 0;

                // Initialize networks if needed
                System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Checking networks");
                var networks = await _networkService.GetNetworksAsync();
                if (!networks.Any())
                {
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Initializing networks");
                    await _networkService.InitializePredefinedNetworksAsync();
                }

                // Navigate to MainPage (direct window page replacement - bypass Navigation stack)
                System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Creating MainPage");
                
                try
                {
                    // Debug: Resolve each dependency separately to find which one hangs
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Resolving IWalletAccountService...");
                    var accountService = _serviceProvider.GetRequiredService<IWalletAccountService>();
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] IWalletAccountService resolved");
                    
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Resolving IWalletStorageService...");
                    var storageService = _serviceProvider.GetRequiredService<IWalletStorageService>();
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] IWalletStorageService resolved");
                    
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Resolving IAntelopeBlockchainClient...");
                    var blockchainClient = _serviceProvider.GetRequiredService<IAntelopeBlockchainClient>();
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] IAntelopeBlockchainClient resolved");
                    
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Resolving INetworkService...");
                    var networkService = _serviceProvider.GetRequiredService<INetworkService>();
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] INetworkService resolved");
                    
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Resolving IPriceFeedService...");
                    var priceFeedService = _serviceProvider.GetRequiredService<IPriceFeedService>();
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] IPriceFeedService resolved");
                    
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] All dependencies resolved, now creating MainPage...");
                    var mainPage = _serviceProvider.GetRequiredService<MainPage>();
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] MainPage resolved successfully");
                    
                    System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Setting Window.Page directly");
                    if (Application.Current?.Windows.Count > 0)
                    {
                        // Wrap in NavigationPage for navigation support within MainPage
                        var navPage = new NavigationPage(mainPage);
                        Application.Current.Windows[0].Page = navPage;
                        System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] Window.Page set to MainPage");
                        
                        // Notify app that wallet is unlocked (process pending ESR)
                        if (Application.Current is App app)
                        {
                            await app.OnWalletUnlockedAsync();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("[ENTER_PASSWORD] ERROR: No windows available");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[ENTER_PASSWORD] ERROR: {ex.Message}");
                    System.Diagnostics.Trace.WriteLine($"[ENTER_PASSWORD] StackTrace: {ex.StackTrace}");
                }
            }
            else
            {
                _failedAttempts++;

                if (_failedAttempts >= 3)
                {
                    ErrorLabel.Text =
                        $"Incorrect password. {5 - _failedAttempts} attempts remaining.";
                }
                else
                {
                    ErrorLabel.Text = "Incorrect password. Please try again.";
                }
                ErrorLabel.IsVisible = true;

                // Lock out after 5 attempts
                if (_failedAttempts >= 5)
                {
                    ErrorLabel.Text = "Too many failed attempts. Please restart the app.";
                    LoginButton.IsEnabled = false;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Error: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            if (_failedAttempts < 5)
            {
                LoginButton.IsEnabled = true;
            }
        }
    }
}
