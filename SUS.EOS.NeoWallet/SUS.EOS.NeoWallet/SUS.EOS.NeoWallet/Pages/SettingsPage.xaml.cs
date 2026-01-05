using SUS.EOS.NeoWallet.Services;
using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Pages;

/// <summary>
/// Settings page with network, theme, and security options
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly ThemeService _themeService;
    private readonly INetworkService _networkService;
    private readonly IWalletStorageService _storageService;

    public SettingsPage(
        ThemeService themeService,
        INetworkService networkService,
        IWalletStorageService storageService)
    {
        InitializeComponent();
        _themeService = themeService;
        _networkService = networkService;
        _storageService = storageService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateThemeUI();
        await LoadNetworkInfo();
    }

    private async Task LoadNetworkInfo()
    {
        try
        {
            var defaultNetwork = await _networkService.GetDefaultNetworkAsync();
            if (defaultNetwork != null)
            {
                DefaultNetworkLabel.Text = defaultNetwork.Name;
                NetworkIconLabel.Text = defaultNetwork.Symbol[..1].ToUpper();
            }
        }
        catch
        {
            DefaultNetworkLabel.Text = "Not configured";
        }
    }

    private async void OnChangeNetworkClicked(object sender, EventArgs e)
    {
        try
        {
            var networks = await _networkService.GetNetworksAsync();
            var networkList = networks.Values.Where(n => n.Enabled).ToList();
            
            if (!networkList.Any())
            {
                await DisplayAlertAsync("No Networks", "No networks are configured.", "OK");
                return;
            }

            var networkNames = networkList.Select(n => n.Name).ToArray();
            var result = await DisplayActionSheet("Select Default Network", "Cancel", null, networkNames);
            
            if (result != null && result != "Cancel")
            {
                var selectedNetwork = networkList.FirstOrDefault(n => n.Name == result);
                if (selectedNetwork != null)
                {
                    var networkId = networks.FirstOrDefault(x => x.Value.ChainId == selectedNetwork.ChainId).Key;
                    if (!string.IsNullOrEmpty(networkId))
                    {
                        await _networkService.SetDefaultNetworkAsync(networkId);
                        DefaultNetworkLabel.Text = selectedNetwork.Name;
                        NetworkIconLabel.Text = selectedNetwork.Symbol[..1].ToUpper();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to change network: {ex.Message}", "OK");
        }
    }

    private async void OnManageNetworksClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Networks", "Network management coming soon", "OK");
    }

    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        var theme = e.Value ? AppTheme.Dark : AppTheme.Light;
        _themeService.SetTheme(theme);
        UpdateThemeUI();
    }

    private void UpdateThemeUI()
    {
        ThemeSwitch.IsToggled = _themeService.IsDarkMode;
        ThemeLabel.Text = _themeService.IsDarkMode ? "Dark theme" : "Light theme";
    }

    private async void OnChangePasswordClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Change Password", "Password change coming soon", "OK");
    }

    private async void OnBackupWalletClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Backup", "Wallet backup coming soon", "OK");
    }

    private async void OnExportKeysClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Warning",
            "Exporting private keys is dangerous. Anyone with your keys can steal your funds. Only proceed if you understand the risks.",
            "I Understand",
            "Cancel");
        
        if (!confirm) return;
        
        await DisplayAlertAsync("Export Keys", "Key export coming soon", "OK");
    }

    private async void OnResetWalletClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "⚠️ Reset Wallet",
            "This will DELETE ALL your wallet data including private keys. This action CANNOT be undone.\n\nMake sure you have backed up your keys!",
            "Delete Everything",
            "Cancel");
        
        if (!confirm) return;
        
        var doubleConfirm = await DisplayAlertAsync(
            "Are you absolutely sure?",
            "Type 'DELETE' to confirm (just tap OK for now)",
            "DELETE",
            "Cancel");
        
        if (doubleConfirm)
        {
            try
            {
                // Delete wallet file
                await _storageService.DeleteWalletAsync();
                
                await DisplayAlertAsync("Wallet Reset", "All data has been deleted.", "OK");
                
                // Restart app to initial state
                var initPage = Application.Current!.Handler.MauiContext!.Services.GetRequiredService<InitializePage>();
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = new NavigationPage(initPage);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"Failed to reset: {ex.Message}", "OK");
            }
        }
    }

    private async void OnTestEsrClicked(object sender, EventArgs e)
    {
        // Test ESR signing request popup
        var esrUri = await DisplayPromptAsync(
            "Test ESR Request",
            "Enter ESR URI (esr://...) or paste ESR payload:",
            "Test",
            "Cancel",
            keyboard: Keyboard.Url);
        
        if (string.IsNullOrEmpty(esrUri))
            return;
        
        try
        {
            var serviceProvider = Application.Current!.Handler.MauiContext!.Services;
            var esrService = serviceProvider.GetRequiredService<SUS.EOS.Sharp.ESR.IEsrService>();
            var popupPage = serviceProvider.GetRequiredService<EsrSigningPopupPage>();
            
            // Parse ESR
            var request = await esrService.ParseRequestAsync(esrUri);
            
            // Show popup
            await Navigation.PushModalAsync(popupPage);
            var result = await popupPage.ShowSigningRequestAsync(
                request,
                rawPayload: esrUri,
                dAppName: "Test ESR");
            
            if (result.Success)
            {
                await DisplayAlertAsync("Success", $"Transaction signed!\nSignatures: {result.Signatures?.Count ?? 0}", "OK");
            }
            else if (result.Cancelled)
            {
                await DisplayAlertAsync("Cancelled", "Signing was cancelled", "OK");
            }
            else
            {
                await DisplayAlertAsync("Failed", $"Error: {result.Error}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to process ESR: {ex.Message}", "OK");
        }
    }

    private async void OnShowLinkIdClicked(object sender, EventArgs e)
    {
        // Show the current Anchor Link ID for debugging
        var serviceProvider = Application.Current!.Handler.MauiContext!.Services;
        var esrManager = serviceProvider.GetRequiredService<IEsrSessionManager>();
        
        var linkId = esrManager.LinkId ?? "Not initialized";
        var status = esrManager.Status.ToString();
        var publicKey = esrManager.RequestPublicKey ?? "Not available";
        
        await DisplayAlertAsync(
            "ESR Session Info",
            $"Status: {status}\n\nLink ID: {linkId}\n\nPublic Key:\n{publicKey}",
            "OK");
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
