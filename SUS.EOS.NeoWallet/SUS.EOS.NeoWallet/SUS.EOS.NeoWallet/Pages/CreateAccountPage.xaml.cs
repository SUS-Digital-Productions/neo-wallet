using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Pages;

public partial class CreateAccountPage : ContentPage
{
    private readonly IWalletAccountService _accountService;
    private readonly IWalletStorageService _storageService;
    private readonly INetworkService _networkService;
    private readonly ICryptographyService _cryptographyService;
    private readonly IServiceProvider _serviceProvider;
    
    private bool passwordsMatch = false;
    private bool agreementAccepted = false;
    private int passwordStrength = 0;

    public CreateAccountPage(
        IWalletAccountService accountService,
        IWalletStorageService storageService,
        INetworkService networkService,
        ICryptographyService cryptographyService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _accountService = accountService;
        _storageService = storageService;
        _networkService = networkService;
        _cryptographyService = cryptographyService;
        _serviceProvider = serviceProvider;
    }

    private void OnPasswordChanged(object sender, TextChangedEventArgs e)
    {
        var password = e.NewTextValue ?? string.Empty;
        passwordStrength = CalculatePasswordStrength(password);

        UpdatePasswordStrengthDisplay();
        CheckPasswordMatch();
        ValidatePasswordRequirements(password);
        UpdateCreateButtonState();
    }

    private void OnConfirmPasswordChanged(object sender, TextChangedEventArgs e)
    {
        CheckPasswordMatch();
        UpdateCreateButtonState();
    }

    private void OnAgreementChanged(object sender, CheckedChangedEventArgs e)
    {
        agreementAccepted = e.Value;
        UpdateCreateButtonState();
    }

    private void CheckPasswordMatch()
    {
        var password = PasswordEntry.Text ?? string.Empty;
        var confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrEmpty(confirmPassword))
        {
            PasswordMatchLabel.IsVisible = false;
            passwordsMatch = false;
            return;
        }

        passwordsMatch = password == confirmPassword;
        PasswordMatchLabel.IsVisible = true;

        if (passwordsMatch)
        {
            PasswordMatchLabel.Text = "✓ Passwords match";
            PasswordMatchLabel.TextColor = Colors.Green;
        }
        else
        {
            PasswordMatchLabel.Text = "✗ Passwords do not match";
            PasswordMatchLabel.TextColor = Colors.Red;
        }
    }

    private int CalculatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        int strength = 0;
        if (password.Length >= 8) strength++;
        if (password.Any(char.IsUpper) && password.Any(char.IsLower)) strength++;
        if (password.Any(char.IsDigit)) strength++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) strength++;

        return strength;
    }

    private void UpdatePasswordStrengthDisplay()
    {
        var colors = new[] { Colors.LightGray, Colors.LightGray, Colors.LightGray, Colors.LightGray };
        var strengthColors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green };
        var strengthLabels = new[] { "Weak", "Fair", "Good", "Strong" };

        for (int i = 0; i < passwordStrength; i++)
        {
            colors[i] = strengthColors[Math.Min(passwordStrength - 1, 3)];
        }

        Strength1.BackgroundColor = colors[0];
        Strength2.BackgroundColor = colors[1];
        Strength3.BackgroundColor = colors[2];
        Strength4.BackgroundColor = colors[3];

        PasswordStrengthLabel.Text = $"Password strength: {(passwordStrength > 0 ? strengthLabels[passwordStrength - 1] : "Weak")}";
        PasswordStrengthLabel.TextColor = passwordStrength > 0 ? strengthColors[passwordStrength - 1] : Colors.Gray;
    }

    private void ValidatePasswordRequirements(string password)
    {
        Req1.TextColor = password.Length >= 8 ? Colors.Green : Colors.Gray;
        Req2.TextColor = (password.Any(char.IsUpper) && password.Any(char.IsLower)) ? Colors.Green : Colors.Gray;
        Req3.TextColor = password.Any(char.IsDigit) ? Colors.Green : Colors.Gray;
        Req4.TextColor = password.Any(c => !char.IsLetterOrDigit(c)) ? Colors.Green : Colors.Gray;
    }

    private void UpdateCreateButtonState()
    {
        CreateButton.IsEnabled = passwordsMatch && agreementAccepted && passwordStrength >= 3;
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
    }

    private void OnToggleConfirmPasswordClicked(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Create button clicked (OnCreateClicked)");
        try
        {
            CreateButton.IsEnabled = false;
            
            var password = PasswordEntry.Text;
            var walletName = AccountNameEntry.Text?.Trim();
            
            if (string.IsNullOrWhiteSpace(walletName))
            {
                walletName = "My Wallet";
            }
            
            // Create wallet without generating keys automatically
            System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Creating wallet (OnCreateClicked)");
            await _storageService.CreateWalletAsync(password, walletName);
            
            // Initialize predefined networks
            System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Initializing networks");
            await _networkService.InitializePredefinedNetworksAsync();
            
            // Get default network (WAX)
            var defaultNetwork = await _networkService.GetDefaultNetworkAsync();
            if (defaultNetwork == null)
            {
                // Set WAX as default
                await _networkService.SetDefaultNetworkAsync("wax");
                defaultNetwork = await _networkService.GetNetworkAsync("wax");
            }
            
            // Navigate to MainPage - user can add keys from there
            System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Showing success alert");
            await this.DisplayAlertAsync("Success", "Wallet created successfully! You can now add private keys or import accounts.", "OK");
            System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Creating MainPage (OnCreateClicked)");
            var mainPage = _serviceProvider.GetRequiredService<MainPage>();
            await Navigation.PushAsync(mainPage);
            System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Navigation completed (OnCreateClicked)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[CREATE_ACCOUNT] ERROR (OnCreateClicked): {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"[CREATE_ACCOUNT] StackTrace: {ex.StackTrace}");
            await this.DisplayAlertAsync("Error", $"Failed to create wallet: {ex.Message}", "OK");
            CreateButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Back button clicked");
        try
        {
            await Shell.Current.GoToAsync("..");
            System.Diagnostics.Trace.WriteLine("[CREATE_ACCOUNT] Back navigation completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[CREATE_ACCOUNT] ERROR navigating back: {ex.Message}");
        }
    }
}
