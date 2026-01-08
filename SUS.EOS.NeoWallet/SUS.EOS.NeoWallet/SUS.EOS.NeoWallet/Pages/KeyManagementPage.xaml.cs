using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;
using SUS.EOS.NeoWallet.Services.Models.WalletData;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet.Pages;

public partial class KeyManagementPage : ContentPage
{
    private readonly IWalletStorageService _storageService;
    private readonly IWalletAccountService _accountService;
    private readonly IAntelopeBlockchainClient _blockchainClient;
    private readonly IWalletContextService _walletContext;

    private readonly ObservableCollection<KeyItemViewModel> _keys = [];
    private string? _sessionPassword;

    public KeyManagementPage(
        IWalletStorageService storageService,
        IWalletAccountService accountService,
        IAntelopeBlockchainClient blockchainClient,
        IWalletContextService walletContext
    )
    {
        InitializeComponent();

        _storageService = storageService;
        _accountService = accountService;
        _blockchainClient = blockchainClient;
        _walletContext = walletContext;

        KeysCollectionView.ItemsSource = _keys;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Prompt for password on page entry
        if (string.IsNullOrWhiteSpace(_sessionPassword))
        {
            try
            {
                var password = await ShowTextInputDialogAsync(
                    "Wallet Password",
                    "Enter your wallet password to access key management:",
                    "OK",
                    "Cancel",
                    isPassword: true
                );

                if (string.IsNullOrWhiteSpace(password))
                {
                    // User cancelled, go back
                    if (Navigation.NavigationStack.Count > 0 && Navigation.NavigationStack.Last() == this)
                    {
                        await Navigation.PopAsync();
                    }
                    return;
                }

                if (!await _storageService.ValidatePasswordAsync(password))
                {
                    await DisplayAlertAsync("Error", "Invalid password", "OK");
                    if (Navigation.NavigationStack.Count > 0 && Navigation.NavigationStack.Last() == this)
                    {
                        await Navigation.PopAsync();
                    }
                    return;
                }

                _sessionPassword = password;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT] Error in password dialog: {ex.Message}");
                // If dialog was dismissed or errored, go back
                if (Navigation.NavigationStack.Count > 0 && Navigation.NavigationStack.Last() == this)
                {
                    await Navigation.PopAsync();
                }
                return;
            }
        }

        await LoadKeysAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Clear password when leaving page
        _sessionPassword = null;
    }

    private async Task LoadKeysAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            EmptyState.IsVisible = false;

            var wallet = await _storageService.LoadWalletAsync();
            var accounts = wallet?.Wallets ?? [];

            // Group accounts by public key
            var keyGroups = accounts
                .GroupBy(a => a.Data.PublicKey)
                .Select(g => new KeyItemViewModel
                {
                    PublicKey = g.Key,
                    AccountCount = g.Count(),
                    Accounts = [.. g],
                })
                .ToList();

            _keys.Clear();
            foreach (var key in keyGroups)
            {
                _keys.Add(key);
            }

            EmptyState.IsVisible = _keys.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT] Error loading keys: {ex.Message}");
            await DisplayAlertAsync("Error", $"Failed to load keys: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnAddKeyClicked(object sender, EventArgs e)
    {
        try
        {
            var privateKey = await ShowTextInputDialogAsync(
                "Import Key",
                "Enter private key (WIF, PVT_K1_, or hex format):",
                "Import",
                "Cancel",
                isPassword: false
            );

            if (string.IsNullOrWhiteSpace(privateKey))
                return;

            var password = await ShowTextInputDialogAsync(
                "Wallet Password",
                "Enter your wallet password to encrypt the key:",
                "Confirm",
                "Cancel",
                isPassword: true
            );

            if (string.IsNullOrWhiteSpace(password))
                return;

            // Validate password
            if (!await _storageService.ValidatePasswordAsync(password))
            {
                await DisplayAlertAsync("Error", "Invalid password", "OK");
                return;
            }

            // Import key - this will trigger get linked accounts
            await GetAndAddLinkedAccountsAsync(privateKey.Trim(), password);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT] Error adding key: {ex.Message}");
            await DisplayAlertAsync("Error", $"Failed to add key: {ex.Message}", "OK");
        }
    }

    private async void OnKeyMenuClicked(object sender, EventArgs e)
    {
        try
        {
            if (
                sender is not Button button
                || button.CommandParameter is not KeyItemViewModel keyItem
            )
                return;

            var action = await DisplayActionSheetAsync(
                $"Key: {keyItem.PublicKey[..20]}...",
                "Cancel",
                null,
                "Get Linked Accounts",
                "Copy Public Key",
                "View Private Key",
                "View Details",
                "Remove from Wallet"
            );

            switch (action)
            {
                case "Get Linked Accounts":
                    await GetLinkedAccountsForKeyAsync(keyItem);
                    break;

                case "Copy Public Key":
                    await ShowCopyKeyDialogAsync(keyItem);
                    break;

                case "View Private Key":
                    await ViewPrivateKeyAsync(keyItem);
                    break;

                case "View Details":
                    await ShowKeyDetailsAsync(keyItem);
                    break;

                case "Remove from Wallet":
                    await RemoveKeyFromWalletAsync(keyItem);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Error handling menu: {ex.Message}"
            );
            await DisplayAlertAsync("Error", $"Failed: {ex.Message}", "OK");
        }
    }

    private async Task GetLinkedAccountsForKeyAsync(KeyItemViewModel keyItem)
    {
        try
        {
            // Use session password (already validated on page entry)
            string? privateKey = null;
            if (_storageService.IsUnlocked)
            {
                privateKey = _storageService.GetUnlockedPrivateKey(keyItem.PublicKey);
            }
            else
            {
                privateKey = await _accountService.GetPrivateKeyAsync(
                    keyItem.Accounts.First().Data.Account,
                    keyItem.Accounts.First().Data.Authority,
                    keyItem.Accounts.First().Data.ChainId,
                    _sessionPassword!
                );
            }

            if (string.IsNullOrEmpty(privateKey))
            {
                await DisplayAlertAsync("Error", "Failed to retrieve private key", "OK");
                return;
            }

            await GetAndAddLinkedAccountsAsync(privateKey, _sessionPassword!);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Error getting linked accounts: {ex.Message}"
            );
            await DisplayAlertAsync("Error", $"Failed: {ex.Message}", "OK");
        }
    }

    private async Task GetAndAddLinkedAccountsAsync(string privateKey, string password)
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            // Derive public key
            var key = SUS.EOS.Sharp.Cryptography.EosioKey.FromPrivateKey(privateKey);
            var publicKey = key.PublicKey;

            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] ========================================"
            );
            System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT] KEY VERIFICATION");
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] ========================================"
            );
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Private key (first 10 chars): {privateKey[..Math.Min(10, privateKey.Length)]}..."
            );
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Derived public key (Legacy):  {key.PublicKey}"
            );
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Derived public key (Modern):  {key.PublicKeyK1}"
            );
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Public key bytes (Hex):       {Convert.ToHexString(key.GetPublicKeyBytes())}"
            );
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] ========================================"
            );
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Searching for accounts with key: {publicKey}"
            );

            // Query blockchain for accounts using this key using Light API
            // Try both legacy and modern formats
            var lightClient = new SUS.EOS.Sharp.Services.LightApiClient(
                "https://wax.light-api.net"
            );

            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Trying Light API with legacy format: {publicKey}"
            );
            var keyResponseLegacy = await lightClient.GetAccountsByKeyAsync(publicKey);

            var modernKey = key.PublicKeyK1;
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Trying Light API with modern format: {modernKey}"
            );
            var keyResponseModern = await lightClient.GetAccountsByKeyAsync(modernKey);

            // Extract accounts from all chains (combine both responses)
            var accountsLegacy =
                keyResponseLegacy
                    .Chains?.SelectMany(chain => chain.Value.Accounts ?? [])
                    .Select(acc => acc.AccountName)
                    .Distinct()
                    .ToList() ?? [];

            var accountsModern =
                keyResponseModern
                    .Chains?.SelectMany(chain => chain.Value.Accounts ?? [])
                    .Select(acc => acc.AccountName)
                    .Distinct()
                    .ToList() ?? [];

            var accounts = accountsLegacy.Union(accountsModern).Distinct().ToList();

            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Found {accountsLegacy.Count} accounts with legacy format"
            );
            foreach (var acc in accountsLegacy)
                System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT]   - {acc}");

            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Found {accountsModern.Count} accounts with modern format"
            );
            foreach (var acc in accountsModern)
                System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT]   - {acc}");

            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Total unique accounts: {accounts.Count}"
            );

            if (accounts.Count == 0)
            {
                await DisplayAlertAsync(
                    "No Accounts Found",
                    $"No accounts found using this key on the current network.\n\nPublic Key: {publicKey}",
                    "OK"
                );
                return;
            }

            // Show account selection dialog
            var selectedAccounts = await ShowAccountSelectionDialogAsync(accounts, publicKey);

            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] User selected {selectedAccounts?.Count ?? 0} accounts"
            );

            if (selectedAccounts == null || selectedAccounts.Count == 0)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[KEYMANAGEMENT] No accounts selected, exiting"
                );
                return;
            }

            // Add selected accounts to wallet
            var addedCount = 0;
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Starting to process {selectedAccounts.Count} selected accounts"
            );

            foreach (var accountName in selectedAccounts)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"[KEYMANAGEMENT] Processing account: {accountName}"
                );
                try
                {
                    // Get full account data from blockchain to determine permissions
                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Fetching account data for {accountName}"
                    );
                    var accountData = await _blockchainClient.GetAccountAsync(accountName);
                    var chainInfo = await _blockchainClient.GetInfoAsync();

                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Chain ID: {chainInfo.ChainId}"
                    );

                    // Debug: Log all account permissions and their keys
                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Account {accountName} has {accountData.Permissions?.Count ?? 0} permissions"
                    );
                    if (accountData.Permissions != null)
                    {
                        foreach (var perm in accountData.Permissions)
                        {
                            var keyCount = perm.RequiredAuth?.Keys?.Count ?? 0;
                            System.Diagnostics.Trace.WriteLine(
                                $"[KEYMANAGEMENT]   Permission '{perm.PermName}' has {keyCount} keys"
                            );
                            if (perm.RequiredAuth?.Keys != null)
                            {
                                foreach (var keyAuth in perm.RequiredAuth.Keys)
                                {
                                    System.Diagnostics.Trace.WriteLine(
                                        $"[KEYMANAGEMENT]     - Key: {keyAuth.Key} (Weight: {keyAuth.Weight})"
                                    );
                                }
                            }
                        }
                    }
                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Looking for public key: {publicKey}"
                    );

                    // Derive both key formats for comparison
                    var keyObj = SUS.EOS.Sharp.Cryptography.EosioKey.FromPrivateKey(privateKey);
                    var legacyFormat = keyObj.PublicKey; // EOS...
                    var modernFormat = keyObj.PublicKeyK1; // PUB_K1_...

                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Comparing with legacy format: {legacyFormat}"
                    );
                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Comparing with modern format: {modernFormat}"
                    );

                    // Debug: Check if publicKey matches either derived format
                    if (publicKey != legacyFormat && publicKey != modernFormat)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[KEYMANAGEMENT] WARNING: Stored publicKey doesn't match derived formats!"
                        );
                        System.Diagnostics.Trace.WriteLine(
                            $"[KEYMANAGEMENT]   Stored:  {publicKey}"
                        );
                        System.Diagnostics.Trace.WriteLine(
                            $"[KEYMANAGEMENT]   Derived: {legacyFormat}"
                        );
                        System.Diagnostics.Trace.WriteLine(
                            $"[KEYMANAGEMENT]   This suggests an encoding bug or wrong private key!"
                        );
                    }

                    // Check each permission's keys against ALL our formats
                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Checking each on-chain key:"
                    );
                    if (accountData.Permissions != null)
                    {
                        foreach (var perm in accountData.Permissions)
                        {
                            if (perm.RequiredAuth?.Keys != null)
                            {
                                foreach (var keyAuth in perm.RequiredAuth.Keys)
                                {
                                    bool matchesPublicKey = keyAuth.Key == publicKey;
                                    bool matchesLegacy = keyAuth.Key == legacyFormat;
                                    bool matchesModern = keyAuth.Key == modernFormat;
                                    bool matchesPublicKeyIgnoreChecksum = KeysMatchIgnoringChecksum(
                                        keyAuth.Key,
                                        publicKey
                                    );
                                    bool matchesLegacyIgnoreChecksum = KeysMatchIgnoringChecksum(
                                        keyAuth.Key,
                                        legacyFormat
                                    );
                                    bool matchesModernIgnoreChecksum = KeysMatchIgnoringChecksum(
                                        keyAuth.Key,
                                        modernFormat
                                    );

                                    System.Diagnostics.Trace.WriteLine(
                                        $"[KEYMANAGEMENT]   Comparing {keyAuth.Key}:"
                                    );
                                    System.Diagnostics.Trace.WriteLine(
                                        $"[KEYMANAGEMENT]     Exact matches: publicKey={matchesPublicKey}, legacy={matchesLegacy}, modern={matchesModern}"
                                    );
                                    System.Diagnostics.Trace.WriteLine(
                                        $"[KEYMANAGEMENT]     Ignore checksum: publicKey={matchesPublicKeyIgnoreChecksum}, legacy={matchesLegacyIgnoreChecksum}, modern={matchesModernIgnoreChecksum}"
                                    );

                                    if (
                                        matchesPublicKey
                                        || matchesLegacy
                                        || matchesModern
                                        || matchesPublicKeyIgnoreChecksum
                                        || matchesLegacyIgnoreChecksum
                                        || matchesModernIgnoreChecksum
                                    )
                                    {
                                        System.Diagnostics.Trace.WriteLine(
                                            $"[KEYMANAGEMENT]   ✓ MATCH FOUND on {perm.PermName}: {keyAuth.Key}"
                                        );
                                    }
                                }
                            }
                        }
                    }

                    // Find permissions that use this key (check both formats AND checksum-ignoring comparison)
                    var permissions =
                        accountData
                            .Permissions?.Where(p =>
                                p.RequiredAuth?.Keys?.Any(k =>
                                    k.Key == publicKey
                                    || k.Key == legacyFormat
                                    || k.Key == modernFormat
                                    || KeysMatchIgnoringChecksum(k.Key, publicKey)
                                    || KeysMatchIgnoringChecksum(k.Key, legacyFormat)
                                    || KeysMatchIgnoringChecksum(k.Key, modernFormat)
                                ) == true
                            )
                            .Select(p => p.PermName)
                            .ToList() ?? [];

                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Found {permissions.Count} permissions for {accountName}: {string.Join(", ", permissions)}"
                    );

                    // Add account for each permission found
                    foreach (var permission in permissions)
                    {
                        try
                        {
                            // Check if account already exists
                            var existingAccount = await _accountService.GetAccountAsync(
                                accountName,
                                permission,
                                chainInfo.ChainId
                            );

                            if (existingAccount != null)
                            {
                                System.Diagnostics.Trace.WriteLine(
                                    $"[KEYMANAGEMENT] Account {accountName}@{permission} already exists, skipping"
                                );
                                continue;
                            }

                            await _accountService.AddAccountAsync(
                                accountName,
                                permission,
                                chainInfo.ChainId, // Use actual chain ID from network
                                privateKey,
                                password,
                                WalletMode.Hot
                            );
                            addedCount++;
                            System.Diagnostics.Trace.WriteLine(
                                $"[KEYMANAGEMENT] Added {accountName}@{permission}"
                            );
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine(
                                $"[KEYMANAGEMENT] Failed to add {accountName}@{permission}: {ex.Message}"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[KEYMANAGEMENT] Failed to process {accountName}: {ex.Message}"
                    );
                }
            }

            await DisplayAlertAsync("Success", $"Added {addedCount} account(s) to wallet", "OK");

            // Reload keys
            await LoadKeysAsync();

            // Refresh wallet context so new accounts appear in selector
            await _walletContext.RefreshAsync();
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async Task<List<string>?> ShowAccountSelectionDialogAsync(
        List<string> accounts,
        string publicKey
    )
    {
        // Create a simple multi-select page
        var selectionPage = new ContentPage { Title = "Select Accounts" };

        var selectedAccounts = new List<string>();
        var checkboxes = new Dictionary<string, CheckBox>();

        var scrollView = new ScrollView { Padding = 20 };

        var layout = new VerticalStackLayout { Spacing = 10 };

        layout.Add(
            new Label
            {
                Text = $"Found {accounts.Count} account(s) using this key:",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 0, 0, 10),
            }
        );

        layout.Add(
            new Label
            {
                Text = publicKey,
                FontSize = 12,
                LineBreakMode = LineBreakMode.MiddleTruncation,
                Margin = new Thickness(0, 0, 0, 20),
            }
        );

        foreach (var account in accounts)
        {
            var frame = new Border { BackgroundColor = Colors.Gray, Padding = 10 };

            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                ],
                ColumnSpacing = 10,
            };

            var checkbox = new CheckBox { IsChecked = true };
            checkboxes[account] = checkbox;

            var label = new Label
            {
                Text = account,
                VerticalOptions = LayoutOptions.Center,
                FontSize = 14,
            };

            grid.Add(checkbox, 0, 0);
            grid.Add(label, 1, 0);

            frame.Content = grid;
            layout.Add(frame);
        }

        var buttonStack = new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 20, 0, 0),
            HorizontalOptions = LayoutOptions.Center,
        };

        var addButton = new Button
        {
            Text = "Add Selected",
            WidthRequest = 150,
            CornerRadius = 8,
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            WidthRequest = 150,
            CornerRadius = 8,
            BackgroundColor = Colors.Gray,
        };

        buttonStack.Add(addButton);
        buttonStack.Add(cancelButton);

        layout.Add(buttonStack);
        scrollView.Content = layout;
        selectionPage.Content = scrollView;

        var tcs = new TaskCompletionSource<List<string>?>();

        addButton.Clicked += (s, e) =>
        {
            var selected = checkboxes
                .Where(kvp => kvp.Value.IsChecked)
                .Select(kvp => kvp.Key)
                .ToList();
            tcs.SetResult(selected);
            Navigation.PopModalAsync();
        };

        cancelButton.Clicked += (s, e) =>
        {
            tcs.SetResult(null);
            Navigation.PopModalAsync();
        };

        await Navigation.PushModalAsync(selectionPage);

        return await tcs.Task;
    }

    private async Task ShowKeyDetailsAsync(KeyItemViewModel keyItem)
    {
        try
        {
            var details = $"Public Key (Legacy EOS):\n{keyItem.PublicKey}\n\n";

            // Try to get both formats using session password
            try
            {
                string? privateKey = null;
                if (_storageService.IsUnlocked)
                {
                    privateKey = _storageService.GetUnlockedPrivateKey(keyItem.PublicKey);
                }
                else
                {
                    privateKey = await _accountService.GetPrivateKeyAsync(
                        keyItem.Accounts.First().Data.Account,
                        keyItem.Accounts.First().Data.Authority,
                        keyItem.Accounts.First().Data.ChainId,
                        _sessionPassword!
                    );
                }

                if (!string.IsNullOrEmpty(privateKey))
                {
                    var keyObj = SUS.EOS.Sharp.Cryptography.EosioKey.FromPrivateKey(privateKey);
                    details =
                        $"Public Key (Legacy EOS):\n{keyObj.PublicKey}\n\n"
                        + $"Public Key (Modern PUB_K1):\n{keyObj.PublicKeyK1}\n\n";
                }
            }
            catch
            {
                // If we can't get private key, just show what we have
            }

            details += $"Used by {keyItem.AccountCount} account(s):\n";

            foreach (var account in keyItem.Accounts)
            {
                details += $"• {account.Data.Account}@{account.Data.Authority}\n";
            }

            await DisplayAlertAsync("Key Details", details, "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Error showing details: {ex.Message}"
            );
            await DisplayAlertAsync("Error", $"Failed to show details: {ex.Message}", "OK");
        }
    }

    private async Task ShowCopyKeyDialogAsync(KeyItemViewModel keyItem)
    {
        try
        {
            string? legacyFormat = null;
            string? modernFormat = null;

            // Use session password to derive both formats
            try
            {
                string? privateKey = null;
                if (_storageService.IsUnlocked)
                {
                    privateKey = _storageService.GetUnlockedPrivateKey(keyItem.PublicKey);
                }
                else
                {
                    privateKey = await _accountService.GetPrivateKeyAsync(
                        keyItem.Accounts.First().Data.Account,
                        keyItem.Accounts.First().Data.Authority,
                        keyItem.Accounts.First().Data.ChainId,
                        _sessionPassword!
                    );
                }

                if (!string.IsNullOrEmpty(privateKey))
                {
                    var keyObj = SUS.EOS.Sharp.Cryptography.EosioKey.FromPrivateKey(privateKey);
                    legacyFormat = keyObj.PublicKey;
                    modernFormat = keyObj.PublicKeyK1;
                }
                else
                {
                    // Fallback to stored public key
                    legacyFormat = keyItem.PublicKey;
                    modernFormat = "(Could not derive)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT] Error deriving key formats: {ex.Message}");
                // Fallback to stored public key
                legacyFormat = keyItem.PublicKey;
                modernFormat = "(Could not derive)";
            }

            var action = await DisplayActionSheetAsync(
                "Select Format to Copy",
                "Cancel",
                null,
                $"Legacy (EOS): {(legacyFormat?.Length > 40 ? legacyFormat[..40] + "..." : legacyFormat)}",
                $"Modern (PUB_K1): {(modernFormat?.Length > 40 ? modernFormat[..40] + "..." : modernFormat)}"
            );

            if (action != null && action != "Cancel")
            {
                string textToCopy;
                if (action.StartsWith("Legacy"))
                {
                    textToCopy = legacyFormat ?? keyItem.PublicKey;
                }
                else
                {
                    textToCopy = modernFormat ?? keyItem.PublicKey;
                }

                if (!textToCopy.Contains("Could not derive"))
                {
                    await Clipboard.SetTextAsync(textToCopy);
                    await DisplayAlertAsync("Copied", "Public key copied to clipboard", "OK");
                }
                else
                {
                    await DisplayAlertAsync(
                        "Cannot Copy",
                        "Could not derive this key format.",
                        "OK"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT] Error copying key: {ex.Message}");
            await DisplayAlertAsync("Error", $"Failed to copy key: {ex.Message}", "OK");
        }
    }

    private async Task ViewPrivateKeyAsync(KeyItemViewModel keyItem)
    {
        try
        {
            // Use session password (already validated on page entry)
            string? privateKey = null;
            if (_storageService.IsUnlocked)
            {
                privateKey = _storageService.GetUnlockedPrivateKey(keyItem.PublicKey);
            }
            else
            {
                privateKey = await _accountService.GetPrivateKeyAsync(
                    keyItem.Accounts.First().Data.Account,
                    keyItem.Accounts.First().Data.Authority,
                    keyItem.Accounts.First().Data.ChainId,
                    _sessionPassword!
                );
            }

            if (string.IsNullOrEmpty(privateKey))
            {
                await DisplayAlertAsync("Error", "Failed to retrieve private key", "OK");
                return;
            }

            // Show private key with copy option
            var result = await DisplayAlertAsync(
                "Private Key",
                $"Private Key:\n{privateKey}\n\n⚠️ Keep this secure! Never share your private key.",
                "Copy to Clipboard",
                "Close"
            );

            if (result)
            {
                await Clipboard.SetTextAsync(privateKey);
                await DisplayAlertAsync("Copied", "Private key copied to clipboard", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[KEYMANAGEMENT] Error viewing private key: {ex.Message}"
            );
            await DisplayAlertAsync("Error", $"Failed to view private key: {ex.Message}", "OK");
        }
    }

    private async Task RemoveKeyFromWalletAsync(KeyItemViewModel keyItem)
    {
        var confirm = await DisplayAlertAsync(
            "Confirm Removal",
            $"Are you sure you want to remove this key?\n\n"
                + $"This will remove {keyItem.AccountCount} account(s) from the wallet:\n"
                + string.Join(
                    "\n",
                    keyItem.Accounts.Select(a => $"• {a.Data.Account}@{a.Data.Authority}")
                )
                + $"\n\n⚠️ Make sure you have a backup of this key!",
            "Remove",
            "Cancel"
        );

        if (!confirm)
            return;

        try
        {
            // Remove all accounts using this key
            foreach (var account in keyItem.Accounts)
            {
                await _accountService.RemoveAccountAsync(
                    account.Data.Account,
                    account.Data.Authority,
                    account.Data.ChainId
                );
            }

            await DisplayAlertAsync(
                "Success",
                "Key and associated accounts removed from wallet",
                "OK"
            );
            await LoadKeysAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[KEYMANAGEMENT] Error removing key: {ex.Message}");
            await DisplayAlertAsync("Error", $"Failed to remove key: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Show a custom text input dialog with proper password masking support
    /// </summary>
    private async Task<string?> ShowTextInputDialogAsync(
        string title,
        string message,
        string accept,
        string cancel,
        bool isPassword = false
    )
    {
        var tcs = new TaskCompletionSource<string?>();

        var dialogPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"), // Semi-transparent overlay
        };

        // Handle hardware/software back button on the dialog
        dialogPage.Disappearing += (s, e) =>
        {
            // If the dialog is dismissed without button click, cancel the operation
            tcs.TrySetResult(null);
        };

        var entry = new Entry
        {
            Placeholder = message,
            IsPassword = isPassword,
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            FontSize = 16,
            Margin = new Thickness(0, 10),
        };

        var acceptButton = new Button
        {
            Text = accept,
            BackgroundColor = Color.FromArgb("#007AFF"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
        };

        var cancelButton = new Button
        {
            Text = cancel,
            BackgroundColor = Color.FromArgb("#8E8E93"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(20, 10),
        };

        acceptButton.Clicked += async (s, e) =>
        {
            await Navigation.PopModalAsync();
            tcs.TrySetResult(entry.Text);
        };

        cancelButton.Clicked += async (s, e) =>
        {
            await Navigation.PopModalAsync();
            tcs.TrySetResult(null);
        };

        entry.Completed += async (s, e) =>
        {
            await Navigation.PopModalAsync();
            tcs.TrySetResult(entry.Text);
        };

        var dialogFrame = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            Padding = 20,
            MaximumWidthRequest = 400,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new VerticalStackLayout
            {
                Spacing = 15,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.Black,
                        HorizontalOptions = LayoutOptions.Center,
                    },
                    new Label
                    {
                        Text = message,
                        FontSize = 14,
                        TextColor = Color.FromArgb("#666666"),
                        HorizontalOptions = LayoutOptions.Start,
                    },
                    entry,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { cancelButton, acceptButton },
                    },
                },
            },
        };

        dialogPage.Content = dialogFrame;

        await Navigation.PushModalAsync(dialogPage, true);
        entry.Focus();

        return await tcs.Task;
    }

    /// <summary>
    /// Compares two EOSIO public keys ignoring checksum differences.
    /// This handles cases where the same key bytes have different checksums due to encoding variations.
    /// </summary>
    private static bool KeysMatchIgnoringChecksum(string key1, string key2)
    {
        if (key1 == key2)
            return true;
        if (string.IsNullOrEmpty(key1) || string.IsNullOrEmpty(key2))
            return false;

        // Strip prefixes
        var k1 = key1.Replace("EOS", "").Replace("PUB_K1_", "").Replace("PUB_R1_", "");
        var k2 = key2.Replace("EOS", "").Replace("PUB_K1_", "").Replace("PUB_R1_", "");

        // If lengths differ significantly, not a match
        if (Math.Abs(k1.Length - k2.Length) > 5)
            return false;

        // Compare first N-5 characters (ignoring last 5 which contain checksum)
        var compareLength = Math.Min(k1.Length, k2.Length) - 5;
        if (compareLength <= 0)
            return false;

        return k1[..compareLength] == k2[..compareLength];
    }
}

public class KeyItemViewModel
{
    public string PublicKey { get; set; } = string.Empty;
    public int AccountCount { get; set; }
    public List<WalletAccount> Accounts { get; set; } = [];

    public string AccountCountText => AccountCount == 1 ? "1 account" : $"{AccountCount} accounts";
}
