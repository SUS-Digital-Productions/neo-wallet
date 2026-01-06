#pragma warning disable CS0618 // DisplayAlert/DisplayActionSheet obsolete warnings

using System.Globalization;
using SUS.EOS.EosioSigningRequest.Models;
using SUS.EOS.EosioSigningRequest.Services;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

/// <summary>
/// Main navigation page with wallet overview and side navigation
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly IAntelopeBlockchainClient _blockchainClient;
    private readonly INetworkService _networkService;
    private readonly IPriceFeedService _priceFeedService;
    private readonly IWalletContextService _walletContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEsrSessionManager _esrSessionManager;
    private bool _esrListenerStarted;

    public MainPage(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        IAntelopeBlockchainClient blockchainClient,
        INetworkService networkService,
        IPriceFeedService priceFeedService,
        IWalletContextService walletContext,
        IServiceProvider serviceProvider,
        IEsrSessionManager esrSessionManager
    )
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Constructor started");
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Calling InitializeComponent");
        InitializeComponent();
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] InitializeComponent completed");
        _accountService = accountService;
        _storageService = storageService;
        _blockchainClient = blockchainClient;
        _networkService = networkService;
        _priceFeedService = priceFeedService;
        _walletContext = walletContext;
        _serviceProvider = serviceProvider;
        _esrSessionManager = esrSessionManager;

        // Subscribe to context changes
        _walletContext.ActiveAccountChanged += OnActiveAccountChanged;
        _walletContext.ActiveNetworkChanged += OnActiveNetworkChanged;

        // Subscribe to ESR signing requests
        _esrSessionManager.SigningRequestReceived += OnEsrSigningRequestReceived;
        _esrSessionManager.StatusChanged += OnEsrStatusChanged;

        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Constructor completed");
    }

    protected override async void OnAppearing()
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] OnAppearing called");
        base.OnAppearing();

        System.Diagnostics.Trace.WriteLine(
            $"[MAINPAGE] Wallet unlocked: {_storageService.IsUnlocked}"
        );

        // Start ESR session manager to listen for signing requests
        await StartEsrListenerAsync();

        // Initialize wallet context if not done
        if (!_walletContext.IsInitialized)
        {
            await _walletContext.InitializeAsync();
        }

        // Update UI from context
        UpdateNetworkUI();
        UpdateAccountUI();

        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Loading wallet data");
        await LoadWalletDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Note: We don't unsubscribe from events because this page might come back
    }

    private void OnActiveAccountChanged(object? sender, WalletAccount? account)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateAccountUI();
            _ = LoadWalletDataAsync();
        });
    }

    private void OnActiveNetworkChanged(object? sender, NetworkConfig? network)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateNetworkUI();
            _ = LoadWalletDataAsync();
        });
    }

    private void UpdateNetworkUI()
    {
        var network = _walletContext.ActiveNetwork;
        if (network != null)
        {
            ChainNameLabel.Text = network.Name;
            ChainIconLabel.Text = network.Symbol[..1].ToUpper();
        }
        else
        {
            ChainNameLabel.Text = "Select Network";
            ChainIconLabel.Text = "?";
        }
    }

    private void UpdateAccountUI()
    {
        var account = _walletContext.ActiveAccount;
        if (account != null)
        {
            AccountNameLabel.Text = account.Data.Account;
            AccountPermissionLabel.Text = $"@{account.Data.Authority}";
            AccountIconLabel.Text = "👤";
            AddressLabel.Text =
                account.Data.PublicKey.Length > 24
                    ? $"{account.Data.PublicKey[..12]}...{account.Data.PublicKey[^10..]}"
                    : account.Data.PublicKey;
        }
        else
        {
            AccountNameLabel.Text = "No Account";
            AccountPermissionLabel.Text = "Tap to select";
            AccountIconLabel.Text = "👤";
            AddressLabel.Text = "No wallet loaded";
        }
    }

    private async void OnAccountSelectorClicked(object? sender, EventArgs e)
    {
        try
        {
            var accounts = await _walletContext.GetAccountsForActiveNetworkAsync();

            if (!accounts.Any())
            {
                await DisplayAlertAsync(
                    "No Accounts",
                    "No accounts found on this network. Import a key first.",
                    "OK"
                );
                return;
            }

            var accountNames = accounts
                .Select(a => $"{a.Data.Account}@{a.Data.Authority}")
                .ToArray();
            var result = await DisplayActionSheet("Select Account", "Cancel", null, accountNames);

            if (!string.IsNullOrEmpty(result) && result != "Cancel")
            {
                var parts = result.Split('@');
                if (parts.Length == 2)
                {
                    var selectedAccount = accounts.FirstOrDefault(a =>
                        a.Data.Account == parts[0] && a.Data.Authority == parts[1]
                    );

                    if (selectedAccount != null)
                    {
                        await _walletContext.SetActiveAccountAsync(selectedAccount);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to change account: {ex.Message}", "OK");
        }
    }

    private async void OnChainSelectorClicked(object sender, EventArgs e)
    {
        try
        {
            var networks = await _networkService.GetNetworksAsync();
            var networkList = networks.Values.Where(n => n.Enabled).ToList();

            if (!networkList.Any())
            {
                await DisplayAlertAsync(
                    "No Networks",
                    "No networks are configured. Please add networks in Settings.",
                    "OK"
                );
                return;
            }

            var networkNames = networkList.Select(n => n.Name).ToArray();
            var result = await DisplayActionSheet("Select Network", "Cancel", null, networkNames);

            if (result != null && result != "Cancel")
            {
                var selectedNetwork = networkList.FirstOrDefault(n => n.Name == result);
                if (selectedNetwork != null)
                {
                    await _walletContext.SetActiveNetworkAsync(selectedNetwork);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to change network: {ex.Message}", "OK");
        }
    }

    private async Task LoadWalletDataAsync()
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] LoadWalletDataAsync started");
        try
        {
            System.Diagnostics.Trace.WriteLine("[MAINPAGE] Loading wallet");
            var wallet = await _storageService.LoadWalletAsync();
            if (wallet == null)
            {
                System.Diagnostics.Trace.WriteLine(
                    "[MAINPAGE] Wallet is null, redirecting to InitializePage"
                );
                var serviceProvider = Application.Current!.Handler.MauiContext!.Services;
                var initializePage = serviceProvider.GetRequiredService<InitializePage>();
                var navPage = new NavigationPage(initializePage);
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = navPage;
                }
                return;
            }
            System.Diagnostics.Trace.WriteLine("[MAINPAGE] Wallet loaded successfully");

            // Check if any keys exist
            if (!wallet.Storage.PublicKeys.Any())
            {
                AddressLabel.Text = "No keys added yet";
                BalanceLabel.Text = "$0.00 USD";
                AssetsCollectionView.ItemsSource = new List<AssetItem>();
                RecentTransactionsCollectionView.ItemsSource = new List<TransactionItem>();
                return;
            }

            // Use active account from context
            var currentAccount = _walletContext.ActiveAccount;

            if (currentAccount != null)
            {
                // Load balances from blockchain
                var balances = await _blockchainClient.GetCurrencyBalanceAsync(
                    "eosio.token",
                    currentAccount.Data.Account,
                    null,
                    CancellationToken.None
                );

                if (balances.Any())
                {
                    var mainBalance = balances.First().Replace(",", ".");
                    var parts = mainBalance.Split(' ');
                    var amount = decimal.TryParse(
                        parts[0],
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var amt
                    )
                        ? amt
                        : 0;
                    var symbol = parts.Length > 1 ? parts[1] : "WAX";

                    // Get USD price
                    var usdValue = await _priceFeedService.ConvertToUsdAsync(symbol, amount);
                    BalanceLabel.Text = $"${usdValue:F2} USD";

                    // Build asset list
                    var assets = balances
                        .Select(b =>
                        {
                            var p = b.Split(' ');
                            return new AssetItem
                            {
                                Symbol = p.Length > 1 ? p[1] : "???",
                                Amount = p[0],
                                Balance = b,
                            };
                        })
                        .ToList();

                    AssetsCollectionView.ItemsSource = assets;
                }
                else
                {
                    BalanceLabel.Text = "$0.00 USD";
                    AssetsCollectionView.ItemsSource = new List<AssetItem>();
                }
            }
            else
            {
                BalanceLabel.Text = "$0.00 USD";
                AssetsCollectionView.ItemsSource = new List<AssetItem>();
            }

            // Placeholder for transactions
            RecentTransactionsCollectionView.ItemsSource = new List<TransactionItem>();
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Error", $"Failed to load data: {ex.Message}", "OK");
        }
    }

    // Navigation handlers
    private void OnNavHomeClicked(object sender, EventArgs e)
    {
        // Already on home - do nothing or refresh
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Home clicked (already here)");
    }

    private async void OnDashboardClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Dashboard clicked");
        var dashboardPage = _serviceProvider.GetRequiredService<DashboardPage>();
        await Navigation.PushAsync(dashboardPage);
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Send clicked");
        var sendPage = _serviceProvider.GetRequiredService<SendPage>();
        await Navigation.PushAsync(sendPage);
    }

    private async void OnReceiveClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Receive clicked");
        var receivePage = _serviceProvider.GetRequiredService<ReceivePage>();
        await Navigation.PushAsync(receivePage);
    }

    private async void OnContractActionsClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Contract Actions clicked");
        var contractActionsPage = _serviceProvider.GetRequiredService<ContractActionsPage>();
        await Navigation.PushAsync(contractActionsPage);
    }

    private async void OnContractTablesClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Contract Tables clicked");
        var contractTablesPage = _serviceProvider.GetRequiredService<ContractTablesPage>();
        await Navigation.PushAsync(contractTablesPage);
    }

    private async void OnImportWalletClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Import wallet clicked");
        var importWalletPage = _serviceProvider.GetRequiredService<ImportWalletPage>();
        await Navigation.PushAsync(importWalletPage);
    }

    private async void OnCreateAccountClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Create account clicked");
        var createAccountPage = _serviceProvider.GetRequiredService<CreateAccountPage>();
        await Navigation.PushAsync(createAccountPage);
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] Settings clicked");
        var settingsPage = _serviceProvider.GetRequiredService<SettingsPage>();
        await Navigation.PushAsync(settingsPage);
    }

    private async void OnViewAllTransactionsClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[MAINPAGE] View all transactions clicked");
        var dashboardPage = _serviceProvider.GetRequiredService<DashboardPage>();
        await Navigation.PushAsync(dashboardPage);
    }

    #region ESR Session Manager

    private async Task StartEsrListenerAsync()
    {
        if (_esrListenerStarted)
            return;

        try
        {
            System.Diagnostics.Trace.WriteLine("[MAINPAGE] Starting ESR session manager...");
            await _esrSessionManager.ConnectAsync();
            _esrListenerStarted = true;
            
            var linkUrl = $"wss://cb.anchor.link/{_esrSessionManager.LinkId}";
            System.Diagnostics.Trace.WriteLine(
                $"[MAINPAGE] ESR session manager started. LinkId: {_esrSessionManager.LinkId}"
            );
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] ========================================");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] ANCHOR LINK URL: {linkUrl}");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] ========================================");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] To test with WAX Bloks:");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] 1. Go to https://wax.bloks.io/");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] 2. Open browser console (F12)");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] 3. Run: localStorage.setItem('anchorLink', '{linkUrl}')");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] 4. Refresh page and try 'Launch Anchor'");
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] ========================================");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[MAINPAGE] Failed to start ESR session manager: {ex.Message}"
            );
        }
    }

    private void OnEsrStatusChanged(object? sender, EsrSessionStatusEventArgs e)
    {
        System.Diagnostics.Trace.WriteLine($"[MAINPAGE] ESR Status changed: {e.Status}");

        MainThread.BeginInvokeOnMainThread(() => {
            // Could update UI to show connection status if needed
        });
    }

    private async void OnEsrSigningRequestReceived(object? sender, EsrSigningRequestEventArgs e)
    {
        System.Diagnostics.Trace.WriteLine(
            $"[MAINPAGE] ESR Signing request received from: {e.Session?.Name ?? "Unknown"}"
        );

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await ShowSigningPopupAsync(e);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[MAINPAGE] Error handling ESR request: {ex.Message}"
            );
        }
    }

    private async Task ShowSigningPopupAsync(EsrSigningRequestEventArgs e)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine("[MAINPAGE] Creating EsrSigningPopupPage...");
            var popupPage = _serviceProvider.GetRequiredService<EsrSigningPopupPage>();

            // Get dApp name from session if available
            var dAppName = e.Session?.Name ?? "Unknown Application";

            // Push the popup page modally
            System.Diagnostics.Trace.WriteLine("[MAINPAGE] Pushing signing popup modal...");
            await Navigation.PushModalAsync(popupPage);

            System.Diagnostics.Trace.WriteLine("[MAINPAGE] Showing signing request...");
            var result = await popupPage.ShowSigningRequestAsync(
                e.Request!,
                rawPayload: e.RawPayload,
                callbackUrl: e.Callback,
                dAppName: dAppName
            );

            System.Diagnostics.Trace.WriteLine(
                $"[MAINPAGE] Signing result: Success={result.Success}, Cancelled={result.Cancelled}"
            );

            if (result.Success && result.Signatures != null && result.Signatures.Count > 0)
            {
                // Send callback response if needed
                System.Diagnostics.Trace.WriteLine(
                    "[MAINPAGE] Signing successful, sending callback..."
                );
                var callback = new EsrCallbackPayload
                {
                    Signature = result.Signatures.FirstOrDefault(),
                    TransactionId = result.TransactionId,
                    SignerActor = result.Account,
                    SignerPermission = result.Permission,
                    LinkChannel = _esrSessionManager.LinkId,
                    BlockNum = 0, // Will be filled by actual block
                };
                await _esrSessionManager.SendCallbackAsync(callback);
            }
            else if (result.Cancelled)
            {
                System.Diagnostics.Trace.WriteLine("[MAINPAGE] Signing cancelled by user");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"[MAINPAGE] Signing failed: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[MAINPAGE] Error showing signing popup: {ex.Message}"
            );
            await DisplayAlert("Error", $"Failed to process signing request: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle ESR deep link from protocol activation (esr:// or anchor://)
    /// </summary>
    public async Task HandleDeepLinkEsrAsync(string esrUri)
    {
        try
        {
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] HandleDeepLinkEsrAsync: {esrUri}");

            // Parse the ESR
            var esrService = _serviceProvider.GetRequiredService<IEsrService>();
            var esrRequest = await esrService.ParseRequestAsync(esrUri);

            System.Diagnostics.Trace.WriteLine(
                $"[MAINPAGE] ESR parsed successfully. ChainId: {esrRequest.ChainId}"
            );

            // Create EventArgs to pass to signing popup
            var eventArgs = new EsrSigningRequestEventArgs
            {
                Request = esrRequest,
                Session = null, // No session for deep link ESR
                Callback = null, // No callback URL in envelope for deep link ESR
                IsIdentityRequest =
                    !esrRequest.Payload.IsTransaction && !esrRequest.Payload.IsAction
            };

            // Show signing popup
            await ShowSigningPopupAsync(eventArgs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[MAINPAGE] Error handling deep link ESR: {ex.Message}"
            );
            System.Diagnostics.Trace.WriteLine($"[MAINPAGE] Stack trace: {ex.StackTrace}");
            await DisplayAlert("Error", $"Failed to process ESR link: {ex.Message}", "OK");
        }
    }

    #endregion

    // Data binding classes
    private class AssetItem
    {
        public string Symbol { get; set; } = "";
        public string Amount { get; set; } = "0";
        public string Balance { get; set; } = "";
    }

    private class TransactionItem
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Amount { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
