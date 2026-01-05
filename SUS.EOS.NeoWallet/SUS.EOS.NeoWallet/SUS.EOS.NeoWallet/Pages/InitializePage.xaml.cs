using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Pages;

public partial class InitializePage : ContentPage
{
    private readonly IWalletStorageService _storageService;
    private readonly INetworkService _networkService;
    private readonly IWalletAccountService _accountService;
    private readonly ICryptographyService _cryptographyService;
    private readonly IServiceProvider _serviceProvider;

    public InitializePage(
        IWalletStorageService storageService,
        INetworkService networkService,
        IWalletAccountService accountService,
        ICryptographyService cryptographyService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _storageService = storageService;
        _networkService = networkService;
        _accountService = accountService;
        _cryptographyService = cryptographyService;
        _serviceProvider = serviceProvider;
    }

    private async void OnCreateWalletClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[INITIALIZE] Create Wallet button clicked");
        try
        {
            System.Diagnostics.Trace.WriteLine("[INITIALIZE] Pushing CreateAccountPage");
            var createAccountPage = _serviceProvider.GetRequiredService<CreateAccountPage>();
            await Navigation.PushAsync(createAccountPage);
            System.Diagnostics.Trace.WriteLine("[INITIALIZE] Navigation to CreateAccountPage completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[INITIALIZE] ERROR navigating: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[INITIALIZE] StackTrace: {ex.StackTrace}");
        }
    }

    private async void OnImportWalletClicked(object sender, EventArgs e)
    {
        try
        {
            // Allow user to import wallet.json file
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select wallet.json file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.json" } }
                })
            });

            if (result == null)
                return;

            // Read and validate the file
            using var stream = await result.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var jsonContent = await reader.ReadToEndAsync();

            // Prompt for password to unlock the imported wallet
            var password = await DisplayPromptAsync(
                "Wallet Password",
                "Enter the password for this wallet:",
                placeholder: "Password"
            );

            if (string.IsNullOrWhiteSpace(password))
                return;

            // Import the wallet file
            var importedWallet = await _storageService.ImportWalletAsync(jsonContent, password);
            
            if (importedWallet != null)
            {
                // Initialize networks if needed
                await _networkService.InitializePredefinedNetworksAsync();
                
                await this.DisplayAlertAsync("Success", "Wallet imported successfully!", "OK");
                await Shell.Current.GoToAsync("DashboardPage");
            }
            else
            {
                await this.DisplayAlertAsync("Error", "Failed to import wallet. Please check the password.", "OK");
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Error", $"Failed to import wallet: {ex.Message}", "OK");
        }
    }

    private async void OnRecoverWalletClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[INITIALIZE] Import Wallet button clicked");
        try
        {
            System.Diagnostics.Trace.WriteLine("[INITIALIZE] Pushing ImportWalletPage");
            var importWalletPage = _serviceProvider.GetRequiredService<ImportWalletPage>();
            await Navigation.PushAsync(importWalletPage);
            System.Diagnostics.Trace.WriteLine("[INITIALIZE] Navigation to ImportWalletPage completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[INITIALIZE] ERROR navigating: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[INITIALIZE] StackTrace: {ex.StackTrace}");
        }
    }
}
