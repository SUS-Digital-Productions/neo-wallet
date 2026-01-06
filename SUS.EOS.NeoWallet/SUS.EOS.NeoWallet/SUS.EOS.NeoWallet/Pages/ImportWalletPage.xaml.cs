using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;

namespace SUS.EOS.NeoWallet.Pages;

public partial class ImportWalletPage : ContentPage
{
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly INetworkService _networkService;
    private readonly ICryptographyService _cryptographyService;
    
    // State
    private string? _selectedFilePath;
    private KeyFormat _detectedKeyFormat = KeyFormat.Unknown;
    private EosioKey? _importedKey;
    private string? _walletPassword;
    private NetworkConfig? _selectedNetwork;
    private readonly ObservableCollection<AccountPermission> _foundAccounts = new();
    private readonly List<string> _manualAccounts = new();
    private int _currentStep = 1;

    public ImportWalletPage(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        INetworkService networkService,
        ICryptographyService cryptographyService)
    {
        InitializeComponent();
        _accountService = accountService;
        _storageService = storageService;
        _networkService = networkService;
        _cryptographyService = cryptographyService;
        
        FoundAccountsCollection.ItemsSource = _foundAccounts;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Load default network
        _selectedNetwork = await _networkService.GetDefaultNetworkAsync();
        if (_selectedNetwork != null)
        {
            SelectedNetworkLabel.Text = _selectedNetwork.Name;
        }
    }

    #region Method Selection

    private void OnPrivateKeyMethodTapped(object? sender, TappedEventArgs e)
    {
        SelectMethod("privatekey");
    }

    private void OnRecoveryMethodTapped(object? sender, TappedEventArgs e)
    {
        SelectMethod("recovery");
    }

    private void OnFileMethodTapped(object? sender, TappedEventArgs e)
    {
        SelectMethod("file");
    }

    private void SelectMethod(string method)
    {
        // Reset method button styles
        PrivateKeyMethodBorder.BackgroundColor = (Color)Application.Current!.Resources["Card"];
        PrivateKeyMethodBorder.Stroke = (Color)Application.Current!.Resources["Border"];
        PrivateKeyMethodLabel.TextColor = (Color)Application.Current!.Resources["TextPrimary"];
        
        RecoveryMethodBorder.BackgroundColor = (Color)Application.Current!.Resources["Card"];
        RecoveryMethodBorder.Stroke = (Color)Application.Current!.Resources["Border"];
        RecoveryMethodLabel.TextColor = (Color)Application.Current!.Resources["TextPrimary"];
        
        FileMethodBorder.BackgroundColor = (Color)Application.Current!.Resources["Card"];
        FileMethodBorder.Stroke = (Color)Application.Current!.Resources["Border"];
        FileMethodLabel.TextColor = (Color)Application.Current!.Resources["TextPrimary"];

        // Hide all sections
        PrivateKeyWizard.IsVisible = false;
        RecoveryPhraseSection.IsVisible = false;
        FileImportSection.IsVisible = false;

        // Show selected section and highlight button
        switch (method)
        {
            case "privatekey":
                PrivateKeyMethodBorder.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
                PrivateKeyMethodBorder.Stroke = (Color)Application.Current!.Resources["Primary"];
                PrivateKeyMethodLabel.TextColor = Colors.White;
                PrivateKeyWizard.IsVisible = true;
                break;
            case "recovery":
                RecoveryMethodBorder.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
                RecoveryMethodBorder.Stroke = (Color)Application.Current!.Resources["Primary"];
                RecoveryMethodLabel.TextColor = Colors.White;
                RecoveryPhraseSection.IsVisible = true;
                break;
            case "file":
                FileMethodBorder.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
                FileMethodBorder.Stroke = (Color)Application.Current!.Resources["Primary"];
                FileMethodLabel.TextColor = Colors.White;
                FileImportSection.IsVisible = true;
                break;
        }
    }

    #endregion

    #region Step 1: Private Key Entry

    private void OnPrivateKeyChanged(object sender, TextChangedEventArgs e)
    {
        var privateKey = e.NewTextValue?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            ContinueToStep2Button.IsEnabled = false;
            FormatDetectionBorder.IsVisible = false;
            PublicKeyPreviewBorder.IsVisible = false;
            PrivateKeyError.IsVisible = false;
            _detectedKeyFormat = KeyFormat.Unknown;
            _importedKey = null;
            return;
        }

        try
        {
            _detectedKeyFormat = EosioKey.DetectFormat(privateKey);
            
            if (_detectedKeyFormat != KeyFormat.Unknown)
            {
                // Try to create the key to get public key
                _importedKey = EosioKey.FromPrivateKey(privateKey);
                
                // Show format detection
                FormatDetectionBorder.IsVisible = true;
                FormatIconLabel.Text = "✓";
                FormatIconLabel.TextColor = (Color)Application.Current!.Resources["Success"];
                
                switch (_detectedKeyFormat)
                {
                    case KeyFormat.LegacyWif:
                        FormatLabel.Text = "WIF format detected (5...)";
                        break;
                    case KeyFormat.ModernK1:
                        FormatLabel.Text = "PVT_K1_ format detected";
                        break;
                    case KeyFormat.ModernR1:
                        FormatLabel.Text = "PVT_R1_ format detected";
                        break;
                    case KeyFormat.RawHex:
                        FormatLabel.Text = "Hexadecimal format detected (64 chars)";
                        break;
                    default:
                        FormatLabel.Text = $"{_detectedKeyFormat} format detected";
                        break;
                }
                
                // Show public key preview
                PublicKeyPreviewBorder.IsVisible = true;
                PublicKeyPreviewLabel.Text = _importedKey.PublicKey;
                
                ContinueToStep2Button.IsEnabled = true;
                PrivateKeyError.IsVisible = false;
            }
            else
            {
                ShowKeyValidationError();
            }
        }
        catch
        {
            ShowKeyValidationError();
        }
    }

    private void ShowKeyValidationError()
    {
        FormatDetectionBorder.IsVisible = true;
        FormatIconLabel.Text = "✗";
        FormatIconLabel.TextColor = (Color)Application.Current!.Resources["Error"];
        FormatLabel.Text = "Invalid private key format";
        PublicKeyPreviewBorder.IsVisible = false;
        ContinueToStep2Button.IsEnabled = false;
        PrivateKeyError.Text = "Please enter a valid WIF, PVT_K1_, PVT_R1_, or hex private key";
        PrivateKeyError.IsVisible = true;
        _importedKey = null;
    }

    private void OnTogglePrivateKeyClicked(object sender, EventArgs e)
    {
        PrivateKeyEntry.IsPassword = !PrivateKeyEntry.IsPassword;
        TogglePrivateKeyButton.Text = PrivateKeyEntry.IsPassword ? "👁" : "🔒";
    }

    private void OnContinueToStep2Clicked(object sender, EventArgs e)
    {
        GoToStep(2);
    }

    #endregion

    #region Step 2: Password

    private void OnPasswordChanged(object sender, TextChangedEventArgs e)
    {
        var password = PasswordEntry.Text ?? string.Empty;
        var confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;
        
        // Calculate password strength
        int strength = CalculatePasswordStrength(password);
        UpdatePasswordStrengthUI(strength);
        
        // Check if passwords match
        if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(confirmPassword))
        {
            if (password != confirmPassword)
            {
                PasswordError.Text = "Passwords do not match";
                PasswordError.IsVisible = true;
                ContinueToStep3Button.IsEnabled = false;
            }
            else if (password.Length < 8)
            {
                PasswordError.Text = "Password must be at least 8 characters";
                PasswordError.IsVisible = true;
                ContinueToStep3Button.IsEnabled = false;
            }
            else
            {
                PasswordError.IsVisible = false;
                ContinueToStep3Button.IsEnabled = true;
                _walletPassword = password;
            }
        }
        else
        {
            PasswordError.IsVisible = false;
            ContinueToStep3Button.IsEnabled = false;
        }
    }

    private int CalculatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;
        
        int strength = 0;
        if (password.Length >= 8) strength++;
        if (password.Length >= 12) strength++;
        if (password.Any(char.IsDigit)) strength++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) strength++;
        
        return Math.Min(strength, 4);
    }

    private void UpdatePasswordStrengthUI(int strength)
    {
        var weakColor = (Color)Application.Current!.Resources["Error"];
        var mediumColor = (Color)Application.Current!.Resources["Warning"];
        var strongColor = (Color)Application.Current!.Resources["Success"];
        var emptyColor = (Color)Application.Current!.Resources["Gray300"];
        
        Strength1.BackgroundColor = strength >= 1 ? (strength == 1 ? weakColor : (strength == 2 ? mediumColor : strongColor)) : emptyColor;
        Strength2.BackgroundColor = strength >= 2 ? (strength == 2 ? mediumColor : strongColor) : emptyColor;
        Strength3.BackgroundColor = strength >= 3 ? strongColor : emptyColor;
        Strength4.BackgroundColor = strength >= 4 ? strongColor : emptyColor;
        
        PasswordStrengthLabel.Text = strength switch
        {
            0 => "",
            1 => "Weak password",
            2 => "Fair password",
            3 => "Good password",
            4 => "Strong password",
            _ => ""
        };
        
        PasswordStrengthLabel.TextColor = strength switch
        {
            1 => weakColor,
            2 => mediumColor,
            3 or 4 => strongColor,
            _ => (Color)Application.Current!.Resources["TextSecondary"]
        };
    }

    private void OnBackToStep1Clicked(object sender, EventArgs e)
    {
        GoToStep(1);
    }

    private async void OnContinueToStep3Clicked(object sender, EventArgs e)
    {
        GoToStep(3);
        await SearchForAccountsAsync();
    }

    #endregion

    #region Step 3: Account Selection

    private async void OnNetworkSelectorTapped(object? sender, TappedEventArgs e)
    {
        var networks = await _networkService.GetNetworksAsync();
        var networkList = networks.Values.Where(n => n.Enabled).ToList();
        var networkNames = networkList.Select(n => n.Name).ToArray();
        
        var result = await DisplayActionSheetAsync("Select Network", "Cancel", null, networkNames);
        
        if (!string.IsNullOrEmpty(result) && result != "Cancel")
        {
            _selectedNetwork = networkList.FirstOrDefault(n => n.Name == result);
            if (_selectedNetwork != null)
            {
                SelectedNetworkLabel.Text = _selectedNetwork.Name;
                await SearchForAccountsAsync();
            }
        }
    }

    private async Task SearchForAccountsAsync()
    {
        if (_importedKey == null || _selectedNetwork == null) return;
        
        LoadingAccountsIndicator.IsRunning = true;
        LoadingAccountsIndicator.IsVisible = true;
        LoadingAccountsLabel.Text = "Searching for accounts on Light API...";
        LoadingAccountsLabel.IsVisible = true;
        FoundAccountsContainer.IsVisible = false;
        
        try
        {
            // Try Light API first (faster and works across chains)
            var accounts = await GetAccountsByPublicKeyFromLightApiAsync(_importedKey.PublicKey, _selectedNetwork.ChainId);
            
            // Fallback to chain API if Light API doesn't have results
            if (accounts.Count == 0)
            {
                LoadingAccountsLabel.Text = "Trying chain API...";
                accounts = await GetAccountsByPublicKeyFromChainAsync(_selectedNetwork.HttpEndpoint, _importedKey.PublicKey);
            }
            
            _foundAccounts.Clear();
            foreach (var account in accounts)
            {
                _foundAccounts.Add(account);
            }
            
            if (_foundAccounts.Count > 0)
            {
                FoundAccountsContainer.IsVisible = true;
                // Select all by default
                FoundAccountsCollection.SelectedItems = _foundAccounts.Cast<object>().ToList();
            }
            
            UpdateImportButtonState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IMPORT] Error searching accounts: {ex.Message}");
            // Don't show error - user can still add accounts manually
        }
        finally
        {
            LoadingAccountsIndicator.IsRunning = false;
            LoadingAccountsIndicator.IsVisible = false;
            LoadingAccountsLabel.IsVisible = false;
        }
    }

    private async Task<List<AccountPermission>> GetAccountsByPublicKeyFromLightApiAsync(string publicKey, string chainId)
    {
        var result = new List<AccountPermission>();
        
        try
        {
            // Use Light API - it returns accounts across all supported chains
            using var lightClient = LightApiClient.ForChain(chainId);
            if (lightClient == null)
            {
                // Chain not supported by Light API, return empty
                return result;
            }

            var response = await lightClient.GetAccountsByKeyAsync(publicKey);
            
            // Find the chain data matching our chain ID
            foreach (var chainData in response.Chains.Values)
            {
                if (chainData.ChainId == chainId)
                {
                    foreach (var account in chainData.Accounts)
                    {
                        // Only include permissions that are key-controlled (not account-controlled)
                        if (account.IsKeyControlled)
                        {
                            result.Add(new AccountPermission
                            {
                                AccountName = account.AccountName,
                                Permission = account.Permission
                            });
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IMPORT] Light API error: {ex.Message}");
        }
        
        return result;
    }

    private async Task<List<AccountPermission>> GetAccountsByPublicKeyFromChainAsync(string endpoint, string publicKey)
    {
        var result = new List<AccountPermission>();
        
        try
        {
            using var client = new AntelopeHttpClient(endpoint);
            
            // Try the get_accounts_by_authorizers endpoint (newer API)
            var response = await client.PostJsonAsync<AccountsByAuthorizersResponse>(
                "/v1/chain/get_accounts_by_authorizers",
                new { keys = new[] { publicKey } }
            );
            
            if (response?.Accounts != null)
            {
                foreach (var account in response.Accounts)
                {
                    result.Add(new AccountPermission
                    {
                        AccountName = account.AccountName,
                        Permission = account.PermissionName
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IMPORT] Chain API error: {ex.Message}");
            // Fallback: Some chains don't support get_accounts_by_authorizers
        }
        
        return result;
    }

    private void OnAccountSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateImportButtonState();
    }

    private void OnAddManualAccountClicked(object sender, EventArgs e)
    {
        var accountName = ManualAccountEntry.Text?.Trim().ToLower() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }
        
        // Validate account name (basic EOSIO rules)
        if (accountName.Length > 12 || !accountName.All(c => char.IsLetterOrDigit(c) || c == '.'))
        {
            Step3Error.Text = "Invalid account name. Must be 1-12 characters (a-z, 1-5, .)";
            Step3Error.IsVisible = true;
            return;
        }
        
        if (_manualAccounts.Contains(accountName))
        {
            Step3Error.Text = "Account already added";
            Step3Error.IsVisible = true;
            return;
        }
        
        _manualAccounts.Add(accountName);
        Step3Error.IsVisible = false;
        ManualAccountEntry.Text = string.Empty;
        
        // Update UI
        UpdateManualAccountsUI();
        UpdateImportButtonState();
    }

    private void UpdateManualAccountsUI()
    {
        ManualAccountsList.Children.Clear();
        
        foreach (var account in _manualAccounts)
        {
            var border = new Border
            {
                BackgroundColor = (Color)Application.Current!.Resources["Surface"],
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Stroke = (Color)Application.Current!.Resources["Border"],
                StrokeThickness = 1,
                Padding = new Thickness(12, 8)
            };
            
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            
            grid.Add(new Label
            {
                Text = account,
                FontSize = 14,
                TextColor = (Color)Application.Current!.Resources["TextPrimary"],
                VerticalOptions = LayoutOptions.Center
            }, 0);
            
            var removeButton = new Button
            {
                Text = "✕",
                BackgroundColor = Colors.Transparent,
                TextColor = (Color)Application.Current!.Resources["Error"],
                FontSize = 14,
                WidthRequest = 32,
                HeightRequest = 32,
                Padding = 0
            };
            removeButton.Clicked += (s, e) => RemoveManualAccount(account);
            grid.Add(removeButton, 1);
            
            border.Content = grid;
            ManualAccountsList.Children.Add(border);
        }
        
        ManualAccountsContainer.IsVisible = _manualAccounts.Count > 0;
    }

    private void RemoveManualAccount(string account)
    {
        _manualAccounts.Remove(account);
        UpdateManualAccountsUI();
        UpdateImportButtonState();
    }

    private void UpdateImportButtonState()
    {
        var hasSelectedAccounts = FoundAccountsCollection.SelectedItems?.Count > 0;
        var hasManualAccounts = _manualAccounts.Count > 0;
        ImportButton.IsEnabled = hasSelectedAccounts || hasManualAccounts;
    }

    private void OnBackToStep2Clicked(object sender, EventArgs e)
    {
        GoToStep(2);
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        if (_importedKey == null || _walletPassword == null || _selectedNetwork == null)
        {
            await DisplayAlertAsync("Error", "Missing required information", "OK");
            return;
        }
        
        try
        {
            ImportButton.IsEnabled = false;
            
            // Create wallet if it doesn't exist
            if (!await _storageService.WalletExistsAsync())
            {
                await _storageService.CreateWalletAsync(_walletPassword);
            }
            
            // Unlock wallet
            await _storageService.UnlockWalletAsync(_walletPassword);
            
            // Collect accounts to import
            var accountsToImport = new List<(string accountName, string permission)>();
            
            // Add selected found accounts
            if (FoundAccountsCollection.SelectedItems != null)
            {
                foreach (var item in FoundAccountsCollection.SelectedItems.Cast<AccountPermission>())
                {
                    accountsToImport.Add((item.AccountName, item.Permission));
                }
            }
            
            // Add manual accounts (default permission: active)
            foreach (var account in _manualAccounts)
            {
                if (!accountsToImport.Any(a => a.accountName == account))
                {
                    accountsToImport.Add((account, "active"));
                }
            }
            
            // Import each account
            int imported = 0;
            foreach (var (accountName, permission) in accountsToImport)
            {
                try
                {
                    await _accountService.ImportAccountAsync(
                        accountName,                    // account
                        permission,                     // authority (e.g., "active")
                        _selectedNetwork.ChainId,       // chainId
                        _importedKey.PrivateKeyWif,     // privateKey
                        _walletPassword                 // password
                    );
                    imported++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IMPORT] Failed to import {accountName}: {ex.Message}");
                }
            }
            
            if (imported > 0)
            {
                await DisplayAlertAsync("Success", 
                    $"Successfully imported {imported} account(s)!\n\nPublic Key:\n{_importedKey.PublicKey}", 
                    "OK");
                
                // Navigate to main page
                var serviceProvider = Application.Current!.Handler.MauiContext!.Services;
                var mainPage = serviceProvider.GetRequiredService<MainPage>();
                await Navigation.PushAsync(mainPage);
            }
            else
            {
                Step3Error.Text = "Failed to import accounts. Please try again.";
                Step3Error.IsVisible = true;
                ImportButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            Step3Error.Text = $"Import failed: {ex.Message}";
            Step3Error.IsVisible = true;
            ImportButton.IsEnabled = true;
        }
    }

    #endregion

    #region Step Navigation

    private void GoToStep(int step)
    {
        _currentStep = step;
        
        // Update step indicators
        UpdateStepIndicator(Step1Indicator, step >= 1, step == 1);
        UpdateStepIndicator(Step2Indicator, step >= 2, step == 2, Step2Label);
        UpdateStepIndicator(Step3Indicator, step >= 3, step == 3, Step3Label);
        
        // Show/hide content
        Step1Content.IsVisible = step == 1;
        Step2Content.IsVisible = step == 2;
        Step3Content.IsVisible = step == 3;
    }

    private void UpdateStepIndicator(Border indicator, bool completed, bool current, Label? label = null)
    {
        if (current)
        {
            indicator.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
            indicator.Stroke = (Color)Application.Current!.Resources["Primary"];
            indicator.StrokeThickness = 0;
            if (label != null) label.TextColor = Colors.White;
        }
        else if (completed)
        {
            indicator.BackgroundColor = (Color)Application.Current!.Resources["Success"];
            indicator.Stroke = (Color)Application.Current!.Resources["Success"];
            indicator.StrokeThickness = 0;
            if (label != null) label.TextColor = Colors.White;
        }
        else
        {
            indicator.BackgroundColor = (Color)Application.Current!.Resources["Surface"];
            indicator.Stroke = (Color)Application.Current!.Resources["Border"];
            indicator.StrokeThickness = 1;
            if (label != null) label.TextColor = (Color)Application.Current!.Resources["TextSecondary"];
        }
    }

    #endregion

    #region Recovery Phrase

    private void OnRecoveryPhraseChanged(object sender, TextChangedEventArgs e)
    {
        var phrase = e.NewTextValue?.Trim() ?? string.Empty;
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        WordCountLabel.Text = $"{words.Length}/12 words";

        if (words.Length == 12)
        {
            ImportFromPhraseButton.IsEnabled = true;
            RecoveryPhraseError.IsVisible = false;
            WordCountLabel.TextColor = (Color)Application.Current!.Resources["Success"];
        }
        else
        {
            ImportFromPhraseButton.IsEnabled = false;
            WordCountLabel.TextColor = (Color)Application.Current!.Resources["TextSecondary"];
            if (!string.IsNullOrWhiteSpace(phrase) && words.Length > 0)
            {
                if (words.Length < 12)
                {
                    RecoveryPhraseError.Text = $"Need {12 - words.Length} more words";
                }
                else
                {
                    RecoveryPhraseError.Text = $"Too many words ({words.Length}), need exactly 12";
                }
                RecoveryPhraseError.IsVisible = true;
            }
            else
            {
                RecoveryPhraseError.IsVisible = false;
            }
        }
    }

    private async void OnImportFromPhraseClicked(object sender, EventArgs e)
    {
        // TODO: Implement recovery phrase import
        // This would involve:
        // 1. Validating BIP39 phrase
        // 2. Deriving private key from phrase
        // 3. Then following similar flow as private key import
        await DisplayAlertAsync("Coming Soon", "Recovery phrase import will be available in a future update.", "OK");
    }

    #endregion

    #region File Import

    private async void OnSelectFileClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.macOS, new[] { "json" } }
                }),
                PickerTitle = "Select wallet backup file"
            });

            if (result != null)
            {
                _selectedFilePath = result.FullPath;
                SelectedFileName.Text = $"📄 {result.FileName}";
                SelectedFileName.IsVisible = true;
                FilePasswordSection.IsVisible = true;
                ImportFromFileButton.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to select file: {ex.Message}", "OK");
        }
    }

    private async void OnImportFromFileClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FilePasswordEntry.Text))
        {
            await DisplayAlertAsync("Error", "Please enter the file password", "OK");
            return;
        }

        // TODO: Implement encrypted wallet file import
        await DisplayAlertAsync("Coming Soon", "File import will be available in a future update.", "OK");
    }

    #endregion

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

// Helper classes
public class AccountPermission
{
    public string AccountName { get; set; } = string.Empty;
    public string Permission { get; set; } = "active";
}

public class AccountsByAuthorizersResponse
{
    public List<AccountAuthInfo>? Accounts { get; set; }
}

public class AccountAuthInfo
{
    public string AccountName { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
}
